// 還原舊版模擬器（存在 Windows 登錄檔）的牌組。
// 舊版把牌組存在 HKCU\Software\Blake Thoennes\Weiss Schwarz（Unity PlayerPrefs），
// 值名稱裡的中文被以 ANSI 誤解碼成亂碼（連 "_h" 的底線都可能被吃掉），遊戲內建的「匯入舊版牌組」
// 直接用亂碼名稱匯入，造成檔名亂碼、內容讀錯。這裡改用 DeckNames（UTF-8 正確清單）
// 計算 Unity 的雜湊去對應每個值，再以正確名稱寫出牌組檔；遊戲匯入產生的亂碼檔會搬到備份資料夾。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

static class LegacyDecks
{
    public const string RegPath = @"Software\Blake Thoennes\Weiss Schwarz";
    public const string BackupFolder = "Decks_匯入亂碼備份";

    public class Deck { public string Name, Safe, Date, Sleeve; public List<string> Cards; public string CardsKey { get { return string.Join("|", Cards); } } }
    public class Plan
    {
        public string DecksDir, Error;
        public int Total, AlreadyOk;
        public List<Deck> ToWrite = new List<Deck>();
        public List<string> ToMove = new List<string>();
        public Dictionary<Deck, string> FileNames = new Dictionary<Deck, string>();
    }

