// WS 模擬器 AI 對手編輯器（介面）
// 用表單設定每張卡的使用時機、權重等，產生遊戲的 V7 AI 腳本，不需要會寫程式。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("WS 模擬器 AI 對手編輯器")]
[assembly: AssemblyProduct("WS 模擬器 中文化套件")]
[assembly: AssemblyVersion("1.0.0.0")]

// ======================================================================
// 樣式
// ======================================================================
static class UI
{
    public static readonly Color Bg = Color.FromArgb(244, 245, 249), Panel = Color.White, Line = Color.FromArgb(227, 230, 239);
    public static readonly Color Text = Color.FromArgb(31, 35, 48), Muted = Color.FromArgb(107, 113, 131);
    public static readonly Color Accent = Color.FromArgb(79, 91, 213), AccentSoft = Color.FromArgb(236, 238, 253);
    public static readonly Color Danger = Color.FromArgb(214, 69, 69), Ok = Color.FromArgb(31, 157, 85);
    public const string FontName = "Microsoft JhengHei UI";
    public static Font F(float size, FontStyle st = FontStyle.Regular) { return new Font(FontName, size, st); }
    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    public static void Fill(Graphics g, RectangleF r, float rad, Color c) { using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
    public static void Stroke(Graphics g, RectangleF r, float rad, Color c) { using (var p = Round(r, rad)) using (var pen = new Pen(c)) g.DrawPath(pen, p); }
    public static void Smooth(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; }
    public static void Str(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near)
    {
        using (var sf = new StringFormat { Alignment = h, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        using (var b = new SolidBrush(c)) g.DrawString(s, f, b, r, sf);
    }
    public static float W(Graphics g, string s, Font f) { return g.MeasureString(s, f, 2000, StringFormat.GenericTypographic).Width; }
}

class FlatButton : Control
{
    public bool Primary, Ghost, Small;
    bool hover, down;
    public FlatButton(string text, int w = 0, bool small = false)
    {
        Small = small; Text = text; Font = UI.F(small ? 9f : 10f); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        using (var g = CreateGraphics()) Size = new Size(Math.Max(w, (int)UI.W(g, text, Font) + (small ? 18 : 28)), small ? 26 : 34);
        Margin = new Padding(3);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Color bg, fg = UI.Text, border = UI.Line;
        if (Primary) { bg = hover ? Color.FromArgb(98, 109, 225) : UI.Accent; fg = Color.White; border = bg; if (down) bg = Color.FromArgb(66, 77, 196); }
        else if (Ghost) { bg = hover ? UI.AccentSoft : Color.Transparent; border = Color.Transparent; fg = UI.Accent; }
        else { bg = hover ? Color.FromArgb(248, 249, 252) : Color.White; if (hover) border = Color.FromArgb(197, 202, 219); }
        if (!Enabled) fg = Color.FromArgb(170, 174, 188);
        if (bg.A > 0) UI.Fill(g, r, 7, bg);
        if (border.A > 0) UI.Stroke(g, r, 7, border);
        UI.Str(g, Text, Font, fg, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
    }
}

// 白色區塊：標題＋內容（由上往下排）
class Box : FlowLayoutPanel
{
    public Box(string title, string sub = null)
    {
        FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = UI.Panel; Padding = new Padding(18, 14, 18, 14); Margin = new Padding(0, 0, 0, 14);
        if (title != null) Controls.Add(new Label { Text = title, Font = UI.F(12.5f, FontStyle.Bold), ForeColor = UI.Text, AutoSize = true, Margin = new Padding(0, 0, 0, sub == null ? 8 : 2) });
        if (sub != null) Controls.Add(Wrap(sub, UI.Muted, 9.5f));
    }
    public static Label Wrap(string text, Color c, float size = 10f)
    {
        return new Label { Text = text, Font = UI.F(size), ForeColor = c, AutoSize = true, Tag = "wrap", Margin = new Padding(0, 2, 0, 8), UseMnemonic = false };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var p = new Pen(UI.Line)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

class Item
{
    public string Key, Text;
    public Item(string k, string t) { Key = k; Text = t; }
    public override string ToString() { return Text; }
}

// 卡圖：縮圖快取、滑鼠移上去顯示大圖
static class Thumbs
{
    static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();
    public static Dictionary<string, CardInfo> ByName = new Dictionary<string, CardInfo>();
    public static Image Get(CardInfo c, int w, int h)
    {
        if (c == null || c.Image == null) return null;
        string key = c.Code + "|" + w + "x" + h;
        Image im;
        if (cache.TryGetValue(key, out im)) return im;
        try
        {
            using (var src = Image.FromFile(c.Image))
            {
                double k = Math.Min(w / (double)src.Width, h / (double)src.Height);
                var bmp = new Bitmap(Math.Max(1, (int)(src.Width * k)), Math.Max(1, (int)(src.Height * k)));
                using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, bmp.Width, bmp.Height); }
                im = bmp;
            }
        }
        catch { im = null; }
        cache[key] = im; return im;
    }
    public static Image ByCardName(string name, int w, int h)
    {
        CardInfo c;
        return name != null && ByName.TryGetValue(name, out c) ? Get(c, w, h) : null;
    }

    static Form tip; static PictureBox tipPic;
    public static void ShowBig(CardInfo c, Control near)
    {
        var im = Get(c, 380, 380);
        if (im == null) return;
        if (tip == null)
        {
            tip = new TipForm();
            tipPic = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
            tip.Controls.Add(tipPic);
        }
        tipPic.Image = im; tip.ClientSize = im.Size;
        var pt = near.PointToScreen(new Point(near.Width + 8, 0));
        var scr = Screen.FromControl(near).WorkingArea;
        if (pt.X + tip.Width > scr.Right) pt.X = near.PointToScreen(Point.Empty).X - tip.Width - 8;
        if (pt.Y + tip.Height > scr.Bottom) pt.Y = scr.Bottom - tip.Height;
        tip.Location = pt; tip.Show();
    }
    public static void HideBig() { if (tip != null) tip.Hide(); }
    class TipForm : Form
    {
        public TipForm() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; BackColor = Color.White; }
        protected override bool ShowWithoutActivation { get { return true; } }
    }
    // 一張卡的圖（可點、滑鼠移上去看大圖）
    public static PictureBox Pic(CardInfo c, int w, int h, Action click)
    {
        var p = new PictureBox { Width = w, Height = h, SizeMode = PictureBoxSizeMode.Zoom, Image = Get(c, w, h), BackColor = UI.Bg, Margin = new Padding(0, 0, 0, 4), Cursor = click != null ? Cursors.Hand : Cursors.Default };
        p.MouseEnter += delegate { ShowBig(c, p); };
        p.MouseLeave += delegate { HideBig(); };
        if (click != null) p.Click += delegate { HideBig(); click(); };
        return p;
    }
}

static class Ctl
{
    public static ComboBox Combo(IEnumerable<Item> items, string sel, int w, Action<string> changed)
    {
        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = w, Font = UI.F(10f), Margin = new Padding(3, 3, 3, 3), MaxDropDownItems = 20 };
        foreach (var it in items) cb.Items.Add(it);
        foreach (Item it in cb.Items) if (it.Key == sel) { cb.SelectedItem = it; break; }
        if (cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0;
        cb.SelectedIndexChanged += delegate { var it = cb.SelectedItem as Item; if (it != null) changed(it.Key); };
        cb.MouseWheel += (s, e) => { if (!cb.DroppedDown) ((HandledMouseEventArgs)e).Handled = true; };   // 避免捲動頁面時誤改
        // 選項裡有卡片時，在名稱前面畫出卡圖
        bool hasCard = false;
        foreach (Item it in cb.Items) if (Thumbs.ByName.ContainsKey(it.Key)) { hasCard = true; break; }
        if (hasCard)
        {
            cb.DrawMode = DrawMode.OwnerDrawFixed; cb.ItemHeight = 44; cb.DropDownHeight = 44 * 10 + 4;
            cb.DrawItem += (s, e) =>
            {
                e.DrawBackground();
                if (e.Index < 0) return;
                var it = cb.Items[e.Index] as Item;
                var im = Thumbs.ByCardName(it.Key, 40, 40);
                if (im != null) e.Graphics.DrawImage(im, e.Bounds.X + 3 + (40 - im.Width) / 2, e.Bounds.Y + (e.Bounds.Height - im.Height) / 2, im.Width, im.Height);
                TextRenderer.DrawText(e.Graphics, it.Text, cb.Font, new Rectangle(e.Bounds.X + 48, e.Bounds.Y, e.Bounds.Width - 50, e.Bounds.Height), e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }
        return cb;
    }
    public static IEnumerable<Item> Levels() { for (int i = 0; i < Defs.LevelNames.Length; i++) yield return new Item(i.ToString(), Defs.LevelNames[i]); }
    public static NumericUpDown Num(double v, double min, double max, int w, Action<double> changed)
    {
        var n = new NumericUpDown { Minimum = (decimal)min, Maximum = (decimal)max, Value = (decimal)Math.Max(min, Math.Min(max, v)), Width = w, Font = UI.F(10f), Margin = new Padding(3, 4, 3, 3) };
        n.ValueChanged += delegate { changed((double)n.Value); };
        n.MouseWheel += (s, e) => { ((HandledMouseEventArgs)e).Handled = true; };
        return n;
    }
    public static CheckBox Check(string text, bool v, Action<bool> changed)
    {
        var c = new CheckBox { Text = text, Checked = v, AutoSize = true, Font = UI.F(10f), ForeColor = UI.Text, Margin = new Padding(3, 4, 12, 4) };
        c.CheckedChanged += delegate { changed(c.Checked); };
        return c;
    }
    public static FlowLayoutPanel Row(string label, int labelW, params Control[] cs)
    {
        var r = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 1, 0, 1) };
        if (label != null) r.Controls.Add(new Label { Text = label, Width = labelW, AutoSize = false, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Font = UI.F(10f), ForeColor = UI.Text });
        foreach (var c in cs) if (c != null) r.Controls.Add(c);
        return r;
    }
    public static Label Hint(string t) { return new Label { Text = t, AutoSize = true, Font = UI.F(9f), ForeColor = UI.Muted, Margin = new Padding(6, 9, 3, 3) }; }
}

// ======================================================================
// 條件編輯器（「全部成立／任一成立」＋多個條件）
// ======================================================================
class CondEditor : FlowLayoutPanel
{
    readonly Cond cond; readonly List<Item> cardItems; readonly Action changed;
    public CondEditor(Cond cond, List<Item> cardItems, Action changed)
    {
        this.cond = cond; this.cardItems = cardItems; this.changed = changed;
        FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoSize = true; Margin = new Padding(0, 2, 0, 4);
        BackColor = Color.FromArgb(248, 249, 252); Padding = new Padding(8, 4, 8, 4);
        Render();
    }
    void Render()
    {
        SuspendLayout();
        Controls.Clear();
        if (cond.Items.Count > 1)
            Controls.Add(Ctl.Row(null, 0, new Label { Text = "以下條件", AutoSize = true, Font = UI.F(10f), Margin = new Padding(3, 8, 0, 0) },
                Ctl.Combo(new[] { new Item("all", "全部成立"), new Item("any", "任一成立") }, cond.Any ? "any" : "all", 110, k => { cond.Any = k == "any"; changed(); }),
                new Label { Text = "時", AutoSize = true, Font = UI.F(10f), Margin = new Padding(0, 8, 0, 0) }));
        foreach (var cl in cond.Items.ToList()) Controls.Add(ClauseRow(cl));
        if (cond.Items.Count == 0) Controls.Add(new Label { Text = "（還沒有條件：按「＋ 加條件」）", AutoSize = true, Font = UI.F(9.5f), ForeColor = UI.Muted, Margin = new Padding(3, 6, 3, 2) });
        var add = new FlatButton("＋ 加條件", 0, true) { Ghost = true };
        add.Click += delegate { cond.Items.Add(new Clause()); Render(); changed(); };
        Controls.Add(add);
        ResumeLayout();
    }
    Control ClauseRow(Clause cl)
    {
        var key = Defs.Key(cl.Key);
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
        row.Controls.Add(Ctl.Combo(Defs.Keys.Select(k => new Item(k.Key, k.Label)), cl.Key, 200, k =>
        {
            cl.Key = k; var nk = Defs.Key(k);
            if (nk.NeedCard && string.IsNullOrEmpty(cl.Card)) cl.Card = "#Char";
            if (nk.Bool) cl.Val = 1; else if (nk.Percent && cl.Val <= 1) cl.Val = 25;
            Render(); changed();
        }));
        if (key.NeedCard) row.Controls.Add(Ctl.Combo(cardItems, cl.Card, 230, k => { cl.Card = k; changed(); }));
        if (key.Bool)
            row.Controls.Add(Ctl.Combo(new[] { new Item("1", "是"), new Item("0", "否") }, cl.Val > 0 ? "1" : "0", 70, k => { cl.Val = k == "1" ? 1 : 0; changed(); }));
        else
        {
            row.Controls.Add(Ctl.Combo(new[] { ">=", "<=", "=", "!=", ">", "<" }.Select(o => new Item(o, o == ">=" ? "≥ 至少" : o == "<=" ? "≤ 最多" : o == "=" ? "＝ 剛好" : o == "!=" ? "≠ 不是" : o == ">" ? "＞ 大於" : "＜ 小於")), cl.Op, 90, k => { cl.Op = k; changed(); }));
            row.Controls.Add(Ctl.Num(cl.Val, 0, key.Percent ? 100 : 60, 64, v => { cl.Val = v; changed(); }));
            if (key.Percent) row.Controls.Add(new Label { Text = "%", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        }
        var del = new FlatButton("✕", 0, true) { Ghost = true };
        del.Click += delegate { cond.Items.Remove(cl); Render(); changed(); };
        row.Controls.Add(del);
        return row;
    }
}

// 目標偏好（排越前面越優先）
class TargetEditor : FlowLayoutPanel
{
    readonly List<string> list; readonly List<Item> items; readonly Action changed; readonly Func<bool> getFb; readonly Action<bool> setFb;
    public TargetEditor(List<string> list, List<Item> items, Action changed, Func<bool> getFb, Action<bool> setFb)
    {
        this.list = list; this.items = items; this.changed = changed; this.getFb = getFb; this.setFb = setFb;
        FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoSize = true; Margin = new Padding(0, 2, 0, 4);
        Render();
    }
    void Render()
    {
        SuspendLayout(); Controls.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            int idx = i;
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
            row.Controls.Add(new Label { Text = (i + 1) + ".", Width = 26, Height = 30, TextAlign = ContentAlignment.MiddleRight, Font = UI.F(10f) });
            string zone, key; Defs.SplitZone(list[i], out zone, out key);
            row.Controls.Add(Ctl.Combo(Defs.Zones.Select(z => new Item(z[0], z[1])), zone, 110, z => { string z0, k0; Defs.SplitZone(list[idx], out z0, out k0); list[idx] = Defs.JoinZone(z, k0); changed(); }));
            row.Controls.Add(Ctl.Combo(items, key, 300, k => { string z0, k0; Defs.SplitZone(list[idx], out z0, out k0); list[idx] = Defs.JoinZone(z0, k); changed(); }));
            if (i > 0)
            {
                var up = new FlatButton("↑", 0, true) { Ghost = true };
                up.Click += delegate { var t = list[idx]; list[idx] = list[idx - 1]; list[idx - 1] = t; Render(); changed(); };
                row.Controls.Add(up);
            }
            var del = new FlatButton("✕", 0, true) { Ghost = true };
            del.Click += delegate { list.RemoveAt(idx); Render(); changed(); };
            row.Controls.Add(del);
            Controls.Add(row);
        }
        var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
        var add = new FlatButton("＋ 新增偏好目標", 0, true) { Ghost = true };
        add.Click += delegate { list.Add(items.Count > 0 ? items[0].Key : "#Char"); Render(); changed(); };
        bar.Controls.Add(add);
        if (list.Count > 0 && setFb != null) bar.Controls.Add(Ctl.Check("偏好的都沒有時，也可以選其他合法目標", getFb(), v => { setFb(v); changed(); }));
        Controls.Add(bar);
        ResumeLayout();
    }
}

// ======================================================================
// 左側清單
// ======================================================================
class NavList : Control
{
    public class Entry { public string Key, Text, Sub; public CardInfo Card; public int Count; public bool Header; }
    public readonly List<Entry> Entries = new List<Entry>();
    public string Selected;
    public event Action<string> Picked;
    int hover = -1;
    readonly Dictionary<string, Image> thumbs = new Dictionary<string, Image>();
    const int RowH = 62, HeadH = 34, SpecialH = 44;
    public NavList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = UI.F(10f); BackColor = UI.Panel;
    }
    int H(Entry e) { return e.Header ? HeadH : e.Card == null ? SpecialH : RowH; }
    public int TotalHeight { get { return Entries.Sum(e => H(e)) + 10; } }
    int IndexAt(int y)
    {
        int acc = 0;
        for (int i = 0; i < Entries.Count; i++) { int h = H(Entries[i]); if (y >= acc && y < acc + h) return Entries[i].Header ? -1 : i; acc += h; }
        return -1;
    }
    protected override void OnMouseMove(MouseEventArgs e) { int i = IndexAt(e.Y); if (i != hover) { hover = i; Cursor = i >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e) { int i = IndexAt(e.Y); if (i >= 0 && Picked != null) Picked(Entries[i].Key); }
    Image Thumb(CardInfo c)
    {
        Image im;
        if (thumbs.TryGetValue(c.Code, out im)) return im;
        im = null;
        try
        {
            if (c.Image != null)
                using (var src = Image.FromFile(c.Image))
                {
                    bool land = src.Width > src.Height;
                    var bmp = new Bitmap(land ? 56 : 40, land ? 40 : 56);
                    using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, bmp.Width, bmp.Height); }
                    im = bmp;
                }
        }
        catch { }
        thumbs[c.Code] = im; return im;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Smooth(g); g.Clear(UI.Panel);
        int y = 0;
        for (int i = 0; i < Entries.Count; i++)
        {
            var en = Entries[i]; int h = H(en);
            if (y + h >= e.ClipRectangle.Top && y <= e.ClipRectangle.Bottom)
            {
                if (en.Header) UI.Str(g, en.Text, UI.F(9.5f, FontStyle.Bold), UI.Muted, new RectangleF(16, y + 10, Width - 20, h - 10));
                else
                {
                    var r = new RectangleF(8, y + 2, Width - 16, h - 4);
                    if (en.Key == Selected) UI.Fill(g, r, 8, UI.AccentSoft);
                    else if (i == hover) UI.Fill(g, r, 8, Color.FromArgb(246, 247, 251));
                    if (en.Card == null) UI.Str(g, en.Text, UI.F(10.5f, en.Key == Selected ? FontStyle.Bold : FontStyle.Regular), en.Key == Selected ? UI.Accent : UI.Text, new RectangleF(20, y, Width - 30, h));
                    else
                    {
                        var im = Thumb(en.Card);
                        if (im != null) g.DrawImage(im, 16 + (40 - Math.Min(40, im.Width)) / 2f, y + (h - im.Height) / 2f, im.Width > 40 ? 40 : im.Width, im.Width > 40 ? 40 * im.Height / (float)im.Width : im.Height);
                        else UI.Fill(g, new RectangleF(16, y + 3, 40, 56), 4, UI.Line);
                        UI.Str(g, en.Text, UI.F(9.5f, en.Key == Selected ? FontStyle.Bold : FontStyle.Regular), UI.Text, new RectangleF(64, y + 8, Width - 110, 22));
                        UI.Str(g, en.Sub, UI.F(8.5f), UI.Muted, new RectangleF(64, y + 30, Width - 110, 20));
                        UI.Str(g, "×" + en.Count, UI.F(9.5f), UI.Muted, new RectangleF(Width - 46, y, 34, h), StringAlignment.Far);
                    }
                }
            }
            y += h;
        }
    }
}

// ======================================================================
// 主視窗
// ======================================================================
class MainForm : Form
{
    readonly string root, myAiDir;
    AiProject proj;
    string savedJson = "";
    string page = "home";
    List<CardInfo> cards = new List<CardInfo>();
    List<Item> cardItems = new List<Item>(), groupItems = new List<Item>();

    TextBox txtName;
    FlatButton btnSave, btnPreview, btnDeck, btnOpen, btnSwap;
    Panel navHost; NavList nav;
    FlowLayoutPanel pageHost;
    Label toast; Timer toastTimer;
    bool building;
    static bool showAdvanced;   // 卡片頁的進階設定是否展開

    public MainForm(string root)
    {
        this.root = root;
        myAiDir = Path.Combine(root, "自製AI");
        CardDb.Root = root;
        Text = "WS 模擬器 AI 對手編輯器";
        Font = UI.F(10f); BackColor = UI.Bg;
        ClientSize = new Size(1280, 860); MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // 上方列
        var head = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = UI.Panel };
        head.Paint += (s, e) => { using (var p = new Pen(UI.Line)) e.Graphics.DrawLine(p, 0, head.Height - 1, head.Width, head.Height - 1); };
        var title = new Label { Text = "AI 對手編輯器", Font = UI.F(15f, FontStyle.Bold), ForeColor = UI.Text, AutoSize = true, Location = new Point(20, 17) };
        var lblName = new Label { Text = "AI 名稱", AutoSize = true, Font = UI.F(10f), ForeColor = UI.Muted, Location = new Point(210, 23) };
        txtName = new TextBox { Font = UI.F(11f), Width = 220, Location = new Point(272, 18) };
        txtName.TextChanged += delegate { if (proj != null && !building) { proj.Name = txtName.Text.Trim(); UpdateTitle(); } };
        btnDeck = new FlatButton("開新 AI"); btnOpen = new FlatButton("開啟");
        btnPreview = new FlatButton("預覽腳本"); btnSave = new FlatButton("儲存並安裝到遊戲", 150) { Primary = true };
        btnSwap = new FlatButton("換牌組");
        btnSwap.Click += delegate { ChangeDeck(); };
        btnDeck.Click += delegate { NewFromDeck(); };
        btnOpen.Click += delegate { OpenExisting(); };
        btnPreview.Click += delegate { Preview(); };
        btnSave.Click += delegate { SaveAndInstall(); };
        head.Controls.AddRange(new Control[] { title, lblName, txtName, btnDeck, btnOpen, btnSwap, btnPreview, btnSave });
        head.Resize += delegate
        {
            int x = head.Width - 20;
            foreach (var b in new[] { btnSave, btnPreview, btnSwap, btnOpen, btnDeck }) { x -= b.Width; b.Location = new Point(x, 15); x -= 8; }
            txtName.Width = Math.Max(120, Math.Min(260, btnDeck.Left - 16 - txtName.Left));
        };
        Controls.Add(head);

        // 左側
        navHost = new Panel { Dock = DockStyle.Left, Width = 320, AutoScroll = true, BackColor = UI.Panel };
        navHost.Paint += (s, e) => { using (var p = new Pen(UI.Line)) e.Graphics.DrawLine(p, navHost.Width - 1, 0, navHost.Width - 1, navHost.Height); };
        nav = new NavList { Location = new Point(0, 8), Width = 300 };
        nav.Picked += k => ShowPage(k);
        navHost.Controls.Add(nav);
        navHost.Resize += delegate { nav.Width = navHost.ClientSize.Width - 2; };
        Controls.Add(navHost);

        // 右側內容
        pageHost = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24, 18, 24, 30), BackColor = UI.Bg };
        pageHost.Resize += delegate { FitWidths(); };
        Controls.Add(pageHost);
        pageHost.BringToFront();

