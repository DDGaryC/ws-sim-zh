// WS 模擬器 舊牌組修復工具：從舊版模擬器（登錄檔）以正確中文名稱還原牌組，並收拾遊戲內建匯入造成的亂碼檔。
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("WS 模擬器 舊牌組修復工具")]
[assembly: AssemblyProduct("WS 模擬器 中文化套件")]
[assembly: AssemblyVersion("1.0.0.0")]

class DeckRescueForm : Form
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "/check")
        {
            var p = LegacyDecks.MakePlan(args[1]);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "deckrescue_check.txt"),
                "total=" + p.Total + " ok=" + p.AlreadyOk + " write=" + p.ToWrite.Count + " move=" + p.ToMove.Count + " err=" + p.Error);
            return;
        }
        if (args.Length >= 2 && args[0] == "/fix")
        {
            var p = LegacyDecks.MakePlan(args[1]);
            var sb = new System.Text.StringBuilder();
            LegacyDecks.Execute(p, args[1], s => sb.AppendLine(s));
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "deckrescue_check.txt"), sb.ToString());
            return;
        }
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.Run(new DeckRescueForm());
    }

    static readonly Color Accent = Color.FromArgb(79, 91, 213);
    TextBox txtDir, log;
    Label lblState;
    Button btnRun;

    DeckRescueForm()
    {
        Text = "WS 模擬器 舊牌組修復工具";
        Font = new Font("Microsoft JhengHei UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; BackColor = Color.White;

        Controls.Add(new Label { Text = "舊牌組修復工具", Font = new Font("Microsoft JhengHei UI", 16f, FontStyle.Bold), ForeColor = Accent, AutoSize = true, Location = new Point(24, 16) });
        Controls.Add(new Label
        {
            Text = "從舊版模擬器以「正確的中文名稱」還原牌組。\n如果曾經按過遊戲裡的「匯入舊版牌組」而出現亂碼，亂碼檔會被搬到備份資料夾（不會刪除）。",
            Location = new Point(26, 56), Size = new Size(590, 48), ForeColor = Color.DimGray
        });
        Controls.Add(new Label { Text = "模擬器資料夾", Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold), Location = new Point(24, 112), AutoSize = true });
        txtDir = new TextBox { Location = new Point(28, 140), Width = 470 };
        var btnBrowse = new Button { Text = "瀏覽…", Location = new Point(508, 138), Size = new Size(104, 30) };
        lblState = new Label { Location = new Point(28, 174), AutoSize = true };
        btnRun = new Button { Text = "開始修復", Location = new Point(28, 206), Size = new Size(170, 42), BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold) };
        btnRun.FlatAppearance.BorderSize = 0;
        log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(28, 262), Size = new Size(584, 186), BackColor = Color.FromArgb(247, 248, 252), BorderStyle = BorderStyle.FixedSingle };
        Controls.AddRange(new Control[] { txtDir, btnBrowse, lblState, btnRun, log });

        txtDir.TextChanged += delegate { Validate2(); };
        btnBrowse.Click += delegate
        {
            using (var dlg = new OpenFileDialog { Title = "請選擇模擬器資料夾裡的 Weiss Schwarz.exe", Filter = "Weiss Schwarz.exe|Weiss Schwarz.exe" })
                if (dlg.ShowDialog(this) == DialogResult.OK) txtDir.Text = Path.GetDirectoryName(dlg.FileName);
        };
        btnRun.Click += delegate { Run(); };

        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        foreach (var d in new[] { exeDir, Path.GetDirectoryName(exeDir) })
            if (d != null && File.Exists(Path.Combine(d, "Weiss Schwarz.exe"))) { txtDir.Text = d; break; }
        Validate2();
        Log("按「開始修復」會先檢查，確認後才會動到檔案。");
    }

    bool DirOk { get { return File.Exists(Path.Combine(txtDir.Text.Trim(), "Weiss Schwarz.exe")); } }
    void Validate2()
    {
        lblState.Text = DirOk ? "✔ 找到模擬器" : "✘ 請選擇有 Weiss Schwarz.exe 的資料夾";
        lblState.ForeColor = DirOk ? Color.SeaGreen : Color.Firebrick;
        btnRun.Enabled = DirOk;
    }
    void Log(string s) { log.AppendText(s + Environment.NewLine); }

    void Run()
    {
        string dir = txtDir.Text.Trim();
        log.Clear();
        Log("正在讀取舊版模擬器的牌組…");
        var plan = LegacyDecks.MakePlan(dir);
        if (plan.Error != null) { Log("✘ " + plan.Error); return; }
        if (plan.Total == 0) { Log("這台電腦上沒有找到舊版模擬器的牌組，不需要修復。"); return; }
        Log("  找到 " + plan.Total + " 副舊牌組（不含 AI 牌組）");
        Log("  其中 " + plan.AlreadyOk + " 副已經在 Decks 裡");
        Log("  需要還原：" + plan.ToWrite.Count + " 副");
        Log("  亂碼檔：" + plan.ToMove.Count + " 個");
        if (plan.ToWrite.Count == 0 && plan.ToMove.Count == 0) { Log("✔ 全部都已經正確，不需要修復。"); return; }
        if (Process.GetProcessesByName("Weiss Schwarz").Length > 0)
            MessageBox.Show(this, "模擬器正在執行中。修復完成後，請重新開啟牌組選單才會看到新的牌組。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        string msg = "將還原 " + plan.ToWrite.Count + " 副牌組" + (plan.ToMove.Count > 0 ? "，並把 " + plan.ToMove.Count + " 個亂碼檔搬到「" + LegacyDecks.BackupFolder + "」資料夾" : "") + "。\n\n要繼續嗎？";
        if (MessageBox.Show(this, msg, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { Log("已取消。"); return; }
        try { LegacyDecks.Execute(plan, dir, Log); Log("✔ 完成！打開遊戲的牌組選單就能看到。"); }
        catch (Exception ex) { Log("✘ 失敗：" + ex.Message); }
    }
}

static class Process
{
    public static System.Diagnostics.Process[] GetProcessesByName(string n) { return System.Diagnostics.Process.GetProcessesByName(n); }
}
