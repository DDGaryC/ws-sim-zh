// ReiPatcher 補丁：讓主畫面播放背景音樂。
// 原本 MainMenuNew.Start() 一開始會呼叫 GameAudioManager.StopBgm() 把音樂關掉；
// 這裡在那之後補上 PlayBgm("main_menu", true)，曲目由 Audio\audio_catalog.json 的 "main_menu" 決定
// （音樂設定工具可以選）。沒有設定 main_menu 時遊戲找不到曲目就不會播放，行為與原本相同。
// 找不到要修改的程式碼（例如模擬器改版）時直接略過，不影響遊戲啟動。
// 編譯：.NET 3.5 的 csc（ReiPatcher 執行在 .NET 2.0/3.5 上）
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReiPatcher;
using ReiPatcher.Patch;

public class WszhMainMenuMusicPatch : PatchBase
{
    const string Tag = "WSZH.MainMenuMusic";
    public override string Name { get { return "WS 中文化套件：主畫面音樂"; } }
    public override string Version { get { return "1.0"; } }

    public override void PrePatch()
    {
        RPConfig.RequestAssembly("Assembly-CSharp.dll");
    }

    public override bool CanPatch(PatcherArguments args)
    {
        if (args.Assembly.Name.Name != "Assembly-CSharp") return false;
        return !GetPatchedAttributes(args.Assembly).Any(a => a.Info == Tag);
    }

    public override void Patch(PatcherArguments args)
    {
        try
        {
            ModuleDefinition mod = args.Assembly.MainModule;
            TypeDefinition menu = mod.GetType("MainMenuNew");
            TypeDefinition audio = mod.GetType("GameAudioManager");
            if (menu == null || audio == null) { Skip("找不到 MainMenuNew 或 GameAudioManager"); return; }

            MethodDefinition start = menu.Methods.FirstOrDefault(m => m.Name == "Start" && !m.HasParameters && m.HasBody);
            MethodDefinition play = audio.Methods.FirstOrDefault(m => m.Name == "PlayBgm" && m.Parameters.Count == 2
                && m.Parameters[0].ParameterType.FullName == "System.String" && m.Parameters[1].ParameterType.FullName == "System.Boolean");
            if (start == null || play == null) { Skip("找不到 Start 或 PlayBgm"); return; }

            Instruction stop = start.Body.Instructions.FirstOrDefault(i =>
                (i.OpCode == OpCodes.Callvirt || i.OpCode == OpCodes.Call) && i.Operand is MethodReference
                && ((MethodReference)i.Operand).Name == "StopBgm" && ((MethodReference)i.Operand).DeclaringType.FullName == "GameAudioManager");
            if (stop == null || stop.Previous == null) { Skip("找不到 StopBgm 呼叫"); return; }

            // StopBgm 前一個指令是載入 GameAudioManager 實例（例如 ldloc），複製它來當 PlayBgm 的對象
            Instruction loadInstance = stop.Previous;
            if (!IsLoadLocal(loadInstance.OpCode)) { Skip("StopBgm 的呼叫方式和預期不同"); return; }

            ILProcessor il = start.Body.GetILProcessor();
            Instruction a = il.Create(OpCodes.Ldloc, (VariableDefinition)ToVariable(loadInstance, start));
            Instruction b = il.Create(OpCodes.Ldstr, "main_menu");
            Instruction c = il.Create(OpCodes.Ldc_I4_1);
            Instruction d = il.Create(OpCodes.Callvirt, play);
            il.InsertAfter(stop, a);
            il.InsertAfter(a, b);
            il.InsertAfter(b, c);
            il.InsertAfter(c, d);

            SetPatchedAttribute(args.Assembly, Tag);
            Console.WriteLine("[WSZH] 已加入主畫面音樂");
        }
        catch (Exception ex)
        {
            Skip(ex.Message);
        }
    }

    static bool IsLoadLocal(OpCode op)
    {
        return op == OpCodes.Ldloc || op == OpCodes.Ldloc_S || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1
            || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3;
    }

    // ldloc.0~3 沒有 operand，轉成明確的變數，用 ldloc 指令重新產生
    static object ToVariable(Instruction ins, MethodDefinition m)
    {
        if (ins.Operand is VariableDefinition) return ins.Operand;
        int idx = ins.OpCode == OpCodes.Ldloc_0 ? 0 : ins.OpCode == OpCodes.Ldloc_1 ? 1 : ins.OpCode == OpCodes.Ldloc_2 ? 2 : 3;
        return m.Body.Variables[idx];
    }

    static void Skip(string why)
    {
        Console.WriteLine("[WSZH] 主畫面音樂補丁略過：" + why);
    }
}
