// WS 中文化套件：AI 選卡修正（遊戲執行時載入的小程式庫，WSZH.AiFix.dll）
// 遊戲的 V7 AI 選「多張」目標時，只會照偏好順序一張一張挑，沒辦法表達
// 「一張拿這種、其餘拿那種」（例如檢索 2 張：1 張上手、1 張放能量區）。
// AiFixPatch.cs 會在 AIVersion7.ChooseEffectTargets 的開頭和結尾各插入一個呼叫，
// 這裡在 AI 選完之後依照卡片區段裡的標記重新分配。
// 全部用反射存取遊戲的型別，不直接參考 Assembly-CSharp，遊戲改版時比較不容易壞；
// 任何地方出錯都直接放棄修正，保留 AI 原本的選擇。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public static class WszhAiFix
{
    class State { public object Ai, Settings, Owner; public List<object> Options = new List<object>(); public int Amount; }
    [ThreadStatic] static State cur;
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Begin(object ai, object options, object settings, int amount, object owner)
    {
        cur = null;
        try
        {
            var s = new State { Ai = ai, Settings = settings, Owner = owner, Amount = amount };
            var e = options as IEnumerable;
            if (e == null) return;
            foreach (var o in e) s.Options.Add(o);
            cur = s;
        }
        catch { cur = null; }
    }

    public static void End(object ai)
    {
        var s = cur; cur = null;
        if (s == null || !ReferenceEquals(s.Ai, ai)) return;
        try { Fix(s); } catch { }
    }

    static object Get(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, All | BindingFlags.DeclaredOnly);
            if (f != null) return f.GetValue(o);
            var p = t.GetProperty(name, All | BindingFlags.DeclaredOnly);
            if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(o, null);
        }
        return null;
    }

    static MethodInfo FindMethod(object o, string name, int paramCount, Type firstParam)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
            foreach (var m in t.GetMethods(All | BindingFlags.DeclaredOnly))
            {
                if (m.Name != name) continue;
                var ps = m.GetParameters();
                if (ps.Length == paramCount && (firstParam == null || ps[0].ParameterType == firstParam)) return m;
            }
        return null;
    }

    // 沒有設定目標偏好、遊戲也沒有標示用途的選擇，遊戲的 AI 幾乎是隨便挑。
    // 如果是從自己的休息室／牌庫／傷害區選卡（通常是要拿回來用），改成挑「檢索／回收時想拿」分數最高的。
    // 只對有 Section WszhSmartTargets 的腳本生效（AI 對手編輯器產生的腳本才有）。
    static void SmartPick(State s, string[] lines, string ownerName, string cat, int num)
    {
        bool on = false;
        foreach (var raw in lines) if (string.Equals((raw ?? "").Trim(), "Section WszhSmartTargets", StringComparison.OrdinalIgnoreCase)) { on = true; break; }
        if (!on) return;
        if (Convert.ToString(Get(s.Settings, "context")) != "Undefined") return;
        object player = Get(s.Ai, "player");
        if (player == null || !ReferenceEquals(Get(s.Owner, "Networkowner"), player)) return;
        object sel = Get(player, "selectedCards");
        if (sel == null) return;
        var selected = new List<object>();
        foreach (var o in (IEnumerable)sel) selected.Add(o);
        if (selected.Count == 0 || selected.Count >= s.Options.Count) return;

        object zone = Get(s.Options[0], "location");
        if (zone == null) return;
        if (!ReferenceEquals(zone, Get(player, "waitingRoom")) && !ReferenceEquals(zone, Get(player, "deck")) && !ReferenceEquals(zone, Get(player, "clock"))) return;
        foreach (var o in s.Options) if (!ReferenceEquals(Get(o, "location"), zone)) return;

        var gp = FindMethod(s.Ai, "GetPriority", 2, typeof(string));
        if (gp == null) return;
        var value = new Dictionary<object, int>();
        foreach (var o in s.Options) value[o] = Convert.ToInt32(gp.Invoke(s.Ai, new object[] { "SearchValue", o }));
        var ideal = new List<object>(s.Options);
        ideal.Sort((x, y) => value[y] != value[x] ? value[y].CompareTo(value[x]) : s.Options.IndexOf(x).CompareTo(s.Options.IndexOf(y)));
        ideal = ideal.GetRange(0, selected.Count);
        long a = 0, b = 0;
        foreach (var o in ideal) a += value[o];
        foreach (var o in selected) { int v; b += value.TryGetValue(o, out v) ? v : 0; }
        if (a <= b) return;   // AI 原本選的已經一樣好，不動

        var clear = FindMethod(sel, "Clear", 0, null);
        var add = FindMethod(sel, "Add", 1, null);
        if (clear == null || add == null) return;
        clear.Invoke(sel, null);
        foreach (var o in ideal) add.Invoke(sel, new[] { o });
        try
        {
            var log = FindMethod(s.Ai, "AILog", 1, typeof(string));
            if (log != null)
            {
                var names = new List<string>();
                foreach (var o in ideal) names.Add(Convert.ToString(Get(o, "name")));
                log.Invoke(s.Ai, new object[] { "[WSZH] " + ownerName + " " + cat + " #" + num + " 改挑最想要的卡：" + string.Join(" / ", names.ToArray()) });
            }
        }
        catch { }
    }

    static void Fix(State s)
    {
        if (s.Owner == null || s.Settings == null || s.Options.Count < 2) return;
        string cat = Convert.ToString(Get(s.Settings, "cardEffectCategory"));
        if (cat != "Auto" && cat != "Act") return;
        int num = Convert.ToInt32(Get(s.Settings, "effectNumber"));
        var lines = Get(s.Ai, "AIFileLines") as string[];
        string ownerName = Convert.ToString(Get(s.Owner, "name"));
        if (lines == null || string.IsNullOrEmpty(ownerName)) return;

        // 找出這張卡的區段，讀出目標偏好與標記
        string title = "[" + ownerName + "]";
        string label = cat + "Target(", marker = "WszhSplit" + cat + num + "x";
        var preds = new List<string>(); int k = -1; bool inSection = false;
        foreach (var raw in lines)
        {
            string t = (raw ?? "").Trim();
            if (t.StartsWith("Section "))
            {
                if (inSection) break;
                inSection = string.Equals(t.Substring(8).Trim(), title, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inSection) continue;
            if (t.StartsWith("Tags:", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var tag in t.Substring(5).Split(','))
                {
                    string g = tag.Trim();
                    int v;
                    if (g.StartsWith(marker, StringComparison.OrdinalIgnoreCase) && int.TryParse(g.Substring(marker.Length), out v)) k = v;
                }
            }
            else if (t.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                // AutoTarget(N, 條件)：找到對應的右括號
                int depth = 0, close = -1;
                for (int i = label.Length - 1; i < t.Length; i++)
                {
                    if (t[i] == '(') depth++;
                    if (t[i] == ')') { depth--; if (depth == 0) { close = i; break; } }
                }
                if (close < 0) continue;
                string inner = t.Substring(label.Length, close - label.Length);
                int comma = inner.IndexOf(',');
                int n;
                if (comma < 0 || !int.TryParse(inner.Substring(0, comma).Trim(), out n) || n != num) continue;
                preds.Add(inner.Substring(comma + 1).Trim());
            }
        }
        if (k <= 0 || k >= preds.Count)           // 需要同時有「只拿一張」和「其餘」兩組偏好
        {
            if (preds.Count == 0) SmartPick(s, lines, ownerName, cat, num);
            return;
        }

        object player = Get(s.Ai, "player");
        object sel = player != null ? Get(player, "selectedCards") : null;
        if (sel == null) return;
        var selected = new List<object>();
        foreach (var o in (IEnumerable)sel) selected.Add(o);
        if (selected.Count < 2) return;           // 只選一張時，照原本的偏好順序就會選到第一組

        var test = FindMethod(s.Ai, "TestCondition", 2, typeof(string));
        if (test == null) return;
        // 兩組偏好分開排名：同一張卡可能兩組都符合（例如第一組最後一條是「任何角色」）
        Func<object, int, int, int> rank = (card, from, to) =>
        {
            for (int i = from; i < to; i++)
                if ((bool)test.Invoke(s.Ai, new object[] { preds[i], card })) return i;
            return int.MaxValue;
        };

        // 第一組（例如上手的卡）只拿最優先的一張
        object first = null; int best = int.MaxValue;
        foreach (var c in s.Options) { int r = rank(c, 0, k); if (r < best) { best = r; first = c; } }
        if (first == null) return;
        // 其餘的從第二組（例如放能量區的卡）依順序挑
        var result = new List<object> { first };
        var others = new List<KeyValuePair<int, object>>();
        foreach (var c in s.Options)
        {
            if (ReferenceEquals(c, first)) continue;
            int r = rank(c, k, preds.Count);
            if (r != int.MaxValue) others.Add(new KeyValuePair<int, object>(r, c));
        }
        for (int want = k; want < preds.Count && result.Count < selected.Count; want++)
            foreach (var kv in others)
            {
                if (result.Count >= selected.Count) break;
                if (kv.Key == want) result.Add(kv.Value);
            }
        // 第二組不夠時，用 AI 原本選的補滿
        foreach (var c in selected) { if (result.Count >= selected.Count) break; if (!result.Contains(c)) result.Add(c); }

        bool same = result.Count == selected.Count;
        if (same) foreach (var c in result) if (!selected.Contains(c)) { same = false; break; }
        if (same) return;

        var clear = FindMethod(sel, "Clear", 0, null);
        var add = FindMethod(sel, "Add", 1, null);
        if (clear == null || add == null) return;
        clear.Invoke(sel, null);
        foreach (var c in result) add.Invoke(sel, new[] { c });
        try
        {
            var log = FindMethod(s.Ai, "AILog", 1, typeof(string));
            if (log != null)
            {
                var names = new List<string>();
                foreach (var c in result) names.Add(Convert.ToString(Get(c, "name")));
                log.Invoke(s.Ai, new object[] { "[WSZH] " + ownerName + " " + cat + " #" + num + " 重新分配目標：" + string.Join(" / ", names.ToArray()) });
            }
        }
        catch { }
    }
}
