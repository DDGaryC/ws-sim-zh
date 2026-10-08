// 下載相關：中文化檔案來自 GitHub 儲存庫 DDGaryC/ws-sim-zh；
// 中文字型與翻譯插件安裝程式直接從 XUnity.AutoTranslator 官方 GitHub Releases 下載（不重新散布）。
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

static class Online
{
    // 測試時可用環境變數 WSZH_BASE 指到本機資料夾（file:///...）
    public static readonly string Base = (Environment.GetEnvironmentVariable("WSZH_BASE") ?? "https://raw.githubusercontent.com/DDGaryC/ws-sim-zh/main/").TrimEnd('/') + "/";

    public const string FontName = "arialuni_sdf_u2019";
    const string FontZip = "https://github.com/bbepis/XUnity.AutoTranslator/releases/download/v5.4.4/TMP_Font_AssetBundles.zip";
    const long FontHeaderOffset = 29174240;   // 該 zip 中 arialuni_sdf_u2019 的 local header 位置（未壓縮存放）
    public const long FontSize = 30986431;
    const string FontSha = "11b47cae3262648dd9c8b8a29dc25d04309a18790e4130e94fd230791e55c037";

    public const string SetupName = "SetupReiPatcherAndAutoTranslator.exe";
    const string SetupZip = "https://github.com/bbepis/XUnity.AutoTranslator/releases/download/v5.2.0/XUnity.AutoTranslator-ReiPatcher-5.2.0.zip";
    const string SetupSha = "dd5d00e507a021e5bca554aa738047d61dd60926c3f425693c78c535bc6c1b00";

    static Online()
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)12288; }   // TLS 1.2 / 1.3
        catch { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
    }

    public class Entry { public string Sha, Path; public long Size; }

    static WebClient Client()
    {
        var wc = new WebClient();
        wc.Headers[HttpRequestHeader.UserAgent] = "ws-sim-zh-installer";
        wc.Headers[HttpRequestHeader.CacheControl] = "no-cache";
        return wc;
    }
    public static byte[] Get(string url) { using (var wc = Client()) return wc.DownloadData(url); }
    public static string GetText(string url) { return Encoding.UTF8.GetString(Get(url)).TrimStart('﻿'); }

    public static string Sha256(byte[] data) { using (var h = SHA256.Create()) return Hex(h.ComputeHash(data)); }
    public static string Sha256(string file)
    {
        using (var h = SHA256.Create())
        using (var f = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            return Hex(h.ComputeHash(f));
    }
    static string Hex(byte[] b) { var sb = new StringBuilder(); foreach (var x in b) sb.Append(x.ToString("x2")); return sb.ToString(); }

    public static string RemoteVersion() { return GetText(Base + "version.txt").Trim(); }

    public static List<Entry> Manifest()
    {
        var list = new List<Entry>();
        foreach (var line in GetText(Base + "manifest.txt").Split('\n'))
        {
            var p = line.TrimEnd('\r').Split('\t');
            if (p.Length < 3 || p[0].StartsWith("#")) continue;
            list.Add(new Entry { Sha = p[0].ToLowerInvariant(), Size = long.Parse(p[1]), Path = p[2] });
        }
        if (list.Count == 0) throw new Exception("下載的檔案清單是空的");
        return list;
    }

    public static string FileUrl(string rel)
    {
        return Base + "files/" + string.Join("/", rel.Split('/').Select(Uri.EscapeDataString));
    }

    public static bool UpToDate(string file, long size, string sha)
    {
        try { var fi = new FileInfo(file); return fi.Exists && fi.Length == size && Sha256(file) == sha; }
        catch { return false; }
    }

    public static byte[] DownloadEntry(Entry e)
    {
        var data = Get(FileUrl(e.Path));
        if (data.LongLength != e.Size || Sha256(data) != e.Sha) throw new Exception("下載的檔案內容不正確：" + e.Path + "（可能是網路不穩，請再試一次）");
        return data;
    }

    static byte[] Range(string url, long from, long to)
    {
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.UserAgent = "ws-sim-zh-installer";
        req.AddRange(from, to);
        req.Timeout = 60000; req.ReadWriteTimeout = 300000;
        using (var resp = (HttpWebResponse)req.GetResponse())
        {
            if (resp.StatusCode != HttpStatusCode.PartialContent) throw new Exception("伺服器不支援分段下載");
            using (var s = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }

    // 只下載官方字型 zip 中 arialuni_sdf_u2019 那一段（約 30 MB），不需要整包 57 MB
    public static byte[] DownloadFont()
    {
        var head = Range(FontZip, FontHeaderOffset, FontHeaderOffset + 29);
        if (head[0] != 0x50 || head[1] != 0x4B || head[2] != 3 || head[3] != 4) throw new Exception("字型檔位置不正確");
        int nameLen = BitConverter.ToUInt16(head, 26), extraLen = BitConverter.ToUInt16(head, 28);
        long start = FontHeaderOffset + 30 + nameLen + extraLen;
        var data = Range(FontZip, start, start + FontSize - 1);
        if (data.LongLength != FontSize || Sha256(data) != FontSha) throw new Exception("下載的字型檔內容不正確（可能是網路不穩，請再試一次）");
        return data;
    }

    public static bool FontOk(string file) { return UpToDate(file, FontSize, FontSha); }

    public static byte[] DownloadSetup()
    {
        var zip = Get(SetupZip);
        using (var a = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
        {
            var e = a.Entries.FirstOrDefault(x => x.Name == SetupName);
            if (e == null) throw new Exception("官方套件裡找不到 " + SetupName);
            using (var s = e.Open()) using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                var data = ms.ToArray();
                if (Sha256(data) != SetupSha) throw new Exception("下載的翻譯插件安裝程式內容不正確");
                return data;
            }
        }
    }

    public static bool SetupOk(string file) { try { return File.Exists(file) && Sha256(file) == SetupSha; } catch { return false; } }

    // 寫入檔案；檔案被佔用時（例如工具開著）先改名讓開再寫
    public static bool WriteFile(string dest, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        try { File.WriteAllBytes(dest, data); return false; }
        catch (IOException)
        {
            string moved = dest + ".old";
            try { if (File.Exists(moved)) File.Delete(moved); } catch { moved = dest + "." + DateTime.Now.Ticks + ".old"; }
            File.Move(dest, moved);
            File.WriteAllBytes(dest, data);
            return true;
        }
    }
}
