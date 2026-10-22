// WS 模擬器 AI 對手編輯器：卡片資料讀取、專案資料、腳本產生
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// ======================================================================
// 卡片資料（CardContent\Cards\<系列>\<彈號>\CardData.txt）
// ======================================================================
class Effect
{
    public string Cat;      // Auto / Act / Cont
    public int Num;         // 同類別中的編號（從 1 開始；只算有說明文字的）
    public string Text = "";
    public string Source;   // 來自哪個共用效果（*Healer 之類），可能為 null
    public bool YesNo, Choice;   // 效果裡有「是／否」的詢問、有多選一的詢問
    public string Timing = "";   // 發動時機（Auto: 後面那個字，例如 OnAttack）
    public int LookUpTo;         // 效果會從自己牌庫頂「看／翻最多幾張」（0＝沒有）
    public bool CostStage;       // 費用要把自己場上的其他角色送走（進休息室、回想區…）
}

class CardInfo
{
    public string Code, Name, Type = "Character", Color = "", Trigger = "", Image, ImageName;
    public bool Counter;   // 反擊（對手回合才能用）的卡
    public int Level, Cost, Power, Soul, Backup;
    public List<string> Traits = new List<string>(), Tags = new List<string>(), Shorthands = new List<string>(), Combos = new List<string>();
    public List<string> Texts = new List<string>();
    public List<Effect> Effects = new List<Effect>();
    public bool IsChar { get { return Type == "Character"; } }
    public bool IsClimax { get { return Type == "Climax"; } }
    public bool IsEvent { get { return Type == "Event"; } }
    public IEnumerable<Effect> Autos { get { return Effects.Where(e => e.Cat == "Auto" && e.Text != ""); } }
    public IEnumerable<Effect> Acts { get { return Effects.Where(e => e.Cat == "Act" && e.Text != ""); } }
    public bool HasShort(string prefix) { return Shorthands.Any(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)); }
    public string ColorName
    {
        get
        {
            switch (Color) { case "Y": return "Yellow"; case "G": return "Green"; case "R": return "Red"; case "B": return "Blue"; case "P": return "Purple"; }
            return "";
        }
    }
    public string AllText { get { return string.Join("\n", Texts.ToArray()); } }
}

static class CardDb
{
    public static string Root;
    static Dictionary<string, List<string>> defines;
    static readonly Dictionary<string, CardInfo> cache = new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);
    static Dictionary<string, List<string>> setDirs;

    public static string CardsDir { get { return Path.Combine(Root, @"CardContent\Cards"); } }

    // 彈號（S121、W75…）→ 可能的資料夾
    static void IndexSets()
    {
        if (setDirs != null) return;
        setDirs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(CardsDir)) return;
        foreach (var series in Directory.GetDirectories(CardsDir))
            foreach (var set in Directory.GetDirectories(series))
            {
                string k = Path.GetFileName(set);
                List<string> l;
                if (!setDirs.TryGetValue(k, out l)) setDirs[k] = l = new List<string>();
                l.Add(set);
            }
    }

    static string SetOf(string code)
    {
        int a = code.IndexOf('/'), b = code.LastIndexOf('-');
        if (a < 0 || b <= a) return null;
        return code.Substring(a + 1, b - a - 1);
    }

    // 載入一批卡號（同一個彈只讀一次）
    public static void Load(IEnumerable<string> codes)
    {
        IndexSets();
        LoadDefines();
        var need = codes.Where(c => !cache.ContainsKey(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in need)
        {
            string s = SetOf(c); List<string> dirs;
            if (s != null && setDirs.TryGetValue(s, out dirs))
                foreach (var d in dirs) { string f = Path.Combine(d, "CardData.txt"); if (File.Exists(f)) files.Add(f); }
        }
        foreach (var f in files) ParseFile(f);
        // 找不到的卡：用全部資料再找一次（彈號資料夾命名不一致時）
        if (need.Any(c => !cache.ContainsKey(c)) && Directory.Exists(CardsDir))
        {
            var rest = new HashSet<string>(need.Where(c => !cache.ContainsKey(c)), StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(CardsDir, "CardData.txt", SearchOption.AllDirectories))
            {
                if (files.Contains(f)) continue;
                string txt;
                try { txt = File.ReadAllText(f); } catch { continue; }
                if (rest.Any(c => txt.Contains(": " + c))) { ParseFile(f); rest.RemoveWhere(c => cache.ContainsKey(c)); if (rest.Count == 0) break; }
            }
        }
    }

    public static CardInfo Get(string code)
    {
        CardInfo c; cache.TryGetValue(code, out c); return c;
    }

    static void LoadDefines()
    {
        if (defines != null) return;
        defines = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string p = Path.Combine(Root, @"Weiss Schwarz_Data\StreamingAssets\CommonEffects_USER_REFERENCE.txt");
        if (!File.Exists(p)) return;
        string cur = null; List<string> body = null;
        foreach (var raw in File.ReadAllLines(p))
        {
            if (raw.StartsWith("Define:"))
            {
                cur = raw.Substring(7).Trim(); body = new List<string>();
                defines[cur] = body;
                continue;
            }
            if (body == null) continue;
            if (raw.Trim().Length == 0) continue;
            if (raw.StartsWith("\t") || raw.StartsWith(" ")) body.Add(raw.Substring(1));
        }
    }

    // 展開 *Name(a,b)
    static List<string> Expand(string call, int depth)
    {
        var res = new List<string>();
        if (depth > 4) return res;
        string name = call, args = "";
        int p = call.IndexOf('(');
        if (p > 0 && call.EndsWith(")")) { name = call.Substring(0, p).Trim(); args = call.Substring(p + 1, call.Length - p - 2); }
        foreach (var kv in defines)
        {
            string dn = kv.Key, dpar = "";
            int q = dn.IndexOf('(');
            if (q > 0) { dpar = dn.Substring(q + 1).TrimEnd(')'); dn = dn.Substring(0, q); }
            if (!string.Equals(dn.Trim(), name, StringComparison.OrdinalIgnoreCase)) continue;
            var pn = dpar.Split(',').Select(x => x.Trim()).Where(x => x != "").ToList();
            var av = SplitArgs(args);
            var order = Enumerable.Range(0, pn.Count).OrderByDescending(i => pn[i].Length).ToList();
            foreach (var line in kv.Value)
            {
                string l = line;
                foreach (int i in order) l = l.Replace("$" + pn[i], i < av.Count ? av[i] : "").Replace(pn[i], i < av.Count ? av[i] : "");
                res.Add(l);
            }
            return res;
        }
        return res;
    }
    static List<string> SplitArgs(string s)
    {
        var r = new List<string>(); int depth = 0; var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c == '(' || c == '[') depth++;
            if (c == ')' || c == ']') depth--;
            if (c == ',' && depth == 0) { r.Add(sb.ToString().Trim()); sb.Clear(); continue; }
            sb.Append(c);
        }
        if (sb.Length > 0 || r.Count > 0) r.Add(sb.ToString().Trim());
        return r;
    }

    static readonly Regex Head = new Regex(@"^(Character|Event|Climax):\s*(\S+)\s*$");
    static void ParseFile(string file)
    {
        string[] lines;
        try { lines = File.ReadAllLines(file); } catch { return; }
        string dir = Path.GetDirectoryName(file);
        CardInfo c = null; var body = new List<string>();
        foreach (var raw in lines)
        {
            var m = Head.Match(raw.Trim());
            if (m.Success) { c = new CardInfo { Type = m.Groups[1].Value, Code = m.Groups[2].Value }; body.Clear(); continue; }
            if (c == null) continue;
            if (raw.Trim() == "EndCard")
            {
                Finish(c, body, dir);
                if (!cache.ContainsKey(c.Code)) cache[c.Code] = c;
                c = null; continue;
            }
            body.Add(raw);
        }
    }

    static void Finish(CardInfo c, List<string> body, string dir)
    {
        // 先展開共用效果（只展開最外層的 *xxx）
        var all = new List<string>(); int depth = 0;
        foreach (var raw in body)
        {
            string t = raw.Trim();
            if (depth == 0 && t.StartsWith("*"))
            {
                string call = t.Substring(1).Trim();
                c.Shorthands.Add(call);
                foreach (var l in Expand(call, 0)) all.Add("\u0001" + call + "\u0002" + l);
                continue;
            }
            all.Add(raw);
            if (!t.StartsWith("Text")) depth += t.Count(ch => ch == '{') - t.Count(ch => ch == '}');
        }
        depth = 0;
        var pendingText = new Dictionary<string, Queue<string>>();
        Effect lastEff = null;
        foreach (var raw0 in all)
        {
            string src = null, raw = raw0;
            if (raw.StartsWith("\u0001")) { int k = raw.IndexOf('\u0002'); src = raw.Substring(1, k - 1); raw = raw.Substring(k + 1); }
            string t = raw.Trim();
            if (t.Length == 0) continue;
            if (depth == 0)
            {
                string key = t, val = "";
                int sp = t.IndexOf(' ');
                if (sp > 0) { key = t.Substring(0, sp); val = t.Substring(sp + 1).Trim(); }
                switch (key)
                {
                    case "Name": c.Name = val; break;
                    case "Color": c.Color = val; break;
                    case "Level": int.TryParse(val, out c.Level); break;
                    case "Cost": int.TryParse(val, out c.Cost); break;
                    case "Power": int.TryParse(val, out c.Power); break;
                    case "Soul": int.TryParse(val, out c.Soul); break;
                    case "Trigger": c.Trigger = val; break;
                    case "Backup": int.TryParse(val.Split(' ')[0], out c.Backup); break;
                    case "Combo": c.Combos.Add(val); break;
                    case "Image": c.ImageName = val; break;
                    case "Counter": c.Counter = true; break;
                    case "Tag": c.Tags.Add(val); break;
                    case "Text":
                        c.Texts.Add(CleanText(val));
                        // 「Text Auto: …」配給下一個還沒有說明的同類效果
                        foreach (var cat in new[] { "Auto", "Act", "Cont" })
                            if (val.StartsWith(cat + ":"))
                            {
                                var e = c.Effects.FirstOrDefault(x => x.Cat == cat && x.Text == "");
                                if (e != null) e.Text = CleanText(val);
                                else
                                {
                                    Queue<string> q; if (!pendingText.TryGetValue(cat, out q)) pendingText[cat] = q = new Queue<string>();
                                    q.Enqueue(CleanText(val));
                                }
                            }
                        break;
                    default:
                        if (key.StartsWith("Trait")) c.Traits.Add(val);
                        break;
                }
                foreach (var cat in new[] { "Auto", "Act", "Cont" })
                    if (t.StartsWith(cat + ":"))
                    {
                        var e = new Effect { Cat = cat, Source = src, Timing = t.Substring(cat.Length + 1).Trim() };
                        lastEff = e;
                        Queue<string> q;
                        if (pendingText.TryGetValue(cat, out q) && q.Count > 0) e.Text = q.Dequeue();
                        c.Effects.Add(e);
                    }
            }
            if (depth > 0 && lastEff != null)
            {
                if (t.StartsWith("Question")) lastEff.YesNo = true;
                if (Regex.IsMatch(t, @"^PayCost .* Send \d+ ")) lastEff.CostStage = true;
                var lm = Regex.Match(t, @"^(Peek|RevealUpTo) Deck (\d+)");
                if (lm.Success) lastEff.LookUpTo = int.Parse(lm.Groups[2].Value);
                lm = Regex.Match(t, @"^\*Check(\d)Add1");
                if (lm.Success) lastEff.LookUpTo = int.Parse(lm.Groups[1].Value);
                if (t.StartsWith("MultipleChoiceQuestion")) lastEff.Choice = true;
            }
            if (!t.StartsWith("Text")) depth += t.Count(ch => ch == '{') - t.Count(ch => ch == '}');
            if (depth < 0) depth = 0;
        }
        foreach (var cat in new[] { "Auto", "Act" })
        {
            int n = 0;
            foreach (var e in c.Effects.Where(x => x.Cat == cat)) if (e.Text != "") e.Num = ++n;
        }
        if (c.Name == null) c.Name = c.Code;
        // 圖檔：檔名通常是卡號（/ 和 - 換成 _），但大小寫、副檔名（jpg／png）不一定，
        // 英文版的卡號數字前面還可能多一個 E（HOL_W104_E001.jpg）；卡片資料有 Image 欄位時用它指定的檔名
        var files = DirImages(dir);
        var keys = new List<string>();
        if (!string.IsNullOrEmpty(c.ImageName)) keys.Add(Norm(Path.GetFileNameWithoutExtension(c.ImageName)));
        keys.Add(Norm(c.Code));
        int dash = c.Code.LastIndexOf('-');
        if (dash > 0) keys.Add(Norm(c.Code.Substring(0, dash)) + "e" + Norm(c.Code.Substring(dash + 1)));
        // 有些彈的檔名沒有系列前綴（W120_E001.jpg 對應 Fks/W120-001）
        int slash = c.Code.IndexOf('/');
        if (slash >= 0 && dash > slash)
        {
            keys.Add(Norm(c.Code.Substring(slash + 1)));
            keys.Add(Norm(c.Code.Substring(slash + 1, dash - slash - 1)) + "e" + Norm(c.Code.Substring(dash + 1)));
        }
        if (dash > 0) keys.Add(Norm(c.Code.Substring(dash + 1)));   // 有些彈的檔名只有編號（001.jpg）
        foreach (var k in keys) { string path; if (files.TryGetValue(k, out path)) { c.Image = path; break; } }
    }

    static string Norm(string x) { var sb = new StringBuilder(); foreach (char ch in x) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch)); return sb.ToString(); }
    static readonly Dictionary<string, Dictionary<string, string>> dirImages = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    static Dictionary<string, string> DirImages(string dir)
    {
        Dictionary<string, string> d;
        if (dirImages.TryGetValue(dir, out d)) return d;
        d = new Dictionary<string, string>();
        try
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".png" && ext != ".jpeg" && ext != ".bmp" && ext != ".gif") continue;
                string k = Norm(Path.GetFileNameWithoutExtension(f));
                if (!d.ContainsKey(k)) d[k] = f;
            }
        }
        catch { }
        dirImages[dir] = d; return d;
    }

    static readonly Regex Sprite = new Regex("<sprite name=\"([^\"]+)\"\\s*/?>");
    static readonly Dictionary<string, string> SpriteNames = new Dictionary<string, string> {
        {"reverse","【倒置】"},{"rest","【橫置】"},{"stand","【正置】"},{"mini_soul","[魂]"},{"mini_gate","[城門]"},{"mini_door","[木門]"},
        {"mini_book","[書]"},{"mini_bag","[寶袋]"},{"mini_bar","[金磚]"},{"mini_shot","[火燒]"},{"mini_wind","[風吹]"},{"mini_standby","[開機]"},
        {"mini_choice","[箭頭]"},{"mini_discovery","[望遠鏡]"},{"mini_chance","[燈泡]"},{"mini_focus","[橘子]"} };
    public static string CleanText(string s)
    {
        s = Sprite.Replace(s, m =>
        {
            string n = m.Groups[1].Value, v;
            if (SpriteNames.TryGetValue(n, out v)) return v;
            var cm = Regex.Match(n, @"^(\d+)cost$");
            if (cm.Success) return "(" + cm.Groups[1].Value + ")";
            return "[" + n + "]";
        });
        return s.Replace("\\n", " ");
    }
}