        toast = new Label { AutoSize = false, Height = 40, BackColor = Color.FromArgb(34, 38, 52), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Visible = false, Font = UI.F(10f) };
        Controls.Add(toast); toast.BringToFront();
        toastTimer = new Timer { Interval = 4000 };
        toastTimer.Tick += delegate { toast.Visible = false; toastTimer.Stop(); };

        FormClosing += (s, e) =>
        {
            if (Dirty && MessageBox.Show(this, "還有設定沒有儲存，確定要關閉嗎？", "AI 對手編輯器", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) e.Cancel = true;
        };
        RefreshAll();
    }

    // ---------------- 狀態 ----------------
    bool Dirty { get { return proj != null && Gen.Serialize(proj) != savedJson; } }
    void Changed() { if (!building) UpdateTitle(); }
    void UpdateTitle()
    {
        Text = "WS 模擬器 AI 對手編輯器" + (proj != null ? " － " + (proj.Name == "" ? "（未命名）" : proj.Name) : "") + (Dirty ? "  （尚未儲存）" : "");
    }
    void Toast(string msg, bool err = false)
    {
        toast.Text = msg; toast.BackColor = err ? UI.Danger : Color.FromArgb(34, 38, 52);
        toast.Width = Math.Min(ClientSize.Width - 80, TextRenderer.MeasureText(msg, toast.Font).Width + 60);
        toast.Location = new Point((ClientSize.Width - toast.Width) / 2, ClientSize.Height - 70);
        toast.Visible = true; toast.BringToFront(); toastTimer.Stop(); toastTimer.Start();
    }

