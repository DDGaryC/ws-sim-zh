// WS 模擬器 AI 對手編輯器：讀取 AI 紀錄（模擬器 0.7.9.6 起，對戰中按 F8 開啟紀錄後，存在 AITraces\*.ai.jsonl）
// 把 AI 每一次的選擇整理成中文：當時的狀況、考慮了哪些卡、每張卡的分數、腳本裡哪幾行條件成立。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class TraceRec
{
    public string Kind, Phase, Op, Msg, File, Section, Expr, Result, Card;
    public int Turn, Decision, Parent, Line, Hand, Stock, Level, Clock, Deck, Player;
    public List<string> Cards = new List<string>();
}

class TraceEntry
{
    public string Title;            // 清單上的一行
    public List<TraceRec> Recs = new List<TraceRec>();
    public bool IsDecision;
}

static class TraceZh
{
    public static readonly Dictionary<string, string> Ops = new Dictionary<string, string> {
        { "ClockPhase", "放傷害區" }, { "PlaceCharacter", "出角色" }, { "ResolveEffect", "處理效果" }, { "AttackOrder", "攻擊順序" },
        { "AttackType", "攻擊方式" }, { "DirectAttack", "直接攻擊" }, { "FrontalAttack", "正面攻擊" }, { "SideAttack", "側面攻擊" },
        { "UseAuto", "要不要用自動效果" }, { "PayStock", "支付能量" }, { "EffectTargets", "選效果目標" }, { "AddToHand", "加入手牌" },
        { "YesNo", "是否詢問" }, { "LevelUp", "升級選卡" }, { "UseAct", "使用起動能力" }, { "Discard", "丟棄手牌" },
        { "PlayClimax", "打出名場面" }, { "Encore", "再演" }, { "Mulligan", "起手換牌" }, { "Number", "選數字" }, { "MultipleChoice", "選項" },
    };
    public static readonly Dictionary<string, string> Phases = new Dictionary<string, string> {
        { "StartingHand", "起手" }, { "Stand", "正置" }, { "Draw", "抽牌" }, { "Clock", "時計" }, { "Main", "主要" }, { "Climax", "名場面" },
        { "Attack", "攻擊" }, { "Encore", "再演" }, { "End", "結束" },
    };
    // 腳本區段（和編輯器的權重名稱一致；編輯器沒有的另外補）
    public static readonly Dictionary<string, string> Sections = new Dictionary<string, string> {
        { "DefenderValue", "當防守角色" }, { "ReverseValue", "想擊倒的對手角色" }, { "ThreatValue", "對手角色的威脅程度" },
        { "DirectAttackValue", "直接攻擊" }, { "TopDeckValue", "留在牌庫頂" }, { "AfterTriggersTopDeckValue", "判定後留在牌庫頂" },
        { "DefensiveBounceValue", "防守時彈回" }, { "SacrificeValue", "願意犧牲" }, { "ShouldClock", "要不要放傷害區" }, { "SkipClock", "跳過放傷害區" },
        { "StartMainPhase", "主要階段開始時的動作" }, { "EndMainPhase", "主要階段結束前的動作" }, { "GoingFirst_Attacker", "先攻第一回合的攻擊手" },
        { "GoingFirst_Backstage", "先攻第一回合的後列" }, { "GoingFirst_ExtraCenterStage", "先攻第一回合多放的前列" },
        { "ShouldEncore3", "要不要用三能量再演" }, { "ShouldEncore3OwnTurn", "自己回合要不要用三能量再演" }, { "SkipTriggerSalvage", "跳過判定回收" },
        { "AIAttacksWithoutClimax", "沒打名場面就攻擊時" }, { "AIPlaysClimax", "打出名場面時" }, { "FirstTurnDiscard", "起手換牌" }, { "Header", "整體設定" },
    };
    public static string Section(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.StartsWith("[")) return "卡片「" + s.Trim('[', ']') + "」的設定";
        var sec = Defs.Section(s);
        if (sec != null) return sec.Label;
        string zh;
        return Sections.TryGetValue(s, out zh) ? zh : s;
    }
    public static string Op(string s) { string zh; return s != null && Ops.TryGetValue(s, out zh) ? zh : s; }
    public static string Phase(string s) { string zh; return s != null && Phases.TryGetValue(s, out zh) ? zh : s; }

    // 條件式翻成中文：卡名「…」保留，其餘逐字換
    static readonly string[][] Words = {
        new[]{ "FreeStock", "可用能量" }, new[]{ "OpponentStock", "對手能量" }, new[]{ "Stock", "能量" }, new[]{ "Level", "等級" }, new[]{ "Clock", "傷害區張數" },
        new[]{ "Hand", "手牌數" }, new[]{ "TurnNumber", "回合數" }, new[]{ "RefreshCount", "洗牌重整次數" }, new[]{ "IsCharacter", "是角色" },
        new[]{ "IsClimax", "是名場面" }, new[]{ "IsEvent", "是事件" }, new[]{ "CardLevel", "卡片等級" }, new[]{ "CardCost", "卡片費用" },
        new[]{ "CardPower", "卡片戰力" }, new[]{ "Standing", "正置中" }, new[]{ "MyTurn", "我的回合" }, new[]{ "OpponentsTurn", "對手的回合" },
        new[]{ "EstimatedClimaxRatio", "牌庫名場面比例" }, new[]{ "PlayableCharactersInHand", "手牌中出得了的角色數" },
        new[]{ "PlayableCharactersNextTurn", "下回合出得了的角色數" }, new[]{ "OpenCenterStage", "前列空位" }, new[]{ "OpenBackstage", "後列空位" },
        new[]{ "OpponentLowestPower", "對手最低戰力" }, new[]{ "UnwantedCardsInHand", "手牌中不要的卡數" }, new[]{ "AttackersAvailable", "可攻擊的角色數" },
        new[]{ "ExpectedLevelNextTurn", "下回合預計等級" }, new[]{ "InBattle", "戰鬥中" }, new[]{ "True", "是" }, new[]{ "False", "否" },
        new[]{ "CenterStage", "前列" }, new[]{ "BackStage", "後列" }, new[]{ "Backstage", "後列" }, new[]{ "LeftCenterStage", "前列左" },
        new[]{ "RightCenterStage", "前列右" }, new[]{ "MiddleCenterStage", "前列中" }, new[]{ "WaitingRoom", "休息室" }, new[]{ "Memory", "思出區" },
        new[]{ "OpponentStage", "對手場上" }, new[]{ "Stage", "場上" }, new[]{ "Deck", "牌庫" }, new[]{ "Red", "紅" }, new[]{ "Blue", "藍" },
        new[]{ "Yellow", "黃" }, new[]{ "Green", "綠" }, new[]{ "Purple", "紫" }, new[]{ "Not", "不是" },
    };
    static readonly string[][] Funcs = {
        new[]{ @"HandContains\((\d+),", "手牌有 $1 張(" }, new[]{ @"WRContains\((\d+),", "休息室有 $1 張(" }, new[]{ @"StageContains\((\d+),", "場上有 $1 張(" },
        new[]{ @"DeckContains\((\d+),", "牌庫有 $1 張(" }, new[]{ @"HaveColor\((\w+)\)", "場上有$1色" },
        new[]{ @"ColorIs\((\w+)\)", "顏色是$1" }, new[]{ @"HasTrait\(([^)]*)\)", "有特徵《$1》" }, new[]{ @"FirstTurnAtLevel\((\d+)\)", "剛升到等級 $1 的回合" },
        new[]{ @"WouldRefreshAfterDraw\((\d+)\)", "再抽 $1 張就會洗牌重整" }, new[]{ @"WouldRefreshAfterMill\((\d+)\)", "再翻 $1 張就會洗牌重整" },
        new[]{ @"GetPriority\((\w+)\)", "〈$1〉分數" }, new[]{ @"InLocation (\w+)", "在$1" }, new[]{ @"Have\((\d+),", "自己有 $1 張(" },
        new[]{ @"KnownTopCardIs\(", "已知牌庫頂是(" }, new[]{ @"Return\((\w+)\)", "→ 決定：$1" }, new[]{ @"\bIf\(", "(" },
    };
    public static readonly Dictionary<string, string> Zones = new Dictionary<string, string> {
        { "StageLeft", "前列左" }, { "StageMiddle", "前列中" }, { "StageRight", "前列右" }, { "BackstageLeft", "後列左" }, { "BackstageRight", "後列右" }, { "StageBackLeft", "後列左" }, { "StageBackRight", "後列右" },
        { "Hand", "手牌" }, { "Deck", "牌庫" }, { "WaitingRoom", "休息室" }, { "Memory", "思出區" }, { "Stock", "能量區" }, { "Clock", "傷害區" },
        { "Level", "等級區" }, { "Climax", "名場面區" }, { "Stage", "場上" }, { "CenterStage", "前列" }, { "BackStage", "後列" }, { "OpponentStage", "對手場上" },
    };
    public static string Zone(string z) { string zh; return Zones.TryGetValue(z, out zh) ? zh : z; }

    public static string Expr(string e)
    {
        if (string.IsNullOrEmpty(e)) return e;
        var parts = Regex.Split(e, @"(\[[^\]]*\])");
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p.StartsWith("[") && p.EndsWith("]")) { sb.Append("「").Append(p.Substring(1, p.Length - 2)).Append("」"); continue; }
            string s = p;
            s = Regex.Replace(s, @"CountCards\((\w+),", m => Zone(m.Groups[1].Value) + "中的張數(");
            s = Regex.Replace(s, @"HasTrait ([^()&|]+?)(?=\)|\s*&&|\s*\|\||$)", "有特徵《$1》");
            foreach (var f in Funcs) s = Regex.Replace(s, f[0], f[1]);
            s = Regex.Replace(s, @"〈(\w+)〉", m => "〈" + Section(m.Groups[1].Value) + "〉");
            foreach (var w in Words) s = Regex.Replace(s, @"\b" + w[0] + @"\b", w[1]);
            s = s.Replace("&&&", " 且 ").Replace("&&", " 且 ").Replace("||", " 或 ").Replace(">=", "≥").Replace("<=", "≤").Replace("!=", "≠");
            sb.Append(s);
        }
        return Regex.Replace(sb.ToString(), @"\s{2,}", " ");
    }

    static string Names(string s) { return Regex.Replace(s, @"\[([^\]]*?)(?: \([A-Z0-9]+/[A-Z0-9-]+\))?\]", "「$1」"); }
    static readonly string[][] Notes = {
        // 開局時列出的腳本整體設定（Section Header）
        new[]{ @"^ColorPriority (.*)$", "顏色優先順序：$1" },
        new[]{ @"^Experience (\d+)$", "經驗值設定：$1" },
        new[]{ @"^ActMode Manual$", "起動能力：只在腳本指定時使用" }, new[]{ @"^ActMode Auto$", "起動能力：自動判斷" },
        new[]{ @"^BackstageMode Manual$", "後列：照腳本放" }, new[]{ @"^BackstageMode Auto$", "後列：自動判斷" },
        new[]{ @"^ClimaxMode Manual$", "名場面：照腳本打" }, new[]{ @"^ClimaxMode Auto$", "名場面：自動判斷" },
        new[]{ @"^NeverPlayFromHandTags: (.*)$", "有這些標籤的卡不從手牌打出：$1" },
        new[]{ @"^EmotePack (.*)$", "表情包：$1" },
        new[]{ @"^Decided not to clock\.$", "決定不放傷害區。" },
        new[]{ @"^(\[.*\]) was chosen as the best attacker \((P\d)\)\.$", "$1 被選為最好的攻擊手（$2）。" },
        new[]{ @"^(\[.*\]) was chosen to attack a character that couldnt be reversed earlier\.$", "$1 被派去攻擊剛才沒能擊倒的角色。" },
        new[]{ @"^(\[.*\]) was chosen to direct attack\.$", "$1 被選來直接攻擊。" },
        new[]{ @"^Planned climax is (\[.*\])\.$", "預定打的名場面：$1" },
        new[]{ @"^Wants to level a (\w+) card\.$", "升級時想放 $1 色的卡。" },
        new[]{ @"^Skipping clock by SkipClock policy: hand=(\d+); refreshes=(\d+); estimatedClimaxRatio=([\d.]+); stock=(\d+); level=(\d+); clock=(\d+)\.$", "依〈跳過放傷害區〉的設定不放傷害區（手牌 $1、洗牌重整 $2 次、牌庫名場面比例 $3、能量 $4、等級 $5、傷害區 $6）" },
        new[]{ @"^Explicit target rule for (\[.*\]) .*produced no matching targets; no target selected\.$", "$1 有指定目標的規則，但找不到符合的目標，所以不選。" },
        new[]{ @"^Reordering the top (\d+) cards of the deck$", "重新排列牌庫頂的 $1 張卡" },
        new[]{ @"^OnAttackMill (\[.*\]): leave-one budget; deck=(\d+), remaining triggers=(\d+), balanced checks=(.*)\.$", "$1 攻擊時翻牌：牌庫 $2 張、剩下的判定 $3 次" },
        new[]{ @"^Decided to clock\.$", "決定放傷害區。" },
        new[]{ @"^Playing (\[.*\])(?: with action plan)?$", "打出 $1" },
        new[]{ @"^Attacker (\[.*\]) was placed successfully\.$", "攻擊手 $1 放好了。" },
        new[]{ @"^Adding (\[.*\]) because it had the highest search value at (-?\d+)$", "加入 $1：它的「檢索／回收時想拿」分數最高（$2）。" },
        new[]{ @"^Global attack order: (.*)$", "攻擊順序：$1" },
        new[]{ @"^(\[.*\]) effect answering (True|False) based on file\.$", "$1 的效果：照腳本回答「$2」。" },
        new[]{ @"^(\[.*\]) AUTO #(\d+) accepted by (.*)$", "$1 的第 $2 個自動效果：條件成立，使用。條件：$3" },
        new[]{ @"^(\[.*\]) AUTO #(\d+) declined by (.*)$", "$1 的第 $2 個自動效果：條件不成立，不用。條件：$3" },
        new[]{ @"^Trying to use act #(\d+) of (\[.*\])$", "嘗試使用 $2 的第 $1 個起動能力" },
        new[]{ @"^(\[.*\]) using act\.$", "$1 使用起動能力。" },
        new[]{ @"^Planned climax was changed to (\[.*\])$", "預定的名場面改成 $1" },
        new[]{ @"^No automatic climax$", "沒有自動打名場面" },
        new[]{ @"^Main-phase center-stage limit set to (\d+)$", "主要階段的前列上限設為 $1" },
        new[]{ @"^Choice context (\w+)$", "選擇的情境：$1" },
        new[]{ @"^Current planned attackers = (.*)$", "目前預定的攻擊手：$1" },
        new[]{ @"^Chose .* from betterOptions\.$", "從比較好的選項裡選。" },
        new[]{ @"^Chose .* from options\.$", "從可以選的卡裡選。" },
        new[]{ @"^Chosen attacker is already on stage\.$", "選好的攻擊手已經在場上。" },
        new[]{ @"^Playing character to backstage (\[.*\]) in action section\.$", "依腳本的動作區段，把 $1 放到後列" },
        new[]{ @"^Explicit target rule for (\[.*\]) enforced\. Legal targets: (.*)$", "$1 有指定目標的規則，照規則選。可選的目標：$2" },
        new[]{ @"^(\[.*\]) chose (\[.*\]) for .*?; context=(\w+).*$", "$1 選了 $2（$3）" },
        new[]{ @"^Attempting to play (\[.*\]) to (\w+); current occupant=(.*?); .*$", "準備把 $1 放到 $2（原本在那裡的：$3）" },
        new[]{ @"^Preserving (\[.*\]) before playing (\[.*\]): moving to (\w+).*$", "打出 $2 前，先把 $1 移到 $3 保留" },
        new[]{ @"^Optimizing attackers globally: (.*)$", "整體調整攻擊手配置：$1" },
        new[]{ @"^Action Plan: (.*)$", "行動計畫：$1" },
        new[]{ @"^Starting main-phase planning\.$", "開始規劃主要階段" },
        new[]{ @"^Entering action section (\w+)\.$", "執行腳本的動作區段〈$1〉" },
        new[]{ @"^A legal direct attack takes precedence over normal frontal/side selection\.$", "可以直接攻擊時，優先直接攻擊。" },
        new[]{ @"^Final legal-attack branch; breaking Great Performance=(\w+); heuristic side preference=(\w+); side legal=(\w+)\.$", "一般的攻擊判斷：會打破大活躍＝$1；偏好側面攻擊＝$2；可以側面攻擊＝$3。" },
        new[]{ @"^Precedence: scripted target; ClockValue >500; ClockValue-SearchValue plus color bonus; unplayable; level; random tie\.$",
               "判斷順序：腳本指定的卡 →「願意放傷害區」> 500 →「願意放傷害區」減「檢索／回收時想拿」再加顏色分 → 出不了的卡 → 等級 → 隨機" },
        new[]{ @"^Precedence: LevelValue >=500; non-climax; desired colors; LevelValue; experience; missing level colors; printed level; random tie\.$",
               "判斷順序：「升級時選它」≥ 500 → 不是名場面 → 想要的顏色 →「升級時選它」→ 經驗 → 等級區缺的顏色 → 卡片等級 → 隨機" },
        new[]{ @"^Ranking order: planned target, cost-zero preference, SearchValue, random tie key\.$", "排序依據：預定的目標 → 費用 0 優先 →「檢索／回收時想拿」→ 隨機" },
        new[]{ @"^context=(\w+); effect=(\d+); optional=(\w+); amount=(\d+)$", "情境：$1；第 $2 個效果；可以不選＝$3；數量 $4" },
        new[]{ @"^amount=(\d+)$", "數量 $1" },
        new[]{ @"^Action entered normal gameplay routine\. Destination=(\w+)$", "開始執行，放到 $1" },
        new[]{ @"^Action entered normal gameplay routine\. Destination=$", "開始執行" },
        new[]{ @"^Script priority=(-?\d+); after strategic keyword policy=(-?\d+)$", "腳本分數 $1，套用策略關鍵字後 $2" },
    };
    public static string Note(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        foreach (var n in Notes)
        {
            var m = Regex.Match(s, n[0], RegexOptions.Singleline);
            if (!m.Success) continue;
            string r = m.Result(n[1]).Replace("＝True", "＝是").Replace("＝False", "＝否").Replace("「True」", "「是」").Replace("「False」", "「否」");
            r = Regex.Replace(r, @"〈(\w+)〉", mm => "〈" + Section(mm.Groups[1].Value) + "〉");
            r = Regex.Replace(r, @"(情境：|（)(AddToHand|PutIntoClock|Undefined|\w+?)(；|）|$)", mm => mm.Groups[1].Value + Context(mm.Groups[2].Value) + mm.Groups[3].Value);
            r = Regex.Replace(r, @"(條件：)(.*)$", mm => mm.Groups[1].Value + Expr(mm.Groups[2].Value));
            r = Regex.Replace(r, @"(StageBack|Stage|Backstage)(Left|Middle|Right)", mm => Zone(mm.Value)).Replace("<none>", "沒有");
            return Names(r).Replace(" -> ", " → ");
        }
        return Names(s);
    }
    static readonly Dictionary<string, string> Contexts = new Dictionary<string, string> {
        { "AddToHand", "加入手牌" }, { "PutIntoClock", "放到傷害區" }, { "PutIntoWaitingRoom", "放到休息室" }, { "PutIntoStock", "放到能量區" },
        { "Undefined", "一般" }, { "OwnEffectQuestion", "自己效果的詢問" }, { "Discard", "丟棄" }, { "PutOnStage", "放到舞台" },
    };
    public static string Context(string c) { string zh; return Contexts.TryGetValue(c, out zh) ? zh : Op(c); }
    public static string CandKey(string k)
    {
        switch (k) { case "plannedTarget": return "是預定的目標"; case "costZeroPreference": return "費用 0 優先"; case "SearchValue": return "檢索分數"; default: return k; }
    }
    public static string Result(string s)
    {
        switch (s)
        {
            case "selected": return "選了";
            case "True": return "是";
            case "False": return "否";
            case "none": return "沒有選";
            case "DirectAttack": return "直接攻擊";
            case "FrontalAttack": return "正面攻擊";
            case "SideAttack": return "側面攻擊";
            default: return s;
        }
    }
}

