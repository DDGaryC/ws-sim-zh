// ReiPatcher 補丁：在 MainMenuNew.Start() 開頭插入 WszhLang.Init()（實際邏輯在 WSZH.Lang.dll，見 Lang.cs）。
// 找不到要修改的程式碼（例如模擬器改版）或缺少 WSZH.Lang.dll 時直接略過，不影響遊戲啟動。
// 編譯：.NET 3.5 的 csc（ReiPatcher 執行在 .NET 2.0/3.5 上）
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ReiPatcher;
using ReiPatcher.Patch;

public class WszhLangPatch : PatchBase
{
    const string Tag = "WSZH.Lang";
    const string HelperName = "WSZH.Lang";
    public override string Name { get { return "WS 中文化套件：遊戲內語言切換"; } }
    public override string Version { get { return "1.0"; } }

    static string HelperSource { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HelperName + ".dll"); } }
    static string ManagedDir { get { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\Weiss Schwarz_Data\Managed")); } }

    public override void PrePatch()
    {
        RPConfig.RequestAssembly("Assembly-CSharp.dll");
        CopyHelper();
    }

    // 遊戲執行時要在 Managed 資料夾找得到 WSZH.Lang.dll（模擬器更新會清掉，所以每次啟動都檢查）
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
        catch (Exception ex) { Console.WriteLine("[WSZH] language switch: copy failed: " + ex.Message); return File.Exists(Path.Combine(ManagedDir, HelperName + ".dll")); }
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
            if (!CopyHelper()) { Console.WriteLine("[WSZH] language switch skipped: helper dll not found"); return; }
            ModuleDefinition mod = args.Assembly.MainModule;
            TypeDefinition menu = mod.GetType("MainMenuNew");
            MethodDefinition start = menu == null ? null : menu.Methods.FirstOrDefault(m => m.Name == "Start" && !m.HasParameters && m.HasBody);
            if (start == null) { Console.WriteLine("[WSZH] language switch skipped: MainMenuNew.Start not found"); return; }

            AssemblyNameReference asmRef = mod.AssemblyReferences.FirstOrDefault(r => r.Name == HelperName);
            if (asmRef == null) { asmRef = new AssemblyNameReference(HelperName, new Version(1, 0, 0, 0)); mod.AssemblyReferences.Add(asmRef); }
            TypeReference helper = new TypeReference("", "WszhLang", mod, asmRef);
            MethodReference init = new MethodReference("Init", mod.TypeSystem.Void, helper);

            ILProcessor il = start.Body.GetILProcessor();
            il.InsertBefore(start.Body.Instructions[0], il.Create(OpCodes.Call, init));
            SetPatchedAttribute(args.Assembly, Tag);
            Console.WriteLine("[WSZH] language switch applied");
        }
        catch (Exception ex) { Console.WriteLine("[WSZH] language switch skipped: " + ex.Message); }
    }
}