    void LoadProject(AiProject p)
    {
        proj = p;
        CardDb.Load(p.Deck);
        cards = Gen.UniqueCards(p);
        var missing = p.Deck.Where(c => CardDb.Get(c) == null).Distinct().ToList();
        Thumbs.ByName = cards.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.First());
        cardItems = new List<Item>();
        foreach (var g in Defs.Groups) cardItems.Add(new Item(g[0], "【" + g[1] + "】"));
        foreach (var c in cards) cardItems.Add(new Item(c.Name, c.Name));
        building = true; txtName.Text = p.Name; building = false;
        RefreshAll();
        if (missing.Count > 0) Toast("有 " + missing.Count + " 種卡在卡片資料裡找不到（" + string.Join("、", missing.Take(3).ToArray()) + "…），會被略過", true);
    }

    void RefreshAll()
    {
        bool has = proj != null;
        btnSave.Enabled = btnPreview.Enabled = btnSwap.Enabled = txtName.Enabled = has;
        nav.Entries.Clear();
        if (has)
        {
            nav.Entries.Add(new NavList.Entry { Key = "global", Text = "⚙  整體設定" });
            nav.Entries.Add(new NavList.Entry { Key = "roles", Text = "★  卡片定位" });
            nav.Entries.Add(new NavList.Entry { Key = "mull", Text = "✋  起手換牌" });
            nav.Entries.Add(new NavList.Entry { Key = "form", Text = "⚔  斬殺回合陣型" });
            var groups = new[] {
                new { T = "角色", F = (Func<CardInfo, bool>)(c => c.IsChar) },
                new { T = "事件", F = (Func<CardInfo, bool>)(c => c.IsEvent) },
                new { T = "名場面", F = (Func<CardInfo, bool>)(c => c.IsClimax) } };
            foreach (var g in groups)
            {
                var list = cards.Where(g.F).OrderBy(c => c.Level).ThenBy(c => c.Cost).ThenBy(c => c.Name).ToList();
                if (list.Count == 0) continue;
                nav.Entries.Add(new NavList.Entry { Header = true, Text = g.T + "（" + list.Sum(c => Count(c)) + " 張）" });
                foreach (var c in list)
                    nav.Entries.Add(new NavList.Entry { Key = "card:" + c.Name, Text = c.Name, Card = c, Count = Count(c), Sub = Sub(c) });
            }
        }
        nav.Height = Math.Max(navHost.ClientSize.Height - 10, nav.TotalHeight);
        nav.Selected = page; nav.Invalidate();
        ShowPage(has && page == "home" ? "global" : page);
        UpdateTitle();
    }
    int Count(CardInfo c) { return proj.Deck.Count(code => { var x = CardDb.Get(code); return x != null && x.Name == c.Name; }); }
    static string Sub(CardInfo c)
    {
        if (c.IsClimax) return "名場面 · " + ColorZh(c.Color) + (c.Trigger != "" ? " · " + TriggerZh(c.Trigger) : "");
        string s = "Lv" + c.Level + " / " + c.Cost + " 費 · " + ColorZh(c.Color);
        if (c.IsChar) s += " · " + c.Power;
        return s;
    }
    static string ColorZh(string c) { return c == "Y" ? "黃" : c == "G" ? "綠" : c == "R" ? "紅" : c == "B" ? "藍" : c == "P" ? "紫" : c; }
    static string TriggerZh(string t)
    {
        t = t.Trim();
        var m = new Dictionary<string, string> { {"Soul","魂"},{"DoubleSoul","雙魂"},{"Gate","城門"},{"Door","木門"},{"Book","書"},{"Bag","寶袋"},{"Bar","金磚"},
            {"Shot","火燒"},{"Wind","風吹"},{"Standby","開機"},{"Choice","箭頭"},{"Discovery","望遠鏡"},{"Chance","燈泡"},{"Focus","橘子"} };
        string v; return m.TryGetValue(t, out v) ? v : t;
    }
    static string ZhColorName(string en) { return en == "Yellow" ? "黃" : en == "Green" ? "綠" : en == "Red" ? "紅" : en == "Blue" ? "藍" : en == "Purple" ? "紫" : en; }

    // ---------------- 頁面 ----------------
    void ShowPage(string key)
    {
        page = key; nav.Selected = key; nav.Invalidate();
        pageHost.SuspendLayout();
        foreach (Control c in pageHost.Controls.Cast<Control>().ToList()) c.Dispose();
        pageHost.Controls.Clear();
        building = true;
        try
        {
            if (proj == null || key == "home") BuildHome();
            else if (key == "global") BuildGlobal();
            else if (key == "mull") BuildMulligan();
            else if (key == "roles") BuildRoles();
            else if (key == "form") BuildFormations();
            else if (key.StartsWith("card:"))
            {
                var c = cards.FirstOrDefault(x => x.Name == key.Substring(5));
                if (c != null) BuildCard(c); else BuildGlobal();
            }
        }
        finally { building = false; }
        FitWidths();
        pageHost.ResumeLayout();
        pageHost.AutoScrollPosition = new Point(0, 0);
    }

    void FitWidths()
    {
        int w = Math.Max(500, pageHost.ClientSize.Width - pageHost.Padding.Horizontal - 4);
        foreach (Control c in pageHost.Controls)
        {
            if (c is Box) { c.MinimumSize = new Size(w, 0); c.MaximumSize = new Size(w, 0); }
            FitLabels(c, w - 40);
        }
    }
    static void FitLabels(Control c, int w)
    {
        foreach (Control x in c.Controls)
        {
            if (x is Label && (x.Tag as string) == "wrap") x.MaximumSize = new Size(Math.Max(200, w - x.Left), 0);
            if (x is FlowLayoutPanel && (x.Tag as string) == "wrapflow") x.MaximumSize = new Size(Math.Max(200, w - x.Left), 0);
            if (x.HasChildren) FitLabels(x, w);
        }
    }

    void Add(Control c) { pageHost.Controls.Add(c); }

    void BuildHome()
    {
        var b = new Box("歡迎使用 AI 對手編輯器",
            "選一副你的牌組，編輯器會自動幫每張卡填好基本設定（名場面怎麼打、哪些卡起手要留、集中角色放後列…），" +
            "什麼都不改也能直接對戰。之後再依照你的打法，一張一張調整「什麼時候用」「想先拿哪張」就好。");
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 6, 0, 0) };
        var nb = new FlatButton("開新 AI（選牌組）", 200) { Primary = true }; nb.Click += delegate { NewFromDeck(); };
        var ob = new FlatButton("開啟已做的 AI", 160); ob.Click += delegate { OpenExisting(); };
        row.Controls.Add(nb); row.Controls.Add(ob);
        b.Controls.Add(row);
        Add(b);
        var how = new Box("做好之後怎麼對戰？");
        how.Controls.Add(Box.Wrap("1. 按右上角「儲存並安裝到遊戲」。\n2. 用「Weiss Schwarz 中文版」捷徑開遊戲（每次開遊戲都會自動把你做的 AI 裝回去，模擬器更新也不會不見）。\n3. 在對戰 AI 的牌組選單選「AI_你的 AI 名稱」。", UI.Text));
        how.Controls.Add(Box.Wrap("你做的 AI 存在模擬器資料夾的「自製AI」裡，是一個 .txt 檔。把這個檔案傳給朋友，他用「開啟已做的 AI」就能直接使用或修改（牌組也包含在裡面）。", UI.Muted, 9.5f));
        Add(how);
    }

    void BuildGlobal()
    {
        var b = new Box("顏色優先順序", "AI 升級、放傷害區時，會盡量先湊齊排在前面的顏色。");
        var colors = proj.Colors;
        var allCols = proj.Deck.Select(CardDb.Get).Where(c => c != null && c.ColorName != "").Select(c => c.ColorName).Distinct().ToList();
        foreach (var c in allCols) if (!colors.Contains(c)) colors.Add(c);
        for (int i = 0; i < colors.Count; i++)
        {
            int idx = i;
            var up = new FlatButton("↑ 往前", 0, true) { Ghost = true, Enabled = i > 0 };
            up.Click += delegate { var t = colors[idx]; colors[idx] = colors[idx - 1]; colors[idx - 1] = t; Changed(); ShowPage("global"); };
            int n = proj.Deck.Count(code => { var x = CardDb.Get(code); return x != null && x.ColorName == colors[idx]; });
            b.Controls.Add(Ctl.Row((i + 1) + ". " + ZhColorName(colors[i]) + "（" + n + " 張）", 160, up));
        }
        Add(b);

        var c2 = new Box("放傷害區", "每回合要不要把一張手牌放到傷害區（換兩張牌）。攻擊手不夠時，AI 會自己決定放。");
        c2.Controls.Add(Ctl.Row("等級低於", 120, Ctl.Num(proj.ClockAlwaysBelow, 0, 4, 60, v => { proj.ClockAlwaysBelow = (int)v; Changed(); }), Ctl.Hint("時一律放（0＝不使用這條規則；2＝等級 0、1 一律放）")));
        c2.Controls.Add(Ctl.Row("其他時候手牌少於", 120, Ctl.Num(proj.ClockIfHandBelow, 0, 12, 60, v => { proj.ClockIfHandBelow = (int)v; Changed(); }), Ctl.Hint("張時放（0＝不放）")));
        c2.Controls.Add(Ctl.Check("等級 3 時不放傷害區", proj.NoClockAtL3, v => { proj.NoClockAtL3 = v; Changed(); }));
        Add(c2);

        var c3 = new Box("三能量再演", "角色在對手回合被倒置時，要不要付 3 能量讓它回到場上。");
        c3.Controls.Add(Ctl.Check("使用三能量再演", proj.Encore3, v => { proj.Encore3 = v; Changed(); }));
        c3.Controls.Add(Ctl.Row("能量至少", 120, Ctl.Num(proj.Encore3Stock, 3, 30, 60, v => { proj.Encore3Stock = (int)v; Changed(); }), Ctl.Hint("張時才再演")));
        c3.Controls.Add(Ctl.Row("只再演「保護」至少", 120, Ctl.Combo(Ctl.Levels().Skip(3), proj.Encore3MinProtect.ToString(), 100, k => { proj.Encore3MinProtect = int.Parse(k); Changed(); }), Ctl.Hint("的角色（在每張卡的「保護」權重設定）")));
        Add(c3);

        var c4 = new Box("其他");
        c4.Controls.Add(Ctl.Row("經驗值目標", 120, Ctl.Num(proj.Experience, 0, 20, 60, v => { proj.Experience = (int)v; Changed(); }), Ctl.Hint("有經驗值條件的牌組才需要設定，例如需要等級區合計 5 以上就填 5")));
        c4.Controls.Add(Ctl.Check("主要階段結束前，盡量補滿三個攻擊手（建議開啟）", proj.FillAttackers, v => { proj.FillAttackers = v; Changed(); }));
        c4.Controls.Add(Ctl.Check("攻擊優先：攻擊手不夠時，優先找出得了的角色，也不把它們丟掉或放傷害區（建議開啟）", proj.AttackFirst, v => { proj.AttackFirst = v; Changed(); }));
        c4.Controls.Add(Ctl.Check("攻擊時翻牌庫的效果（例如看最多 4 張）：盡量讓牌庫剩一張，打出的名場面才洗得回牌庫；不一定翻滿（建議開啟）", proj.MillBudget, v => { proj.MillBudget = v; Changed(); }));
        c4.Controls.Add(Ctl.Check("AI 會在對戰中發表情", proj.Emotes, v => { proj.Emotes = v; Changed(); }));
        Add(c4);

        var c5 = new Box("進階：自訂腳本內容", "會原封不動加進腳本（例如自己寫的 Section）。不懂腳本語法的話不需要填。語法可參考「AI腳本說明_中文.md」。");
        c5.Controls.Add(RawBox(proj.Raw, v => proj.Raw = v));
        Add(c5);
    }

    TextBox RawBox(string v, Action<string> set)
    {
        var t = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f), Text = (v ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"), Height = 140, Width = 700, AcceptsTab = true };
        t.TextChanged += delegate { set(t.Text); Changed(); };
        return t;
    }

    void BuildFormations()
    {
        var intro = new Box("斬殺回合陣型",
            "直接指定「等級夠了、手上有名場面」的那個回合，前列和後列要放誰。AI 會照順序一張一張放，並且先把連動要付的能量留下來；" +
            "能量不夠時，排在後面的會被放棄，所以最重要的卡請排在最前面。同一張卡想放兩隻，就加兩格。");
        var addF = new FlatButton("＋ 新增陣型", 0, true);
        addF.Click += delegate
        {
            var f = new Formation();
            var cx = cards.FirstOrDefault(x => x.IsClimax && cards.Any(y => y.IsChar && y.Level >= 3 && proj.Cfg(y.Name).Combo == x.Name));
            if (cx != null)
            {
                f.Climax = cx.Name;
                var fin = cards.First(y => y.IsChar && y.Level >= 3 && proj.Cfg(y.Name).Combo == cx.Name);
                f.Slots.Add(new Slot { Card = fin.Name }); f.Slots.Add(new Slot { Card = fin.Name });
            }
            proj.Formations.Add(f); Changed(); ShowPage("form");
        };
        intro.Controls.Add(addF);
        Add(intro);

        var chars = cards.Where(x => x.IsChar).OrderByDescending(x => x.Level).ThenBy(x => x.Name).Select(x => new Item(x.Name, "Lv" + x.Level + "  " + x.Name)).ToList();
        var cxs = cards.Where(x => x.IsClimax).Select(x => new Item(x.Name, x.Name)).ToList();
        cxs.Insert(0, new Item("", "（不需要名場面）"));
        int no = 1;
        foreach (var f0 in proj.Formations.ToList())
        {
            var f = f0;
            var b = new Box("陣型 " + (no++));
            var delF = new FlatButton("✕ 刪除這個陣型", 0, true) { Ghost = true };
            delF.Click += delegate { proj.Formations.Remove(f); Changed(); ShowPage("form"); };
            b.Controls.Add(Ctl.Row("我的等級至少", 130, Ctl.Num(f.MinLevel, 0, 3, 60, v => { f.MinLevel = (int)v; Changed(); }), delF));
            b.Controls.Add(Ctl.Row("手上要有名場面", 130, Ctl.Combo(cxs, f.Climax, 340, k => { f.Climax = k; Changed(); }), Ctl.Hint("有選的話，AI 這回合會打出它")));
            b.Controls.Add(Box.Wrap("要放上場的卡（排越前面越優先）：", UI.Text));
            for (int i = 0; i < f.Slots.Count; i++)
            {
                int idx = i; var s = f.Slots[i];
                var up = new FlatButton("↑", 0, true) { Ghost = true, Enabled = i > 0 };
                up.Click += delegate { var t = f.Slots[idx]; f.Slots[idx] = f.Slots[idx - 1]; f.Slots[idx - 1] = t; Changed(); ShowPage("form"); };
                var del = new FlatButton("✕", 0, true) { Ghost = true };
                del.Click += delegate { f.Slots.RemoveAt(idx); Changed(); ShowPage("form"); };
                b.Controls.Add(Ctl.Row((i + 1) + ".", 30,
                    Ctl.Combo(new[] { new Item("f", "前列"), new Item("b", "後列") }, s.Back ? "b" : "f", 80, k => { s.Back = k == "b"; Changed(); }),
                    Ctl.Combo(chars, s.Card, 380, k => { s.Card = k; Changed(); }), up, del));
            }
            var add = new FlatButton("＋ 加一格", 0, true) { Ghost = true };
            add.Click += delegate { f.Slots.Add(new Slot { Card = chars.Count > 0 ? chars[0].Key : "" }); Changed(); ShowPage("form"); };
            b.Controls.Add(add);
            Add(b);
        }
    }

    // 卡片定位總覽：一頁設定所有角色的定位
    void BuildRoles()
    {
        var intro = new Box("卡片定位",
            "替每隻角色選一個最接近的用途，AI 就會照這個方向使用它。這是最主要的設定；選好定位之後，大部分的卡都不需要再調其他東西。");
        var auto = new FlatButton("依卡片內容自動判斷（只填還是「一般」的卡）", 0, true);
        auto.Click += delegate
        {
            int n = 0;
            foreach (var c in cards.Where(x => x.IsChar))
            {
                var cfg = proj.Cfg(c.Name);
                if (cfg.Role != "" || cfg.W.Count > 0) continue;
                string r = Defaults.GuessRole(proj, c);
                if (r != "") { cfg.Role = r; n++; }
            }
            Changed(); ShowPage("roles");
            Toast(n > 0 ? "已替 " + n + " 種卡填上定位" : "沒有可以自動判斷的卡（已經設定過權重的卡不會被改動）");
        };
        intro.Controls.Add(auto);
        var legend = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
        foreach (var r in Defs.Roles.Where(x => x.Key != ""))
            legend.Controls.Add(new Label { Text = "・" + r.Label + "：" + r.Desc, AutoSize = true, Font = UI.F(9.5f), ForeColor = UI.Muted, Tag = "wrap", Margin = new Padding(0, 1, 0, 1) });
        intro.Controls.Add(legend);
        Add(intro);

        var roleItems = Defs.Roles.Select(r => new Item(r.Key, r.Label)).ToList();
        foreach (var lvGroup in cards.Where(x => x.IsChar).GroupBy(x => x.Level).OrderBy(g => g.Key))
        {
            var b = new Box("等級 " + lvGroup.Key + " 角色", "點卡圖可以進入那張卡的細部設定；滑鼠移到卡圖上可以看大圖。");
            var grid = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Tag = "wrapflow", Margin = new Padding(0) };
            foreach (var c0 in lvGroup.OrderBy(x => x.Cost).ThenBy(x => x.Name))
            {
                var c = c0; var cfg = proj.Cfg(c.Name);
                string note = "×" + Count(c) + (cfg.W.Count > 0 ? "　有調過權重" : "") + (!string.IsNullOrEmpty(cfg.Combo) ? "　連動" : "");
                grid.Controls.Add(Tile(c, note, Ctl.Combo(roleItems, cfg.Role, 150, k => { cfg.Role = k; cfg.W.Clear(); Changed(); })));
            }
            b.Controls.Add(grid);
            Add(b);
        }
    }

    void BuildMulligan()
    {
        var b = new Box("起手換牌", "開局抽 5 張後，AI 會保留下面設定的卡，其他的換掉。");
        var bar = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
        var p1 = new FlatButton("只留等級 0 角色", 0, true); p1.Click += delegate { foreach (var c in cards) proj.Cfg(c.Name).Keep = c.IsChar && c.Level == 0 ? 4 : -1; Changed(); ShowPage("mull"); };
        var p2 = new FlatButton("全部都不留", 0, true); p2.Click += delegate { foreach (var c in cards) proj.Cfg(c.Name).Keep = -1; Changed(); ShowPage("mull"); };
        bar.Controls.Add(p1); bar.Controls.Add(p2);
        b.Controls.Add(bar);
        var keepItems = new[] { new Item("-1", "換掉"), new Item("1", "留 1 張"), new Item("2", "留 2 張"), new Item("3", "留 3 張"), new Item("4", "全部留") };
        var grid = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Tag = "wrapflow", Margin = new Padding(0) };
        foreach (var c0 in cards.OrderBy(x => x.IsClimax ? 2 : x.IsEvent ? 1 : 0).ThenBy(x => x.Level).ThenBy(x => x.Name))
        {
            var c = c0; var cfg = proj.Cfg(c.Name);
            grid.Controls.Add(Tile(c, (c.IsClimax ? "名場面" : "Lv" + c.Level) + "　×" + Count(c), Ctl.Combo(keepItems, cfg.Keep.ToString(), 150, k => { cfg.Keep = int.Parse(k); Changed(); })));
        }
        b.Controls.Add(grid);
        Add(b);
    }

    // 卡圖＋下方一個選單的小方塊（卡片定位、起手換牌共用）
    Control Tile(CardInfo c, string note, Control below)
    {
        var t = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 14, 14) };
        t.Controls.Add(Thumbs.Pic(c, 150, 150, delegate { ShowPage("card:" + c.Name); }));
        below.Margin = new Padding(0, 0, 0, 2);
        t.Controls.Add(below);
        t.Controls.Add(new Label { Text = note, Width = 150, Height = 18, AutoEllipsis = true, Font = UI.F(8.5f), ForeColor = UI.Muted, Margin = new Padding(0) });
        return t;
    }

    void BuildCard(CardInfo c)
    {
        var cfg = proj.Cfg(c.Name);
        var advanced = new List<Action>();   // 進階區塊：預設收起來，按下按鈕才建立
        // 卡片資訊
        var info = new Box(null);
        var line = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        var pic = new PictureBox { Width = 190, Height = 266, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 16, 0), BackColor = UI.Bg };
        try { if (c.Image != null) using (var src = Image.FromFile(c.Image)) pic.Image = new Bitmap(src); } catch { }
        line.Controls.Add(pic);
        var right = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        right.Controls.Add(new Label { Text = c.Name, Font = UI.F(14f, FontStyle.Bold), AutoSize = true, Tag = "wrap", ForeColor = UI.Text, UseMnemonic = false });
        string meta = (c.IsChar ? "角色" : c.IsEvent ? "事件" : "名場面") + " · " + c.Code + " · " + ColorZh(c.Color);
        if (!c.IsClimax) meta += " · 等級 " + c.Level + " · 費用 " + c.Cost;
        if (c.IsChar) meta += " · 戰力 " + c.Power + " · 魂 " + c.Soul;
        if (c.Trigger != "") meta += " · 判定：" + TriggerZh(c.Trigger);
        if (c.Traits.Count > 0) meta += " · <" + string.Join("/", c.Traits.ToArray()) + ">";
        meta += " · 牌組中 " + Count(c) + " 張";
        right.Controls.Add(new Label { Text = meta, Font = UI.F(9.5f), ForeColor = UI.Muted, AutoSize = true, Tag = "wrap", Margin = new Padding(0, 4, 0, 8) });
        var txt = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = UI.F(9.5f), BackColor = Color.FromArgb(250, 250, 252), BorderStyle = BorderStyle.FixedSingle, Width = 640, Height = 190, Text = string.Join("\r\n\r\n", c.Texts.ToArray()) };
        right.Controls.Add(txt);
        line.Controls.Add(right);
        info.Controls.Add(line);
        info.Resize += delegate { txt.Width = Math.Max(300, info.Width - 190 - 16 - 50); };
        Add(info);

        // 定位與連動
        if (c.IsChar)
        {
            var b = new Box("這張卡的定位", "選一個最接近的用途，AI 就會照這個方向使用它。需要更細的調整，再到最下面的「進階設定」。");
            Label roleDesc = Box.Wrap(Defs.GetRole(cfg.Role).Desc, UI.Muted, 9.5f);
            b.Controls.Add(Ctl.Row("定位", 130, Ctl.Combo(Defs.Roles.Select(r => new Item(r.Key, r.Label)), cfg.Role, 240, k =>
                {
                    cfg.Role = k; cfg.W.Clear();   // 換定位時，細部權重回到新定位的預設值
                    roleDesc.Text = Defs.GetRole(k).Desc;
                    Changed();
                    if (showAdvanced) BeginInvoke(new Action(delegate { ShowPage(page); }));
                })));
            b.Controls.Add(roleDesc);
            var climaxes = cards.Where(x => x.IsClimax).Select(x => new Item(x.Name, x.Name)).ToList();
            climaxes.Insert(0, new Item("", "（沒有連動）"));
            NumericUpDown costBox = null;
            b.Controls.Add(Ctl.Row("名場面連動", 130, Ctl.Combo(climaxes, cfg.Combo, 340, k =>
                {
                    cfg.Combo = k; cfg.ComboCost = Defaults.DetectComboCost(c, k); cfg.ComboHandMax = Defaults.DetectComboHandMax(c, k);
                    if (costBox != null) costBox.Value = Math.Max(0, cfg.ComboCost);
                    Changed();
                }),
                Ctl.Hint("有連動時，AI 會在這隻角色要攻擊時打出該名場面")));
            costBox = Ctl.Num(Math.Max(0, cfg.ComboCost), 0, 10, 60, v => { cfg.ComboCost = (int)v; Changed(); });
            b.Controls.Add(Ctl.Row("連動的手牌上限", 130, Ctl.Num(Math.Max(0, cfg.ComboHandMax), 0, 12, 60, v => { cfg.ComboHandMax = (int)v; Changed(); }),
                Ctl.Hint("連動要求「手牌幾張以下」時填這個數字；AI 會在攻擊前把出得了的角色放到空位，把手牌降下來。0＝沒有這個條件")));
            b.Controls.Add(Ctl.Row("連動要付的能量", 130, costBox, Ctl.Hint("手上有連動名場面時，AI 出牌會先留下這些能量（每隻各留一份）；0＝不用留")));
            Add(b);
            advanced.Add(delegate {
            b = new Box("進階：出牌與攻擊");
            b.Controls.Add(Ctl.Row("攻擊方式", 130, Ctl.Combo(new[] { new Item("normal", "讓 AI 判斷"), new Item("prefer", "偏好側面攻擊"), new Item("force", "一律側面攻擊"), new Item("never", "不側面攻擊") }, cfg.AttackMode, 160, k => { cfg.AttackMode = k; Changed(); })));
            if (c.Backup > 0)
                b.Controls.Add(Ctl.Row("助太刀（+" + c.Backup + "）", 130, Ctl.Combo(new[] { new Item("auto", "能救下角色時使用"), new Item("always", "一有機會就使用"), new Item("never", "不當助太刀使用") }, cfg.BackupMode, 180, k => { cfg.BackupMode = k; Changed(); })));
            var flags = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0, 4, 0, 0), MaximumSize = new Size(900, 0) };
            flags.Controls.Add(Ctl.Check("不從手牌打出", cfg.NeverPlay, v => { cfg.NeverPlay = v; Changed(); }));
            flags.Controls.Add(Ctl.Check("只放到空位（不覆蓋）", cfg.OnlyOpen, v => { cfg.OnlyOpen = v; Changed(); }));
            flags.Controls.Add(Ctl.Check("放在前列中間", cfg.Middle, v => { cfg.Middle = v; Changed(); }));
            flags.Controls.Add(Ctl.Check("只在攻擊手不夠時才上前列", cfg.Emergency, v => { cfg.Emergency = v; Changed(); }));
            flags.Controls.Add(Ctl.Check("場上只有一張時不拿它攻擊", cfg.DontAttackOnlyCopy, v => { cfg.DontAttackOnlyCopy = v; Changed(); }));
            flags.Controls.Add(Ctl.Check("不去打對手的炸彈角色", cfg.NeverAttackBombs, v => { cfg.NeverAttackBombs = v; Changed(); }));
            flags.Controls.Add(Ctl.Check("登場時回復（治療）", cfg.Healer, v => { cfg.Healer = v; Changed(); }));
            b.Controls.Add(flags);
            Add(b);
            });
        }
        if (c.IsClimax)
        {
            var b = new Box("名場面怎麼打");
            b.Controls.Add(Ctl.Row("打出時機", 110, Ctl.Combo(new[] {
                new Item("basic", "一般：攻擊手夠時就打"), new Item("combo", "只配合連動角色打"), new Item("both", "連動優先，沒有連動也會打"),
                new Item("standby", "開機名場面"), new Item("stocksoul", "能量魂名場面"), new Item("never", "不打出（只當判定用）") }, cfg.ClimaxMode, 260, k => { cfg.ClimaxMode = k; Changed(); })));
            var partners = cards.Where(x => x.IsChar && proj.Cfg(x.Name).Combo == c.Name).Select(x => x.Name).ToList();
            b.Controls.Add(Box.Wrap(partners.Count > 0 ? "連動角色：" + string.Join("、", partners.ToArray()) + "（在角色頁的「名場面連動」設定）" : "沒有角色設定和它連動（在角色頁的「名場面連動」設定）", UI.Muted, 9.5f));
            if (c.Trigger.Trim() == "Choice" || c.HasShort("ChoiceClimax"))
            {
                b.Controls.Add(Ctl.Row("箭頭判定：手牌少於", 150, Ctl.Num(cfg.ChoiceHandBelow, 0, 15, 60, v => { cfg.ChoiceHandBelow = (int)v; Changed(); }), Ctl.Hint("張時拿回手牌，否則放能量區")));
                b.Controls.Add(Box.Wrap("箭頭判定要拿休息室的哪張角色（排越前面越優先；沒有設定＝拿「檢索時想拿」分數最高的）：", UI.Text));
                b.Controls.Add(new TargetEditor(cfg.ChoiceTargets, cardItems, Changed, null, null));
            }
            Add(b);
        }
        if (c.IsEvent)
        {
            var b = new Box("事件怎麼打", c.Counter ? "這是反擊事件，只能在對手回合使用，下面的打出時機對它沒有作用。" : null);
            if (cfg.Ev == null) cfg.Ev = new EffCfg { Mode = c.Counter ? "default" : "always", When = "start" };
            var ev = cfg.Ev;
            var evCond = new CondEditor(ev.Cond, cardItems, Changed) { Visible = ev.Mode == "cond" };
            var evOr = OrCondsUI(ev, Changed);
            evOr.Visible = ev.Mode == "cond";
            b.Controls.Add(Ctl.Row("什麼時候打出", 110, Ctl.Combo(new[] {
                new Item("default", "不主動打出"), new Item("always", "能打就打"), new Item("cond", "只在條件成立時打出") }, ev.Mode, 200, k =>
                { ev.Mode = k; evCond.Visible = k == "cond"; evOr.Visible = k == "cond"; Changed(); }),
                Ctl.Combo(new[] { new Item("start", "主要階段一開始"), new Item("end", "主要階段結束前") }, ev.When, 180, k => { ev.When = k; Changed(); }),
                Ctl.Hint("能量夠付費用時才會打")));
            b.Controls.Add(evCond);
            b.Controls.Add(evOr);
            b.Controls.Add(Ctl.Check("不從手牌打出", cfg.NeverPlay, v => { cfg.NeverPlay = v; Changed(); }));
            b.Controls.Add(Box.Wrap("選擇目標時的偏好（排越前面越優先）：", UI.Text));
            b.Controls.Add(new TargetEditor(cfg.EventTargets, cardItems, Changed, null, null));
            Add(b);
        }

        // 權重與特殊情況（進階）
        advanced.Add(delegate {
        var wb = new Box("進階：權重", "決定 AI 遇到各種選擇時多重視這張卡。預設值來自上面選的定位；「普通」＝交給 AI 自己判斷。");
        foreach (var sec in Defs.Sections)
        {
            if (sec.Only == "char" && !c.IsChar) continue;
            int lv = Defs.Eff(cfg, sec.Key);
            var s = sec;
            wb.Controls.Add(Ctl.Row(sec.Label, 150, Ctl.Combo(Ctl.Levels(), lv.ToString(), 100, k =>
            {
                int v = int.Parse(k);
                cfg.W.Remove(s.Key);
                if (v != Defs.Eff(cfg, s.Key)) cfg.W[s.Key] = v;   // 和定位預設值一樣就不用另外記
                Changed();
            }), Ctl.Hint(sec.Hint)));
        }
        Add(wb);

        var ob = new Box("進階：特殊情況", "在特定情況下把某個權重改成別的值，例如「我的等級 ≥ 2 時，當攻擊手改成『很高』」。");
        BuildOverrides(ob, c, cfg);
        Add(ob);
        });

        // 效果
        foreach (var e in c.Autos)
        {
            string key = e.Num.ToString();
            EffCfg ec; if (!cfg.Auto.TryGetValue(key, out ec)) ec = new EffCfg();
            var b = new Box("自動效果 " + e.Num + (e.Source != null ? "（" + e.Source + "）" : ""));
            b.Controls.Add(Box.Wrap(e.Text, UI.Muted, 9.5f));
            var condHost = new CondEditor(ec.Cond, cardItems, Changed) { Visible = ec.Mode == "cond" };
            var ecRef = ec;
            b.Controls.Add(Ctl.Row("要不要使用", 110, Ctl.Combo(new[] {
                new Item("default", "讓 AI 判斷"), new Item("always", "能用就用"), new Item("never", "從不使用"),
                new Item("spare", "付完連動費用還有剩才用"), new Item("cond", "只在條件成立時使用") }, ec.Mode, 220, k =>
                {
                    ecRef.Mode = k; condHost.Visible = k == "cond";
                    if (k == "default" && ecRef.Targets.Count == 0) cfg.Auto.Remove(key); else cfg.Auto[key] = ecRef;
                    Changed();
                }), Ctl.Hint("有「可以」字眼的效果（可選擇要不要發動）才有差別")));
            b.Controls.Add(condHost);
            var orHostA = OrCondsUI(ec, () => { cfg.Auto[key] = ecRef; Changed(); });
            orHostA.Visible = ec.Mode == "cond";
            condHost.VisibleChanged += delegate { orHostA.Visible = condHost.Visible; };
            b.Controls.Add(orHostA);
            b.Controls.Add(Box.Wrap("選擇目標時的偏好（排越前面越優先；沒有設定＝讓 AI 判斷）：", UI.Text));
            b.Controls.Add(new TargetEditor(ec.Targets, cardItems, () => { cfg.Auto[key] = ecRef; Changed(); }, () => ecRef.Fallback, v => ecRef.Fallback = v));
            b.Controls.Add(Box.Wrap("一次選兩張以上時（例如檢索 2 張，1 張上手、1 張放能量區）：上面的偏好只會拿一張，其餘的從下面挑。沒有這種效果就不用設定。", UI.Muted, 9.5f));
            b.Controls.Add(new TargetEditor(ec.Targets2, cardItems, () => { cfg.Auto[key] = ecRef; Changed(); }, null, null));
            Add(b);
        }
        foreach (var e in c.Acts)
        {
            string key = e.Num.ToString();
            EffCfg ec; if (!cfg.Act.TryGetValue(key, out ec)) ec = new EffCfg();
            var b = new Box("起動效果 " + e.Num + (e.Source != null ? "（" + e.Source + "）" : ""));
            b.Controls.Add(Box.Wrap(e.Text, UI.Muted, 9.5f));
            var condHost = new CondEditor(ec.Cond, cardItems, Changed) { Visible = ec.Mode == "cond" };
            var ecRef = ec;
            ComboBox whenBox = null;
            b.Controls.Add(Ctl.Row("要不要使用", 110, Ctl.Combo(new[] {
                new Item("default", "不主動使用"), new Item("always", "每回合都用"), new Item("cond", "只在條件成立時使用") }, ec.Mode, 200, k =>
                {
                    ecRef.Mode = k; condHost.Visible = k == "cond"; if (whenBox != null) whenBox.Enabled = k != "default";
                    if (k == "default" && ecRef.Targets.Count == 0) cfg.Act.Remove(key); else cfg.Act[key] = ecRef;
                    Changed();
                }), Ctl.Hint("這張卡在場上時，AI 才會使用")));
            whenBox = Ctl.Combo(new[] { new Item("end", "主要階段結束前（出完角色後）"), new Item("start", "主要階段一開始") }, ec.When, 260, k => { ecRef.When = k; Changed(); });
            whenBox.Enabled = ec.Mode != "default";
            b.Controls.Add(Ctl.Row("什麼時候用", 110, whenBox));
            b.Controls.Add(condHost);
            var orHostB = OrCondsUI(ec, () => { cfg.Act[key] = ecRef; Changed(); });
            orHostB.Visible = ec.Mode == "cond";
            condHost.VisibleChanged += delegate { orHostB.Visible = condHost.Visible; };
            b.Controls.Add(orHostB);
            var alsoHost = new CondEditor(ec.AlsoCond, cardItems, () => { cfg.Act[key] = ecRef; Changed(); }) { Visible = ec.PlayFor };
            var alsoLabel = Box.Wrap("另外，符合下面的條件時，也可以當一般的卡出場（沒有條件＝不能用效果就不出）：", UI.Text);
            alsoLabel.Visible = ec.PlayFor;
            b.Controls.Add(Ctl.Check("這張卡是為了這個效果才出場：能用效果時才從手牌出（只放空位），出場後馬上用", ec.PlayFor, v =>
            {
                ecRef.PlayFor = v; alsoHost.Visible = v; alsoLabel.Visible = v;
                if (v && ecRef.Mode == "default") ecRef.Mode = "always";
                cfg.Act[key] = ecRef; Changed();
                if (v) BeginInvoke(new Action(delegate { ShowPage(page); }));
            }));
            b.Controls.Add(alsoLabel);
            b.Controls.Add(alsoHost);
            var earlyHost = new CondEditor(ec.EarlyCond, cardItems, () => { cfg.Act[key] = ecRef; Changed(); }) { Visible = ec.Early };
            b.Controls.Add(Ctl.Check("符合下面的條件時，主要階段一開始就先用（先用再出牌；這張卡還在手上的話會先放到後列）", ec.Early, v =>
            {
                ecRef.Early = v; earlyHost.Visible = v;
                if (v && ecRef.EarlyCond.Empty)
                {
                    ecRef.EarlyCond.Any = true;
                    ecRef.EarlyCond.Items.Add(new Clause { Key = "Ratio", Op = ">=", Val = 20 });
                    ecRef.EarlyCond.Items.Add(new Clause { Key = "Playable", Op = "=", Val = 0 });
                    cfg.Act[key] = ecRef; Changed();
                    BeginInvoke(new Action(delegate { ShowPage(page); }));   // 重新畫出預設條件
                    return;
                }
                cfg.Act[key] = ecRef; Changed();
            }));
            b.Controls.Add(earlyHost);
            b.Controls.Add(Box.Wrap("選擇目標時的偏好（排越前面越優先）：", UI.Text));
            b.Controls.Add(new TargetEditor(ec.Targets, cardItems, () => { cfg.Act[key] = ecRef; Changed(); }, () => ecRef.Fallback, v => ecRef.Fallback = v));
            b.Controls.Add(Box.Wrap("一次選兩張以上時（例如檢索 2 張，1 張上手、1 張放能量區）：上面的偏好只會拿一張，其餘的從下面挑。沒有這種效果就不用設定。", UI.Muted, 9.5f));
            b.Controls.Add(new TargetEditor(ec.Targets2, cardItems, () => { cfg.Act[key] = ecRef; Changed(); }, null, null));
            Add(b);
        }

        advanced.Add(delegate {
        var rb = new Box("進階：自訂標籤", "會原封不動加進這張卡的區段（例如 GainPowerBeforeAttacking(1000)）。不懂腳本語法的話不需要填。");
        rb.Controls.Add(RawBox(cfg.Raw, v => cfg.Raw = v));
        Add(rb);
        });

        // 進階設定預設收起來
        var tog = new FlatButton(showAdvanced ? "▲ 隱藏進階設定" : "▼ 顯示進階設定（攻擊方式、細部權重、特殊情況、自訂標籤）", 0);
        tog.Margin = new Padding(0, 0, 0, 14);
        tog.Click += delegate { showAdvanced = !showAdvanced; var keep = pageHost.AutoScrollPosition; ShowPage(page); pageHost.AutoScrollPosition = new Point(0, -keep.Y); };
        Add(tog);
        if (showAdvanced) foreach (var build in advanced) build();
    }

    // 「或是另一組條件」的編輯區：符合任何一組就算成立
    Control OrCondsUI(EffCfg ec, Action save)
    {
        var host = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        Action render = null;
        render = delegate
        {
            host.SuspendLayout();
            foreach (Control x in host.Controls.Cast<Control>().ToList()) x.Dispose();
            host.Controls.Clear();
            foreach (var g0 in ec.OrConds.ToList())
            {
                var g = g0;
                var del = new FlatButton("✕ 刪除這組", 0, true) { Ghost = true };
                del.Click += delegate { ec.OrConds.Remove(g); save(); render(); FitWidths(); };
                host.Controls.Add(Ctl.Row(null, 0, new Label { Text = "或是符合這一組：", AutoSize = true, Font = UI.F(10f), Margin = new Padding(3, 8, 3, 0) }, del));
                host.Controls.Add(new CondEditor(g, cardItems, save));
            }
            var add = new FlatButton("＋ 加另一組條件（符合任何一組就成立）", 0, true) { Ghost = true };
            add.Click += delegate
            {
                var g = new Cond();
                g.Items.Add(new Clause { Key = "Level", Op = ">=", Val = 3 });
                ec.OrConds.Add(g); save(); render(); FitWidths();
            };
            host.Controls.Add(add);
            host.ResumeLayout();
        };
        render();
        return host;
    }

    void BuildOverrides(Box ob, CardInfo c, CardCfg cfg)
    {
        var host = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        Action render = null;
        render = delegate
        {
            host.SuspendLayout();
            foreach (Control x in host.Controls.Cast<Control>().ToList()) x.Dispose();
            host.Controls.Clear();
            var secs = Defs.Sections.Where(s => s.Only != "char" || c.IsChar).Select(s => new Item(s.Key, s.Label)).ToList();
            foreach (var o in cfg.Overrides.ToList())
            {
                var oo = o;
                var del = new FlatButton("✕ 刪除", 0, true) { Ghost = true };
                del.Click += delegate { cfg.Overrides.Remove(oo); Changed(); render(); FitWidths(); };
                host.Controls.Add(Ctl.Row(null, 0,
                    Ctl.Combo(secs, o.Section, 170, k => { oo.Section = k; Changed(); }),
                    new Label { Text = "改成", AutoSize = true, Margin = new Padding(3, 8, 3, 0) },
                    Ctl.Combo(Ctl.Levels(), o.Level.ToString(), 100, k => { oo.Level = int.Parse(k); Changed(); }),
                    new Label { Text = "，當：", AutoSize = true, Margin = new Padding(3, 8, 3, 0) }, del));
                host.Controls.Add(new CondEditor(o.Cond, cardItems, Changed) { Margin = new Padding(20, 0, 0, 10) });
            }
            var add = new FlatButton("＋ 加特殊情況", 0, true) { Ghost = true };
            add.Click += delegate
            {
                var o = new Override { Section = c.IsChar ? "AttackerValue" : "SearchValue", Level = 5 };
                o.Cond.Items.Add(new Clause { Key = "Level", Op = ">=", Val = 2 });
                cfg.Overrides.Add(o); Changed(); render(); FitWidths();
            };
            host.Controls.Add(add);
            host.ResumeLayout();
        };
        render();
        ob.Controls.Add(host);
    }

    // ---------------- 動作 ----------------
    bool ConfirmDiscard()
    {
        if (!Dirty) return true;
        return MessageBox.Show(this, "目前的 AI 還沒有儲存，要放棄這些修改嗎？", "AI 對手編輯器", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    void NewFromDeck()
    {
        if (!ConfirmDiscard()) return;
        string deckDir = Path.Combine(root, "Decks");
        if (!Directory.Exists(deckDir)) { Toast("找不到 Decks 資料夾", true); return; }
        var names = Directory.GetFiles(deckDir, "*.txt").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, StringComparer.CurrentCulture).ToList();
        string pick = PickDialog.Show(this, "選擇牌組", "選一副牌組，AI 會使用這副牌。可以輸入文字搜尋。", names);
        if (pick == null) return;
        var codes = ReadDeck(Path.Combine(deckDir, pick + ".txt"));
        if (codes.Count == 0) { Toast("這副牌組是空的", true); return; }
        var p = new AiProject { Name = pick.StartsWith("AI_") ? pick.Substring(3) : pick, Deck = codes };
        CardDb.Load(codes);
        Defaults.Apply(p, false);
        page = "global";
        LoadProject(p);
        savedJson = "";
        UpdateTitle();
        Toast("已自動填好基本設定，可以直接儲存，也可以再一張一張調整");
    }

    // 換牌組：同名的卡保留原本的設定，新的卡自動補上預設值
    void ChangeDeck()
    {
        if (proj == null) return;
        string deckDir = Path.Combine(root, "Decks");
        if (!Directory.Exists(deckDir)) { Toast("找不到 Decks 資料夾", true); return; }
        var names = Directory.GetFiles(deckDir, "*.txt").Select(Path.GetFileNameWithoutExtension).Where(n => !n.StartsWith("AI_")).OrderBy(x => x, StringComparer.CurrentCulture).ToList();
        string pick = PickDialog.Show(this, "換牌組", "選新的牌組。同名的卡會保留原本的設定，新的卡自動補上預設值；不在新牌組裡的卡，設定會留著（之後換回來還在）。", names);
        if (pick == null) return;
        var codes = ReadDeck(Path.Combine(deckDir, pick + ".txt"));
        if (codes.Count == 0) { Toast("這副牌組是空的", true); return; }
        CardDb.Load(codes);
        var before = new HashSet<string>(cards.Select(c => c.Name));
        proj.Deck = codes;
        Defaults.Apply(proj, true);
        var after = Gen.UniqueCards(proj).Select(c => c.Name).ToList();
        var added = after.Where(n => !before.Contains(n)).ToList();
        var removed = before.Where(n => !after.Contains(n)).ToList();
        page = "global";
        LoadProject(proj);
        string msg = "已換成「" + pick + "」";
        if (added.Count > 0) msg += "；新增 " + added.Count + " 種卡（已填預設值，可以再調整）";
        if (removed.Count > 0) msg += "；少了 " + removed.Count + " 種卡";
        if (added.Count == 0 && removed.Count == 0) msg += "；卡的種類沒有變，只有張數不同";
        var lost = proj.Formations.SelectMany(f => f.Slots).Where(s => !after.Contains(s.Card)).Select(s => s.Card).Distinct().ToList();
        if (lost.Count > 0) msg += "。注意：陣型裡有新牌組沒有的卡（" + string.Join("、", lost.ToArray()) + "）";
        Toast(msg + "。記得按儲存", lost.Count > 0);
    }

    static List<string> ReadDeck(string path)
    {
        var res = new List<string>(); bool inCards = false;
        foreach (var l in File.ReadAllLines(path))
        {
            string t = l.Trim();
            if (t == "Cards:") { inCards = true; continue; }
            if (inCards && t.Length > 0 && !t.StartsWith("#")) res.Add(t);
        }
        return res;
    }

    void OpenExisting()
    {
        if (!ConfirmDiscard()) return;
        Directory.CreateDirectory(myAiDir);
        var names = Directory.GetFiles(myAiDir, "*.txt").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToList();
        string pick = PickDialog.Show(this, "開啟已做的 AI", "「自製AI」資料夾裡的 AI。也可以按「從其他位置開啟」選朋友傳給你的檔案。", names, "從其他位置開啟…");
        if (pick == null) return;
        string path;
        if (pick == "\u0001")
        {
            using (var ofd = new OpenFileDialog { Filter = "AI 腳本 (*.txt)|*.txt", Title = "開啟 AI 腳本" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                path = ofd.FileName;
            }
        }
        else path = Path.Combine(myAiDir, pick + ".txt");
        AiProject p = null;
        try { p = Gen.ReadEmbedded(File.ReadAllText(path, Encoding.UTF8)); } catch { }
        if (p == null) { Toast("這個檔案不是用 AI 對手編輯器做的，目前無法開啟", true); return; }
        page = "global";
        LoadProject(p);
        CardDb.Load(p.Deck);
        Defaults.Apply(p, true);   // 牌組有新卡時補上預設
        cards = Gen.UniqueCards(p);
        savedJson = Gen.Serialize(p);
        RefreshAll();
    }

    void Preview()
    {
        if (proj == null) return;
        string s = Gen.Build(proj);
        var errs = Gen.Check(proj, s);
        using (var f = new Form { Text = "預覽腳本", ClientSize = new Size(900, 700), StartPosition = FormStartPosition.CenterParent, Font = UI.F(10f) })
        {
            var t = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 10f), Text = s, BackColor = Color.White };
            var lbl = new Label { Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), ForeColor = errs.Count == 0 ? UI.Ok : UI.Danger, Text = errs.Count == 0 ? "✓ 檢查通過：括號成對、引用的卡都在牌組裡" : "⚠ " + string.Join("；", errs.Take(5).ToArray()) };
            f.Controls.Add(t); f.Controls.Add(lbl);
            f.ShowDialog(this);
        }
    }

    void SaveAndInstall()
    {
        if (proj == null) return;
        string name = (proj.Name ?? "").Trim();
        if (name == "") { Toast("請先在上方輸入 AI 名稱", true); txtName.Focus(); return; }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("//")) { Toast("AI 名稱不能包含 \\ / : * ? \" < > | 這些符號", true); return; }
        string script = Gen.Build(proj);
        var errs = Gen.Check(proj, script);
        if (errs.Count > 0 && MessageBox.Show(this, "檢查發現問題：\n\n" + string.Join("\n", errs.Take(8).ToArray()) + "\n\n還是要儲存嗎？", "AI 對手編輯器", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            Directory.CreateDirectory(myAiDir);
            string mine = Path.Combine(myAiDir, name + ".txt");
            if (File.Exists(mine))
            {
                AiProject old = null;
                try { old = Gen.ReadEmbedded(File.ReadAllText(mine, Encoding.UTF8)); } catch { }
                if (savedJson == "" && MessageBox.Show(this, "「自製AI」裡已經有叫「" + name + "」的 AI，要覆蓋它嗎？", "AI 對手編輯器", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }
            File.WriteAllText(mine, script, new UTF8Encoding(false));
            Installer.Install(root, name, script, proj.Deck);
            savedJson = Gen.Serialize(proj);
            UpdateTitle();
            Toast("已儲存並安裝。開遊戲後，在對戰 AI 的牌組選「AI_" + name + "」就能對戰");
        }
        catch (Exception ex) { Toast("儲存失敗：" + ex.Message, true); }
    }
}

// ======================================================================
// 安裝到遊戲：腳本→AIData、牌組→Decks、登記到 AIList.txt
// ======================================================================
static class Installer
{
    public const string Marker = "// 自製AI（AI 對手編輯器）";
    public static void Install(string root, string name, string script, List<string> deck)
    {
        string aiData = Path.Combine(root, @"Weiss Schwarz_Data\StreamingAssets\AIData");
        if (!Directory.Exists(aiData)) throw new Exception("找不到 AIData 資料夾");
        File.WriteAllText(Path.Combine(aiData, "WSAI_" + name + ".txt"), script, new UTF8Encoding(false));
        string deckPath = Path.Combine(root, "Decks", "AI_" + name + ".txt");
        var sb = new StringBuilder();
        sb.Append("# WeissSim Deck\r\nName: AI_").Append(name).Append("\r\nDate: ").Append(DateTime.Now.ToString("HH:mm  MM/dd/yyyy")).Append("\r\nSleeve: \r\n\r\nCards:\r\n");
        sb.Append(string.Join("\r\n", deck.ToArray()));
        File.WriteAllText(deckPath, sb.ToString(), new UTF8Encoding(false));
        Register(aiData, name);
    }
    public static void Register(string aiData, string name)
    {
        string list = Path.Combine(aiData, "AIList.txt");
        var lines = File.Exists(list) ? File.ReadAllLines(list, Encoding.UTF8).ToList() : new List<string>();
        // 移除同名的舊登記（Deck 行 ~ Version 行）
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim() == "File WSAI_" + name)
            {
                int s = i; while (s > 0 && !lines[s].Trim().StartsWith("Deck ")) s--;
                if (s > 0 && lines[s - 1].Trim() == Marker) s--;
                int e = i; while (e < lines.Count - 1 && !lines[e].Trim().StartsWith("Version ")) e++;
                lines.RemoveRange(s, e - s + 1);
                while (s < lines.Count && s > 0 && lines[s].Trim() == "" && lines[s - 1].Trim() == "") lines.RemoveAt(s);
                i = s - 1;
            }
        }
        while (lines.Count > 0 && lines[lines.Count - 1].Trim() == "") lines.RemoveAt(lines.Count - 1);
        lines.Add(""); lines.Add(Marker);
        lines.Add("Deck AI_" + name); lines.Add("Name " + name); lines.Add("File WSAI_" + name); lines.Add("Version 7");
        File.WriteAllText(list, string.Join("\r\n", lines.ToArray()) + "\r\n", new UTF8Encoding(false));
    }
}

