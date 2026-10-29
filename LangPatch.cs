// ReiPatcher 補丁（實際邏輯在 WSZH.Lang.dll）：
//  1. MainMenuNew.Start() 開頭插入 WszhLang.Init()：遊戲內語言切換（Lang.cs）
//  2. WeissCard.EnrichenText(string) 開頭插入 str = WszhCardText.Translate(str)：卡片效果翻譯（CardText.cs）
//  4. WeissCard.EnrichenText(string) 結尾改成 return WszhCardText.Post(結果)：中文效果改靠左對齊
//  3. CardLibrary.Search 開頭插入：效果欄有中文時改用 WszhCardText.SearchCjk（用翻譯後的效果文字搜尋）
// 兩項各有自己的標記，已經套用過的不會重複套用（之後新增的項目也能補上去）。
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
    const string CardTag = "WSZH.CardText";
    const string SearchTag = "WSZH.CardSearch";
    const string AlignTag = "WSZH.CardAlign";
    const string JpArtTag = "WSZH.JpArt";
    const string ReplayFixTag = "WSZH.ReplayFix";
    const string ReplayFix2Tag = "WSZH.ReplayFix2";
    const string NameSearchTag = "WSZH.NameSearch";
    const string SeriesSearchTag = "WSZH.SeriesSearch";
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
        var tags = GetPatchedAttributes(args.Assembly).Select(a => a.Info).ToList();
        return !tags.Contains(Tag) || !tags.Contains(CardTag) || !tags.Contains(SearchTag) || !tags.Contains(AlignTag) || !tags.Contains(JpArtTag) || !tags.Contains(ReplayFixTag) || !tags.Contains(ReplayFix2Tag)
            || !tags.Contains(NameSearchTag) || !tags.Contains(SeriesSearchTag);
    }

    public override void Patch(PatcherArguments args)
    {
        try
        {
            if (!CopyHelper()) { Console.WriteLine("[WSZH] language switch skipped: helper dll not found"); return; }
            ModuleDefinition mod = args.Assembly.MainModule;
            var tags = GetPatchedAttributes(args.Assembly).Select(a => a.Info).ToList();
            AssemblyNameReference asmRef = mod.AssemblyReferences.FirstOrDefault(r => r.Name == HelperName);
            if (asmRef == null) { asmRef = new AssemblyNameReference(HelperName, new Version(1, 0, 0, 0)); mod.AssemblyReferences.Add(asmRef); }

            if (!tags.Contains(Tag))
            {
                TypeDefinition menu = mod.GetType("MainMenuNew");
                MethodDefinition start = menu == null ? null : menu.Methods.FirstOrDefault(m => m.Name == "Start" && !m.HasParameters && m.HasBody);
                if (start == null) Console.WriteLine("[WSZH] language switch skipped: MainMenuNew.Start not found");
                else
                {
                    MethodReference init = new MethodReference("Init", mod.TypeSystem.Void, new TypeReference("", "WszhLang", mod, asmRef));
                    ILProcessor il = start.Body.GetILProcessor();
                    il.InsertBefore(start.Body.Instructions[0], il.Create(OpCodes.Call, init));
                    SetPatchedAttribute(args.Assembly, Tag);
                    Console.WriteLine("[WSZH] language switch applied");
                }
            }

            if (!tags.Contains(CardTag))
            {
                TypeDefinition card = mod.GetType("WeissCard");
                MethodDefinition enrich = card == null ? null : card.Methods.FirstOrDefault(m => m.Name == "EnrichenText" && m.HasBody && !m.IsStatic
                    && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String" && m.ReturnType.FullName == "System.String");
                if (enrich == null) Console.WriteLine("[WSZH] card text skipped: WeissCard.EnrichenText not found");
                else
                {
                    var tr = new MethodReference("Translate", mod.TypeSystem.String, new TypeReference("", "WszhCardText", mod, asmRef));
                    tr.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    ILProcessor il = enrich.Body.GetILProcessor();
                    Instruction first = enrich.Body.Instructions[0];
                    // str = WszhCardText.Translate(str)
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg, enrich.Parameters[0]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, tr));
                    il.InsertBefore(first, il.Create(OpCodes.Starg, enrich.Parameters[0]));
                    SetPatchedAttribute(args.Assembly, CardTag);
                    Console.WriteLine("[WSZH] card text applied");
                }
            }

            if (!tags.Contains(AlignTag))
            {
                TypeDefinition card = mod.GetType("WeissCard");
                MethodDefinition enrich = card == null ? null : card.Methods.FirstOrDefault(m => m.Name == "EnrichenText" && m.HasBody && !m.IsStatic
                    && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String" && m.ReturnType.FullName == "System.String");
                if (enrich == null) Console.WriteLine("[WSZH] card align skipped: WeissCard.EnrichenText not found");
                else
                {
                    var post = new MethodReference("Post", mod.TypeSystem.String, new TypeReference("", "WszhCardText", mod, asmRef));
                    post.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    ILProcessor il = enrich.Body.GetILProcessor();
                    // 每個 ret 改成「call Post; ret」：原本的 ret 指令物件直接改成 call，跳到這裡的分支不用調整
                    foreach (Instruction ret in enrich.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList())
                    {
                        ret.OpCode = OpCodes.Call; ret.Operand = post;
                        il.InsertAfter(ret, il.Create(OpCodes.Ret));
                    }
                    SetPatchedAttribute(args.Assembly, AlignTag);
                    Console.WriteLine("[WSZH] card align applied");
                }
            }

            if (!tags.Contains(SearchTag))
            {
                TypeDefinition lib = mod.GetType("CardLibrary");
                MethodDefinition search = lib == null ? null : lib.Methods.FirstOrDefault(m => m.Name == "Search" && m.IsStatic && m.HasBody
                    && m.Parameters.Count == 11 && m.Parameters.All(x => x.ParameterType.FullName == "System.String"));
                if (search == null) Console.WriteLine("[WSZH] card search skipped: CardLibrary.Search not found");
                else
                {
                    TypeReference helper = new TypeReference("", "WszhCardText", mod, asmRef);
                    var isCjk = new MethodReference("IsCjk", mod.TypeSystem.Boolean, helper);
                    isCjk.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    var cjk = new MethodReference("SearchCjk", mod.TypeSystem.Object, helper);
                    for (int i = 0; i < 11; i++) cjk.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    ILProcessor il = search.Body.GetILProcessor();
                    Instruction first = search.Body.Instructions[0];
                    // if (WszhCardText.IsCjk(textInput)) return (List<WeissCard>)WszhCardText.SearchCjk(...所有參數...);
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg, search.Parameters[9]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, isCjk));
                    il.InsertBefore(first, il.Create(OpCodes.Brfalse, first));
                    for (int i = 0; i < 11; i++) il.InsertBefore(first, il.Create(OpCodes.Ldarg, search.Parameters[i]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, cjk));
                    il.InsertBefore(first, il.Create(OpCodes.Castclass, search.ReturnType));
                    il.InsertBefore(first, il.Create(OpCodes.Ret));
                    SetPatchedAttribute(args.Assembly, SearchTag);
                    Console.WriteLine("[WSZH] card search applied");
                }
            }
            if (!tags.Contains(JpArtTag))
            {
                TypeDefinition lib = mod.GetType("CardLibrary");
                MethodDefinition gi = lib == null ? null : lib.Methods.FirstOrDefault(m => m.Name == "GetImage" && m.IsStatic && m.HasBody
                    && m.Parameters.Count == 3 && m.Parameters.All(x => x.ParameterType.FullName == "System.String"));
                if (gi == null) Console.WriteLine("[WSZH] jp art skipped: CardLibrary.GetImage not found");
                else
                {
                    var hook = new MethodReference("Hook", mod.TypeSystem.Void, new TypeReference("", "WszhJpArt", mod, asmRef));
                    hook.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    hook.Parameters.Add(new ParameterDefinition(new ByReferenceType(mod.TypeSystem.String)));
                    ILProcessor il = gi.Body.GetILProcessor();
                    Instruction first = gi.Body.Instructions[0];
                    // WszhJpArt.Hook(cardID, ref altImagePath)
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg, gi.Parameters[0]));
                    il.InsertBefore(first, il.Create(OpCodes.Ldarga, gi.Parameters[2]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, hook));
                    SetPatchedAttribute(args.Assembly, JpArtTag);
                    Console.WriteLine("[WSZH] jp art applied");
                }
            }
            if (!tags.Contains(ReplayFixTag))
            {
                TypeDefinition player = mod.GetType("Player");
                MethodDefinition end = player == null ? null : player.Methods.FirstOrDefault(m => m.Name == "ChangePhaseButtonClickedImmediate" && !m.IsStatic && !m.HasParameters && m.HasBody);
                if (end == null) Console.WriteLine("[WSZH] replay fix skipped: Player.ChangePhaseButtonClickedImmediate not found");
                else
                {
                    var hook = new MethodReference("Intercept", mod.TypeSystem.Boolean, new TypeReference("", "WszhReplayFix", mod, asmRef));
                    hook.Parameters.Add(new ParameterDefinition(mod.TypeSystem.Object));
                    ILProcessor il = end.Body.GetILProcessor();
                    Instruction first = end.Body.Instructions[0];
                    // if (WszhReplayFix.Intercept(this)) return;
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
                    il.InsertBefore(first, il.Create(OpCodes.Call, hook));
                    il.InsertBefore(first, il.Create(OpCodes.Brfalse, first));
                    il.InsertBefore(first, il.Create(OpCodes.Ret));
                    SetPatchedAttribute(args.Assembly, ReplayFixTag);
                    Console.WriteLine("[WSZH] replay fix applied");
                }
            }
            // 卡片名稱欄有中文／日文時改走 SearchCjk（比對日文卡名）
            if (!tags.Contains(NameSearchTag))
            {
                TypeDefinition lib = mod.GetType("CardLibrary");
                MethodDefinition search = lib == null ? null : lib.Methods.FirstOrDefault(m => m.Name == "Search" && m.IsStatic && m.HasBody
                    && m.Parameters.Count == 11 && m.Parameters.All(x => x.ParameterType.FullName == "System.String"));
                if (search == null) Console.WriteLine("[WSZH] name search skipped: CardLibrary.Search not found");
                else
                {
                    TypeReference helper = new TypeReference("", "WszhNames", mod, asmRef);
                    var hasCjk = new MethodReference("HasCjk", mod.TypeSystem.Boolean, helper);
                    hasCjk.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    var cjk = new MethodReference("SearchCjk", mod.TypeSystem.Object, new TypeReference("", "WszhCardText", mod, asmRef));
                    for (int i = 0; i < 11; i++) cjk.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    ILProcessor il = search.Body.GetILProcessor();
                    Instruction first = search.Body.Instructions[0];
                    // if (WszhNames.HasCjk(cardName)) return (List<WeissCard>)WszhCardText.SearchCjk(...所有參數...);
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg, search.Parameters[2]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, hasCjk));
                    il.InsertBefore(first, il.Create(OpCodes.Brfalse, first));
                    for (int i = 0; i < 11; i++) il.InsertBefore(first, il.Create(OpCodes.Ldarg, search.Parameters[i]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, cjk));
                    il.InsertBefore(first, il.Create(OpCodes.Castclass, search.ReturnType));
                    il.InsertBefore(first, il.Create(OpCodes.Ret));
                    SetPatchedAttribute(args.Assembly, NameSearchTag);
                    Console.WriteLine("[WSZH] name search applied");
                }
            }
            // 牌組編輯的系列欄打中文：DeckBuilderSearch.SeriesMatchesQuery(series, query) 開頭插入
            //   if (WszhNames.SeriesMatch(series, query)) return true;
            if (!tags.Contains(SeriesSearchTag))
            {
                TypeDefinition dbs = mod.GetType("DeckBuilderSearch");
                MethodDefinition sm = dbs == null ? null : dbs.Methods.FirstOrDefault(m => m.Name == "SeriesMatchesQuery" && !m.IsStatic && m.HasBody
                    && m.Parameters.Count == 2 && m.ReturnType.FullName == "System.Boolean");
                if (sm == null) Console.WriteLine("[WSZH] series search skipped: DeckBuilderSearch.SeriesMatchesQuery not found");
                else
                {
                    var hook = new MethodReference("SeriesMatch", mod.TypeSystem.Boolean, new TypeReference("", "WszhNames", mod, asmRef));
                    hook.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    hook.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                    ILProcessor il = sm.Body.GetILProcessor();
                    Instruction first = sm.Body.Instructions[0];
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg, sm.Parameters[0]));
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg, sm.Parameters[1]));
                    il.InsertBefore(first, il.Create(OpCodes.Call, hook));
                    il.InsertBefore(first, il.Create(OpCodes.Brfalse, first));
                    il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_1));
                    il.InsertBefore(first, il.Create(OpCodes.Ret));
                    SetPatchedAttribute(args.Assembly, SeriesSearchTag);
                    Console.WriteLine("[WSZH] series search applied");
                }
            }
            // 打出名場面後走的是 ChangePhaseButtonClicked()（重播時一開頭就 return），同樣在開頭插入檢查
            if (!tags.Contains(ReplayFix2Tag))
            {
                TypeDefinition player = mod.GetType("Player");
                MethodDefinition clicked = player == null ? null : player.Methods.FirstOrDefault(m => m.Name == "ChangePhaseButtonClicked" && !m.IsStatic && !m.HasParameters && m.HasBody);
                if (clicked == null) Console.WriteLine("[WSZH] replay fix 2 skipped: Player.ChangePhaseButtonClicked not found");
                else
                {
                    var hook = new MethodReference("Intercept", mod.TypeSystem.Boolean, new TypeReference("", "WszhReplayFix", mod, asmRef));
                    hook.Parameters.Add(new ParameterDefinition(mod.TypeSystem.Object));
                    ILProcessor il = clicked.Body.GetILProcessor();
                    Instruction first = clicked.Body.Instructions[0];
                    il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
                    il.InsertBefore(first, il.Create(OpCodes.Call, hook));
                    il.InsertBefore(first, il.Create(OpCodes.Brfalse, first));
                    il.InsertBefore(first, il.Create(OpCodes.Ret));
                    SetPatchedAttribute(args.Assembly, ReplayFix2Tag);
                    Console.WriteLine("[WSZH] replay fix 2 applied");
                }
            }
        }
        catch (Exception ex) { Console.WriteLine("[WSZH] language switch skipped: " + ex.Message); }
    }
}