    // Unity PlayerPrefs 的雜湊：djb2 XOR 變體，UTF-8 位元組以 signed char 參與
    static uint Hash(string s)
    {
        unchecked
        {
            uint h = 5381;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) { int sb = b >= 128 ? b - 256 : b; h = (h * 33) ^ (uint)sb; }
            return h;
        }
    }
    static string Text(object v)
    {
        var bytes = v as byte[];
        return bytes != null ? Encoding.UTF8.GetString(bytes).TrimEnd('\0') : Convert.ToString(v);
    }
    public static string SafeName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name) sb.Append("\\/:*?\"<>|".IndexOf(c) >= 0 || c < 32 ? '_' : c);
        string s = sb.ToString().Trim().TrimEnd('.');
        return s.Length == 0 ? "deck" : s;
    }

    public static List<Deck> ReadRegistry()
    {
        var result = new List<Deck>();
        using (var k = Registry.CurrentUser.OpenSubKey(RegPath))
        {
            if (k == null) return result;
            var names = k.GetValueNames();
            var byHash = new Dictionary<uint, List<string>>();
            var rx = new Regex(@"h(\d+)$");
            string deckNamesKey = null;
            foreach (var n in names)
            {
                if (n.StartsWith("DeckNames")) deckNamesKey = n;
                var m = rx.Match(n); uint hv;
                if (m.Success && uint.TryParse(m.Groups[1].Value, out hv))
                {
                    List<string> l;
                    if (!byHash.TryGetValue(hv, out l)) byHash[hv] = l = new List<string>();
                    l.Add(n);
                }
            }
            if (deckNamesKey == null) return result;
            Func<string, string, string> lookup = (prefix, name) =>
            {
                List<string> l;
                if (!byHash.TryGetValue(Hash(prefix + name), out l)) return null;
                foreach (var n in l) if (n.StartsWith(prefix)) return Text(k.GetValue(n));
                return null;
            };
            foreach (var name in Text(k.GetValue(deckNamesKey)).Split('|'))
            {
                if (name.Length == 0 || name.StartsWith("AI_")) continue;   // AI 牌組遊戲會自己提供
                string cards = lookup("Deck_", name);
                if (cards == null) continue;
                result.Add(new Deck
                {
                    Name = name, Safe = SafeName(name),
                    Date = lookup("Date_", name) ?? "", Sleeve = lookup("Sleeve_", name) ?? "",
                    Cards = cards.Split('|').Select(c => c.Trim()).Where(c => c.Length > 0).ToList()
                });
            }
        }
        return result;
    }

    static List<string> ReadCards(string file, out string nameLine)
    {
        var cards = new List<string>(); nameLine = null;
        bool inCards = false;
        foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (nameLine == null && line.StartsWith("Name:")) nameLine = line.Substring(5).Trim();
            if (line == "Cards:") { inCards = true; continue; }
            if (inCards && line.Length > 0) cards.Add(line);
        }
        return cards;
    }

    public static Plan MakePlan(string simDir)
    {
        var plan = new Plan { DecksDir = Path.Combine(simDir, "Decks") };
        List<Deck> decks;
        try { decks = ReadRegistry(); } catch (Exception ex) { plan.Error = "讀取登錄檔失敗：" + ex.Message; return plan; }
        plan.Total = decks.Count;
        if (decks.Count == 0) return plan;
        Directory.CreateDirectory(plan.DecksDir);

        var trueSafe = new HashSet<string>(decks.Select(d => d.Safe), StringComparer.OrdinalIgnoreCase);
        var regKeys = new HashSet<string>(decks.Select(d => d.CardsKey));
        var keep = new List<KeyValuePair<string, string>>();   // 檔名（無副檔名）, 卡表
        foreach (var f in Directory.GetFiles(plan.DecksDir, "*.txt"))
        {
            string nm = Path.GetFileNameWithoutExtension(f), nameLine;
            List<string> cards;
            try { cards = ReadCards(f, out nameLine); } catch { continue; }
            string key = string.Join("|", cards);
            bool garbledName = nm.IndexOf('�') >= 0 || (nameLine != null && nameLine != nm && nameLine.Replace('/', '_') != nm);
            bool looksImported = regKeys.Contains(key) && !trueSafe.Contains(nm) && (nm.Contains("_") || nm.Contains("?") || garbledName);
            if (garbledName && (regKeys.Contains(key) || nm.IndexOf('�') >= 0) || looksImported) plan.ToMove.Add(f);
            else keep.Add(new KeyValuePair<string, string>(nm, key));
        }
        var keptNames = new HashSet<string>(keep.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);
        // 同名且同內容 → 已存在；內容相同但檔名不是任何舊牌組名稱（使用者自己改過名）→ 也算已存在，但每個檔只能抵一副
        var sameNameSameCards = new HashSet<string>(keep.Select(p => p.Key.ToLowerInvariant() + "\n" + p.Value));
        var renamedByKey = keep.Where(p => !trueSafe.Contains(p.Key)).GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.Count());
        foreach (var d in decks)
        {
            if (sameNameSameCards.Contains(d.Safe.ToLowerInvariant() + "\n" + d.CardsKey)) { plan.AlreadyOk++; continue; }
            int left;
            if (renamedByKey.TryGetValue(d.CardsKey, out left) && left > 0) { renamedByKey[d.CardsKey] = left - 1; plan.AlreadyOk++; continue; }
            string fn = d.Safe; int n = 2;
            if (keptNames.Contains(fn)) { fn = d.Safe + " (舊版)"; while (keptNames.Contains(fn)) fn = d.Safe + " (舊版" + n++ + ")"; }
            keptNames.Add(fn);
            plan.ToWrite.Add(d); plan.FileNames[d] = fn;
        }
        return plan;
    }

    public static void Execute(Plan plan, string simDir, Action<string> log)
    {
        if (plan.ToMove.Count > 0)
        {
            string backup = Path.Combine(simDir, BackupFolder);
            Directory.CreateDirectory(backup);
            foreach (var f in plan.ToMove)
            {
                string dest = Path.Combine(backup, Path.GetFileName(f)); int n = 2;
                while (File.Exists(dest)) dest = Path.Combine(backup, Path.GetFileNameWithoutExtension(f) + " (" + n++ + ").txt");
                File.Move(f, dest);
            }
            log("  已把 " + plan.ToMove.Count + " 個亂碼牌組檔搬到「" + BackupFolder + "」資料夾（沒有刪除）");
        }
        foreach (var d in plan.ToWrite)
        {
            string fn = plan.FileNames[d];
            var lines = new List<string> { "# WeissSim Deck", "Name: " + fn, "Date: " + d.Date, "Sleeve: " + d.Sleeve, "", "Cards:" };
            lines.AddRange(d.Cards);
            File.WriteAllText(Path.Combine(plan.DecksDir, fn + ".txt"), string.Join("\n", lines), new UTF8Encoding(false));
        }
        log("  已用正確名稱還原 " + plan.ToWrite.Count + " 副牌組" + (plan.AlreadyOk > 0 ? "（另有 " + plan.AlreadyOk + " 副原本就在，略過）" : ""));
    }
}