// ======================================================================
// 專案資料（會以 JSON 形式嵌在產生的腳本開頭註解中）
// ======================================================================
class Clause
{
    public string Key = "Level";   // 見 Cond.Keys
    public string Card = "";       // 需要卡片的條件：卡名或類別代碼
    public string Op = ">=";
    public double Val = 1;
}
class Cond
{
    public bool Any;               // true＝任一成立；false＝全部成立
    public List<Clause> Items = new List<Clause>();
    public bool Empty { get { return Items.Count == 0; } }
}
class EffCfg
{
    public string Mode = "default"; // AUTO：default / always / never / cond；ACT：default / always / cond
    public string When = "end";     // ACT：start / end
    public Cond Cond = new Cond();
    public List<Cond> OrConds = new List<Cond>();   // 另外幾組條件：符合任何一組就算成立
    public List<string> Targets = new List<string>();
    public bool PlayFor;                  // ACT：這張卡是為了這個效果才出場（能用才從手牌出，出場後馬上用）
    public Cond AlsoCond = new Cond();    // PlayFor 時，另外允許出場的條件（空白＝沒有）
    public bool Early;                    // ACT：符合 EarlyCond 時，主要階段一開始就先用（先用再出牌）
    public Cond EarlyCond = new Cond();
    public List<string> Targets2 = new List<string>();   // 一次選多張時「其餘的卡」的偏好（需要 AI 選卡修正補丁）
    public bool Fallback = true;
}
// 斬殺回合陣型：一格＝一張想放上場的卡（排越前面越優先）
class Slot
{
    public string Card = "";
    public bool Back;              // true＝後列
}
class Formation
{
    public int MinLevel = 3;
    public string Climax = "";     // 手上要有這張名場面才啟動（空白＝不需要）
    public List<Slot> Slots = new List<Slot>();
}
class Override
{
    public string Section = "AttackerValue";
    public int Level = 3;
    public Cond Cond = new Cond();
}
class CardCfg
{
    public int Keep = -1;                 // 起手換牌：-1 不保留、1~4 保留張數
    public string Role = "";              // 角色定位（見 Defs.Roles）；權重沒有另外設定時，用定位的預設值
    public Dictionary<string, int> W = new Dictionary<string, int>();   // 權重：0~6，3＝普通
    public List<Override> Overrides = new List<Override>();
    public Dictionary<string, EffCfg> Auto = new Dictionary<string, EffCfg>();   // key＝效果編號
    public Dictionary<string, EffCfg> Act = new Dictionary<string, EffCfg>();
    public bool NeverPlay, OnlyOpen, Middle, Emergency, DontAttackOnlyCopy, NeverAttackBombs;
    public string AttackMode = "normal";  // normal / prefer / force / never
    public string ClimaxMode = "";        // basic / combo / both / standby / stocksoul / never
    public string Combo = "";             // 角色：搭配的名場面
    public int ComboCost = -1;            // 連動要付的能量（-1＝還沒偵測）
    public EffCfg Ev;                     // 事件卡：什麼時候打出（null＝還沒設定過，會填預設值）
    public int ComboHandMax = -1;         // 連動要求「手牌 N 張以下」（-1＝還沒偵測，0＝沒有這個條件）
    public string BackupMode = "auto";    // auto / always / never
    public bool Healer;
    public int ChoiceHandBelow = 7;       // 箭頭：手牌少於幾張時回手牌
    public List<string> ChoiceTargets = new List<string>();   // 箭頭：要拿哪張（排越前面越優先）
    public List<string> EventTargets = new List<string>();
    public string Raw = "";
}
class AiProject
{
    public int Format = 1;
    public string Name = "";
    public List<string> Deck = new List<string>();
    public List<string> Colors = new List<string>();
    public int Experience;
    public int ClockAlwaysBelow = 2;      // 等級低於此值時一律放傷害區
    public bool NoClockAtL3 = true;
    public int ClockIfHandBelow = 5;      // 其他時候手牌少於幾張就放（0＝不放）
    public bool Encore3 = true;
    public int Encore3Stock = 7;
    public int Encore3MinProtect = 4;     // 保護權重至少「高」才再演
    public bool FillAttackers = true;
    public bool MillBudget = true;        // 攻擊時翻牌：盡量讓牌庫剩一張，打出的名場面才洗得回牌庫
    public bool AttackFirst = true;       // 攻擊優先：攻擊手不夠時，優先找／保留能出的角色
    public bool Emotes = true;
    public Dictionary<string, CardCfg> Cards = new Dictionary<string, CardCfg>();
    public List<Formation> Formations = new List<Formation>();
    public string Raw = "";

    public CardCfg Cfg(string name)
    {
        CardCfg c;
        if (!Cards.TryGetValue(name, out c)) Cards[name] = c = new CardCfg();
        return c;
    }
}

