// WS 中文化套件：卡片效果文字翻譯（和 Lang.cs 一起編進 WSZH.Lang.dll）
// LangPatch.cs 會在 WeissCard.EnrichenText(string) 開頭插入 str = WszhCardText.Translate(str)。
// 遊戲顯示卡片說明、牌組編輯器、效果發動提示之前都會經過 EnrichenText，所以在這裡把英文逐行換成中文。
// 翻譯檔：AutoTranslator\Translation\{目前語言}\CardText\*.json（{ "英文原文": "中文", ... }）。
// 目前語言跟著 XUnity（遊戲內語言切換）走；English 沒有 CardText 資料夾，就維持原文。
// 任何地方出錯都直接回傳原文，不影響遊戲。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class WszhCardText
{
    static string loadedLang;
    static Dictionary<string, string> map = new Dictionary<string, string>();
    static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    static System.Reflection.FieldInfo langField;
    static bool langLookupDone;

    static string CurrentLanguage()
    {
        try
        {
            if (!langLookupDone)
            {
                langLookupDone = true;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "XUnity.AutoTranslator.Plugin.Core") continue;
                    var t = a.GetType("XUnity.AutoTranslator.Plugin.Core.Configuration.Settings");
                    if (t != null) langField = t.GetField("Language", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                }
            }
            return langField == null ? null : (string)langField.GetValue(null);
        }
        catch { return null; }
    }

    public static string Translate(string str)
    {
        try
        {
            if (string.IsNullOrEmpty(str)) return str;
            string lang = CurrentLanguage();
            if (lang != loadedLang) Load(lang);
            if (map.Count == 0) return str;
            string hit;
            if (cache.TryGetValue(str, out hit)) return hit;

            string[] lines = str.Split('\n');
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string whole;
                // 先拿整行比對（判定效果那類句子開頭本身就是圖示標籤）
                if (map.TryGetValue(line, out whole) || map.TryGetValue(line.TrimEnd(), out whole)) { lines[i] = whole; changed = true; continue; }
                // 行首有空白（「選擇以下 1 個效果」之後的各選項常見），或整行用引號括起來的選項
                string tr = line.Trim();
                if (tr != line && tr.Length > 0 && map.TryGetValue(tr, out whole)) { lines[i] = whole; changed = true; continue; }
                if (tr.Length > 2 && tr[0] == '"' && tr[tr.Length - 1] == '"' && map.TryGetValue(tr.Substring(1, tr.Length - 2).Trim(), out whole))
                { lines[i] = "「" + whole + "」"; changed = true; continue; }
                // 對不到時，行首可能有遊戲加上的標籤（例如 <color=#000000>），拿掉再比對
                // 一個一個拿掉行首標籤再比對（對戰時是「顏色標籤＋判定圖示＋文字」，判定圖示本身是譯文的一部分）
                int p = 0;
                while (p < line.Length && line[p] == '<')
                {
                    int e = line.IndexOf('>', p);
                    if (e < 0) break;
                    p = e + 1;
                    string rest = line.Substring(p), zh;
                    if (map.TryGetValue(rest, out zh) || map.TryGetValue(rest.TrimEnd(), out zh)) { lines[i] = line.Substring(0, p) + zh; changed = true; break; }
                }
            }
            string result = changed ? string.Join("\n", lines) : str;
            if (cache.Count > 5000) cache.Clear();
            cache[str] = result;
            return result;
        }
        catch { return str; }
    }

    static void Load(string lang)
    {
        loadedLang = lang;
        map = new Dictionary<string, string>();
        cache.Clear();
        if (string.IsNullOrEmpty(lang)) return;
        try
        {
            // 香港版不另外存檔：讀台灣版，再換成香港區域用語
            string dir = CardTextDir(lang == "hk" ? "tw" : lang);
            if (!Directory.Exists(dir)) return;
            hkMode = lang == "hk";
            foreach (string f in Directory.GetFiles(dir, "*.json"))
            {
                try { Parse(File.ReadAllText(f, Encoding.UTF8)); }
                catch (Exception ex) { Debug.Log("[WSZH] card text: failed to read " + Path.GetFileName(f) + ": " + ex.Message); }
            }
            Debug.Log("[WSZH] card text loaded for " + lang + ": " + map.Count + " lines");
        }
        catch (Exception ex) { Debug.Log("[WSZH] card text load failed: " + ex.Message); }
    }

    // ---- EnrichenText 處理完之後：有中文時改成靠左對齊 ----
    // 卡片說明框是左右對齊，中文句子幾乎沒有空格，整行多出來的寬度會全塞進少數幾個空格，出現很大的空白。
    // （不能在 Translate 裡加：EnrichenText 會把沒有「/」的 <...> 標籤當成特徵名稱改成 «...»）
    public static string Post(string s)
    {
        try
        {
            if (map.Count == 0 || string.IsNullOrEmpty(s) || s.StartsWith("<align")) return s;
            foreach (char c in s) if (c >= 0x4E00 && c <= 0x9FFF) return "<align=left>" + s;
            return s;
        }
        catch { return s; }
    }

    // ---- 用中文搜尋卡片效果 ----
    // LangPatch.cs 會在 CardLibrary.Search 開頭插入：效果欄有中文時改由 SearchCjk 處理。
    public static bool IsCjk(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s) if (c >= 0x3000 && c <= 0x9FFF || c >= 0xFF00 && c <= 0xFFEF) return true;
        return false;
    }

    // 先用「效果欄空白」的條件呼叫原本的搜尋，再用翻譯後的效果文字過濾
    public static object SearchCjk(string seriesName, string setName, string cardName, string levelInput, string costInput, string colorInput,
        string typeInput, string triggerInput, string traitName, string textInput, string cardIdInput)
    {
        Type lib = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { lib = a.GetType("CardLibrary"); if (lib != null) break; }
        var search = lib.GetMethod("Search", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        var list = search.Invoke(null, new object[] { seriesName, setName, cardName, levelInput, costInput, colorInput, typeInput, triggerInput, traitName, "", cardIdInput }) as System.Collections.IList;
        if (list == null) return list;
        string q = textInput.Trim().ToLowerInvariant();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var c = list[i];
            string text = c.GetType().GetField("text").GetValue(c) as string ?? "";
            string zh = Translate(text);
            if (zh.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && text.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) list.RemoveAt(i);
        }
        return list;
    }

    // ---- 開發用：把每張卡實際顯示的效果文字匯出（Ctrl+Shift+F12，見 Lang.cs）----
    // 卡片資料裡的共用效果簡寫（*xxx）要遊戲讀卡時才會展開，所以直接從遊戲的卡片庫拿最準。
    // 會強制讀取所有系列，輸出到模擬器資料夾的 CardText_dump.tsv：卡號<Tab>系列<Tab>效果文字（換行寫成 \n）
    public static string Dump()
    {
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        Type lib = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { lib = a.GetType("CardLibrary"); if (lib != null) break; }
        if (lib == null) return "找不到 CardLibrary";
        var series = lib.GetField("allSeries", Any).GetValue(null) as System.Collections.IEnumerable;
        var read = lib.GetMethod("ReadSeriesFolder", Any);
        if (series == null || read == null) return "卡片庫結構和預期不同";
        var sb = new StringBuilder();
        int cards = 0, folders = 0;
        foreach (var s in series)
        {
            string sname = (string)s.GetType().GetField("seriesName", Any).GetValue(s);
            var fl = s.GetType().GetField("seriesFolders", Any).GetValue(s) as System.Collections.IEnumerable;
            if (fl == null) continue;
            foreach (var f in fl)
            {
                var ft = f.GetType();
                if (!(bool)ft.GetField("loaded", Any).GetValue(f))
                {
                    try { Drain(read.Invoke(null, new object[] { f, false }) as System.Collections.IEnumerator); }
                    catch (Exception ex) { Debug.Log("[WSZH] dump: failed to load folder in " + sname + ": " + ex.Message); continue; }
                }
                folders++;
                var list = ft.GetField("cards", Any).GetValue(f) as System.Collections.IEnumerable;
                if (list == null) continue;
                foreach (var c in list)
                {
                    var ct = c.GetType();
                    string id = ct.GetField("id", Any).GetValue(c) as string;
                    string text = ct.GetField("text", Any).GetValue(c) as string;
                    if (string.IsNullOrEmpty(text)) continue;
                    sb.Append(id).Append('\t').Append(sname).Append('\t').Append(text.Replace("\r", "").Replace("\n", "\\n")).Append('\n');
                    cards++;
                }
            }
        }
        string root = Path.GetDirectoryName(Application.dataPath);
        string outp = Path.Combine(root, "CardText_dump.tsv");   // 不放 AutoTranslator：那個資料夾會整個打包進中文化套件
        File.WriteAllText(outp, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[WSZH] card text dump: " + cards + " cards from " + folders + " folders -> " + outp);
        return null;
    }

    static void Drain(System.Collections.IEnumerator e)
    {
        if (e == null) return;
        int guard = 0;
        while (e.MoveNext() && guard++ < 10000000)
        {
            var inner = e.Current as System.Collections.IEnumerator;
            if (inner != null) Drain(inner);
        }
    }

    // 把翻譯檔的寫法轉成遊戲會畫成圖示的關鍵字
    static string ForDisplay(string zh)
    {
        // 圖示後面多留一個空格：中文字比英文寬，只留一格時圖示會壓到第一個字
        return zh.Replace("【自】", "Auto:  ").Replace("【永】", "Cont:  ").Replace("【起】", "Act:  ").Replace("【名場面連動】", "CxCombo  ");
    }

    static bool hkMode;

    public static string CardTextDir(string lang)
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, "AutoTranslator"), "Translation"), lang), "CardText");
    }

    // 下載完新的翻譯後呼叫：下次顯示卡片時重新載入
    public static void Reload() { loadedLang = ""; }

    public static int LineCount()
    {
        try { string l = CurrentLanguage(); if (l != loadedLang) Load(l); } catch { }
        return map.Count;
    }

    // ---- 「更新卡片翻譯」按鈕：從 GitHub 下載卡片翻譯（像模擬器的「更新卡片」） ----
    // 伺服器上 cardtext/manifest.txt（简体：manifest_cn.txt）每行「sha256<Tab>大小<Tab>檔名」，檔案在 cardtext/tw/（cn/）底下。
    // 只下載本機沒有或內容不同的檔案；伺服器上已經沒有的檔案會刪掉。香港版共用台灣版。
    const string CardTextBase = "https://raw.githubusercontent.com/DDGaryC/ws-sim-zh/main/cardtext/";

    public static string StampFile() { return Path.Combine(Path.GetDirectoryName(CardTextDir("tw")), "cardtext_updated.txt"); }

    public static string LastUpdated()
    {
        try { return File.Exists(StampFile()) ? File.ReadAllText(StampFile()).Trim() : null; } catch { return null; }
    }

    static string Sha256(byte[] data)
    {
        using (var h = System.Security.Cryptography.SHA256.Create())
        {
            var sb = new StringBuilder();
            foreach (var b in h.ComputeHash(data)) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    static UnityEngine.Networking.UnityWebRequest Get(string url)
    {
        var r = UnityEngine.Networking.UnityWebRequest.Get(url);
        r.SetRequestHeader("Cache-Control", "no-cache");
        r.timeout = 60;
        return r;
    }

    // lang：目前的語言。简体中文下載 cardtext/cn/（清單 manifest_cn.txt），其他（台灣、香港）下載 cardtext/tw/
    public static System.Collections.IEnumerator Download(string lang, Action<string> status)
    {
        string sub = lang == "cn" ? "cn" : "tw";
        status("正在檢查卡片翻譯…");
        string manifest;
        using (var r = Get(CardTextBase + (sub == "cn" ? "manifest_cn.txt" : "manifest.txt")))
        {
            yield return r.SendWebRequest();
            if (r.responseCode == 404) { status("雲端還沒有卡片翻譯檔，請稍後再試"); yield break; }
            if (r.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { status("無法連線到 GitHub：" + r.error); yield break; }
            manifest = r.downloadHandler.text;
        }
        var list = new List<string[]>();
        foreach (var raw in manifest.Split('\n'))
        {
            var p = raw.Trim().Split('\t');
            if (p.Length >= 3 && !p[0].StartsWith("#") && p[2].EndsWith(".json") && p[2].IndexOfAny(new[] { '/', '\\' }) < 0) list.Add(p);
        }
        if (list.Count == 0) { status("伺服器上的卡片翻譯清單是空的"); yield break; }

        string dir = CardTextDir(sub);
        Directory.CreateDirectory(dir);
        int changed = 0, n = 0;
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in list)
        {
            n++;
            keep.Add(p[2]);
            string path = Path.Combine(dir, p[2]);
            bool same = false;
            try { same = File.Exists(path) && Sha256(File.ReadAllBytes(path)) == p[0].ToLowerInvariant(); } catch { }
            if (same) continue;
            status("正在下載卡片翻譯（" + n + " / " + list.Count + "）…");
            using (var r = Get(CardTextBase + sub + "/" + Uri.EscapeDataString(p[2])))
            {
                yield return r.SendWebRequest();
                if (r.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { status("下載失敗：" + p[2] + "（" + r.error + "），請再試一次"); yield break; }
                byte[] data = r.downloadHandler.data;
                if (Sha256(data) != p[0].ToLowerInvariant()) { status("下載的內容不正確：" + p[2] + "（可能是網路不穩），請再試一次"); yield break; }
                File.WriteAllBytes(path, data);
                changed++;
            }
        }
        foreach (var f in Directory.GetFiles(dir, "*.json"))
            if (!keep.Contains(Path.GetFileName(f))) { try { File.Delete(f); changed++; } catch { } }
        try { File.WriteAllText(StampFile(), DateTime.Now.ToString("yyyy-MM-dd HH:mm")); } catch { }
        Reload();
        int lines = LineCount();
        status(changed == 0 ? "卡片翻譯已是最新（共 " + lines + " 條）" : "卡片翻譯更新完成（共 " + lines + " 條）");
    }

    static void Add(string en, string zh)
    {
        if (hkMode) zh = zh.Replace("休息室", "控制室").Replace("能量區", "費用區");
        // 卡片資料裡的字面「\n」，遊戲顯示時會變成換行，所以分成好幾行各自對應
        if (en.Contains("\\n"))
        {
            string[] a = en.Split(new[] { "\\n" }, StringSplitOptions.None);
            string[] b = zh.Split(new[] { "\\n" }, StringSplitOptions.None);
            if (a.Length == b.Length) { for (int i = 0; i < a.Length; i++) map[a[i]] = ForDisplay(b[i]); return; }
        }
        map[en] = ForDisplay(zh);
    }

    // 簡單的 JSON 物件解析（只需要 {"字串": "字串", ...}），不依賴其他程式庫
    static void Parse(string s)
    {
        int i = 0;
        Skip(s, ref i);
        if (i >= s.Length || s[i] != '{') return;
        i++;
        while (true)
        {
            Skip(s, ref i);
            if (i >= s.Length || s[i] == '}') return;
            string k = Str(s, ref i);
            Skip(s, ref i);
            if (s[i] != ':') return;
            i++;
            Skip(s, ref i);
            string v = Str(s, ref i);
            if (k != null && v != null) Add(k, v);
            Skip(s, ref i);
            if (i < s.Length && s[i] == ',') i++;
        }
    }

    static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static string Str(string s, ref int i)
    {
        if (s[i] != '"') return null;
        i++;
        var sb = new StringBuilder();
        while (i < s.Length && s[i] != '"')
        {
            char c = s[i++];
            if (c != '\\') { sb.Append(c); continue; }
            char n = s[i++];
            switch (n)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: sb.Append(n); break;
            }
        }
        i++;
        return sb.ToString();
    }
}