class TraceForm : Form
{
    readonly List<TraceEntry> entries = new List<TraceEntry>();
    readonly ListBox list;
    readonly FlowLayoutPanel detail;
    readonly CheckBox onlyDecisions;
    readonly Label info;
    readonly string root;
    readonly ToolTip tip = new ToolTip();
    readonly Dictionary<string, CardInfo> cardByName = new Dictionary<string, CardInfo>();
    StringBuilder plain = new StringBuilder();      // 同一份說明的純文字（檢查用的 /tracedump）

    public TraceForm(string root)
    {
        this.root = root;
        CardDb.Root = root;
        Text = "讀取 AI 紀錄";
        Font = UI.F(10f); BackColor = UI.Bg;
        ClientSize = new Size(1320, 860); MinimumSize = new Size(960, 600);
        StartPosition = FormStartPosition.CenterParent;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var head = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = UI.Panel };
        var title = new Label { Text = "AI 紀錄解說", Font = UI.F(15f, FontStyle.Bold), ForeColor = UI.Text, AutoSize = true, Location = new Point(20, 10) };
        var open = new FlatButton("開啟紀錄檔", 120) { Primary = true };
        open.Click += delegate { Pick(); };
        onlyDecisions = new CheckBox { Text = "只看 AI 的選擇（不看出牌、攻擊等動作）", AutoSize = true, Font = UI.F(10f), ForeColor = UI.Text };
        onlyDecisions.CheckedChanged += delegate { Fill(); };
        info = new Label { AutoSize = true, ForeColor = UI.Muted, Font = UI.F(9.5f), Location = new Point(22, 48) };
        head.Controls.AddRange(new Control[] { title, open, onlyDecisions, info });
        head.Resize += delegate
        {
            open.Location = new Point(head.Width - open.Width - 20, 14);
            onlyDecisions.Location = new Point(open.Left - onlyDecisions.Width - 16, 21);
        };
        Controls.Add(head);