// ======================================================================
// 條件、權重等的定義（介面與產生器共用）
// ======================================================================
static class Defs
{
    // 條件種類：代碼、顯示名稱、是否需要選卡、是否為「是／否」
    public class CondKey { public string Key, Label, Expr; public bool NeedCard, Bool, Percent; }
    public static readonly List<CondKey> Keys = new List<CondKey> {
        new CondKey{Key="Level",Label="我的等級",Expr="Level"},
        new CondKey{Key="OpponentLevel",Label="對手等級",Expr="OpponentLevel"},
        new CondKey{Key="Clock",Label="我的傷害區張數",Expr="Clock"},
        new CondKey{Key="OpponentClock",Label="對手傷害區張數",Expr="OpponentClock"},
        new CondKey{Key="Stock",Label="我的能量",Expr="Stock"},
        new CondKey{Key="OpponentStock",Label="對手能量",Expr="OpponentStock"},
        new CondKey{Key="Hand",Label="我的手牌張數",Expr="Hand"},
        new CondKey{Key="OpponentHand",Label="對手手牌張數",Expr="OpponentHand"},
        new CondKey{Key="Deck",Label="我的牌庫張數",Expr="Deck"},
        new CondKey{Key="OpponentDeck",Label="對手牌庫張數",Expr="OpponentDeck"},
        new CondKey{Key="RefreshCount",Label="我洗牌重置過幾次",Expr="RefreshCount"},
        new CondKey{Key="Ratio",Label="我牌庫的名場面比例（%）",Expr="EstimatedClimaxRatio",Percent=true},
        new CondKey{Key="Playable",Label="手牌中現在出得了的角色數",Expr="PlayableCharactersInHand"},
        new CondKey{Key="OpenCenterStage",Label="前列空位數",Expr="OpenCenterStage"},
        new CondKey{Key="OpenBackstage",Label="後列空位數",Expr="OpenBackstage"},
        new CondKey{Key="InHand",Label="手牌中的張數：",Expr="CountCards(Hand,{0})",NeedCard=true},
        new CondKey{Key="OnStage",Label="場上的張數：",Expr="CountCards(Stage,{0})",NeedCard=true},
        new CondKey{Key="InHandStage",Label="手牌＋場上的張數：",Expr="CountCards(Hand,{0}) + CountCards(Stage,{0})",NeedCard=true},
        new CondKey{Key="InWR",Label="休息室的張數：",Expr="CountCards(WaitingRoom,{0})",NeedCard=true},
        new CondKey{Key="InMemory",Label="回想區的張數：",Expr="CountCards(Memory,{0})",NeedCard=true},
        new CondKey{Key="InWRMem",Label="休息室＋回想區的張數：",Expr="CountCards(WaitingRoom,{0}) + CountCards(Memory,{0})",NeedCard=true},
        new CondKey{Key="InDeck",Label="牌庫中的張數：",Expr="CountCards(Deck,{0})",NeedCard=true},
        new CondKey{Key="InStock",Label="能量區的張數：",Expr="CountCards(Stock,{0})",NeedCard=true},
        new CondKey{Key="OppStage",Label="對手場上的張數：",Expr="CountCards(OpponentStage,{0})",NeedCard=true},
        new CondKey{Key="InCenter",Label="我前列的張數：",Expr="CountCards(CenterStage,{0})",NeedCard=true},
        new CondKey{Key="InBack",Label="我後列的張數：",Expr="CountCards(BackStage,{0})",NeedCard=true},
        new CondKey{Key="InClimax",Label="我名場面區的張數：",Expr="CountCards(Climax,{0})",NeedCard=true},
        new CondKey{Key="MyTurn",Label="現在是我的回合",Expr="MyTurn",Bool=true},
        new CondKey{Key="OppTurn",Label="現在是對手的回合",Expr="OpponentsTurn",Bool=true},
    };
    public static CondKey Key(string k) { return Keys.FirstOrDefault(x => x.Key == k) ?? Keys[0]; }

    // 卡片類別（條件與目標偏好共用）。卡名之外的選項以「#」開頭
    public static readonly List<string[]> Groups = new List<string[]> {
        new[]{"#Char","任何角色","IsCharacter"},
        new[]{"#Climax","任何名場面","IsClimax"},
        new[]{"#Event","任何事件","IsEvent"},
        new[]{"#Any","任何卡","True"},
        new[]{"#L0","等級 0 角色","IsCharacter && CardLevel = 0"},
        new[]{"#L1","等級 1 角色","IsCharacter && CardLevel = 1"},
        new[]{"#L2","等級 2 角色","IsCharacter && CardLevel = 2"},
        new[]{"#L3","等級 3 角色","IsCharacter && CardLevel = 3"},
        new[]{"#L1up","等級 1 以上角色","IsCharacter && CardLevel >= 1"},
        new[]{"#L2up","等級 2 以上角色","IsCharacter && CardLevel >= 2"},
        new[]{"#Cost0","0 費角色","IsCharacter && CardCost = 0"},
        new[]{"#NoTrig","沒有魂判定的角色","IsCharacter && Not If(HasTrigger Soul)"},
        new[]{"#Dump","適合放傷害區的卡（「願意放傷害區」是正的）","GetPriority(ClockValue) > 0"},
        new[]{"#NotKeep","不是要保留的卡（「願意放傷害區」不是負的）","GetPriority(ClockValue) >= 0"},
        new[]{"#Trig","有魂判定的角色","IsCharacter && If(HasTrigger Soul)"},
        new[]{"#Standing","正置的角色","IsCharacter && Standing"},
        new[]{"#Rested","橫置的角色","IsCharacter && Rested"},
        new[]{"#Reversed","倒置的角色","IsCharacter && Reversed"},
        new[]{"#HiPower","戰力最高的角色（6000 以上）","IsCharacter && CardPower >= 6000"},
    };
    public static string GroupLabel(string k) { var g = Groups.FirstOrDefault(x => x[0] == k); return g != null ? g[1] : k; }
    // 目標偏好可以限定區域，存成「@區域|卡名或類別」
    public static readonly List<string[]> Zones = new List<string[]> {
        new[]{"","任何地方"}, new[]{"Hand","手牌裡的"}, new[]{"Clock","傷害區的"}, new[]{"WaitingRoom","休息室的"}, new[]{"Deck","牌庫的"},
        new[]{"Stock","能量區的"}, new[]{"CenterStage","前列的"}, new[]{"BackStage","後列的"}, new[]{"Stage","場上的"}, new[]{"Level","等級區的"}, new[]{"Memory","回想區的"} };
    public static void SplitZone(string k, out string zone, out string key)
    {
        zone = ""; key = k ?? "";
        if (key.StartsWith("@")) { int bar = key.IndexOf('|'); if (bar > 0) { zone = key.Substring(1, bar - 1); key = key.Substring(bar + 1); } }
    }
    public static string JoinZone(string zone, string key) { return string.IsNullOrEmpty(zone) ? key : "@" + zone + "|" + key; }
    public static string Pred(string k)
    {
        string zone0, key0; SplitZone(k, out zone0, out key0);
        if (zone0 != "") return Pred(key0) + " && If(InLocation " + zone0 + ")";
        if (string.IsNullOrEmpty(k)) return "True";
        var g = Groups.FirstOrDefault(x => x[0] == k);
        if (g != null) return g[2];
        return "[" + k + "]";
    }

    public static readonly string[] LevelNames = { "絕不", "很低", "低", "普通", "高", "很高", "最優先" };

    // 角色定位：一次決定一組權重。W＝{ 區段, 等級 } 兩兩一組
    public class Role { public string Key, Label, Desc; public object[] W = new object[0]; }
    public static readonly List<Role> Roles = new List<Role> {
        new Role{Key="",Label="一般",Desc="不特別設定，交給 AI 判斷。"},
        new Role{Key="attacker",Label="主攻手",Desc="優先放到前列攻擊。",W=new object[]{"AttackerValue",5}},
        new Role{Key="finisher",Label="終結者（連動主力）",Desc="後期的主力：優先上場、優先找、不輕易丟掉或犧牲。",
            W=new object[]{"AttackerValue",5,"SearchValue",5,"ProtectValue",5,"WRValue",1,"ClockValue",1,"LevelValue",1}},
        new Role{Key="combo",Label="連動角色",Desc="配合名場面的角色：比較會上場、比較想拿、會保護。",
            W=new object[]{"AttackerValue",4,"SearchValue",4,"ProtectValue",4}},
        new Role{Key="engine",Label="後列引擎（集中等）",Desc="固定放後列、不拿去攻擊，會保護、比較想拿。",
            W=new object[]{"BackstageValue",5,"AttackerValue",1,"SearchValue",4,"ProtectValue",4,"WRValue",2,"ClockValue",2}},
        new Role{Key="support",Label="後列應援",Desc="偏好放後列幫前列加戰力。",W=new object[]{"BackstageValue",4,"AttackerValue",2}},
        new Role{Key="backup",Label="助太刀（留在手上）",Desc="盡量留在手上當助太刀，不優先上場，也比較不會丟掉。",
            W=new object[]{"AttackerValue",1,"WRValue",2,"ClockValue",2}},
        new Role{Key="utility",Label="功能角色（用完就好）",Desc="主要是用它的效果，上場後被打掉也沒關係。",W=new object[]{"ProtectValue",2}},
        new Role{Key="filler",Label="湊數／可以丟",Desc="需要丟牌或放傷害區時先用它，不特別去找。",
            W=new object[]{"WRValue",4,"ClockValue",4,"SearchValue",2,"ProtectValue",1}},
        new Role{Key="none",Label="不出場",Desc="不從手牌打出，只拿來付費用（丟牌、放傷害區）。",W=new object[]{"WRValue",5,"ClockValue",5,"SearchValue",1}},
    };
    public static Role GetRole(string k) { return Roles.FirstOrDefault(r => r.Key == (k ?? "")) ?? Roles[0]; }
    // 這張卡在某個權重區段的實際等級：自己設定的優先，其次是定位的預設，都沒有就是「普通」
    public static int Eff(CardCfg cfg, string sec)
    {
        int lv;
        if (cfg.W.TryGetValue(sec, out lv)) return lv;
        var w = GetRole(cfg.Role).W;
        for (int i = 0; i + 1 < w.Length; i += 2) if ((string)w[i] == sec) return (int)w[i + 1];
        return 3;
    }

    // 權重區段：代碼、名稱、說明、七個等級的數值
    public class Sec { public string Key, Label, Hint; public int[] V; public string Only; }
    public static readonly List<Sec> Sections = new List<Sec> {
        new Sec{Key="AttackerValue",Label="當攻擊手",Hint="越高越會被放到前列攻擊；「絕不」＝不拿它攻擊",V=new[]{-5000,50,150,300,600,1000,2000},Only="char"},
        new Sec{Key="BackstageValue",Label="放後列",Hint="比「當攻擊手」高時，會優先放到後列",V=new[]{-5000,-500,-100,0,600,1200,3000},Only="char"},
        new Sec{Key="AttackOrder",Label="攻擊順序",Hint="越高越先攻擊",V=new[]{-2000,-1000,-300,0,300,1000,2000},Only="char"},
        new Sec{Key="ProtectValue",Label="保護",Hint="越高越不會被犧牲；也影響再演和助太刀",V=new[]{-1000,-200,-50,0,300,1000,10000},Only="char"},
        new Sec{Key="SearchValue",Label="檢索／回收時想拿",Hint="從牌庫或休息室拿卡時，越高越優先",V=new[]{-5000,-500,-100,0,500,1500,5000}},
        new Sec{Key="WRValue",Label="願意丟到休息室",Hint="需要丟手牌時，越高越先丟",V=new[]{-5000,-1000,-200,0,200,1000,5000}},
        new Sec{Key="ClockValue",Label="願意放傷害區",Hint="放傷害區時，越高越先放",V=new[]{-20000,-1000,-200,0,200,600,1000}},
        new Sec{Key="LevelValue",Label="升級時選它",Hint="升級時，越高越會被放到等級區",V=new[]{-20000,-1000,-200,0,200,500,1000}},
        new Sec{Key="StandbyValue",Label="開機時選它",Hint="開機判定／開機名場面時，越高越會被叫出來",V=new[]{-5000,-1000,-100,0,1000,5000,15000},Only="char"},
    };
    public static Sec Section(string k) { return Sections.FirstOrDefault(s => s.Key == k); }
}

