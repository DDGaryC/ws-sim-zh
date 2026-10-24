// 日版卡圖（測試版）：遊戲讀卡圖時，若快取裡有日版圖就改用日版；沒有就在背景從日版官網下載，
// 下載完成後，下次顯示這張卡就會是日版。原本的卡圖檔案完全不動（「更新卡片」的 SHA 檢查不受影響）。
// 快取放在模擬器資料夾的 wszh_jpart\（不在 CardContent\Cards 裡）。
// 由 ReiPatcher 補丁在 CardLibrary.GetImage(string cardID, string imageID, string altImagePath) 開頭呼叫 Hook。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using UnityEngine;

public static class WszhJpArt
{
    // 設定頁「日版卡圖」開關（預設關閉；記在 PlayerPrefs）
    const string PrefKey = "WSZH_JpArt";
    static int enabled = -1;
    public static bool Enabled
    {
        get { if (enabled < 0) enabled = PlayerPrefs.GetInt(PrefKey, 0); return enabled == 1; }
        set
        {
            enabled = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefKey, enabled); PlayerPrefs.Save();
            // 已讀進記憶體的卡圖全部清掉，下次顯示時依新設定重新讀
            swapped.Clear();
            Forget(null);
        }
    }
    const string ImgBase = "https://ws-tcg.com/wordpress/wp-content/images/cardlist/";
    const string Api = "https://ws-tcg.com/manage/CardListUser/detailByNoJson?card_number=";
    const int Workers = 4;

    static string dir;
    static readonly object lk = new object();
    // 待下載清單：最後要求的先下載（畫面上正在看的卡優先，不會排在一大堆之前瀏覽過的卡後面）
    static readonly LinkedList<string> queue = new LinkedList<string>();
    static readonly HashSet<string> queued = new HashSet<string>();
    static readonly HashSet<string> missing = new HashSet<string>();      // 日版官網沒有的卡（英文版獨有等）
    static readonly HashSet<string> swapped = new HashSet<string>();      // 已把遊戲記憶體裡的英文圖換掉的卡
    static IDictionary texCache;
    static bool started;

    static string Dir
    {
        get
        {
            if (dir == null)
            {
                dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "wszh_jpart");
                Directory.CreateDirectory(dir);
                string mf = Path.Combine(dir, "_missing.txt");
                if (File.Exists(mf)) foreach (var l in File.ReadAllLines(mf)) if (l.Trim() != "") missing.Add(l.Trim());
            }
            return dir;
        }
    }

    // BD/W63-E001 → BD/W63-001；TE/PE 同理。沒有 / 或 - 的視為無法對照
    public static string JpNumber(string id)
    {
        int dash = id.LastIndexOf('-');
        if (dash < 0 || id.IndexOf('/') < 0) return null;
        string head = id.Substring(0, dash + 1), no = id.Substring(dash + 1);
        if (no.StartsWith("TE")) no = "T" + no.Substring(2);
        else if (no.StartsWith("PE")) no = "P" + no.Substring(2);
        else if (no.StartsWith("E")) no = no.Substring(1);
        return head + no;
    }

    // 下載下來的是 PNG（.png），第一次用到時轉成 JPG（.jpg，約小三分之二）再用
    // 日版官網一定沒有的系列，直接跳過不查：
    // 英文版原創（WX／SX／WXE／ENS／ENW／EN_）、中文版原創（SZ）、Blau（01B 這類）、OS 品牌系列（R01 這類，本來就是日文卡圖）
    static readonly System.Text.RegularExpressions.Regex NoJpSet =
        new System.Text.RegularExpressions.Regex(@"/(WXE?\d|SX\d|ENS\d|ENW\d|EN_|SZ\d|\d+[BSE]-|R\d+-)");

    static string FileOf(string jp) { return Path.Combine(Dir, jp.Replace('/', '_').Replace('-', '_') + ".png"); }
    static string JpgOf(string jp) { return Path.Combine(Dir, jp.Replace('/', '_').Replace('-', '_') + ".jpg"); }

    // 快取的檔案數與大小（設定頁顯示用）
    public static void CacheInfo(out int count, out long bytes)
    {
        count = 0; bytes = 0;
        try
        {
            foreach (var f in new DirectoryInfo(Dir).GetFiles())
                if (f.Extension == ".jpg" || f.Extension == ".png") { count++; bytes += f.Length; }
        }
        catch { }
    }

    // 由補丁呼叫：altImagePath 是空的（不是異畫卡）時，有日版快取就把路徑換成日版圖
    public static void Hook(string cardID, ref string altImagePath)
    {
        try
        {
            if (!Enabled || string.IsNullOrEmpty(cardID) || !string.IsNullOrEmpty(altImagePath)) return;
            string jp = JpNumber(cardID);
            if (jp == null || NoJpSet.IsMatch(jp)) return;
            string f = JpgOf(jp);
            if (!File.Exists(f) && File.Exists(FileOf(jp))) ToJpg(FileOf(jp), f);
            if (File.Exists(f))
            {
                altImagePath = f;
                if (swapped.Add(cardID)) Forget(cardID);       // 記憶體裡若已有英文圖，清掉讓它重新讀
                return;
            }
            lock (lk)
            {
                if (missing.Contains(jp)) return;
                if (queued.Contains(jp))
                {
                    var node = queue.Find(jp);                 // 還在排隊（不是下載中）就移到最前面
                    if (node != null) { queue.Remove(node); queue.AddFirst(node); }
                    return;
                }
                queued.Add(jp); queue.AddFirst(jp);
                if (!started) { started = true; for (int i = 0; i < Workers; i++) new Thread(Work) { IsBackground = true }.Start(); }
                Monitor.PulseAll(lk);
            }
        }
        catch (Exception ex) { Debug.Log("[WSZH] jpart hook: " + ex.Message); }
    }

    // PNG → JPG（品質 80），刪掉 PNG。日版官網的名場面是橫的，模擬器的卡圖一律是直的，
    // 所以橫的圖順時針轉 90°。要用 Texture2D，所以在主執行緒（Hook）裡做，不在下載執行緒做
    static void ToJpg(string png, string jpg)
    {
        try
        {
            var src = new Texture2D(2, 2);
            if (!src.LoadImage(File.ReadAllBytes(png))) return;
            Texture2D outTex = src;
            int w = src.width, h = src.height;
            if (w > h)
            {
                Color32[] a = src.GetPixels32(), b = new Color32[a.Length];
                // Unity 的像素從左下角開始：原圖 (x, y) → 新圖 (y, w-1-x)，新圖寬 h、高 w
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        b[(w - 1 - x) * h + y] = a[y * w + x];
                outTex = new Texture2D(h, w, TextureFormat.RGBA32, false);
                outTex.SetPixels32(b);
                outTex.Apply();
            }
            File.WriteAllBytes(jpg + ".part", outTex.EncodeToJPG(80));
            if (File.Exists(jpg)) File.Delete(jpg);
            File.Move(jpg + ".part", jpg);
            File.Delete(png);
            if (outTex != src) UnityEngine.Object.Destroy(outTex);
            UnityEngine.Object.Destroy(src);
        }
        catch (Exception ex) { Debug.Log("[WSZH] jpart convert " + Path.GetFileName(png) + ": " + ex.Message); }
    }

    static void Forget(string cardID)
    {
        if (texCache == null)
        {
            Type t = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType("CardLibrary"); if (t != null) break; }
            var fi = t == null ? null : t.GetField("loadedTextures", BindingFlags.NonPublic | BindingFlags.Static);
            texCache = fi == null ? null : fi.GetValue(null) as IDictionary;
        }
        if (texCache == null) return;
        if (cardID == null) texCache.Clear();                         // 全部清掉（切換開關時）
        else if (texCache.Contains(cardID)) texCache.Remove(cardID);
    }

    static void Work()
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        while (true)
        {
            string jp;
            lock (lk)
            {
                while (queue.Count == 0) Monitor.Wait(lk);
                jp = queue.First.Value; queue.RemoveFirst();
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool ok = false, error = false;
            try { ok = Fetch(jp); } catch (Exception ex) { error = true; Debug.Log("[WSZH] jpart " + jp + " failed: " + ex.GetType().Name + " " + ex.Message); }
            sw.Stop();
            lock (lk)
            {
                queued.Remove(jp);
                // 連線錯誤不記成「官網沒有」，下次顯示這張卡會再試
                if (!ok && !error && !missing.Contains(jp))
                {
                    missing.Add(jp);
                    try { File.AppendAllText(Path.Combine(Dir, "_missing.txt"), jp + "\n"); } catch { }
                }
            }
            if (!error) Debug.Log("[WSZH] jpart " + jp + (ok ? " downloaded in " : " not on JP site (") + sw.ElapsedMilliseconds + (ok ? " ms" : " ms)"));
        }
    }

    // 模擬器有些舊系列的卡號只有兩位數（DC/W01-03），官網是三位數（DC/W01-003），對不到時補零再試
    static bool Fetch(string jp)
    {
        if (FetchAs(jp, FileOf(jp))) return true;
        var m = System.Text.RegularExpressions.Regex.Match(jp, @"^(.*-[A-Z]*)(\d{1,2})$");
        if (!m.Success) return false;
        if (FetchAs(m.Groups[1].Value + m.Groups[2].Value.PadLeft(3, '0'), FileOf(jp))) return true;
        // 有些補充包在官網是 P 開頭（BD/WE32-01 → BD/WE32-P01）
        return m.Groups[1].Value.EndsWith("-") && FetchAs(m.Groups[1].Value + "P" + m.Groups[2].Value.PadLeft(2, '0'), FileOf(jp));
    }

    // 先用卡號推算的網址；失敗再查官網 API 的 picture 欄位
    static bool FetchAs(string jp, string file)
    {
        string lower = jp.ToLowerInvariant();
        string set = lower.Replace('/', '_').Substring(0, lower.LastIndexOf('-'));
        string guess = ImgBase + lower.Substring(0, 1) + "/" + set + "/" + lower.Replace('/', '_').Replace('-', '_') + ".png";
        if (Download(guess, file)) return true;
        string json;
        try
        {
            using (var wc = new WebClient()) { wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0"; wc.Encoding = System.Text.Encoding.UTF8; json = wc.DownloadString(Api + Uri.EscapeDataString(jp)); }
        }
        catch (WebException ex)
        {
            var r = ex.Response as HttpWebResponse;
            if (r != null && (int)r.StatusCode >= 400 && (int)r.StatusCode < 500) return false;   // 官網查無此卡
            throw;                                                                                // 連線問題：下次再試
        }
        int i = json.IndexOf("\"picture\":\"");
        if (i < 0) return false;
        i += 11;
        int j = json.IndexOf('"', i);
        string pic = json.Substring(i, j - i).Replace("\\/", "/");
        return pic != "" && Download(ImgBase + pic, file);
    }

    static bool Download(string url, string file)
    {
        try
        {
            byte[] data;
            using (var wc = new WebClient()) { wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0"; data = wc.DownloadData(url); }
            if (data == null || data.Length < 1000) return false;
            bool png = data[0] == 0x89, jpg = data[0] == 0xFF && data[1] == 0xD8, gif = data[0] == 'G' && data[1] == 'I' && data[2] == 'F';
            if (!png && !jpg && !gif) return false;                                    // 不是圖片（例如 404 頁面）
            // 舊系列的卡圖是 GIF，遊戲讀不了，先轉成 PNG（JPG 遊戲可以直接讀，副檔名不影響）
            if (gif) data = GifToPng(data);
            string tmp = file + ".part";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
            return true;
        }
        catch (WebException) { return false; }
    }

    static byte[] GifToPng(byte[] gif)
    {
        using (var src = new MemoryStream(gif))
        using (var bmp = new System.Drawing.Bitmap(src))
        using (var dst = new MemoryStream())
        {
            bmp.Save(dst, System.Drawing.Imaging.ImageFormat.Png);
            return dst.ToArray();
        }
    }
}