// 簡單的選擇視窗（可搜尋）
static class PickDialog
{
    public static string Show(IWin32Window owner, string title, string hint, List<string> items, string extraButton = null)
    {
        using (var f = new Form { Text = title, ClientSize = new Size(520, 560), StartPosition = FormStartPosition.CenterParent, Font = UI.F(10f), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = UI.Panel })
        {
            var lbl = new Label { Text = hint, Location = new Point(16, 12), Size = new Size(488, 40), ForeColor = UI.Muted };
            var search = new TextBox { Location = new Point(16, 56), Width = 488, Font = UI.F(11f) };
            var lb = new ListBox { Location = new Point(16, 92), Size = new Size(488, 400), Font = UI.F(10.5f), IntegralHeight = false };
            Action fill = delegate
            {
                lb.BeginUpdate(); lb.Items.Clear();
                foreach (var s in items) if (search.Text == "" || s.IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0) lb.Items.Add(s);
                lb.EndUpdate();
                if (lb.Items.Count > 0) lb.SelectedIndex = 0;
            };
            fill();
            search.TextChanged += delegate { fill(); };
            string result = null;
            var ok = new FlatButton("確定", 100) { Primary = true, Location = new Point(404, 506) };
            var cancel = new FlatButton("取消", 90) { Location = new Point(306, 506) };
            ok.Click += delegate { if (lb.SelectedItem != null) { result = (string)lb.SelectedItem; f.Close(); } };
            cancel.Click += delegate { f.Close(); };
            lb.DoubleClick += delegate { if (lb.SelectedItem != null) { result = (string)lb.SelectedItem; f.Close(); } };
            f.Controls.AddRange(new Control[] { lbl, search, lb, ok, cancel });
            if (extraButton != null)
            {
                var ex = new FlatButton(extraButton) { Location = new Point(16, 506) };
                ex.Click += delegate { result = "\u0001"; f.Close(); };
                f.Controls.Add(ex);
            }
            if (items.Count == 0) lb.Items.Add("（沒有項目）");
            f.AcceptButton = null;
            search.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && lb.SelectedItem != null) { result = (string)lb.SelectedItem; f.Close(); } if (e.KeyCode == Keys.Down && lb.Items.Count > 0) lb.Focus(); };
            f.ShowDialog(owner);
            if (result == "（沒有項目）") return null;
            return result;
        }
    }
}