// ======================================================================
// 腳本產生
// ======================================================================
static class Gen
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const string DataTag = "// WSAI-DATA:";

    public static string Expr(Clause c)
    {
        var k = Defs.Key(c.Key);
        string e = k.NeedCard ? string.Format(k.Expr, Defs.Pred(c.Card)) : k.Expr;
        if (k.Bool) return c.Val > 0 ? e : "Not " + e;
        double v = k.Percent ? c.Val / 100.0 : c.Val;
        string op = c.Op == "≥" ? ">=" : c.Op == "≤" ? "<=" : c.Op == "≠" ? "!=" : c.Op;
        return e + " " + op + " " + v.ToString(Inv);
    }
    public static string Expr(Cond c)
    {
        if (c == null || c.Empty) return "True";
        var parts = c.Items.Select(Expr).ToList();
        if (parts.Count == 1) return parts[0];
        return string.Join(c.Any ? " ||| " : " &&& ", parts.ToArray());
    }

    public static List<Cond> Groups(EffCfg e)
    {
        var res = new List<Cond>();
        if (!e.Cond.Empty) res.Add(e.Cond);
        foreach (var o in e.OrConds) if (o != null && !o.Empty) res.Add(o);
        return res;
    }
    public static string ExprAny(EffCfg e)
    {
        var g = Groups(e);
        return g.Count == 0 ? "True" : string.Join(" ||| ", g.Select(Expr).ToArray());
    }

    public static string Serialize(AiProject p)
    {
        var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        return ser.Serialize(p);
    }
    public static AiProject Deserialize(string json)
    {
        var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        return ser.Deserialize<AiProject>(json);
    }
    public static AiProject ReadEmbedded(string script)
    {
        var sb = new StringBuilder();
        foreach (var l in script.Split('\n'))
        {
            string t = l.Trim();
            if (t.StartsWith(DataTag)) sb.Append(t.Substring(DataTag.Length).Trim());
        }
        if (sb.Length == 0) return null;
        return Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(sb.ToString())));
    }

    static void W(StringBuilder sb, string s) { sb.Append(s).Append("\r\n"); }

    public static List<CardInfo> UniqueCards(AiProject p)
    {
        var res = new List<CardInfo>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in p.Deck)
        {
            var c = CardDb.Get(code);
            if (c != null && seen.Add(c.Name)) res.Add(c);
        }
        return res;
    }

    public static string Build(AiProject p)
    {
        var cards = UniqueCards(p);
        var sb = new StringBuilder();
        W(sb, "// 由「WS 模擬器 AI 對手編輯器」產生。請用編輯器修改：直接改這個檔案的內容，下次在編輯器儲存時會被覆蓋。");
        W(sb, "// AI 名稱：" + p.Name);
        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialize(p)));
        for (int i = 0; i < b64.Length; i += 160) W(sb, DataTag + b64.Substring(i, Math.Min(160, b64.Length - i)));
        W(sb, "");

        // ---- 標頭 ----
        W(sb, "Section Header");
        var cols = p.Colors.Where(c => c != "").ToList();
        if (cols.Count > 0) W(sb, "ColorPriority " + string.Join(" ", cols.ToArray()));
        W(sb, "Experience " + p.Experience);
        W(sb, "ActMode Manual");
        W(sb, "BackstageMode Auto");
        W(sb, "ClimaxMode Auto");
        W(sb, "");

        // ---- 起手換牌 ----
        W(sb, "Section FirstTurnDiscard");
        foreach (var c in cards)
        {
            var cfg = p.Cfg(c.Name);
            if (cfg.Keep > 0) W(sb, "KeepCopies(" + cfg.Keep + ") [" + c.Name + "]");
        }
        W(sb, "");

        // ---- 表情 ----
        if (p.Emotes)
        {
            foreach (var kv in new[] {
                new[]{"OpponentTakes4OrMoreDamageNotCanceled","A, M, H"}, new[]{"OpponentTakes7OrMoreTotalDamage","K, G, W"},
                new[]{"OpponentCancels4OrMoreDamage","R, Z, W"}, new[]{"AIUsesBackup","N, S, Y"}, new[]{"OpponentCancels3OrMoreTimes","J, V, D"},
                new[]{"AIPlaysClimax","F, I, Q"}, new[]{"OpponentPlaysBackupOrCounter","R, X, K"}, new[]{"OpponentAttacksFor4OrMoreDamage","U, V, C"},
                new[]{"AIRefreshPenaltyClimax","C, Z, P"}, new[]{"AIPassesWithoutAttacks","B, Z"}, new[]{"AITriggers2OrMoreClimaxes","J, Z, D"} })
            { W(sb, "Section " + kv[0]); W(sb, "Emote " + kv[1]); W(sb, ""); }
        }

        // ---- 被效果放上舞台時要去前列還是後列 ----
        // 遊戲用 DefenderValue 判斷：是 0 的角色不會被放到前列，前列有空位也會跑去蓋後列的角色。
        // 所以攻擊型的角色給正值，後列型的維持 0。
        W(sb, "Section DefenderValue");
        W(sb, "Default(100) IsCharacter");
        foreach (var c in cards.Where(x => x.IsChar))
        {
            var cfg = p.Cfg(c.Name);
            int av = Defs.Section("AttackerValue").V[Defs.Eff(cfg, "AttackerValue")];
            int bv = Defs.Section("BackstageValue").V[Defs.Eff(cfg, "BackstageValue")];
            int dv = (av <= 0 || bv >= av || CannotAttack(c)) ? 0 : Defs.Eff(cfg, "AttackerValue") >= 5 ? 300 : Math.Min(100, av);
            if (dv != 100) W(sb, "Priority(" + dv + ") [" + c.Name + "]");
        }
        W(sb, "");

        // ---- 權重 ----
        foreach (var sec in Defs.Sections)
        {
            W(sb, "Section " + sec.Key);
            if (sec.Key == "AttackerValue")
            {
                W(sb, "Default(" + sec.V[3] + ") IsCharacter");
                foreach (var c in cards.Where(x => CannotAttack(x) && Defs.Eff(p.Cfg(x.Name), "AttackerValue") != 0))
                    W(sb, "Priority(" + sec.V[0] + ") [" + c.Name + "]");
            }
            foreach (var c in cards)
            {
                if (sec.Only == "char" && !c.IsChar) continue;
                var cfg = p.Cfg(c.Name);
                int lv = Defs.Eff(cfg, sec.Key);
                if (lv != 3) W(sb, "Priority(" + sec.V[lv] + ") [" + c.Name + "]");
            }
            // 特殊情況放在後面（最後符合的那一行勝出）
            foreach (var c in cards)
                foreach (var o in p.Cfg(c.Name).Overrides.Where(o => o.Section == sec.Key))
                {
                    // 「任一成立」拆成多行（任何一行符合就會套用）
                    if (o.Cond.Any && o.Cond.Items.Count > 1)
                        foreach (var it in o.Cond.Items) W(sb, "Priority(" + sec.V[o.Level] + ") [" + c.Name + "] &&& " + Expr(it));
                    else W(sb, "Priority(" + sec.V[o.Level] + ") [" + c.Name + "]" + (o.Cond.Empty ? "" : " &&& " + Expr(o.Cond)));
                }
            // 手上已經有兩張以上的同名角色：再拿的價值不高
            if (sec.Key == "SearchValue")
                foreach (var c in cards.Where(x => x.IsChar))
                    W(sb, "PriorityBonus(-1500) [" + c.Name + "] &&& CountCards(Hand,[" + c.Name + "]) >= 2");
            // 連動名場面：手上只有一張時不要丟、不要放傷害區；手上沒有時優先找
            foreach (var cx in cards.Where(x => x.IsClimax))
            {
                var partners = cards.Where(x => x.IsChar && p.Cfg(x.Name).Combo == cx.Name).ToList();
                if (partners.Count == 0) continue;
                string lv = " &&& Level >= " + Math.Max(0, partners.Min(x => x.Level) - 1);
                if (sec.Key == "ClockValue" || sec.Key == "WRValue") W(sb, "Priority(-10000) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) <= 1" + lv);
                if (sec.Key == "SearchValue") W(sb, "Priority(3000) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) = 0" + lv);
                // 離用得到還很久的名場面：先不急著拿，要丟牌時也比較可以丟
                int early = partners.Min(x => x.Level) - 1;
                if (early >= 1)
                {
                    if (sec.Key == "SearchValue") W(sb, "Priority(-400) [" + cx.Name + "] &&& Level < " + early);
                    if (sec.Key == "WRValue" || sec.Key == "ClockValue") W(sb, "PriorityBonus(300) [" + cx.Name + "] &&& Level < " + early);
                }
                // 連動角色在休息室，而且場上有能把它叫上場的卡（例如集中命中就叫出 0 費角色）：名場面也算用得到
                foreach (var pc in partners)
                    foreach (var cl in Callers(cards).Where(x => x.Card != pc && x.CanCall(pc)))
                    {
                        string viaCall = " &&& CountCards(WaitingRoom,[" + pc.Name + "]) >= 1 &&& CountCards(Stage,[" + cl.Card.Name + "]) >= 1";
                        if (sec.Key == "ClockValue" || sec.Key == "WRValue") W(sb, "Priority(-10000) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) <= 1" + viaCall);
                        if (sec.Key == "SearchValue") W(sb, "Priority(3000) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) = 0" + viaCall);
                    }
                // 同時缺好幾張名場面時，要分得出先後：
                //   3000＝快要用得到（上面那幾條）
                //   4500＝等級已經到了，而且連動角色在手牌或場上（這回合就打得出連動）
                //   6000＝連動角色已經站在場上
                if (sec.Key == "ClockValue" || sec.Key == "WRValue")
                    foreach (var pc in partners)
                        W(sb, "Priority(-14000) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) <= 1 &&& Level >= " + pc.Level
                            + " &&& CountCards(Hand,[" + pc.Name + "]) + CountCards(Stage,[" + pc.Name + "]) >= 1");
                if (sec.Key == "SearchValue")
                    foreach (var pc in partners)
                        W(sb, "Priority(4500) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) = 0 &&& Level >= " + pc.Level
                            + " &&& CountCards(Hand,[" + pc.Name + "]) + CountCards(Stage,[" + pc.Name + "]) >= 1");
                // 連動角色已經在場上（例如被效果提早叫出來）：不管等級，名場面都算現在用得到
                foreach (var pc in partners)
                {
                    string onStage = " &&& CountCards(Stage,[" + pc.Name + "]) >= 1";
                    if (sec.Key == "ClockValue" || sec.Key == "WRValue") W(sb, "Priority(-18000) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) <= 1" + onStage);
                    if (sec.Key == "SearchValue") W(sb, "Priority(6000) [" + cx.Name + "] &&& CountCards(Hand,[" + cx.Name + "]) = 0" + onStage);
                }
            }
            // 陣型需要的卡：快到那個等級時優先找齊、不要丟；缺顏色時優先把該顏色放進等級區／傷害區
            foreach (var f in p.Formations)
            {
                var wanted = f.Slots.Where(x => cards.Any(c => c.Name == x.Card && c.IsChar)).GroupBy(x => x.Card).ToList();
                if (wanted.Count == 0) continue;
                string near = " &&& Level >= " + Math.Max(0, f.MinLevel - 1);
                foreach (var g in wanted)
                {
                    string copies = "CountCards(Hand,[" + g.Key + "]) + CountCards(Stage,[" + g.Key + "])";
                    if (sec.Key == "SearchValue") W(sb, "Priority(4000) [" + g.Key + "]" + near + " &&& " + copies + " < " + g.Count());
                    if (sec.Key == "ClockValue" || sec.Key == "WRValue") W(sb, "Priority(-8000) [" + g.Key + "]" + near + " &&& " + copies + " <= " + g.Count());
                }
                string notKey = string.Join("", wanted.Select(g => " &&& Not [" + g.Key + "]").ToArray());
                foreach (var col in wanted.Select(g => cards.First(c => c.Name == g.Key)).Where(c => c.Level >= 1 && c.ColorName != "").Select(c => c.ColorName).Distinct())
                {
                    if (sec.Key == "LevelValue") W(sb, "Priority(700) IsCharacter &&& ColorIs(" + col + ") &&& Not HaveColor(" + col + ")" + notKey);
                }
            }
            if (p.AttackFirst)
            {
                // 攻擊優先（寫在最後，會蓋過上面的設定）
                const string need = "IsCharacter &&& CanDeployCharacter &&& GetPriority(AttackerValue) > 0 &&& AttackersAvailable(True) < AttackTarget";
                if (sec.Key == "SearchValue")
                {
                    W(sb, "// 等級還很低時，高等級角色先不急著拿");
                    W(sb, "PriorityBonus(-600) IsCharacter &&& Level <= 1 &&& CardLevel >= Level + 2");
                    W(sb, "// 攻擊手不夠時，優先拿現在出得了的角色");
                    W(sb, "Priority(20000) " + need);
                }
                if (sec.Key == "ClockValue")
                {
                    W(sb, "// 現在還用不到的高等級角色，比較適合放傷害區");
                    W(sb, "PriorityBonus(300) IsCharacter &&& Level <= 1 &&& CardLevel >= Level + 2");
                }
                if (sec.Key == "ClockValue" || sec.Key == "WRValue")
                {
                    W(sb, "// 攻擊手不夠時，不要把現在出得了的角色丟掉");
                    W(sb, "Priority(-20000) " + need);
                }
            }
            // 陣型缺顏色：把手上那個顏色的卡放進傷害區（最優先，蓋過上面所有設定；陣型本身的卡除外）
            if (sec.Key == "ClockValue")
                foreach (var cr in ColorNeeds(p, cards))
                    W(sb, "Priority(15000) IsCharacter &&& ColorIs(" + cr[0] + ") &&& Not HaveColor(" + cr[0] + ") &&& Level >= " + cr[1] + cr[2]);
            W(sb, "");
        }

        // ---- 主要階段：ACT 與補攻擊手 ----
        foreach (var when in new[] { "start", "end" })
        {
            W(sb, "Section " + (when == "start" ? "StartMainPhase" : "EndMainPhase"));
            if (when == "start")
            {
                // 手上有連動名場面時，先把連動要付的能量留下來（等級低的先寫，等級高的會蓋過去）
                foreach (var c in cards.Where(x => x.IsChar).OrderBy(x => x.Level))
                {
                    var cc = p.Cfg(c.Name);
                    if (string.IsNullOrEmpty(cc.Combo) || cc.ComboCost <= 0 || !cards.Any(x => x.Name == cc.Combo)) continue;
                    string copies = "CountCards(Hand,[" + c.Name + "]) + CountCards(Stage,[" + c.Name + "])";
                    W(sb, "If(Level >= " + c.Level + " &&& CountCards(Hand,[" + cc.Combo + "]) >= 1 &&& " + copies + " >= 1)");
                    W(sb, "{");
                    W(sb, "    ReserveStock(" + string.Join(" + ", Enumerable.Repeat(copies, cc.ComboCost).ToArray()) + ")");
                    W(sb, "}");
                    if (c.Level >= 1)
                    {
                        // 等級還沒到，但連動角色已經被提早叫上場：一樣要留能量
                        string staged = "CountCards(Stage,[" + c.Name + "])";
                        W(sb, "If(Level < " + c.Level + " &&& CountCards(Hand,[" + cc.Combo + "]) >= 1 &&& " + staged + " >= 1)");
                        W(sb, "{");
                        W(sb, "    ReserveStock(" + string.Join(" + ", Enumerable.Repeat(staged, cc.ComboCost).ToArray()) + ")");
                        W(sb, "}");
                    }
                }
            }
            // 事件卡：遊戲的 AI 不會自己打事件，要在這裡明確寫出來
            foreach (var c in cards.Where(x => x.IsEvent))
            {
                var cc = p.Cfg(c.Name);
                var ev = cc.Ev;
                if (ev == null || ev.Mode == "default" || cc.NeverPlay || ev.When != when) continue;
                bool evCond = ev.Mode == "cond" && Groups(ev).Count > 0;
                W(sb, "If(HandContains(1,[" + c.Name + "]) &&& FreeStock >= " + c.Cost + ")");
                W(sb, "{");
                if (evCond) { W(sb, "    If(" + ExprAny(ev) + ")"); W(sb, "    {"); }
                W(sb, (evCond ? "        " : "    ") + "PlayEvent([" + c.Name + "])");
                if (evCond) W(sb, "    }");
                W(sb, "}");
            }
            if (when == "start")
                foreach (var c in cards)
                    foreach (var kv in p.Cfg(c.Name).Act)
                    {
                        var e = kv.Value;
                        if (e.Mode == "default" || !e.Early || e.EarlyCond.Empty) continue;
                        int cost0 = 0;
                        var info0 = c.Acts.FirstOrDefault(x => x.Num.ToString() == kv.Key);
                        if (info0 != null) { var cm0 = Regex.Match(info0.Text, @"\[\((\d+)\)"); if (cm0.Success) cost0 = int.Parse(cm0.Groups[1].Value); }
                        W(sb, "// 先用再出牌：" + c.Name);
                        W(sb, "If(" + Expr(e.EarlyCond) + ")");
                        W(sb, "{");
                        W(sb, "    If(Not StageContains(1,[" + c.Name + "]) &&& HandContains(1,[" + c.Name + "]) &&& OpenBackstage >= 1)");
                        W(sb, "    {");
                        W(sb, "        PlayBackstage([" + c.Name + "])");
                        W(sb, "    }");
                        W(sb, "    If(StageContains(1,[" + c.Name + "])" + (cost0 > 0 ? " &&& FreeStock >= " + cost0 : "") + ")");
                        W(sb, "    {");
                        W(sb, "        UseAct(" + kv.Key + ", [" + c.Name + "])");
                        W(sb, "    }");
                        W(sb, "}");
                    }
            if (when == "start")
                foreach (var f in p.Formations.OrderBy(x => x.MinLevel))
                {
                    var slots = f.Slots.Where(s => cards.Any(x => x.Name == s.Card && x.IsChar)).ToList();
                    if (slots.Count == 0) continue;
                    bool hasCx = !string.IsNullOrEmpty(f.Climax) && cards.Any(x => x.Name == f.Climax);
                    string members = string.Join(" &&& ", slots.Where(s => !s.Back).Select(s => s.Card).Distinct().Select(n => "Not [" + n + "]").ToArray());
                    string reserve = ComboReserve(p, cards, hasCx ? f.Climax : null);
                    W(sb, "// 陣型：等級 " + f.MinLevel + " 以上" + (hasCx ? "、手上有「" + f.Climax + "」" : ""));
                    W(sb, "If(Level >= " + f.MinLevel + (hasCx ? " &&& CountCards(Hand,[" + f.Climax + "]) >= 1" : "") + ")");
                    W(sb, "{");
                    if (hasCx) W(sb, "    PlanClimax([" + f.Climax + "])");
                    var seen = new Dictionary<string, int>();
                    foreach (var s in slots)
                    {
                        string key = s.Card + (s.Back ? "|b" : "|f");
                        int want; seen.TryGetValue(key, out want); seen[key] = ++want;
                        var cc = p.Cfg(s.Card);
                        int cost = !s.Back && hasCx && cc.Combo == f.Climax ? Math.Max(0, cc.ComboCost) : 0;
                        string zone = s.Back ? "Backstage" : "CenterStage";
                        W(sb, "    If(CountCards(" + zone + ",[" + s.Card + "]) < " + want + " &&& CountCards(Hand,[" + s.Card + "]) >= 1)");
                        W(sb, "    {");
                        W(sb, "        ReserveStock(" + reserve + (cost > 0 ? " + " + cost : "") + ")");
                        if (s.Back) W(sb, "        PlayBackstage([" + s.Card + "])");
                        else W(sb, "        ReplaceAttacker([" + s.Card + "]" + (members != "" ? ", " + members : "") + ")");
                        W(sb, "    }");
                    }
                    W(sb, "    ReserveStock(" + reserve + ")");
                    W(sb, "}");
                }
            // 為了效果才出場的卡：能用就出場並馬上用（主要階段開始、結束前各試一次）
            foreach (var c in cards.Where(x => x.IsChar))
            {
                var pf = PlayForAct(p, c);
                if (pf.Key == null) continue;
                foreach (var ready in ReadyConds(c, pf.Key, pf.Value))
                {
                    W(sb, "If(HandContains(1,[" + c.Name + "]) &&& OpenStageForNormalPlay &&& " + ready + ")");
                    W(sb, "{");
                    W(sb, "    PlayCharacter([" + c.Name + "])");
                    W(sb, "    IfSo");
                    W(sb, "    {");
                    W(sb, "        UseAct(" + pf.Key + ", [" + c.Name + "])");
                    W(sb, "    }");
                    W(sb, "}");
                }
            }
            foreach (var c in cards)
                foreach (var kv in p.Cfg(c.Name).Act)
                {
                    var e = kv.Value;
                    if (e.Mode == "default" || e.When != when) continue;
                    bool hasCond = e.Mode == "cond" && Groups(e).Count > 0;
                    // 起動效果要付能量時，不能動用留給連動的能量
                    int actCost = 0;
                    var actInfo = c.Acts.FirstOrDefault(x => x.Num.ToString() == kv.Key);
                    if (actInfo != null) { var cm = Regex.Match(actInfo.Text, @"\[\((\d+)\)"); if (cm.Success) actCost = int.Parse(cm.Groups[1].Value); }
                    W(sb, "If(StageContains(1,[" + c.Name + "])" + (actCost > 0 ? " &&& FreeStock >= " + actCost : "") + ")");
                    W(sb, "{");
                    if (hasCond) { W(sb, "    If(" + ExprAny(e) + ")"); W(sb, "    {"); }
                    W(sb, (hasCond ? "        " : "    ") + "UseAct(" + kv.Key + ", [" + c.Name + "])");
                    if (hasCond) W(sb, "    }");
                    W(sb, "}");
                }
            if (when == "end")
                foreach (var c in cards.Where(x => x.IsChar))
                {
                    var cc = p.Cfg(c.Name);
                    if (string.IsNullOrEmpty(cc.Combo) || cc.ComboHandMax <= 0 || !cards.Any(x => x.Name == cc.Combo)) continue;
                    // 打出名場面後手牌會少一張，所以出牌前手牌要 <= 上限 + 1
                    W(sb, "// 連動要求手牌 " + cc.ComboHandMax + " 張以下：把出得了的角色放到空位，手牌降到夠少");
                    W(sb, "If(CountCards(Hand,[" + cc.Combo + "]) >= 1 &&& CountCards(CenterStage,[" + c.Name + "]) >= 1 &&& Hand > " + (cc.ComboHandMax + 1) + ")");
                    W(sb, "{");
                    W(sb, "    FillStage(5,IsCharacter)");
                    W(sb, "}");
                }
            if (when == "end" && p.FillAttackers)
                W(sb, "FillAttackers(AttackTarget,IsCharacter &&& GetPriority(AttackerValue) > 0 &&& Not If(InLocation BackStage))");
            W(sb, "");
        }

        // ---- 放傷害區 ----
        W(sb, "Section ShouldClock");
        // 陣型缺顏色、手上又有那個顏色的卡：一定放傷害區（等級 3 也放）
        foreach (var cr in ColorNeeds(p, cards))
        {
            W(sb, "If(Level >= " + cr[1] + " &&& Not HaveColor(" + cr[0] + ") &&& CountCards(Hand,IsCharacter && ColorIs(" + cr[0] + ")" + cr[2].Replace("&&&", "&&") + ") >= 1)");
            W(sb, "{"); W(sb, "    Return(True)"); W(sb, "}");
        }
        if (p.NoClockAtL3) { W(sb, "If(Level >= 3)"); W(sb, "{"); W(sb, "    Return(False)"); W(sb, "}"); }
        if (p.ClockAlwaysBelow > 0) { W(sb, "If(Level < " + p.ClockAlwaysBelow + ")"); W(sb, "{"); W(sb, "    Return(True)"); W(sb, "}"); }
        if (p.ClockIfHandBelow > 0) W(sb, "Return(Hand < " + p.ClockIfHandBelow + ")");
        else W(sb, "Return(False)");
        W(sb, "");

        // 手上每一張都是要留的卡（連動名場面、陣型的卡、補攻擊手要用的角色）時，不放傷害區。
        // 沒有這一段的話，遊戲在攻擊手不夠時會硬放一張，等於隨機丟掉一張重要的卡。
        W(sb, "Section SkipClock");
        W(sb, "Return(CountCards(Hand,GetPriority(ClockValue) > -5000) = 0)");
        W(sb, "");

        // 費用要把場上的角色送走時，哪些可以拿去付（正值＝可以）
        W(sb, "Section CostSacrificeValue");
        W(sb, "Default(-1000) IsCharacter");
        W(sb, "Priority(1000) IsCharacter &&& CardLevel < Level");
        foreach (var c in cards.Where(x => x.IsChar))
        {
            string role = p.Cfg(c.Name).Role;
            if (role == "filler" || role == "utility" || role == "none") W(sb, "Priority(1500) [" + c.Name + "]");
        }
        W(sb, "Priority(2000) IsCharacter &&& Rested");
        W(sb, "Priority(3000) IsCharacter &&& Reversed");
        foreach (var c in cards.Where(x => x.IsChar))
        {
            var cc = p.Cfg(c.Name);
            bool key = cc.Role == "finisher" || cc.Role == "combo" || cc.Role == "engine" || cc.Role == "support" || !string.IsNullOrEmpty(cc.Combo)
                || Defs.Eff(cc, "ProtectValue") >= 4 || p.Formations.Any(f => f.Slots.Any(s => s.Card == c.Name));
            if (key) W(sb, "Priority(-10000) [" + c.Name + "]");
        }
        W(sb, "Priority(-20000) IsCharacter &&& Standing &&& AttackersAvailable(True) < AttackTarget");
        W(sb, "");

        // ---- 三能量再演 ----
        W(sb, "Section ShouldEncore3");
        if (p.Encore3)
        {
            var prot = Defs.Section("ProtectValue");
            int min = p.Encore3MinProtect;
            if (min > 3) { W(sb, "If(GetPriority(ProtectValue) < " + prot.V[min] + ")"); W(sb, "{"); W(sb, "    Return(False)"); W(sb, "}"); }
            W(sb, "Return(Stock >= " + Math.Max(3, p.Encore3Stock) + ")");
        }
        else W(sb, "Return(False)");
        W(sb, "");

        if (!string.IsNullOrWhiteSpace(p.Raw)) { W(sb, "// ---- 進階：整體自訂內容 ----"); W(sb, p.Raw.Trim()); W(sb, ""); }

        // AI 選卡修正補丁的開關：沒有設定目標偏好時，從休息室／牌庫／傷害區選卡會挑「檢索時想拿」最高的
        W(sb, "Section WszhSmartTargets");
        W(sb, "Default(0)");
        W(sb, "");

        // ---- 「為了效果才出場」的卡：用一張自訂的表記錄「現在可以出場」的情況（1＝可以） ----
        foreach (var c in cards.Where(x => x.IsChar))
        {
            var pf = PlayForAct(p, c);
            if (pf.Key == null) continue;
            var e = pf.Value;
            W(sb, "Section " + PlayTable(cards, c));
            W(sb, "Default(0)");
            foreach (var ready in ReadyConds(c, pf.Key, e)) W(sb, "Priority(1) " + ready);
            if (!e.AlsoCond.Empty)
            {
                if (e.AlsoCond.Any) foreach (var it in e.AlsoCond.Items) W(sb, "Priority(1) " + Expr(it));
                else W(sb, "Priority(1) " + Expr(e.AlsoCond));
            }
            W(sb, "");
        }

        // ---- 每張卡 ----
        foreach (var c in cards)
        {
            var cfg = p.Cfg(c.Name);
            W(sb, "Section [" + c.Name + "]");
            if (c.IsChar)
            {
                if (cfg.NeverPlay || cfg.Role == "none") W(sb, "NeverPlayFromHand");
                else if (PlayForAct(p, c).Key != null)
                {
                    // 只有「可以出場」的表回答 1 時才出，而且只放到空位，不蓋掉別的角色
                    W(sb, "NeverPlayFromHand GetPriority(" + PlayTable(cards, c) + ") = 0");
                    if (!cfg.OnlyOpen) W(sb, "OnlyPlayToOpenStage");
                }
                else
                {
                    // 會被放到後列的角色（「放後列」不低於「當攻擊手」）：後列滿了的時候，
                    // 只有後列有「比它不重要」的角色才出，避免把集中之類的後列引擎蓋掉
                    int bv = Defs.Section("BackstageValue").V[Defs.Eff(cfg, "BackstageValue")];
                    int av = Defs.Section("AttackerValue").V[Defs.Eff(cfg, "AttackerValue")];
                    if (av <= 0 || CannotAttack(c))
                        // 不能（不要）攻擊的卡：後列沒有位置可以放就留在手上
                        W(sb, "NeverPlayFromHand OpenBackstage = 0 &&& CountCards(BackStage,GetPriority(BackstageValue) < " + bv + ") = 0");
                    else if (bv >= av && !cfg.OnlyOpen)
                        // 其他後列型的卡：只放到空位（後列滿了就放前列空位），除非後列有比它不重要的角色可以蓋
                        W(sb, "OnlyPlayToOpenStage CountCards(BackStage,GetPriority(BackstageValue) < " + bv + ") = 0");
                }
                if (cfg.OnlyOpen) W(sb, "OnlyPlayToOpenStage");
                if (cfg.Middle) W(sb, "RequiresMiddlePosition");
                // 「放後列」明顯高於「當攻擊手」的角色（集中、應援…）：固定留在後列，不讓 AI 把它拉到前列去攻擊
                int wa = Defs.Eff(cfg, "AttackerValue"), wb = Defs.Eff(cfg, "BackstageValue");
                // 不能攻擊的角色（卡片寫明不能攻擊，或「當攻擊手」設成絕不）也要加：
                // 否則遊戲的 AI 會一直想拿它當攻擊手又發現不行，在主要階段無限重來而卡住
                if (cfg.Emergency || wa == 0 || CannotAttack(c)) W(sb, "EmergencyAttackerOnly");
                // 後列型的卡：已經在後列的留在後列；攻擊手不夠時，手上多的那幾張可以補到前列
                else if (wb >= 4 && wb > wa) W(sb, "EmergencyAttackerOnly(AttackTarget)");
                if (p.MillBudget)
                    foreach (var me in c.Autos.Where(x => x.LookUpTo > 0 && x.Timing.StartsWith("OnAttack", StringComparison.OrdinalIgnoreCase)))
                    {
                        // 能讓牌庫剛好剩一張就照那個張數翻（名場面離開後才洗牌，就會被洗回去）；
                        // 做不到時，牌庫夠濃就翻到第一張角色為止，不夠濃才翻滿
                        bool isCombo = !string.IsNullOrEmpty(cfg.Combo) && cards.Any(x => x.Name == cfg.Combo) && cfg.Combo.IndexOfAny(new[] { '(', ')' }) < 0
                            && (me.Text.IndexOf("\"" + cfg.Combo + "\"", StringComparison.OrdinalIgnoreCase) >= 0 || me.Text.IndexOf("CxCombo", StringComparison.OrdinalIgnoreCase) >= 0);
                        W(sb, "OnAttackMill(" + me.Num + "," + me.LookUpTo + ",IsCharacter,0.25)" + (isCombo ? " If(Climax " + cfg.Combo + ")" : ""));
                    }
                if (cfg.AttackMode == "prefer") W(sb, "PreferSideAttack(0)");
                if (cfg.AttackMode == "force") W(sb, "ForceSideAttack");
                if (cfg.AttackMode == "never") W(sb, "DontSideAttack");
                if (cfg.DontAttackOnlyCopy) W(sb, "DontAttackWithOnlyCopy");
                if (cfg.NeverAttackBombs) W(sb, "NeverAttackBombs");
                if (cfg.Healer) W(sb, "Healer");
                if (!string.IsNullOrEmpty(cfg.Combo) && cards.Any(x => x.Name == cfg.Combo)) W(sb, "OnAttackCombo([" + cfg.Combo + "])");
                if (c.Backup > 0)
                {
                    if (cfg.BackupMode == "never") W(sb, "DontBackupForPower");
                    else
                    {
                        W(sb, "Backup(" + c.Backup + ")");
                        if (cfg.BackupMode == "always") W(sb, "UseBackup");
                    }
                }
            }
            if (c.IsEvent)
            {
                if (cfg.NeverPlay) W(sb, "NeverPlayFromHand");
                if (cfg.EventTargets.Count > 0) W(sb, "EventTarget(" + string.Join(", ", cfg.EventTargets.Select(Defs.Pred).ToArray()) + ")");
            }
            if (c.IsClimax)
            {
                string m = cfg.ClimaxMode;
                if (m == "basic" || m == "both") W(sb, "BasicClimax");
                if (m == "standby") W(sb, "StandbyClimax");
                if (m == "stocksoul") W(sb, "StockSoulClimax");
                if (m == "never") W(sb, "NeverPlayFromHand");
                // 連動等級：用搭配角色中最低的等級
                var partners = cards.Where(x => x.IsChar && p.Cfg(x.Name).Combo == c.Name).ToList();
                if (partners.Count > 0) W(sb, "ComboLevel(" + partners.Min(x => x.Level) + ")");
                if (c.Trigger.Trim() == "Choice" || c.HasShort("ChoiceClimax"))
                {
                    W(sb, "AutoAnswer(1,1)");
                    W(sb, "AutoAnswer(1,0) Hand < " + cfg.ChoiceHandBelow);
                    foreach (var t in cfg.ChoiceTargets) W(sb, "AutoTarget(1, " + Defs.Pred(t) + ")");
                    if (cfg.ChoiceTargets.Count > 0) W(sb, "AutoTargetFallback(1, True)");
                }
            }
            // 「一張拿這種、其餘拿那種」的標記，由 WSZH.AiFix 補丁讀取
            var splitTags = new List<string>();
            foreach (var kv in cfg.Auto) if (kv.Value.Targets.Count > 0 && kv.Value.Targets2.Count > 0) splitTags.Add("WszhSplitAuto" + kv.Key + "x" + kv.Value.Targets.Count);
            foreach (var kv in cfg.Act) if (kv.Value.Targets.Count > 0 && kv.Value.Targets2.Count > 0) splitTags.Add("WszhSplitAct" + kv.Key + "x" + kv.Value.Targets.Count);
            if (splitTags.Count > 0) W(sb, "Tags: " + string.Join(", ", splitTags.ToArray()));
            foreach (var kv in cfg.Auto)
            {
                var e = kv.Value; string n = kv.Key;
                if (e.Mode == "always") W(sb, "AutoIf(" + n + ") True");
                if (e.Mode == "never") W(sb, "DontUseAuto(" + n + ")");
                if (e.Mode == "cond") W(sb, "AutoIfAndOnlyIf(" + n + ") " + ExprAny(e));
                if (e.Mode == "spare") W(sb, "AutoIfAndOnlyIf(" + n + ") Stock > " + ComboReserve(p, cards, null));
                // 有些效果不是「付費用」而是直接問「要不要」，遊戲是用另一種標籤（AutoAnswer）決定，兩種都寫才會生效。
                // 多選一的效果不寫，避免把「是／否」當成選項編號
                var qi = c.Autos.FirstOrDefault(x => x.Num.ToString() == n);
                if (qi != null && qi.YesNo && !qi.Choice)
                {
                    if (e.Mode == "always") W(sb, "AutoAnswer(" + n + ",1)");
                    if (e.Mode == "never") W(sb, "AutoAnswer(" + n + ",0)");
                    if (e.Mode == "cond") W(sb, "AutoAnswer(" + n + ",If(" + ExprAny(e) + "))");
                    if (e.Mode == "spare") W(sb, "AutoAnswer(" + n + ",If(Stock > " + ComboReserve(p, cards, null) + "))");
                }
                bool callA = Callers(cards).Any(x => x.Card == c && x.Cat == "Auto" && x.Num.ToString() == n);
                string openA = callA ? " " + StageOpen : "";
                foreach (var t in e.Targets) W(sb, "AutoTarget(" + n + ", " + Defs.Pred(t) + ")" + openA);
                if (e.Targets.Count > 0) foreach (var t in e.Targets2) W(sb, "AutoTarget(" + n + ", " + Defs.Pred(t) + ")" + openA);
                if (e.Targets.Count > 0 && e.Fallback) W(sb, "AutoTargetFallback(" + n + ", True)" + openA);
            }
            foreach (var cl in Callers(cards).Where(x => x.Card == c))
                W(sb, cl.Cat + "Target(" + cl.Num + ", False) " + StageFull);
            foreach (var ce in c.Autos.Where(x => x.CostStage))
            {
                EffCfg ue;
                bool has = cfg.Auto.TryGetValue(ce.Num.ToString(), out ue);
                string expendable = "IsCharacter && Standing && Not [" + c.Name + "] && GetPriority(CostSacrificeValue) > 0";
                // 使用者沒有另外指定「要不要用」時，才加上這個限制
                if (!has || ue.Mode == "default") W(sb, "AutoIfAndOnlyIf(" + ce.Num + ") CountCards(Stage," + expendable + ") >= 1");
                W(sb, "AutoTarget(" + ce.Num + ", " + expendable + " && If(InLocation Stage))");
                if (!has || ue.Targets.Count == 0) W(sb, "AutoTargetFallback(" + ce.Num + ", Not If(InLocation Stage))");
            }
            foreach (var cl in Callers(cards).Where(x => x.Card == c && x.Cat == "Act"))
            {
                bool any = false;
                foreach (var pc in cards.Where(x => x.IsChar && x != c && cl.CanCall(x)))
                {
                    string pcx = p.Cfg(pc.Name).Combo;
                    if (string.IsNullOrEmpty(pcx) || !cards.Any(x => x.Name == pcx)) continue;
                    W(sb, "ActTarget(" + cl.Num + ", [" + pc.Name + "] && If(InLocation WaitingRoom)) CountCards(Hand,[" + pcx + "]) >= 1 &&& CountCards(Stage,[" + pc.Name + "]) = 0 &&& " + StageOpen);
                    any = true;
                }
                EffCfg ue;
                bool userHasTargets = cfg.Act.TryGetValue(cl.Num.ToString(), out ue) && ue.Targets.Count > 0;
                if (any && !userHasTargets) W(sb, "ActTargetFallback(" + cl.Num + ", True) " + StageOpen);
            }
            foreach (var kv in cfg.Act)
            {
                var e = kv.Value; string n = kv.Key;
                bool callB = Callers(cards).Any(x => x.Card == c && x.Cat == "Act" && x.Num.ToString() == n);
                string openB = callB ? " " + StageOpen : "";
                foreach (var t in e.Targets) W(sb, "ActTarget(" + n + ", " + Defs.Pred(t) + ")" + openB);
                if (e.Targets.Count > 0) foreach (var t in e.Targets2) W(sb, "ActTarget(" + n + ", " + Defs.Pred(t) + ")" + openB);
                if (e.Targets.Count > 0 && e.Fallback) W(sb, "ActTargetFallback(" + n + ", True)" + openB);
            }
            if (!string.IsNullOrWhiteSpace(cfg.Raw)) foreach (var l in cfg.Raw.Replace("\r", "").Split('\n')) if (l.Trim() != "") W(sb, l.Trim());
            W(sb, "");
        }
        return sb.ToString();
    }

    // 這張卡有沒有設定「為了某個起動效果才出場」；有的話回傳 { 效果編號, 設定 }
    static KeyValuePair<string, EffCfg> PlayForAct(AiProject p, CardInfo c)
    {
        foreach (var kv in p.Cfg(c.Name).Act) if (kv.Value.PlayFor) return kv;
        return new KeyValuePair<string, EffCfg>(null, null);
    }
    static string PlayTable(List<CardInfo> cards, CardInfo c) { return "WszhPlay" + (cards.IndexOf(c) + 1); }
    // 「現在能用這個起動效果」的條件；「任一成立」拆成多條（每條都帶上能量檢查）
    static List<string> ReadyConds(CardInfo c, string num, EffCfg e)
    {
        int cost = 0;
        var info = c.Acts.FirstOrDefault(x => x.Num.ToString() == num);
        if (info != null) { var cm = Regex.Match(info.Text, @"\[\((\d+)\)"); if (cm.Success) cost = int.Parse(cm.Groups[1].Value); }
        string stock = "FreeStock >= " + cost;
        var res = new List<string>();
        var groups = Groups(e);
        if (e.Mode != "cond" || groups.Count == 0) res.Add(stock);
        else
            foreach (var g in groups)
            {
                if (g.Any) foreach (var it in g.Items) res.Add(stock + " &&& " + Expr(it));
                else res.Add(stock + " &&& " + Expr(g));
            }
        return res;
    }

    // 舞台還有空位／舞台全滿（前列倒置的位置也算空位）
    const string StageOpen = "OpenCenterStage + OpenBackstage >= 1";
    const string StageFull = "OpenCenterStage + OpenBackstage = 0";

    // 會把休息室的角色放上舞台的效果（例如集中命中時叫出 0 費角色）
    public class Caller
    {
        public CardInfo Card; public string Cat; public int Num, MaxCost = 99, MaxLevel = 99;
        public bool CanCall(CardInfo target) { return target.IsChar && target.Cost <= MaxCost && target.Level <= MaxLevel; }
    }
    public static List<Caller> Callers(List<CardInfo> cards)
    {
        var res = new List<Caller>();
        foreach (var c in cards.Where(x => x.IsChar))
            foreach (var e in c.Effects.Where(x => (x.Cat == "Act" || x.Cat == "Auto") && x.Text != ""))
            {
                string t = e.Text;
                int wr = t.IndexOf("in your waiting room", StringComparison.OrdinalIgnoreCase);
                if (wr < 0) continue;
                string after = t.Substring(wr);
                if (after.IndexOf("position of your stage", StringComparison.OrdinalIgnoreCase) < 0
                    && after.IndexOf("put it on your stage", StringComparison.OrdinalIgnoreCase) < 0
                    && after.IndexOf("place it on your stage", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var cl = new Caller { Card = c, Cat = e.Cat, Num = e.Num };
                string before = t.Substring(0, wr);
                var m = Regex.Match(before, @"cost (\d+) or lower", RegexOptions.IgnoreCase);
                if (m.Success) cl.MaxCost = int.Parse(m.Groups[1].Value);
                m = Regex.Match(before, @"level (\d+) or lower", RegexOptions.IgnoreCase);
                if (m.Success) cl.MaxLevel = int.Parse(m.Groups[1].Value);
                res.Add(cl);
            }
        return res;
    }

    public static bool CannotAttack(CardInfo c)
    {
        return c.IsChar && c.Texts.Any(t => t.Trim().Equals("Cont: This card cannot attack.", StringComparison.OrdinalIgnoreCase));
    }

    // 陣型需要的顏色：{ 顏色, 從哪個等級開始湊, 「不是陣型的卡」條件 }
    static List<string[]> ColorNeeds(AiProject p, List<CardInfo> cards)
    {
        var res = new List<string[]>();
        foreach (var f in p.Formations)
        {
            var keys = f.Slots.Select(x => x.Card).Distinct().Select(n => cards.FirstOrDefault(c => c.Name == n && c.IsChar)).Where(c => c != null).ToList();
            string notKey = string.Join("", keys.Select(c => " &&& Not [" + c.Name + "]").ToArray());
            foreach (var col in keys.Where(c => c.Level >= 1 && c.ColorName != "").Select(c => c.ColorName).Distinct())
                if (!res.Any(r => r[0] == col)) res.Add(new[] { col, Math.Max(0, f.MinLevel - 1).ToString(), notKey });
        }
        return res;
    }

    // 目前在前列的連動角色總共要付多少能量（climax＝只算這張名場面的連動；null＝全部）
    static string ComboReserve(AiProject p, List<CardInfo> cards, string climax)
    {
        var parts = new List<string>();
        foreach (var c in cards.Where(x => x.IsChar))
        {
            var cc = p.Cfg(c.Name);
            if (string.IsNullOrEmpty(cc.Combo) || cc.ComboCost <= 0 || !cards.Any(x => x.Name == cc.Combo)) continue;
            if (climax != null && cc.Combo != climax) continue;
            for (int i = 0; i < cc.ComboCost; i++) parts.Add("CountCards(CenterStage,[" + c.Name + "])");
        }
        return parts.Count == 0 ? "0" : string.Join(" + ", parts.ToArray());
    }

    // 跟遊戲一樣的基本檢查：括號是否成對、[卡名] 是否都在牌組裡
    public static List<string> Check(AiProject p, string script)
    {
        var errs = new List<string>();
        var names = new HashSet<string>(UniqueCards(p).Select(c => c.Name.Trim().Replace('《', '«').Replace('》', '»').ToLowerInvariant()));
        var lines = script.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string t = Regex.Split(lines[i], "//")[0].Trim();
            int depth = 0, open = -1;
            for (int j = 0; j < t.Length; j++)
            {
                if (t[j] == '(') depth++;
                if (t[j] == ')') depth--;
                if (t[j] == '[') open = j;
                if (t[j] == ']' && open >= 0)
                {
                    string n = t.Substring(open + 1, j - open - 1).Trim().Replace('《', '«').Replace('》', '»').ToLowerInvariant();
                    if (!names.Contains(n)) errs.Add("第 " + (i + 1) + " 行：牌組裡沒有「" + n + "」");
                }
            }
            if (depth != 0) errs.Add("第 " + (i + 1) + " 行：括號沒有成對");
        }
        foreach (var c in UniqueCards(p)) if (c.Name.Contains("//")) errs.Add("卡名含有「//」，遊戲會把它當成註解：" + c.Name);
        return errs;
    }
}

