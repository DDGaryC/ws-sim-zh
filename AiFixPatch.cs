// ReiPatcher 補丁：在 AIVersion7.ChooseEffectTargets 的開頭與每個結尾插入
// WszhAiFix.Begin / WszhAiFix.End 的呼叫（實際邏輯在 WSZH.AiFix.dll，見 AiFix.cs）。
// 找不到要修改的程式碼（例如模擬器改版）或缺少 WSZH.AiFix.dll 時直接略過，不影響遊戲啟動。
// 編譯：.NET 3.5 的 csc（ReiPatcher 執行在 .NET 2.0/3.5 上）
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using ReiPatcher;
using ReiPatcher.Patch;

public class WszhAiFixPatch : PatchBase
{
    const string Tag = "WSZH.AiFix";
    const string HelperName = "WSZH.AiFix";
    public override string Name { get { return "WS 中文化套件：AI 選卡修正"; } }
    public override string Version { get { return "1.0"; } }

    static string HelperSource { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HelperName + ".dll"); } }
    static string ManagedDir { get { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\Weiss Schwarz_Data\Managed")); } }

    public override void PrePatch()
    {
        RPConfig.RequestAssembly("Assembly-CSharp.dll");
        CopyHelper();
    }

    // 遊戲執行時要在 Managed 資料夾找得到 WSZH.AiFix.dll（模擬器更新會清掉，所以每次啟動都檢查）
    static bool CopyHelper()
    {
        try
        {
            if (!File.Exists(HelperSource) || !Directory.Exists(ManagedDir)) return false;
            string dst = Path.Combine(ManagedDir, HelperName + ".dll");
            var a = new FileInfo(HelperSource);
            if (!File.Exists(dst) || new FileInfo(dst).Length != a.Length || File.GetLastWriteTimeUtc(dst) != a.LastWriteTimeUtc)
                File.Copy(HelperSource, dst, true);
            return true;
        }
        catch (Exception ex) { Console.WriteLine("[WSZH] AI fix: copy failed: " + ex.Message); return File.Exists(Path.Combine(ManagedDir, HelperName + ".dll")); }
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
            if (!CopyHelper()) { Console.WriteLine("[WSZH] AI fix skipped: helper dll not found"); return; }
            string why = Apply(args.Assembly);
            if (why != null) { Console.WriteLine("[WSZH] AI fix skipped: " + why); return; }
            SetPatchedAttribute(args.Assembly, Tag);
            Console.WriteLine("[WSZH] AI fix applied");
        }
        catch (Exception ex) { Console.WriteLine("[WSZH] AI fix skipped: " + ex.Message); }
    }

    // 回傳 null＝成功；否則是略過的原因（這時不會留下改到一半的內容）
    public static string Apply(AssemblyDefinition asm)
    {
        ModuleDefinition mod = asm.MainModule;
        TypeDefinition ai = mod.GetType("AIVersion7");
        if (ai == null) return "AIVersion7 not found";
        MethodDefinition m = ai.Methods.FirstOrDefault(x => x.Name == "ChooseEffectTargets" && x.HasBody && x.Parameters.Count == 5 && !x.IsStatic);
        if (m == null) return "ChooseEffectTargets not found";
        // 參數：options, settings, optional, amount, effectOwner
        if (m.Parameters[3].ParameterType.FullName != "System.Int32" || m.Parameters[0].ParameterType.IsValueType
            || m.Parameters[1].ParameterType.IsValueType || m.Parameters[4].ParameterType.IsValueType) return "unexpected parameters";
        if (m.Body.Instructions.Any(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name == "WszhAiFix"))
            return "already patched";

        AssemblyNameReference asmRef = mod.AssemblyReferences.FirstOrDefault(r => r.Name == HelperName);
        if (asmRef == null) { asmRef = new AssemblyNameReference(HelperName, new Version(1, 0, 0, 0)); mod.AssemblyReferences.Add(asmRef); }
        TypeReference helper = new TypeReference("", "WszhAiFix", mod, asmRef);
        TypeReference obj = mod.TypeSystem.Object, i4 = mod.TypeSystem.Int32, vd = mod.TypeSystem.Void;
        MethodReference begin = new MethodReference("Begin", vd, helper);
        begin.Parameters.Add(new ParameterDefinition(obj)); begin.Parameters.Add(new ParameterDefinition(obj)); begin.Parameters.Add(new ParameterDefinition(obj));
        begin.Parameters.Add(new ParameterDefinition(i4)); begin.Parameters.Add(new ParameterDefinition(obj));
        MethodReference end = new MethodReference("End", vd, helper);
        end.Parameters.Add(new ParameterDefinition(obj));

        m.Body.SimplifyMacros();   // 先把短跳躍展開，插入指令後才不會超出範圍
        ILProcessor il = m.Body.GetILProcessor();
        // 結尾：把每個 ret 改成「ldarg.0; call End; ret」。原本的 ret 指令物件改成 ldarg.0，跳到這裡的分支就不用調整
        foreach (Instruction ret in m.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList())
        {
            ret.OpCode = OpCodes.Ldarg_0; ret.Operand = null;
            Instruction call = il.Create(OpCodes.Call, end);
            il.InsertAfter(ret, call);
            il.InsertAfter(call, il.Create(OpCodes.Ret));
        }
        // 開頭：WszhAiFix.Begin(this, options, settings, amount, effectOwner)
        Instruction first = m.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg, m.Parameters[0]));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg, m.Parameters[1]));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg, m.Parameters[3]));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg, m.Parameters[4]));
        il.InsertBefore(first, il.Create(OpCodes.Call, begin));
        m.Body.OptimizeMacros();
        return null;
    }
}