        var help = new Label
        {
            Dock = DockStyle.Top, Height = 46, Padding = new Padding(20, 6, 20, 0), ForeColor = UI.Muted, Font = UI.F(9.5f),
            Text = "怎麼產生紀錄：和電腦 AI 對戰時按 F8 開「AI 檢查工具」，把「紀錄」切到「詳細」，打完後紀錄會存在模擬器資料夾的 AITraces。\n" +
                   "左邊每一行是 AI 的一次選擇或動作，點一下右邊會列出當時的狀況、每張卡的分數，以及腳本裡哪幾行條件成立。滑鼠移到卡圖上可以看大圖。"
        };
        Controls.Add(help);
        head.SendToBack();                 // 標題列放最上面，說明在它下面

        var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = UI.Line };
        list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = UI.F(10f), IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 46 };
        list.SelectedIndexChanged += delegate { ShowEntry(list.SelectedItem as TraceEntry); };
        list.DrawItem += DrawItem;
        split.Panel1.Controls.Add(list);
        detail = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Padding = new Padding(18, 12, 18, 24) };
        detail.Resize += delegate { FitDetail(); };
        split.Panel2.Controls.Add(detail);
        Controls.Add(split);
        split.BringToFront();
        Shown += delegate { split.SplitterDistance = 480; if (entries.Count == 0) Pick(); };
    }

    void DrawItem(object sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0) return;
        var t = list.Items[e.Index] as TraceEntry;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        string card = MainCard(t);
        var im = card == null ? null : Thumbs.Get(Card(card), 30, 42);
        if (im != null) e.Graphics.DrawImage(im, e.Bounds.X + 6 + (30 - im.Width) / 2, e.Bounds.Y + 2, im.Width, im.Height);
        var color = sel ? SystemColors.HighlightText : t.IsDecision ? UI.Text : UI.Muted;
        TextRenderer.DrawText(e.Graphics, t.Title, t.IsDecision ? UI.F(10f) : UI.F(9.5f), new Rectangle(e.Bounds.X + 42, e.Bounds.Y, e.Bounds.Width - 46, e.Bounds.Height), color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    // 清單上那一行代表的卡：選擇的結果、動作的卡
    static string MainCard(TraceEntry t)
    {
        if (t == null) return null;
        if (t.IsDecision)
        {
            var end = t.Recs.LastOrDefault(x => x.Kind == "decisionEnd");
            if (end != null && end.Cards.Count > 0) return end.Cards[0];
            var begin = t.Recs.FirstOrDefault(x => x.Kind == "decisionBegin");
            return begin != null && begin.Cards.Count > 0 ? begin.Cards[0] : null;
        }
        var f = t.Recs[0];
        if (!string.IsNullOrEmpty(f.Card)) return f.Card;
        var m = Regex.Match(f.Msg ?? "", @"\[([^\]]+?)(?: \([A-Z0-9]+/[A-Z0-9-]+\))?\]");
        return m.Success ? m.Groups[1].Value : null;
    }

    CardInfo Card(string name) { CardInfo c; return name != null && cardByName.TryGetValue(name, out c) ? c : null; }

    void Pick()
    {
        string dir = Path.Combine(root, "AITraces");
        using (var d = new OpenFileDialog { Title = "選擇 AI 紀錄檔", Filter = "AI 紀錄 (*.ai.jsonl)|*.ai.jsonl|所有檔案|*.*", InitialDirectory = Directory.Exists(dir) ? dir : root })
        {
            if (Directory.Exists(dir))
            {
                var last = new DirectoryInfo(dir).GetFiles("*.ai.jsonl").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (last != null) d.FileName = last.Name;
            }
            if (d.ShowDialog(this) != DialogResult.OK) return;
            LoadTrace(d.FileName);
        }
    }

    static int I(Dictionary<string, object> r, string k) { object o; return r.TryGetValue(k, out o) && o != null ? Convert.ToInt32(o) : 0; }
    static string S(Dictionary<string, object> r, string k) { object o; return r.TryGetValue(k, out o) && o != null ? o.ToString() : ""; }

    void LoadTrace(string path)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var recs = new List<TraceRec>();
            var codes = new Dictionary<string, string>();          // 卡名 → 卡號（紀錄裡每張卡都有 definition）
            string deckName = "", build = "";
            Func<object, string> name = o =>
            {
                var d = o as Dictionary<string, object>;
                if (d == null) return "";
                string n = S(d, "name"), code = S(d, "definition");
                if (n != "" && code != "" && !codes.ContainsKey(n)) codes[n] = code;
                return n;
            };
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (line.Length == 0) continue;
                var r = js.Deserialize<Dictionary<string, object>>(line);
                var t = new TraceRec
                {
                    Kind = S(r, "kind"), Phase = S(r, "phase"), Op = S(r, "operation"), Msg = S(r, "message"), File = S(r, "file"), Section = S(r, "section"),
                    Expr = S(r, "expression"), Result = S(r, "result"), Turn = I(r, "turn"), Decision = I(r, "decision"), Parent = I(r, "parent"), Line = I(r, "line"),
                    Hand = I(r, "hand"), Stock = I(r, "stock"), Level = I(r, "level"), Clock = I(r, "clock"), Deck = I(r, "deck"), Player = I(r, "player"),
                };
                object c; if (r.TryGetValue("card", out c)) t.Card = name(c);
                object cs; if (r.TryGetValue("cards", out cs) && cs is System.Collections.ArrayList) foreach (var x in (System.Collections.ArrayList)cs) t.Cards.Add(name(x));
                if (t.Kind == "header") build = S(r, "build");
                if (t.Kind == "rule" && deckName == "" && t.File != "") deckName = t.File;
                recs.Add(t);
            }
            cardByName.Clear();
            try
            {
                CardDb.Load(codes.Values.Distinct());
                foreach (var kv in codes) { var ci = CardDb.Get(kv.Value); if (ci != null) cardByName[kv.Key] = ci; }
            }
            catch { }
            Build(recs);
            info.Text = Path.GetFileName(path) + "　｜　腳本：" + (deckName == "" ? "?" : deckName) + "　｜　" + entries.Count(e => e.IsDecision) + " 次選擇" + (build == "" ? "" : "　｜　模擬器 " + build.Split(' ')[0]);
            Fill();
        }
        catch (Exception ex) { MessageBox.Show(this, "讀取失敗：" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { Cursor = Cursors.Default; }
    }

    void Build(List<TraceRec> recs)
    {
        entries.Clear();
        var byDecision = new Dictionary<int, TraceEntry>();
        foreach (var r in recs)
        {
            if (r.Kind == "header" || r.Kind == "match" || r.Kind == "footer") continue;
            if (r.Decision > 0)
            {
                TraceEntry e;
                if (!byDecision.TryGetValue(r.Decision, out e)) { e = new TraceEntry { IsDecision = true }; byDecision[r.Decision] = e; entries.Add(e); }
                e.Recs.Add(r);
                continue;
            }
            // 不屬於選擇的：出角色、攻擊、名場面、動作區段等，一筆一行（數值、條件細節略過）
            if (r.Kind == "actionBegin" || r.Kind == "attackChoice" || r.Kind == "plan" || (r.Kind == "note" && r.Msg != ""))
                entries.Add(new TraceEntry { Recs = { r } });
        }
        foreach (var e in entries)
        {
            var f = e.Recs[0];
            string head = "T" + f.Turn + " " + TraceZh.Phase(f.Phase) + "　";
            if (e.IsDecision)
            {
                var end = e.Recs.LastOrDefault(x => x.Kind == "decisionEnd");
                string res = end == null ? "（沒有結果）" : end.Cards.Count > 0 ? "→ " + string.Join("、", end.Cards.ToArray()) : "→ " + TraceZh.Result(end.Result);
                var begin = e.Recs.FirstOrDefault(x => x.Kind == "decisionBegin");
                string who = (f.Op == "UseAuto" || f.Op == "YesNo" || f.Op == "UseAct") && begin != null && begin.Cards.Count == 1 ? begin.Cards[0] + " " : "";
                e.Title = head + "【" + TraceZh.Op(f.Op) + "】" + who + res;
            }
            else if (f.Kind == "actionBegin") e.Title = head + "　" + TraceZh.Op(f.Op) + (f.Card != "" ? "：" + f.Card : "") + Dest(f.Msg);
            else if (f.Kind == "attackChoice") e.Title = head + "　" + TraceZh.Result(f.Result) + (f.Card != "" ? "：" + f.Card : "");
            else e.Title = head + "　" + TraceZh.Note(f.Msg).Replace("\n", " ");
        }
    }

    static string Dest(string msg)
    {
        var m = Regex.Match(msg ?? "", @"Destination=(\w+)");
        if (!m.Success) return "";
        string d = m.Groups[1].Value;
        return " → " + TraceZh.Zone(d);
    }

    void Fill()
    {
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var e in entries) if (!onlyDecisions.Checked || e.IsDecision) list.Items.Add(e);
        list.EndUpdate();
        if (list.Items.Count > 0) list.SelectedIndex = 0; else detail.Controls.Clear();
    }

    public static void Dump(string path, string outPath)
    {
        using (var f = new TraceForm(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(path)))))
        {
            f.LoadTrace(path);
            var sb = new StringBuilder();
            sb.Append("卡圖找到 ").Append(f.cardByName.Count(kv => kv.Value.Image != null)).Append(" / ").Append(f.cardByName.Count).Append(" 張\n");
            foreach (var e in f.entries) { f.ShowEntry(e); sb.Append(f.plain).Append("\n==========\n"); }
            File.WriteAllText(outPath, sb.ToString().Replace("\n", "\r\n"), new UTF8Encoding(true));
        }
    }

    // ---- 右邊的說明（文字＋卡圖）----
    int Wd { get { return Math.Max(300, detail.ClientSize.Width - detail.Padding.Horizontal - 24); } }
    void FitDetail() { foreach (Control c in detail.Controls) c.MaximumSize = new Size(Wd, 0); }
    void Add(string s, Font f, Color c, int top = 0)
    {
        plain.Append(s).Append('\n');
        detail.Controls.Add(new Label { Text = s, Font = f, ForeColor = c, AutoSize = true, UseMnemonic = false, MaximumSize = new Size(Wd, 0), Margin = new Padding(0, top, 0, 2) });
    }
    void H(string s) { Add(s, UI.F(12.5f, FontStyle.Bold), UI.Text); }
    void Sub(string s) { Add(s, UI.F(10.5f, FontStyle.Bold), UI.Accent, 14); }
    void P(string s, Color? c = null) { Add(s, UI.F(10.5f), c ?? UI.Text); }
    void Muted(string s) { Add(s, UI.F(9.5f), UI.Muted); }

    // 一排卡圖，每張下面一行小字（分數之類）；滑鼠移上去看大圖和卡名
    void Cards(IEnumerable<KeyValuePair<string, string>> items, Func<string, bool> highlight = null)
    {
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, MaximumSize = new Size(Wd, 0), Margin = new Padding(0, 2, 0, 4) };
        foreach (var kv in items)
        {
            plain.Append("　").Append(kv.Value == "" ? "" : kv.Value + "　").Append(kv.Key).Append('\n');
            var ci = Card(kv.Key);
            bool hi = highlight != null && highlight(kv.Key);
            var cell = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 8, 6), Padding = new Padding(3), BackColor = hi ? Color.FromArgb(214, 242, 222) : Color.White };
            Control pic;
            if (ci != null && ci.Image != null) pic = Thumbs.Pic(ci, 76, 106, null);
            else pic = new Label { Width = 76, Height = 106, UseMnemonic = false, Text = kv.Key, Font = UI.F(8.5f), ForeColor = UI.Muted, BackColor = UI.Bg, TextAlign = ContentAlignment.MiddleCenter };
            tip.SetToolTip(pic, kv.Key);
            cell.Controls.Add(pic);
            if (kv.Value != "") cell.Controls.Add(new Label { Text = kv.Value, Width = 76, AutoSize = false, Height = 20, TextAlign = ContentAlignment.MiddleCenter, Font = UI.F(9.5f, FontStyle.Bold), ForeColor = hi ? UI.Ok : UI.Text, Margin = Padding.Empty });
            row.Controls.Add(cell);
        }
        detail.Controls.Add(row);
    }
    static IEnumerable<KeyValuePair<string, string>> Plain(IEnumerable<string> names) { return names.Select(n => new KeyValuePair<string, string>(n, "")); }

    void ShowEntry(TraceEntry e)
    {
        detail.SuspendLayout();
        foreach (Control c in detail.Controls.Cast<Control>().ToList()) c.Dispose();
        detail.Controls.Clear();
        plain = new StringBuilder();
        try { ShowCore(e); }
        finally { detail.ResumeLayout(); detail.AutoScrollPosition = Point.Empty; }
    }

    void ShowCore(TraceEntry e)
    {
        if (e == null) return;
        var f = e.Recs[0];
        H(e.Title.Trim());
        P("等級 " + f.Level + "｜傷害區 " + f.Clock + " 張｜能量 " + f.Stock + "｜手牌 " + f.Hand + "｜牌庫 " + f.Deck, UI.Muted);
        if (!e.IsDecision)
        {
            string c = MainCard(e);
            if (c != null) Cards(Plain(new[] { c }));
            if (f.Msg != "") { Sub("說明"); P(TraceZh.Note(f.Msg)); }
            return;
        }

        var begin = e.Recs.FirstOrDefault(x => x.Kind == "decisionBegin");
        var end = e.Recs.LastOrDefault(x => x.Kind == "decisionEnd");
        var chosen = new HashSet<string>(end != null ? end.Cards : new List<string>());
        Sub("結果");
        if (end == null) P("（紀錄裡沒有結果，可能被中斷）", UI.Danger);
        else if (end.Cards.Count > 0) { P("選了：", UI.Ok); Cards(Plain(end.Cards), n => true); }
        else P(TraceZh.Result(end.Result), UI.Ok);

        if (begin != null && begin.Msg != "")
        {
            Sub("AI 怎麼判斷");
            P(begin.Msg.StartsWith("effect=") || begin.Msg.StartsWith("Auto:") ? "效果：" + Regex.Replace(begin.Msg, @"^effect=\d+; ", "") : TraceZh.Note(begin.Msg));
        }
        if (begin != null && begin.Cards.Count > 0) { Sub("可以選的卡（" + begin.Cards.Count + " 張，綠底是選中的）"); Cards(Plain(begin.Cards), chosen.Contains); }

        // 每張卡在各區段的分數（priority 紀錄）
        var pri = e.Recs.Where(x => x.Kind == "priority" && !string.IsNullOrEmpty(x.Card)).ToList();
        if (pri.Count > 0)
        {
            Sub("每張卡的分數（越高越優先）");
            foreach (var g in pri.GroupBy(x => x.Section))
            {
                Muted("〈" + TraceZh.Section(g.Key) + "〉");
                var seen = new HashSet<string>();
                var items = new List<KeyValuePair<string, string>>();
                foreach (var x in g.OrderByDescending(x => { int v; int.TryParse(x.Result, out v); return v; }))
                    if (seen.Add(x.Card)) items.Add(new KeyValuePair<string, string>(x.Card, x.Result));
                Cards(items, chosen.Contains);
                int adj = g.Count(x => { var m = Regex.Match(x.Msg, @"Script priority=(-?\d+); after strategic keyword policy=(-?\d+)"); return m.Success && m.Groups[1].Value != m.Groups[2].Value; });
                if (adj > 0) Muted("　有 " + adj + " 筆分數被策略關鍵字調整過（腳本原本的分數和最後用的不同）");
            }
        }

        // 候選排序（加入手牌等）
        var cand = e.Recs.Where(x => x.Kind == "candidate").ToList();
        if (cand.Count > 0)
        {
            Sub("候選排序");
            Muted(TraceZh.Note(cand[0].Msg));
            foreach (var g in cand.GroupBy(x => x.Card).Take(20))
                P("　" + g.Key + "：" + string.Join("；", g.Select(x => TraceZh.CandKey(x.Expr) + "＝" + TraceZh.Result(x.Result)).Distinct().ToArray()));
        }

        // 腳本規則：成立的列出來，不成立的只算數量
        var rules = e.Recs.Where(x => (x.Kind == "rule" || x.Kind == "command") && x.File != "").ToList();
        var hit = rules.Where(x => x.Result == "True" || x.Kind == "command").ToList();
        if (rules.Count > 0)
        {
            Sub("腳本裡成立的規則（" + hit.Count + " 條成立，" + (rules.Count - hit.Count) + " 條沒成立）");
            foreach (var x in hit.Take(40))
            {
                P("● " + x.File + " 第 " + x.Line + " 行　〈" + TraceZh.Section(x.Section) + "〉");
                Muted("　" + TraceZh.Expr(x.Expr));
            }
            if (hit.Count > 40) Muted("　…還有 " + (hit.Count - 40) + " 條");
        }

        var notes = e.Recs.Where(x => x.Kind == "note" || x.Kind == "attackChoice").ToList();
        if (notes.Count > 0) { Sub("AI 的備註"); foreach (var x in notes) P("・" + TraceZh.Note(x.Msg)); }
        if (e.Recs.Any(x => x.Kind == "truncated")) Muted("（這次選擇的細節太多，模擬器只記了前 512 筆，後面的分數和條件沒有記下來）");
    }
}
