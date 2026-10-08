// WS 模擬器 中文化套件 安裝程式
// 編譯：build.ps1（使用 Windows 內建的 .NET Framework csc.exe）。檔案在安裝時從 GitHub 下載（見 Online.cs）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("WS 模擬器 中文化套件 安裝程式")]
[assembly: AssemblyProduct("WS 模擬器 中文化套件")]
[assembly: AssemblyDescription("WS 模擬器（Weiss Schwarz Simulator）中文化套件的安裝與更新程式。原始碼：https://github.com/DDGaryC/ws-sim-zh")]
[assembly: AssemblyCompany("ws-sim-zh")]
[assembly: AssemblyCopyright("ws-sim-zh")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

class Installer : Form
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] args)
    {
        // 無介面模式（測試／進階用）：Installer.exe /silent "模擬器資料夾" [tw|hk|cn]、/update-silent "模擬器資料夾"
        if (args.Length >= 2 && (args[0] == "/silent" || args[0] == "/update-silent"))
        {
            var f = new Installer();
            bool upd = args[0] == "/update-silent";
            try { f.Install(args[1], upd ? null : (args.Length > 2 ? args[2] : "tw"), true, false, !upd, upd); Environment.ExitCode = 0; }
            catch (Exception ex) { f.log.AppendText("✘ " + ex.Message); Environment.ExitCode = 1; }
            File.WriteAllText(Path.Combine(args[1], "中文化安裝紀錄.txt"), f.log.Text, Encoding.UTF8);
            return;
        }
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // 更新模式：由啟動腳本在有新版本時呼叫
        if (args.Length >= 2 && args[0] == "/update") { Application.Run(new Installer(args[1])); return; }
        Application.Run(new Installer());
    }

    static readonly Color Accent = Color.FromArgb(79, 91, 213);
    const string GameExe = "Weiss Schwarz.exe";

    TextBox txtDir, log;
    Label lblDirState;
    Button btnBrowse, btnInstall, btnLaunch;
    RadioButton rbTw, rbHk, rbCn;
    CheckBox chkDesktop, chkMusic, chkDecks;
    string installedDir;
    readonly bool updateUi;

    // 更新模式的小視窗：自動開始，完成後自動關閉
    Installer(string dir) : this()
    {
        updateUi = true;
        Text = "WS 模擬器 中文化套件 更新";
        foreach (Control c in Controls) c.Visible = false;
        log.Visible = true; log.Location = new Point(20, 20); log.Size = new Size(620, 200);
        ClientSize = new Size(660, 240);
        txtDir.Text = dir;
        Shown += delegate
        {
            log.Clear();
            var t = new Thread(() =>
            {
                string err = null;
                try { Install(dir, null, Directory.Exists(Path.Combine(dir, "MusicTool")), false, false, true); }
                catch (Exception ex) { err = ex.Message; Log("✘ 更新失敗：" + ex.Message); }
                BeginInvoke(new Action(() =>
                {
                    if (err != null) MessageBox.Show(this, "更新失敗：" + err + "\n\n這次會先用目前的版本開啟遊戲。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else { var tm = new System.Windows.Forms.Timer { Interval = 1500 }; tm.Tick += delegate { tm.Stop(); Close(); }; tm.Start(); return; }
                    Close();
                }));
            });
            t.IsBackground = true; t.SetApartmentState(ApartmentState.STA); t.Start();
        };
    }

    Installer()
    {
        Text = "WS 模擬器 中文化套件 安裝程式";
        Font = new Font("Microsoft JhengHei UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(660, 630);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var bold = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold);
        Controls.Add(new Label { Text = "WS 模擬器 中文化套件", Font = new Font("Microsoft JhengHei UI", 17f, FontStyle.Bold), Location = new Point(26, 18), AutoSize = true, ForeColor = Accent });
        Controls.Add(new Label { Text = "中文介面（繁體／簡體）、中文字型、更新後自動修復的啟動捷徑，以及音樂設定工具。", Location = new Point(28, 60), AutoSize = true, ForeColor = Color.DimGray });

        Controls.Add(new Label { Text = "1. 選擇模擬器資料夾", Font = bold, Location = new Point(26, 100), AutoSize = true });
        txtDir = new TextBox { Location = new Point(30, 130), Width = 480 };
        txtDir.TextChanged += delegate { ValidateDir(); };
        btnBrowse = new Button { Text = "瀏覽…", Location = new Point(520, 128), Size = new Size(110, 32) };
        btnBrowse.Click += delegate { Browse(); };
        lblDirState = new Label { Location = new Point(30, 164), AutoSize = true };
        Controls.AddRange(new Control[] { txtDir, btnBrowse, lblDirState });

        Controls.Add(new Label { Text = "2. 介面語言", Font = bold, Location = new Point(26, 200), AutoSize = true });
        rbTw = new RadioButton { Text = "繁體中文（台灣）", Location = new Point(30, 228), AutoSize = true, Checked = true };
        rbHk = new RadioButton { Text = "繁體中文（香港）", Location = new Point(210, 228), AutoSize = true };
        rbCn = new RadioButton { Text = "简体中文", Location = new Point(390, 228), AutoSize = true };
        Controls.AddRange(new Control[] { rbTw, rbHk, rbCn });

        Controls.Add(new Label { Text = "3. 其他選項", Font = bold, Location = new Point(26, 266), AutoSize = true });
        chkMusic = new CheckBox { Text = "安裝音樂設定工具（換背景音樂、音效，設定循環點）", Location = new Point(30, 294), AutoSize = true, Checked = true };
        chkDesktop = new CheckBox { Text = "在桌面建立捷徑", Location = new Point(30, 322), AutoSize = true, Checked = true };
        bool hasLegacy = false;
        try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(LegacyDecks.RegPath)) hasLegacy = k != null; } catch { }
        chkDecks = new CheckBox { Text = hasLegacy ? "用正確的中文名稱還原舊版模擬器的牌組（偵測到舊版牌組）" : "還原舊版模擬器的牌組（這台電腦沒有偵測到）", Location = new Point(30, 350), AutoSize = true, Checked = hasLegacy, Enabled = hasLegacy };
        Controls.AddRange(new Control[] { chkMusic, chkDesktop, chkDecks });

        btnInstall = new Button { Text = "開始安裝", Location = new Point(30, 390), Size = new Size(170, 44), Font = bold, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnInstall.FlatAppearance.BorderSize = 0;
        btnInstall.Click += delegate { StartInstall(); };
        btnLaunch = new Button { Text = "▶ 啟動遊戲", Location = new Point(212, 390), Size = new Size(150, 44), Enabled = false };
        btnLaunch.Click += delegate { LaunchGame(); };
        Controls.AddRange(new Control[] { btnInstall, btnLaunch });

        log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(30, 448), Size = new Size(600, 162), BackColor = Color.FromArgb(247, 248, 252), BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(log);

        txtDir.Text = GuessDir() ?? "";
        ValidateDir();
        Log("按「瀏覽」選擇模擬器資料夾裡的 Weiss Schwarz.exe，然後按「開始安裝」。");
    }

    // ---------- 資料夾 ----------
    static string GuessDir()
    {
        var candidates = new System.Collections.Generic.List<string>();
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        candidates.Add(exeDir);
        candidates.Add(Path.GetDirectoryName(exeDir));
        foreach (var root in new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents") })
        {
            try { if (Directory.Exists(root)) candidates.AddRange(Directory.GetDirectories(root)); } catch { }
        }
        foreach (var d in candidates)
        {
            try { if (!string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, GameExe))) return d; } catch { }
        }
        return null;
    }

    bool DirOk { get { return File.Exists(Path.Combine(txtDir.Text.Trim(), GameExe)); } }

    void ValidateDir()
    {
        if (txtDir.Text.Trim() == "") { lblDirState.Text = "尚未選擇資料夾"; lblDirState.ForeColor = Color.DimGray; }
        else if (DirOk) { lblDirState.Text = "✔ 找到模擬器"; lblDirState.ForeColor = Color.SeaGreen; }
        else { lblDirState.Text = "✘ 這個資料夾裡沒有 Weiss Schwarz.exe"; lblDirState.ForeColor = Color.Firebrick; }
        btnInstall.Enabled = DirOk;
    }

    void Browse()
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "請選擇模擬器資料夾裡的 Weiss Schwarz.exe";
            dlg.Filter = "Weiss Schwarz.exe|Weiss Schwarz.exe|程式 (*.exe)|*.exe";
            if (DirOk) dlg.InitialDirectory = txtDir.Text.Trim();
            if (dlg.ShowDialog(this) == DialogResult.OK) txtDir.Text = Path.GetDirectoryName(dlg.FileName);
        }
    }

    // ---------- 安裝 ----------
    void Log(string s)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Log), s); return; }
        log.AppendText(s + Environment.NewLine);
    }

    void StartInstall()
    {
        string dir = txtDir.Text.Trim();
        if (Process.GetProcessesByName("Weiss Schwarz").Length > 0)
        {
            MessageBox.Show(this, "模擬器正在執行中，請先關閉遊戲再安裝。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (Process.GetProcessesByName("MusicTool").Length > 0)
        {
            MessageBox.Show(this, "音樂設定工具正在執行中，請先關閉它再安裝。\n（如果有尚未儲存的音樂設定，記得先按「儲存」）", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string lang = rbHk.Checked ? "hk" : rbCn.Checked ? "cn" : "tw";
        bool music = chkMusic.Checked, desktop = chkDesktop.Checked, decks = chkDecks.Checked;
        SetBusy(true);
        log.Clear();
        var t = new Thread(() =>
        {
            bool ok = false;
            try { Install(dir, lang, music, desktop, decks, false); ok = true; }
            catch (Exception ex) { Log("✘ 安裝失敗：" + ex.Message); }
            BeginInvoke(new Action(() =>
            {
                SetBusy(false);
                if (ok)
                {
                    installedDir = dir;
                    btnLaunch.Enabled = true;
                    MessageBox.Show(this, "安裝完成！\n\n以後請用「Weiss Schwarz 中文版」捷徑啟動遊戲。" + (music ? "\n換音樂請用「WS 音樂設定工具」捷徑。" : ""), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }));
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    void SetBusy(bool busy)
    {
        btnInstall.Enabled = !busy && DirOk;
        btnBrowse.Enabled = txtDir.Enabled = !busy;
        btnInstall.Text = busy ? "安裝中…" : "開始安裝";
        UseWaitCursor = busy;
    }

    // lang 為 null 表示保留目前的語言設定（更新模式）
    void Install(string dir, string lang, bool music, bool desktop, bool decks, bool update)
    {
        Log((update ? "更新位置：" : "安裝位置：") + dir);
        string iniPath = Path.Combine(dir, @"AutoTranslator\Config.ini");
        if (lang == null)
        {
            lang = "tw";
            try { var m = Regex.Match(File.ReadAllText(iniPath, Encoding.UTF8), @"(?m)^Language=(\w+)"); if (m.Success) lang = m.Groups[1].Value; } catch { }
        }
        foreach (var old in new[] { @"MusicTool\server.ps1", @"MusicTool\index.html", @"MusicTool\launch.vbs" })
            try { File.Delete(Path.Combine(dir, old)); } catch { }

        // 1. 從 GitHub 下載中文化檔案（內容相同的檔案不會重新下載）
        Log("正在取得最新版本資訊…");
        string version; List<Online.Entry> manifest;
        try { version = Online.RemoteVersion(); manifest = Online.Manifest(); }
        catch (Exception ex) { throw new Exception("無法連線到 GitHub 取得中文化檔案，請確認網路連線（" + ex.Message + "）"); }
        Log("  中文化套件 v" + version + "，共 " + manifest.Count + " 個檔案");
        int n = 0, skipped = 0;
        string root = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
        foreach (var e in manifest)
        {
            if (!music && e.Path.StartsWith("MusicTool/", StringComparison.OrdinalIgnoreCase)) continue;
            string dest = Path.GetFullPath(Path.Combine(dir, e.Path.Replace('/', '\\')));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            bool isIni = e.Path.Equals("AutoTranslator/Config.ini", StringComparison.OrdinalIgnoreCase);
            if (!isIni && Online.UpToDate(dest, e.Size, e.Sha)) { skipped++; continue; }
            if (isIni && File.Exists(dest) && update) { skipped++; continue; }   // 更新時保留使用者自己的設定檔
            Log("  下載 " + e.Path);
            if (Online.WriteFile(dest, Online.DownloadEntry(e))) Log("    （正在使用中，已改名為 .old 後更新，重新開啟該程式即可）");
            n++;
        }
        Log("  已更新 " + n + " 個檔案" + (skipped > 0 ? "（" + skipped + " 個已是最新，略過）" : ""));

        // 1b. 中文字型、翻譯插件安裝程式：直接從 XUnity 官方下載
        string fontPath = Path.Combine(dir, Online.FontName);
        if (!Online.FontOk(fontPath))
        {
            Log("正在下載中文字型（約 30 MB，來源：XUnity.AutoTranslator 官方）…");
            Online.WriteFile(fontPath, Online.DownloadFont());
            Log("  中文字型下載完成");
        }
        string setupPath = Path.Combine(dir, Online.SetupName);
        if (!Online.SetupOk(setupPath))
        {
            Log("正在下載翻譯插件安裝程式（來源：XUnity.AutoTranslator 官方）…");
            Online.WriteFile(setupPath, Online.DownloadSetup());
        }
        // 清掉之前留下的 .old 檔（刪不掉代表還在使用中，下次再清）
        foreach (var old in Directory.GetFiles(dir, "*.old", SearchOption.AllDirectories)) try { File.Delete(old); } catch { }

        // 2. 設定語言
        string ini = iniPath;
        string text = File.ReadAllText(ini, Encoding.UTF8);
        text = Regex.Replace(text, @"(?m)^Language=.*$", "Language=" + lang);
        File.WriteAllText(ini, text, new UTF8Encoding(true));
        Log("介面語言：" + (lang == "tw" ? "繁體中文（台灣）" : lang == "hk" ? "繁體中文（香港）" : "简体中文"));

        // 3. 安裝翻譯插件（更新模式下，插件還在就不用重裝）
        bool pluginOk = File.Exists(Path.Combine(dir, @"Weiss Schwarz_Data\Managed\XUnity.AutoTranslator.Plugin.Core.dll"));
        if (!update || !pluginOk) {
        Log("正在安裝翻譯插件（XUnity.AutoTranslator）…");
        var psi = new ProcessStartInfo(Path.Combine(dir, "SetupReiPatcherAndAutoTranslator.exe"))
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using (var p = Process.Start(psi))
        {
            p.StandardInput.WriteLine();   // 安裝程式最後會等使用者按任意鍵
            p.StandardInput.Close();
            p.StandardError.ReadToEndAsync();
            p.StandardOutput.ReadToEnd();
            p.WaitForExit(120000);
        }
        if (!File.Exists(Path.Combine(dir, @"Weiss Schwarz_Data\Managed\XUnity.AutoTranslator.Plugin.Core.dll")))
            throw new Exception("翻譯插件沒有安裝成功，請確認資料夾可以寫入（不要放在 Program Files 之類需要管理員權限的位置）。");
        Log("  翻譯插件安裝完成");
        }

        // 版本紀錄與更新程式（啟動腳本會用它們檢查與套用更新）
        File.WriteAllText(Path.Combine(dir, "wszh_version.txt"), version, new UTF8Encoding(false));
        try
        {
            string self = Path.GetFullPath(Application.ExecutablePath), updater = Path.Combine(dir, "wszh_updater.exe");
            if (!string.Equals(self, Path.GetFullPath(updater), StringComparison.OrdinalIgnoreCase)) Online.WriteFile(updater, File.ReadAllBytes(self));
        }
        catch (Exception ex) { Log("  （複製更新程式失敗：" + ex.Message + "）"); }

        if (update)
        {
            try { FixBanlist(Path.Combine(dir, @"Weiss Schwarz_Data\StreamingAssets\Banlist.txt")); } catch { }
            Log("✔ 已更新到 v" + version);
            return;
        }

        // 4. 捷徑
        Log("正在建立捷徑…");
        string wscript = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wscript.exe");
        string gameIcon = Path.Combine(dir, GameExe) + ",0";
        string musicIcon = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SndVol.exe") + ",0";
        string gameArgs = "\"" + Path.Combine(dir, "WSLaunch.vbs") + "\"";
        string musicExe = Path.Combine(dir, @"MusicTool\MusicTool.exe");
        MakeShortcut(Path.Combine(dir, "Weiss Schwarz (Patch and Run).lnk"), wscript, gameArgs, dir, gameIcon, "以中文介面啟動 Weiss Schwarz 模擬器");
        MakeShortcut(Path.Combine(dir, "Weiss Schwarz 中文版.lnk"), wscript, gameArgs, dir, gameIcon, "以中文介面啟動 Weiss Schwarz 模擬器");
        if (music) MakeShortcut(Path.Combine(dir, "音樂設定工具.lnk"), musicExe, "", Path.Combine(dir, "MusicTool"), musicIcon, "WS 模擬器 音樂設定工具");
        if (desktop)
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            MakeShortcut(Path.Combine(desk, "Weiss Schwarz 中文版.lnk"), wscript, gameArgs, dir, gameIcon, "以中文介面啟動 Weiss Schwarz 模擬器");
            if (music) MakeShortcut(Path.Combine(desk, "WS 音樂設定工具.lnk"), musicExe, "", Path.Combine(dir, "MusicTool"), musicIcon, "WS 模擬器 音樂設定工具");
            Log("  已在桌面建立捷徑");
        }
        // 5. 修正禁限卡表：模擬器讀到空白行或只有註解的行會在主選單初始化時當掉（更新提示按鈕會沒反應）
        try { if (FixBanlist(Path.Combine(dir, @"Weiss Schwarz_Data\StreamingAssets\Banlist.txt"))) Log("已修正禁限卡表（移除會讓遊戲出錯的空白行，原檔備份為 Banlist.txt.orig）"); }
        catch (Exception ex) { Log("  修正禁限卡表失敗：" + ex.Message); }

        // 6. 舊版牌組
        if (decks)
        {
            Log("正在還原舊版模擬器的牌組…");
            try
            {
                var plan = LegacyDecks.MakePlan(dir);
                if (plan.Error != null) Log("  " + plan.Error);
                else if (plan.Total == 0) Log("  沒有找到舊版牌組");
                else LegacyDecks.Execute(plan, dir, Log);
            }
            catch (Exception ex) { Log("  還原牌組失敗：" + ex.Message + "（可以之後再用「舊牌組修復工具」）"); }
        }
        Log("✔ 安裝完成！以後請用「Weiss Schwarz 中文版」捷徑啟動遊戲。");
        Log("  請不要使用遊戲裡的「匯入舊版牌組」，中文名稱會變亂碼；需要時請用「舊牌組修復工具.exe」。");
        Log("  模擬器更新後，捷徑會自動重新安裝翻譯插件；中文化有新版本時，開遊戲會詢問是否更新。");
    }

    static bool SameAsExisting(ZipArchiveEntry e, string dest)
    {
        try
        {
            var fi = new FileInfo(dest);
            if (!fi.Exists || fi.Length != e.Length) return false;
            using (var a = e.Open())
            using (var b = new FileStream(dest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var ba = new byte[81920]; var bb = new byte[81920];
                while (true)
                {
                    int na = 0, r;
                    while (na < ba.Length && (r = a.Read(ba, na, ba.Length - na)) > 0) na += r;
                    int nb = 0;
                    while (nb < na && (r = b.Read(bb, nb, na - nb)) > 0) nb += r;
                    if (na != nb) return false;
                    for (int i = 0; i < na; i++) if (ba[i] != bb[i]) return false;
                    if (na == 0) return true;
                }
            }
        }
        catch { return false; }
    }

    static bool FixBanlist(string p)
    {
        if (!File.Exists(p)) return false;
        var lines = File.ReadAllLines(p);
        var keep = new System.Collections.Generic.List<string>();
        foreach (var l in lines)
        {
            int c = l.IndexOf("//");
            if ((c >= 0 ? l.Substring(0, c) : l).Trim().Length > 0) keep.Add(l);
        }
        if (keep.Count == lines.Length) return false;
        if (!File.Exists(p + ".orig")) File.Copy(p, p + ".orig");
        File.WriteAllText(p, string.Join("\r\n", keep) + "\r\n", new UTF8Encoding(false));
        return true;
    }

    static void MakeShortcut(string lnk, string target, string args, string workDir, string icon, string desc)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(t);
        try
        {
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("Arguments", BindingFlags.SetProperty, null, sc, new object[] { args });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
            st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { icon });
            st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            Marshal.FinalReleaseComObject(sc);
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }

    void LaunchGame()
    {
        if (installedDir == null) return;
        Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wscript.exe"),
            "\"" + Path.Combine(installedDir, "WSLaunch.vbs") + "\"");
        Close();
    }
}
