// 效能紀錄（找「用了中文化之後變卡」的原因用）：
// 統計中文化各個掛鉤花的時間，以及遊戲每秒有幾幀超過 50 毫秒（卡頓），每 10 秒寫一行到 Player.log（[WSZH] perf …）。
// 只做加總，不影響遊戲；平常不寫紀錄，要查效能時把 PlayerPrefs 的 WSZH_Perf 設成 1。
// 2026-10-27 的量測結果：
//   ・卡頓主因：TMP 找中文字時先去「LiberationSans SDF - Fallback」動態字型白找，一段卡片說明排版 10ms → 改字型順序後 0.3ms（Lang.cs FixFontFallbacks）
//   ・次要：卡片翻譯用 Dictionary 常駐記憶體讓 GC 變慢（改 CompactMap）、模擬器不限幀率約 1000 FPS（預設改 60）
using System.Diagnostics;
using UnityEngine;

public static class WszhPerf
{
    static readonly string[] Names = { "卡片翻譯", "對齊", "日版卡圖", "設定頁/重播" };
    static readonly long[] ticks = new long[4];
    static readonly int[] calls = new int[4];
    static float nextReport = 10f, worstFrame;
    static int frames, slowFrames, enabled = -1, lastGc;

    public static void Add(int slot, long start)
    {
        ticks[slot] += Stopwatch.GetTimestamp() - start;
        calls[slot]++;
    }

    // 每幀由 WszhLang.Update 呼叫
    static int fontRebuilt; static bool hooked;
    public static void Frame()
    {
        if (!hooked) { hooked = true; Font.textureRebuilt += f => fontRebuilt++; }
        float dt = Time.unscaledDeltaTime;
        frames++;
        if (dt > 0.05f) slowFrames++;
        if (dt > worstFrame) worstFrame = dt;
        if (Time.realtimeSinceStartup < nextReport) return;
        nextReport = Time.realtimeSinceStartup + 10f;
        // 平常不寫紀錄；要查效能時在模擬器資料夾放一個 wszh_perf.txt（或 PlayerPrefs 設 WSZH_Perf = 1）
        if (enabled < 0) enabled = PlayerPrefs.GetInt("WSZH_Perf", 0) == 1
            || System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "wszh_perf.txt")) ? 1 : 0;
        if (enabled == 0) { System.Array.Clear(ticks, 0, ticks.Length); System.Array.Clear(calls, 0, calls.Length); frames = 0; slowFrames = 0; worstFrame = 0; return; }
        var sb = new System.Text.StringBuilder("[WSZH] perf 10s: scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            + " frames=" + frames + " slow(>50ms)=" + slowFrames + " worst=" + (int)(worstFrame * 1000) + "ms"
            + " GC=" + (System.GC.CollectionCount(0) - lastGc) + " heap=" + (System.GC.GetTotalMemory(false) / 1048576) + "MB");
        lastGc = System.GC.CollectionCount(0);
        sb.Append(" fontRebuilt=" + fontRebuilt); fontRebuilt = 0;
        for (int i = 0; i < ticks.Length; i++)
        {
            if (calls[i] == 0) continue;
            sb.Append(" | " + Names[i] + " " + calls[i] + " 次 " + (ticks[i] * 1000.0 / Stopwatch.Frequency).ToString("0.0") + "ms");
            ticks[i] = 0; calls[i] = 0;
        }
        UnityEngine.Debug.Log(sb.ToString());
        frames = 0; slowFrames = 0; worstFrame = 0;
    }
}
