// 對戰重播修正：
// 打出名場面（PlayClimax）後遊戲會自動結束名場面階段（Player.ChangePhaseButtonClickedImmediate），
// 對戰時這一步被記錄在「打出名場面」的動作裡面（parent = 那個動作）。
// 重播時模擬器會擋掉這個自動呼叫，改由重播控制器另外送出「結束階段」，但控制器要等玩家空閒才送，
// 那時打出名場面的動作已經結束，順序和紀錄不同，重播就停在「第 N 步的操作和紀錄不一致」。
// 這裡在 ChangePhaseButtonClickedImmediate 開頭檢查：重播中、紀錄的下一步正好是這個玩家
// 「在某個動作裡面」的結束階段，就讓這次自動呼叫照常執行（當成重播指令），順序就和紀錄一致。
// 由 ReiPatcher 補丁在 Player.ChangePhaseButtonClickedImmediate() 與 ChangePhaseButtonClicked() 開頭插入：
//   if (WszhReplayFix.Intercept(this)) return;
// （打出名場面後呼叫的是 ChangePhaseButtonClicked，它在重播時一開頭就 return，所以兩個都要掛）
using System;
using System.Reflection;

public static class WszhReplayFix
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Type session;
    static PropertyInfo pRunning, pApplying, pCurrent, pNext, pNextPlayer;
    static FieldInfo fKind, fSource, fParent;
    static MethodInfo mEnd;
    static bool inside;

    static bool Init(object player)
    {
        if (session != null) return true;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType("GameLogReplaySession");
            if (t == null) continue;
            pRunning = t.GetProperty("Running", Any); pApplying = t.GetProperty("ApplyingCommand", Any);
            pCurrent = t.GetProperty("Current", Any); pNext = t.GetProperty("Next", Any); pNextPlayer = t.GetProperty("NextPlayer", Any);
            var it = a.GetType("GameLogReplayInput");
            if (it != null) { fKind = it.GetField("kind", Any); fSource = it.GetField("source", Any); fParent = it.GetField("parent", Any); }
            mEnd = player.GetType().GetMethod("ChangePhaseButtonClickedImmediate", Any, null, Type.EmptyTypes, null);
            if (pRunning != null && pApplying != null && pCurrent != null && pNext != null && pNextPlayer != null && fKind != null && fSource != null && fParent != null && mEnd != null)
                session = t;
            break;
        }
        return session != null;
    }

    public static bool Intercept(object player)
    {
        try
        {
            // 註：重播在執行「打出名場面」這類動作時，ApplyingCommand 本來就是 true，所以不能拿它判斷要不要處理；
            // 用自己的旗標避免下面呼叫 ChangePhaseButtonClickedImmediate 時又進來一次
            if (inside || player == null || !Init(player)) return false;
            if (!(bool)pRunning.GetValue(null, null)) return false;
            var cur = pCurrent.GetValue(null, null);
            var next = cur == null ? null : pNext.GetValue(cur, null);
            if (next == null || (string)fKind.GetValue(next) != "command") return false;
            string src = (string)fSource.GetValue(next);
            if ((src != "EndPhase" && src != "EndEncore") || (long)fParent.GetValue(next) == 0L) return false;
            if (!ReferenceEquals(pNextPlayer.GetValue(cur, null), player)) return false;
            bool was = (bool)pApplying.GetValue(null, null);
            inside = true;
            pApplying.SetValue(null, true, null);
            try { mEnd.Invoke(player, null); }        // ApplyingCommand 為 true：原本的程式會照常執行並記錄（和紀錄比對）
            finally { pApplying.SetValue(null, was, null); inside = false; }
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log("[WSZH] replay fix: " + ex.Message);
            return false;
        }
    }
}