static class Program
{
    static string FindRoot()
    {
        string d = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 4 && d != null; i++)
        {
            if (File.Exists(Path.Combine(d, "Weiss Schwarz.exe"))) return d;
            d = Path.GetDirectoryName(d.TrimEnd('\\'));
        }
        return null;
    }

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length >= 4 && args[0] == "--selftest") return SelfTest(args[1], args[2], args[3]);
        if (args.Length >= 2 && args[0] == "/reinstall") return Reinstall(args[1]);
        string root = args.Length > 0 && Directory.Exists(args[0]) ? args[0] : FindRoot();
        if (root == null)
        {
            MessageBox.Show("找不到 WS 模擬器資料夾。請把這個程式放在模擬器資料夾（有 Weiss Schwarz.exe 的地方）裡面的「AI編輯器」資料夾。", "AI 對手編輯器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        Application.Run(new MainForm(root));
        return 0;
    }

    // 模擬器更新後，把「自製AI」裡的 AI 重新裝回遊戲（啟動腳本會呼叫）
    static int Reinstall(string root)
    {
        try
        {
            string dir = Path.Combine(root, "自製AI");
            string aiData = Path.Combine(root, @"Weiss Schwarz_Data\StreamingAssets\AIData");
            if (!Directory.Exists(dir) || !Directory.Exists(aiData)) return 0;
            string listText = File.Exists(Path.Combine(aiData, "AIList.txt")) ? File.ReadAllText(Path.Combine(aiData, "AIList.txt"), Encoding.UTF8) : "";
            foreach (var f in Directory.GetFiles(dir, "*.txt"))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                string script = File.ReadAllText(f, Encoding.UTF8);
                // 編輯器更新後，用新版的規則重新產生腳本（設定不變）
                try
                {
                    var proj = Gen.ReadEmbedded(script);
                    if (proj != null)
                    {
                        CardDb.Root = root;
                        CardDb.Load(proj.Deck);
                        if (proj.Deck.All(c => CardDb.Get(c) != null))
                        {
                            Defaults.Apply(proj, true);
                            string fresh = Gen.Build(proj);
                            if (fresh != script && Gen.Check(proj, fresh).Count == 0) { File.WriteAllText(f, fresh, new UTF8Encoding(false)); script = fresh; }
                        }
                    }
                }
                catch { }
                string target = Path.Combine(aiData, "WSAI_" + name + ".txt");
                if (!File.Exists(target) || File.ReadAllText(target, Encoding.UTF8) != script) File.WriteAllText(target, script, new UTF8Encoding(false));
                if (!listText.Contains("File WSAI_" + name + "\r") && !listText.Contains("File WSAI_" + name + "\n")) Installer.Register(aiData, name);
                string deck = Path.Combine(root, "Decks", "AI_" + name + ".txt");
                if (!File.Exists(deck))
                {
                    var p = Gen.ReadEmbedded(script);
                    if (p != null) Installer.Install(root, name, script, p.Deck);
                }
            }
            return 0;
        }
        catch { return 1; }
    }

    // 自動測試：選一副牌 → 產生腳本與畫面截圖
    static int SelfTest(string root, string deckName, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var log = new StringBuilder();
        try
        {
            CardDb.Root = root;
            var deckFile = Path.Combine(root, "Decks", deckName + ".txt");
            var codes = new List<string>(); bool inCards = false;
            foreach (var l in File.ReadAllLines(deckFile)) { string t = l.Trim(); if (t == "Cards:") { inCards = true; continue; } if (inCards && t != "") codes.Add(t); }
            CardDb.Load(codes);
            var p = new AiProject { Name = "測試_" + deckName, Deck = codes };
            Defaults.Apply(p, false);
            string script = Gen.Build(p);
            File.WriteAllText(Path.Combine(outDir, "script.txt"), script, new UTF8Encoding(false));
            var errs = Gen.Check(p, script);
            log.AppendLine("cards=" + Gen.UniqueCards(p).Count + " missing=" + codes.Count(c => CardDb.Get(c) == null) + " errors=" + errs.Count);
            foreach (var e in errs) log.AppendLine("  " + e);
            var back = Gen.ReadEmbedded(script);
            log.AppendLine("roundtrip=" + (back != null && Gen.Serialize(back) == Gen.Serialize(p)));
            foreach (var c in Gen.UniqueCards(p))
                log.AppendLine((c.IsChar ? "C" : c.IsEvent ? "E" : "X") + " L" + c.Level + " " + c.Name + " | auto=" + c.Autos.Count() + " act=" + c.Acts.Count() + " backup=" + c.Backup + " combo=" + p.Cfg(c.Name).Combo + " cx=" + p.Cfg(c.Name).ClimaxMode + " short=" + string.Join(",", c.Shorthands.ToArray()));

            var f = new MainForm(root);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-3000, 0);
            f.Show(); Application.DoEvents();
            var mi = typeof(MainForm).GetMethod("LoadProject", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Invoke(f, new object[] { p });
            var show = typeof(MainForm).GetMethod("ShowPage", BindingFlags.NonPublic | BindingFlags.Instance);
            var shots = new List<string> { "global", "roles", "mull" };
            var firstChar = Gen.UniqueCards(p).Where(c => c.IsChar).OrderByDescending(c => c.Autos.Count() + c.Acts.Count()).FirstOrDefault();
            if (firstChar != null)
            {
                shots.Add("card:" + firstChar.Name);
                var fc = p.Cfg(firstChar.Name);
                var ov = new Override { Section = "AttackerValue", Level = 5 };
                ov.Cond.Items.Add(new Clause { Key = "Level", Op = ">=", Val = 2 });
                ov.Cond.Items.Add(new Clause { Key = "InHand", Card = "#Climax", Op = ">=", Val = 1 });
                fc.Overrides.Add(ov);
                var a1 = firstChar.Autos.FirstOrDefault();
                if (a1 != null)
                {
                    var ec = new EffCfg { Mode = "cond" };
                    ec.Cond.Any = true;
                    ec.Cond.Items.Add(new Clause { Key = "Stock", Op = ">=", Val = 2 });
                    ec.Cond.Items.Add(new Clause { Key = "MyTurn", Val = 1 });
                    ec.Targets.Add("#L2up"); ec.Targets.Add(firstChar.Name);
                    fc.Auto[a1.Num.ToString()] = ec;
                }
                script = Gen.Build(p);
                File.WriteAllText(Path.Combine(outDir, "script2.txt"), script, new UTF8Encoding(false));
                log.AppendLine("errors2=" + Gen.Check(p, script).Count);
            }
            var cx = Gen.UniqueCards(p).FirstOrDefault(c => c.IsClimax);
            if (cx != null) shots.Add("card:" + cx.Name);
            int n = 0;
            foreach (var s in shots)
            {
                show.Invoke(f, new object[] { s });
                Application.DoEvents();
                for (int k = 0; k < 2; k++)
                {
                    if (k == 1)
                    {
                        var host = (FlowLayoutPanel)typeof(MainForm).GetField("pageHost", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f);
                        host.AutoScrollPosition = new Point(0, k * 820);
                        Application.DoEvents();
                    }
                    using (var bmp = new Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(Path.Combine(outDir, "page" + n + (k == 1 ? "b" : "") + ".png"), ImageFormat.Png);
                    }
                }
                n++;
            }
            f.Hide();
        }
        catch (Exception ex) { log.AppendLine("EXCEPTION " + ex); }
        File.WriteAllText(Path.Combine(outDir, "selftest.txt"), log.ToString(), new UTF8Encoding(false));
        return 0;
    }
}