// ======================================================================
// 新牌組的預設設定（盡量讓什麼都不調也能打）
// ======================================================================
static class Defaults
{
    // 從連動效果的文字找出要付幾點能量，例如「CxCombo [(1)] When "OVER ZONE" …」
    // 連動效果有沒有「手牌 N 張以下」的條件
    public static int DetectComboHandMax(CardInfo c, string climax)
    {
        if (string.IsNullOrEmpty(climax)) return 0;
        foreach (var t in c.Texts)
        {
            if (t.IndexOf("\"" + climax + "\"", StringComparison.OrdinalIgnoreCase) < 0 && t.IndexOf("CxCombo", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var m = Regex.Match(t, @"(\d+) or (less|fewer) cards in your hand", RegexOptions.IgnoreCase);
            if (m.Success) return int.Parse(m.Groups[1].Value);
        }
        return 0;
    }

    public static int DetectComboCost(CardInfo c, string climax)
    {
        if (string.IsNullOrEmpty(climax)) return 0;
        foreach (var t in c.Texts)
        {
            if (t.IndexOf("\"" + climax + "\"", StringComparison.OrdinalIgnoreCase) < 0 && t.IndexOf("CxCombo", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var m = Regex.Match(t, @"\[\((\d+)\)");
            if (m.Success) return int.Parse(m.Groups[1].Value);
        }
        return 0;
    }

    public static void Apply(AiProject p, bool onlyNew)
    {
        var cards = Gen.UniqueCards(p);
        // 顏色：依張數排序
        if (!onlyNew || p.Colors.Count == 0)
        {
            p.Colors = p.Deck.Select(CardDb.Get).Where(c => c != null && c.ColorName != "")
                .GroupBy(c => c.ColorName).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
        }
        var climaxNames = cards.Where(c => c.IsClimax).Select(c => c.Name).ToList();
        foreach (var c in cards.OrderBy(x => x.IsClimax ? 1 : 0))
        {
            if (onlyNew && p.Cards.ContainsKey(c.Name)) continue;
            var cfg = p.Cfg(c.Name);
            if (c.IsChar && c.Level == 0) cfg.Keep = 4;
            if (c.IsChar)
            {
                // 連動：要是「這張卡自己的」自動／起動效果提到名場面才算。
                // 卡片資料的 Combo 欄位也會列出只是「給別人能力」的相關卡（例如回想區支援），那種不算連動攻擊手
                // 在休息室／回想區／手牌才發動的連動（例如「這張卡在休息室時，名場面打出就放進回想區」）也不算
                var own = c.Effects.Where(e => (e.Cat == "Auto" || e.Cat == "Act") && e.Text != "").Select(e => e.Text)
                    .Where(t => t.IndexOf("this card is in your waiting room", StringComparison.OrdinalIgnoreCase) < 0
                             && t.IndexOf("this card is in your memory", StringComparison.OrdinalIgnoreCase) < 0
                             && t.IndexOf("this card is in your hand", StringComparison.OrdinalIgnoreCase) < 0).ToList();
                string combo = climaxNames.FirstOrDefault(x => own.Any(t => t.IndexOf("\"" + x + "\"", StringComparison.OrdinalIgnoreCase) >= 0));
                if (combo == null && own.Any(t => t.IndexOf("CxCombo", StringComparison.OrdinalIgnoreCase) >= 0))
                    combo = c.Combos.FirstOrDefault(x => climaxNames.Contains(x));
                cfg.Combo = combo ?? "";
                cfg.Healer = c.Tags.Contains("Healer") || c.HasShort("Healer") || c.HasShort("StockHealer");
                bool brainstorm = c.HasShort("Act1") && c.Shorthands.Any(s => s.IndexOf("Brainstorm", StringComparison.OrdinalIgnoreCase) >= 0)
                    || c.Acts.Any(a => a.Text.IndexOf("BRAINSTORM", StringComparison.OrdinalIgnoreCase) >= 0);
                if (brainstorm)
                {
                    cfg.Combo = "";   // 集中角色的卡片資料常列出名場面，但它不是連動攻擊手
                    cfg.Role = "engine";
                    foreach (var a in c.Acts.Where(a => a.Text.IndexOf("BRAINSTORM", StringComparison.OrdinalIgnoreCase) >= 0 || (a.Source ?? "").IndexOf("Brainstorm", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        // 集中要付能量：預設只在手牌偏少、或牌庫名場面比例夠高時才用，避免每回合都把能量花掉
                        var be = new EffCfg { Mode = "cond", When = "end" };
                        be.Cond.Any = true;
                        be.Cond.Items.Add(new Clause { Key = "Hand", Op = "<=", Val = 4 });
                        be.Cond.Items.Add(new Clause { Key = "Ratio", Op = ">=", Val = 20 });
                        cfg.Act[a.Num.ToString()] = be;
                    }
                }
                // 低等級的應援（後列加攻）角色：偏好放後列
                bool assist = c.Level <= 1 && c.Shorthands.Any(s => s.IndexOf("Assist", StringComparison.OrdinalIgnoreCase) >= 0);
                if (assist && !brainstorm) cfg.Role = "support";
                if (Gen.CannotAttack(c)) { cfg.Role = "engine"; cfg.Combo = ""; }   // 不能攻擊的角色只會放後列
                if (cfg.Role == "" && cfg.Combo != "") cfg.Role = c.Level >= 3 ? "finisher" : "combo";
                if (cfg.Role == "" && c.Backup > 0) cfg.Role = "backup";
            }
            if (c.IsClimax)
            {
                bool hasPartner = cards.Any(x => x.IsChar && p.Cfg(x.Name).Combo == c.Name);
                bool standby = c.Tags.Any(t => t.IndexOf("Standby", StringComparison.OrdinalIgnoreCase) >= 0) || c.Trigger.Trim() == "Standby" && c.HasShort("StandbyClimax");
                bool stockSoul = c.Shorthands.Any(s => s.IndexOf("StockSoul", StringComparison.OrdinalIgnoreCase) >= 0);
                // 「全體 +1000／+1 魂」或「全體 +2 魂」這類名場面，沒有連動角色時也值得打
                bool basic = c.Tags.Any(t => t.IndexOf("1K1", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("2Soul", StringComparison.OrdinalIgnoreCase) >= 0)
                    || c.Texts.Any(t => t.IndexOf("All your characters get +", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("All of your characters get +", StringComparison.OrdinalIgnoreCase) >= 0);
                if (standby) cfg.ClimaxMode = "standby";
                else if (stockSoul) cfg.ClimaxMode = "stocksoul";
                else if (hasPartner) cfg.ClimaxMode = basic ? "both" : "combo";
                else cfg.ClimaxMode = "basic";
                if (!hasPartner) cfg.W["ClockValue"] = 4;   // 沒有連動的名場面放傷害區比較不可惜（升級時則不要選）
                cfg.W["LevelValue"] = 1;
            }
        }
        // 事件卡：不是反擊的，預設在主要階段一開始能打就打（遊戲的 AI 不會自己打事件）
        foreach (var c in cards.Where(x => x.IsEvent))
        {
            var cfg = p.Cfg(c.Name);
            if (cfg.Ev == null) cfg.Ev = new EffCfg { Mode = c.Counter ? "default" : "always", When = "start" };
        }
        foreach (var c in cards.Where(x => x.IsChar))
        {
            var cfg = p.Cfg(c.Name);
            if (cfg.ComboCost < 0) cfg.ComboCost = DetectComboCost(c, cfg.Combo);
            if (cfg.ComboHandMax < 0) cfg.ComboHandMax = DetectComboHandMax(c, cfg.Combo);
        }
    }

    // 依卡片內容猜一個定位（給「自動判斷」按鈕用；不會改動其他設定）
    public static string GuessRole(AiProject p, CardInfo c)
    {
        if (!c.IsChar) return "";
        var cfg = p.Cfg(c.Name);
        bool brainstorm = c.Shorthands.Any(s => s.IndexOf("Brainstorm", StringComparison.OrdinalIgnoreCase) >= 0)
            || c.Acts.Any(a => a.Text.IndexOf("BRAINSTORM", StringComparison.OrdinalIgnoreCase) >= 0);
        if (brainstorm) return "engine";
        if (c.Level <= 1 && c.Shorthands.Any(s => s.IndexOf("Assist", StringComparison.OrdinalIgnoreCase) >= 0)) return "support";
        if (!string.IsNullOrEmpty(cfg.Combo)) return c.Level >= 3 ? "finisher" : "combo";
        if (c.Backup > 0) return "backup";
        return "";
    }
}
