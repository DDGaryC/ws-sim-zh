// 中文搜尋：系列欄打中文（「加速世界」「點兔」）、卡片名稱欄打日文或中文（「黒雪姫」「黑雪姬」）。
// 資料由 card_text\make_name_maps.py 產生，放在模擬器的 AutoTranslator\WSZH\：
//   series.txt  系列英文名<Tab>中文別名（| 分隔）
//   cjkmap.txt  漢字對照：日文漢字、簡體字 → 繁體，比對前兩邊都先統一
// 由 ReiPatcher 補丁呼叫：DeckBuilderSearch.SeriesMatchesQuery 開頭（SeriesMatch）、CardLibrary.Search（WszhCardText.SearchCjk 用 NameMatch）
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class WszhNames
{
    static Dictionary<char, char> map;
    static Dictionary<string, string[]> series;     // 英文系列名 → 統一後的中文別名

    static string Dir { get { return Path.Combine(Path.Combine(Path.GetDirectoryName(Application.dataPath), "AutoTranslator"), "WSZH"); } }

    static void Load()
    {
        if (map != null) return;
        map = new Dictionary<char, char>();
        series = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string f = Path.Combine(Dir, "cjkmap.txt");
            if (File.Exists(f))
                foreach (var line in File.ReadAllLines(f, Encoding.UTF8))
                    if (line.Length >= 3 && line[1] == '\t' && !line.StartsWith("#")) map[line[0]] = line[2];
            f = Path.Combine(Dir, "series.txt");
            if (File.Exists(f))
                foreach (var line in File.ReadAllLines(f, Encoding.UTF8))
                {
                    if (line.StartsWith("#")) continue;
                    int t = line.IndexOf('\t'); if (t <= 0) continue;
                    var names = line.Substring(t + 1).Split('|');
                    for (int i = 0; i < names.Length; i++) names[i] = Norm(names[i]);
                    series[line.Substring(0, t).Trim()] = names;
                }
        }
        catch (Exception ex) { Debug.Log("[WSZH] names load: " + ex.Message); }
    }

    public static bool HasCjk(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s) if ((c >= 0x3040 && c <= 0x30FF) || (c >= 0x3400 && c <= 0x9FFF) || (c >= 0xF900 && c <= 0xFAFF)) return true;
        return false;
    }

    // 統一寫法：漢字換成繁體、英文小寫、去掉空白和標點（「」『』・"…）
    public static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c0 in s)
        {
            char c = c0;
            if (c >= 0xFF01 && c <= 0xFF5E) c = (char)(c - 0xFEE0);      // 全形英數 → 半形
            if (char.IsWhiteSpace(c) || "「」『』・·\"“”'’《》«»()（）~～!?,.、。:：;；-—_/".IndexOf(c) >= 0) continue;
            char m;
            if (map != null && map.TryGetValue(c, out m)) c = m;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    // 系列欄：打的字有中文或日文時，比對中文別名
    public static bool SeriesMatch(string seriesName, string query)
    {
        try
        {
            if (!HasCjk(query) || string.IsNullOrEmpty(seriesName)) return false;
            Load();
            string[] names;
            if (!series.TryGetValue(seriesName.Trim(), out names)) return false;
            string q = Norm(query);
            if (q.Length == 0) return false;
            foreach (var n in names) if (n.Contains(q)) return true;
        }
        catch { }
        return false;
    }

    // 卡片名稱欄：比對日文卡名（模擬器 JPNames.txt／卡片資料的 JPName；沒有的話用補充資料）
    static System.Reflection.FieldInfo fJp, fId;
    public static bool NameMatch(object card, string query)
    {
        if (card == null) return false;
        Load();
        string jp = JapaneseName(card);
        if (string.IsNullOrEmpty(jp)) return false;
        return Norm(jp).Contains(Norm(query));
    }

    // 卡片的日文名：遊戲資料有就用遊戲的，沒有就查補充資料（日版官網抓的，AutoTranslator\WSZH\jpnames.txt）
    // 只用來搜尋和顯示，不寫回遊戲的卡片資料（效果判斷「卡名含有…」也會用到那個欄位，改了可能影響對戰）
    public static string JapaneseName(object card)
    {
        if (card == null) return null;
        var t = card.GetType();
        if (fJp == null) fJp = t.GetField("japaneseName");
        if (fId == null) fId = t.GetField("id");
        string jp = fJp == null ? null : fJp.GetValue(card) as string;
        if (!string.IsNullOrEmpty(jp)) return jp;
        string id = fId == null ? null : fId.GetValue(card) as string;
        return SupplementName(id);
    }

    static CompactMap extra;
    public static bool HasGameJapaneseName(object card)
    {
        if (card == null) return false;
        if (fJp == null) fJp = card.GetType().GetField("japaneseName");
        return fJp != null && !string.IsNullOrEmpty(fJp.GetValue(card) as string);
    }

    public static string SupplementName(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;
        if (extra == null)
        {
            extra = CompactMap.Empty;
            try
            {
                string f = Path.Combine(Dir, "jpnames.txt");
                if (File.Exists(f))
                {
                    var d = new Dictionary<string, string>();
                    foreach (var line in File.ReadAllLines(f, Encoding.UTF8))
                    {
                        int t = line.IndexOf('\t');
                        if (t > 0 && !line.StartsWith("#")) d[line.Substring(0, t)] = line.Substring(t + 1);
                    }
                    extra = CompactMap.Build(d);
                }
            }
            catch (Exception ex) { Debug.Log("[WSZH] jpnames load: " + ex.Message); }
        }
        string name;
        return extra.TryGetValue(cardId, out name) ? name : null;
    }
}
