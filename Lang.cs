// WS 中文化套件：遊戲內語言切換（遊戲執行時載入的小程式庫，WSZH.Lang.dll）
// LangPatch.cs 會在 MainMenuNew.Start() 開頭插入 WszhLang.Init()。
// 設定頁左下會出現「語言 / Language」選項，可以切換 繁體中文（台灣）／繁體中文（香港）／简体中文／English。
// 切換方式：改 XUnity.AutoTranslator 的語言設定、重新計算翻譯檔路徑，再呼叫它內建的「重新載入翻譯」
// （找不到翻譯的文字會自動恢復原文，所以 English 就是切到沒有翻譯內容的 en 資料夾）。
// 選擇會寫回 AutoTranslator\Config.ini，下次開遊戲沿用。
// XUnity 全部用反射存取；任何地方出錯都只顯示提示，不影響遊戲。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WszhLang : MonoBehaviour
{
    static WszhLang inst;
    static readonly string[] Codes = { "tw", "hk", "cn", "en" };
    static readonly string[] Names = { "繁體中文（台灣）", "繁體中文（香港）", "简体中文", "English" };
    const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    string current;
    Graphic status; float statusUntil; bool statusShown;

    public static void Init()
    {
        try
        {
            if (inst != null) return;
            var go = new GameObject("WSZH_LanguageSwitch");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<WszhLang>();
        }
        catch (Exception ex) { Debug.Log("[WSZH] language switch init failed: " + ex.Message); }
    }

    void Start()
    {
        current = ReadLanguage() ?? "tw";
        Debug.Log("[WSZH] language switch ready, current=" + current);
        ApplyFpsCap();
        StartCoroutine(PreloadCardText());
    }

    // 卡片翻譯（4 萬多條）第一次用到時才載入會頓 0.3 秒左右，改成在主選單就先載好
    IEnumerator PreloadCardText()
    {
        yield return new WaitForSecondsRealtime(1f);
        try { WszhCardText.LineCount(); } catch { }
    }

    // ---- 幀率上限 ----
    // 模擬器本身沒有限制幀率，會用 1000 FPS 左右全速跑；翻譯外掛每一幀都要檢查文字，
    // 幀數越高負擔越重，較弱的電腦會變卡、變燙。預設限制在 60，設定頁可以改。
    static readonly int[] FpsChoices = { 60, 120, 30, 0 };   // 0 = 不限制
    const string FpsPref = "WSZH_FpsCap";
    static int FpsCap { get { return PlayerPrefs.GetInt(FpsPref, 60); } }

    static void ApplyFpsCap()
    {
        int cap = FpsCap;
        if (cap > 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = cap; }
        else Application.targetFrameRate = -1;
        Debug.Log("[WSZH] fps cap: " + (cap > 0 ? cap.ToString() : "none"));
    }

    static string FpsLabel()
    {
        int cap = FpsCap;
        if (ReadLanguage() == "en") return "Frame rate limit: " + (cap > 0 ? cap + " FPS" : "None");
        return Zh("幀率上限：" + (cap > 0 ? cap + " FPS" : "不限制"));
    }

    Graphic fpsLabel;
    void OnFpsClick()
    {
        int i = Array.IndexOf(FpsChoices, FpsCap);
        PlayerPrefs.SetInt(FpsPref, FpsChoices[(i + 1) % FpsChoices.Length]);
        PlayerPrefs.Save();
        ApplyFpsCap();
        if (fpsLabel != null) SetTextOf(fpsLabel, FpsLabel());
    }

    // ---- XUnity 存取 ----
    static Assembly xuAsm;
    static Assembly Xu()
    {
        if (xuAsm == null) xuAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "XUnity.AutoTranslator.Plugin.Core");
        return xuAsm;
    }

    static Type XuType(string name)
    {
        var a = Xu();
        return a == null ? null : a.GetType("XUnity.AutoTranslator.Plugin.Core." + name);
    }

    // 目前語言：每幀會讀好幾次，欄位只找一次（找程式集、型別很慢）
    static FieldInfo langField;
    static string ReadLanguage()
    {
        try
        {
            if (langField == null)
            {
                var s = XuType("Configuration.Settings");
                langField = s == null ? null : s.GetField("Language", Any);
            }
            return langField == null ? null : (string)langField.GetValue(null);
        }
        catch { return null; }
    }

    static object EnvCurrent()
    {
        var pe = XuType("PluginEnvironment");
        return pe == null ? null : pe.GetField("Current", Any).GetValue(null);
    }

    static object GetProp(object o, string name)
    {
        // 介面上的屬性：在實作型別找不到時，改從它實作的介面找
        Type t = o.GetType();
        PropertyInfo p = t.GetProperty(name, Any);
        if (p == null) foreach (var i in t.GetInterfaces()) { p = i.GetProperty(name, Any); if (p != null) break; }
        return p == null ? null : p.GetValue(o, null);
    }

    static object Index(object o, string key)
    {
        var p = o.GetType().GetProperty("Item", Any, null, null, new[] { typeof(string) }, null);
        return p == null ? null : p.GetValue(o, new object[] { key });
    }

    // 回傳 null＝成功，否則是錯誤原因
    static string Switch(string lang)
    {
        var s = XuType("Configuration.Settings");
        var plugin = XuType("AutoTranslationPlugin");
        var helper = XuType("Utilities.LanguageHelper");
        object env = EnvCurrent();
        if (s == null || plugin == null || env == null) return "找不到翻譯插件";
        string root = (string)GetProp(env, "TranslationPath");
        if (root == null) return "找不到翻譯資料夾";
        if (!Directory.Exists(Path.Combine(Path.Combine(root, "Translation"), lang))) return "沒有這個語言的翻譯檔";

        s.GetField("Language", Any).SetValue(null, lang);
        // 和 XUnity 啟動時一樣，用新語言重新算出各個翻譯檔的路徑
        string[][] pairs = {
            new[] { "OutputFile", "AutoTranslationsFilePath" }, new[] { "SubstitutionFile", "SubstitutionFilePath" },
            new[] { "PreprocessorsFile", "PreprocessorsFilePath" }, new[] { "PostprocessorsFile", "PostprocessorsFilePath" },
            new[] { "TranslationDirectory", "TranslationsPath" } };
        foreach (var pr in pairs)
        {
            var src = s.GetField(pr[0], Any); var dst = s.GetField(pr[1], Any);
            if (src == null || dst == null) continue;
            string raw = (string)src.GetValue(null);
            if (raw == null) continue;
            string path = Path.Combine(root, raw.Replace('/', Path.DirectorySeparatorChar)).Replace("{lang}", lang).Replace("{Lang}", lang);
            dst.SetValue(null, path);
        }
        try
        {
            var ws = s.GetField("ToLanguageUsesWhitespaceBetweenWords", Any);
            var rq = helper == null ? null : helper.GetMethod("RequiresWhitespaceUponLineMerging", Any);
            if (ws != null && rq != null) ws.SetValue(null, rq.Invoke(null, new object[] { lang }));
        }
        catch { }

        // 重新載入前 XUnity 會先讀主翻譯檔，檔案不存在會讓整個重新載入中斷（English 原本就沒有這個檔）
        try
        {
            string main = (string)s.GetField("AutoTranslationsFilePath", Any).GetValue(null);
            if (!string.IsNullOrEmpty(main) && !File.Exists(main))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(main));
                File.WriteAllText(main, "\n", new UTF8Encoding(true));
            }
        }
        catch { }

        object inst = plugin.GetField("Current", Any).GetValue(null);
        var reload = plugin.GetMethod("ReloadTranslations", Any, null, Type.EmptyTypes, null);
        if (inst == null || reload == null) return "翻譯插件版本不支援";
        reload.Invoke(inst, null);

        Save(env, root, lang);
        return null;
    }

    // 記住選擇：改 XUnity 記憶體裡的設定（它之後存檔也不會改回去），並直接改寫 Config.ini 的 Language= 這一行
    static void Save(object env, string root, string lang)
    {
        try
        {
            object prefs = GetProp(env, "Preferences");
            object key = prefs == null ? null : Index(Index(prefs, "General"), "Language");
            var vp = key == null ? null : key.GetType().GetProperty("Value", Any);
            if (vp != null) vp.SetValue(key, lang, null);
        }
        catch { }
        try
        {
            string ini = Path.Combine(root, "Config.ini");
            if (!File.Exists(ini)) return;
            byte[] raw = File.ReadAllBytes(ini);
            bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
            string next = Regex.Replace(text, @"(?m)^Language=.*?(\r?)$", "Language=" + lang + "$1");
            if (next == text) return;
            var enc = new UTF8Encoding(bom);
            File.WriteAllText(ini, next, enc);
        }
        catch { }
    }

    // ---- 介面：設定頁左下（「讓對手看到你的自訂卡套」的下方）----
    // 複製設定頁原本的標題文字與按鈕圖樣，再建一個遊戲同款（TextMeshPro）的下拉選單，外觀和遊戲一致。
    IEnumerator BuildLater()
    {
        yield return null;   // 等設定頁的物件都初始化好
        try { Build(); }
        catch (Exception ex) { Debug.Log("[WSZH] language UI failed: " + ex); }
        // 沒建成功的話等 2 秒再試，不要每一幀重試
        if (built == null) yield return new WaitForSecondsRealtime(2f);
        building = false;
    }

    static Vector2 ScreenPos(Component c, Canvas cv)
    {
        Camera cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
        var rt = c.transform as RectTransform;
        Vector3 w = rt != null ? rt.TransformPoint(rt.rect.center) : c.transform.position;
        return RectTransformUtility.WorldToScreenPoint(cam, w);
    }

    // 已擺好的物件：版面配置（Layout Group）可能在下一幀把位置改回去，所以之後再套用一次
    readonly List<KeyValuePair<RectTransform, Vector2>> placed = new List<KeyValuePair<RectTransform, Vector2>>();
    Canvas placedCanvas;

    void Place(RectTransform rt, Vector2 screen, Canvas cv)
    {
        // 複製來的文字原本受所在區塊的版面配置控制，要先讓它不受配置影響，位置才改得動
        var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        PlaceAt(rt, screen, cv);
        placed.Add(new KeyValuePair<RectTransform, Vector2>(rt, screen));
        placedCanvas = cv;
    }

    IEnumerator Replace()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        foreach (var kv in placed) if (kv.Key != null && placedCanvas != null) PlaceAt(kv.Key, kv.Value, placedCanvas);
    }

    static void PlaceAt(RectTransform rt, Vector2 screen, Canvas cv)
    {
        Camera cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
        Vector3 world;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rt.parent, screen, cam, out world)) return;
        // 讓物件的中心（不是 pivot）對到指定位置
        Vector3 centerOffset = rt.TransformPoint(rt.rect.center) - rt.position;
        rt.position = world - centerOffset;
    }

    void Build()
    {
        Type osType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("OptionsScreen")).FirstOrDefault(t => t != null);
        var os = osType == null ? null : FindObjectOfType(osType);
        if (os == null) { Debug.Log("[WSZH] language UI: OptionsScreen not found"); return; }
        var on = osType.GetField("sendImageOnButton", Any).GetValue(os) as Button;
        var off = osType.GetField("sendImageOffButton", Any).GetValue(os) as Button;
        var back = osType.GetField("mainMenuButton", Any).GetValue(os) as Button;
        if (on == null || off == null || back == null) { Debug.Log("[WSZH] language UI: option buttons not found"); return; }
        Canvas cv = on.GetComponentInParent<Canvas>();
        if (cv == null) { Debug.Log("[WSZH] language UI: canvas not found"); return; }
        cv = cv.rootCanvas;

        Vector2 pOn = ScreenPos(on, cv), pOff = ScreenPos(off, cv);
        float cx = (pOn.x + pOff.x) / 2, by = pOn.y;

        // 這一區的標題：按鈕上方、同一欄、字最大的那段文字
        // （設定頁的標題是 Unity 舊式的 Text，也可能是 TextMeshPro，兩種都找）
        Graphic refTitle = null;
        var all = FindObjectsOfType<Graphic>().Where(t => (t is TMP_Text || t is Text) && t.GetComponentInParent<Button>() == null).ToList();
        foreach (var t in all)
        {
            Canvas tc = t.canvas; if (tc == null) continue;
            Vector2 p = ScreenPos(t, tc.rootCanvas);
            if (Mathf.Abs(p.x - cx) > Screen.width * 0.2f || p.y <= by || p.y > by + Screen.height * 0.3f) continue;
            if (refTitle == null || SizeOf(t) > SizeOf(refTitle)) refTitle = t;
        }
        if (refTitle == null)
        {
            // 找不到時把畫面上的文字都記下來，方便查原因
            var sb = new StringBuilder("[WSZH] language UI: buttons at " + cx + "," + by + " screen " + Screen.width + "x" + Screen.height + "; texts:");
            foreach (var t in all)
            {
                Canvas tc = t.canvas;
                Vector2 p = tc == null ? Vector2.zero : ScreenPos(t, tc.rootCanvas);
                string tx = t is TMP_Text ? ((TMP_Text)t).text : ((Text)t).text;
                sb.Append("\n  " + t.GetType().Name + " '" + (tx ?? "").Replace("\n", " ").Substring(0, Math.Min(20, (tx ?? "").Length)) + "' at " + p + " size " + SizeOf(t) + " canvas " + (tc == null ? "-" : tc.renderMode.ToString()));
            }
            Debug.Log(sb.ToString());
        }
        TMP_Text backLabel = back.GetComponentInChildren<TMP_Text>(true);
        if (backLabel == null) { Debug.Log("[WSZH] language UI: back button label not found"); return; }
        float H = Screen.height;
        // 找不到原本的標題時，改用「回到主界面」按鈕上的文字當範本（同樣是遊戲的字型）
        Transform host = refTitle != null ? refTitle.transform.parent : back.transform.parent;
        Graphic tmpl = refTitle;
        float titleSize = refTitle != null ? SizeOf(refTitle) : backLabel.fontSize * 1.25f;
        if (tmpl == null) tmpl = backLabel;

        // 擺放位置：預設是左欄「讓對手看到你的自訂卡套」下方，標題／選單／幀率按鈕／提示由上往下
        Vector2 titlePos = new Vector2(cx, by - H * 0.085f), ddPos = new Vector2(cx, by - H * 0.155f),
                fpsPos = new Vector2(cx, by - H * 0.222f), stPos = new Vector2(cx, by - H * 0.285f);
        Vector2 ddSize = Vector2.zero;
        // 模擬器 0.7.9.5 起那裡多了「Emote Pack」（標籤在左、下拉選單在右）：改成跟它同樣的排法，放在它下面
        var emote = osType.GetField("emotePackDropdown", Any) == null ? null : osType.GetField("emotePackDropdown", Any).GetValue(os) as TMP_Dropdown;
        if (emote != null && emote.gameObject.activeInHierarchy)
        {
            var ert = (RectTransform)emote.transform;
            Vector2 ep = ScreenPos(emote, cv);
            float rh = ert.rect.height * ert.lossyScale.y, rw = ert.rect.width * ert.lossyScale.x;
            float labelX = ep.x - rw * 1.05f;
            Graphic emoteLabel = null; float best = float.MaxValue;
            foreach (var t in all)
            {
                Canvas tc = t.canvas; if (tc == null || t.GetComponentInParent<TMP_Dropdown>() != null) continue;
                Vector2 p = ScreenPos(t, tc.rootCanvas);
                if (Mathf.Abs(p.y - ep.y) > rh || p.x >= ep.x - rw * 0.5f) continue;
                float dist = ep.x - p.x;
                if (dist < best) { best = dist; emoteLabel = t; labelX = p.x; }
            }
            titlePos = new Vector2(labelX, ep.y - rh * 1.35f);
            ddPos = new Vector2(ep.x, ep.y - rh * 1.35f);
            fpsPos = new Vector2(ep.x, ep.y - rh * 2.6f);
            stPos = new Vector2(labelX, ep.y - rh * 2.6f);
            ddSize = ert.rect.size;
            if (emoteLabel != null) { tmpl = emoteLabel; titleSize = SizeOf(emoteLabel); }
        }

        // 標題
        var titleGo = MakeText(tmpl, cv.transform, "WSZH_LanguageTitle");
        titleGo.name = "WSZH_LanguageTitle";
        var titleRt = (RectTransform)titleGo.transform;
        float textW = ((RectTransform)back.transform).rect.width * 2f;
        SetSizeOf(titleGo.GetComponent<Graphic>(), titleSize);
        Normalize(titleGo.GetComponent<Graphic>(), textW, titleSize);
        titleGo.GetComponent<Graphic>().raycastTarget = false;
        SetTextOf(titleGo.GetComponent<Graphic>(), "語言 / Language");
        placed.Clear();
        Place((RectTransform)titleGo.transform, titlePos, cv);

        // 下拉選單（用「回到主界面」按鈕的圖樣與字型）
        Image backImg = back.GetComponent<Image>();
        var res = new TMP_DefaultControls.Resources();
        if (backImg != null) { res.standard = backImg.sprite; res.background = backImg.sprite; res.inputField = backImg.sprite; }
        GameObject ddGo = TMP_DefaultControls.CreateDropdown(res);
        ddGo.name = "WSZH_LanguageDropdown";
        ddGo.transform.SetParent(host, false);
        var dd = ddGo.GetComponent<TMP_Dropdown>();
        var ddRt = (RectTransform)ddGo.transform;
        var backRt = (RectTransform)back.transform;
        ddRt.sizeDelta = ddSize != Vector2.zero ? ddSize : new Vector2(backRt.rect.width * 1.05f, backRt.rect.height);
        Place(ddRt, ddPos, cv);

        foreach (var t in ddGo.GetComponentsInChildren<TMP_Text>(true))
        {
            t.font = backLabel.font;
            t.fontSharedMaterial = backLabel.fontSharedMaterial;
            t.color = backLabel.color;
            t.fontSize = backLabel.fontSize * 0.85f;
            t.enableAutoSizing = false;
            t.alignment = TextAlignmentOptions.Center;
        }
        // 選項列高度配合字的大小
        var item = dd.template.GetComponentInChildren<Toggle>(true);
        if (item != null)
        {
            var itemRt = (RectTransform)item.transform;
            float ih = backRt.rect.height * 0.8f;
            itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, ih);
            var content = (RectTransform)itemRt.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, ih);
            dd.template.sizeDelta = new Vector2(dd.template.sizeDelta.x, ih * Codes.Length + 8);
            // 勾選記號：預設是白色方塊，改成淡灰色的整列底色來標示目前選項
            if (item.graphic != null)
            {
                var g = (RectTransform)item.graphic.transform;
                g.anchorMin = Vector2.zero; g.anchorMax = Vector2.one; g.offsetMin = Vector2.zero; g.offsetMax = Vector2.zero;
                item.graphic.color = new Color(0.75f, 0.75f, 0.75f, 0.6f);
                item.graphic.transform.SetSiblingIndex(1);
            }
        }
        // 右邊的箭頭：預設是白色方塊，改成文字「▼」
        if (dd.captionText != null)
        {
            Transform arrow = ddGo.transform.Find("Arrow");
            if (arrow != null)
            {
                var img = arrow.GetComponent<Image>();
                if (img != null) img.enabled = false;
                var ag = new GameObject("WSZH_Arrow", typeof(RectTransform));
                ag.transform.SetParent(arrow, false);
                var at = ag.AddComponent<Text>();
                try { at.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                at.color = backLabel.color; at.fontSize = Mathf.RoundToInt(backLabel.fontSize * 0.55f);
                at.alignment = TextAnchor.MiddleCenter; at.text = "▼"; at.raycastTarget = false;
                at.horizontalOverflow = HorizontalWrapMode.Overflow; at.verticalOverflow = VerticalWrapMode.Overflow;
                var art = (RectTransform)ag.transform;
                art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;
                var arrowRt = (RectTransform)arrow;
                arrowRt.sizeDelta = new Vector2(backRt.rect.height * 0.6f, backRt.rect.height * 0.6f);
            }
        }

        dd.ClearOptions();
        dd.AddOptions(new List<string>(Names));
        int cur = Array.IndexOf(Codes, current);
        dd.SetValueWithoutNotify(cur < 0 ? 0 : cur);
        dd.RefreshShownValue();
        dd.onValueChanged.AddListener(OnPick);
        built = ddGo;
        Debug.Log("[WSZH] language UI built");

        // 切換結果的提示（複製標題、字小一點，放在下拉選單下方）
        var stGo = MakeText(tmpl, cv.transform, "WSZH_LanguageStatus");
        stGo.name = "WSZH_LanguageStatus";
        status = stGo.GetComponent<Graphic>();
        SetSizeOf(status, titleSize * 0.55f);
        Normalize(status, textW, titleSize * 0.55f);
        status.raycastTarget = false;
        SetTextOf(status, "");
        Place((RectTransform)stGo.transform, stPos, cv);

        // 幀率上限按鈕（語言選單下方；按一下切換 60 → 120 → 30 → 不限制）
        var fpsRt = MakeButton(back, cv, "WSZH_FpsButton", FpsLabel(), OnFpsClick, out fpsLabel);
        fpsRt.sizeDelta = new Vector2(ddRt.rect.width, ddRt.rect.height * 0.85f);
        if (fpsLabel != null) SetSizeOf(fpsLabel, backLabel.fontSize * 0.7f);
        Place(fpsRt, fpsPos, cv);
        StartCoroutine(Replace());
        StartCoroutine(BuildCardTextLater(os, osType, cv, H));
    }

    // 「Card storage…」那幾行是遊戲稍後才填的，填完版面會移動，所以等它排好再放按鈕
    IEnumerator BuildCardTextLater(object os, Type osType, Canvas cv, float H)
    {
        var updText = osType.GetField("updateStatusText", Any).GetValue(os) as Text;
        float until = Time.realtimeSinceStartup + 3f;
        while (updText != null && string.IsNullOrEmpty(updText.text) && Time.realtimeSinceStartup < until) yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        if (cv == null) yield break;
        Canvas.ForceUpdateCanvases();
        try { BuildCardTextUI(os, osType, cv, H); }
        catch (Exception ex) { Debug.Log("[WSZH] card text UI failed: " + ex); }
    }

    // ---- 設定頁右下：「更新卡片翻譯」按鈕（放在模擬器「更新卡片」那一區的下方） ----
    Graphic cardStatus;
    bool cardBusy;

    Button cardUpd; Text cardUpdText; Canvas cardCanvas;
    RectTransform cardBtnRt, cardStatusRt;
    float cardLastBottom;

    // 「Card storage…」最後一行文字的下緣（螢幕座標）。按下「更新卡片」後這段文字會變長，所以要隨時重算
    float CardInfoBottom()
    {
        Vector2 pBtn = ScreenPos(cardUpd, cardCanvas);
        var urt = (RectTransform)cardUpd.transform;
        float bottom = pBtn.y - urt.rect.height * urt.lossyScale.y * 0.5f;
        if (cardUpdText != null && cardUpdText.gameObject.activeInHierarchy && !string.IsNullOrEmpty(cardUpdText.text))
        {
            // 文字框本身可能比實際文字大很多，用文字實際的高度算出最後一行的位置
            Camera cam = cardCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cardCanvas.worldCamera;
            var tr = (RectTransform)cardUpdText.transform;
            var corners = new Vector3[4];
            tr.GetWorldCorners(corners);
            float top = RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y;
            bottom = Mathf.Min(bottom, top - cardUpdText.preferredHeight * tr.lossyScale.y);
        }
        return bottom;
    }

    void PlaceCardTextUI()
    {
        if (cardBtnRt == null || cardUpd == null || cardCanvas == null) return;
        float bottom = CardInfoBottom();
        if (Mathf.Abs(bottom - cardLastBottom) < 0.5f) return;
        cardLastBottom = bottom;
        float cx = ScreenPos(cardUpd, cardCanvas).x;
        float h = cardBtnRt.rect.height * cardBtnRt.lossyScale.y;
        float w = cardBtnRt.rect.width * cardBtnRt.lossyScale.x;
        // 「更新卡片翻譯」和「日版卡圖」左右並排，下面是兩行狀態文字
        // 三顆按鈕並排：更新卡片翻譯｜日版卡圖開關｜下載牌組的日版卡圖
        PlaceAt(cardBtnRt, new Vector2(cx - w * 1.08f, bottom - h * 0.65f), cardCanvas);
        if (jpBtnRt != null) PlaceAt(jpBtnRt, new Vector2(cx, bottom - h * 0.65f), cardCanvas);
        if (prefetchRt != null) PlaceAt(prefetchRt, new Vector2(cx + w * 1.08f, bottom - h * 0.65f), cardCanvas);
        if (cardStatusRt != null) PlaceAt(cardStatusRt, new Vector2(cx, bottom - h * 1.55f), cardCanvas);
    }

    // 新建一顆按鈕，只沿用範本按鈕的圖樣與字型
    // （不直接複製：原按鈕上的遊戲元件會把位置與文字改回去）
    RectTransform MakeButton(Button src, Canvas cv, string name, string label, UnityEngine.Events.UnityAction onClick, out Graphic labelGraphic, Transform parent = null)
    {
        labelGraphic = null;
        var srcRt = (RectTransform)src.transform;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent ?? cv.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(srcRt.rect.width * srcRt.lossyScale.x / rt.lossyScale.x, srcRt.rect.height * srcRt.lossyScale.y / rt.lossyScale.y);
        var img = go.AddComponent<Image>();
        var srcImg = src.targetGraphic as Image ?? src.GetComponent<Image>();
        if (srcImg != null) { img.sprite = srcImg.sprite; img.type = srcImg.type; img.color = srcImg.color; img.pixelsPerUnitMultiplier = srcImg.pixelsPerUnitMultiplier; }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = src.transition; btn.colors = src.colors; btn.spriteState = src.spriteState;
        btn.onClick.AddListener(onClick);
        Graphic srcLabel = (Graphic)src.GetComponentInChildren<TMP_Text>(true) ?? src.GetComponentInChildren<Text>(true);
        if (srcLabel != null)
        {
            var lg = MakeText(srcLabel, go.transform, "Label");
            var lrt = (RectTransform)lg.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lgr = lg.GetComponent<Graphic>();
            lgr.raycastTarget = false;
            var lt = lgr as TMP_Text; if (lt != null) lt.alignment = TextAlignmentOptions.Center;
            var lu = lgr as Text; if (lu != null) lu.alignment = TextAnchor.MiddleCenter;
            SetTextOf(lgr, label);
            labelGraphic = lgr;
        }
        return rt;
    }

    // ---- 日版卡圖開關 ----
    RectTransform jpBtnRt;
    Graphic jpBtnLabel, cardBtnLabel;

    // 換語言後重新顯示按鈕與狀態文字（簡體／繁體）
    void RefreshCardTextLabels()
    {
        if (cardBtnLabel != null) SetTextOf(cardBtnLabel, Zh("更新卡片翻譯"));
        if (jpBtnLabel != null) SetTextOf(jpBtnLabel, JpArtLabel());
        if (fpsLabel != null) SetTextOf(fpsLabel, FpsLabel());
        updaterShown = null;
        ShowCardTextInfo();
    }

    static string JpArtLabel() { return Zh(WszhJpArt.Enabled ? "日版卡圖：開" : "日版卡圖：關"); }

    // ---- 下載牌組用到的日版卡圖（預先下載，對戰中就不用臨時下載） ----
    RectTransform prefetchRt;
    int shownPending = -1;
    void OnPrefetch()
    {
        if (!WszhJpArt.Enabled) { WszhJpArt.Enabled = true; if (jpBtnLabel != null) SetTextOf(jpBtnLabel, JpArtLabel()); }
        int n = 0;
        try { n = WszhJpArt.PrefetchDecks(); } catch (Exception ex) { Debug.Log("[WSZH] prefetch: " + ex.Message); }
        if (n == 0 && cardStatus != null) { SetTextOf(cardStatus, Zh("牌組用到的日版卡圖都已經下載好了")); shownPending = 0; }
        else ShowCardTextInfo();
    }

    void OnJpArtToggle()
    {
        WszhJpArt.Enabled = !WszhJpArt.Enabled;
        if (jpBtnLabel != null) SetTextOf(jpBtnLabel, JpArtLabel());
        ShowCardTextInfo();
    }

    IEnumerator FollowCardInfo()
    {
        while (cardBtnRt != null)
        {
            TranslateUpdaterStatus();
            PlaceCardTextUI();
            // 預先下載進行中：更新剩餘張數（每 0.5 秒）
            if (Time.frameCount % 30 == 0 && WszhJpArt.Pending != shownPending && shownPending > 0 && !cardBusy) ShowCardTextInfo();
            yield return null;
        }
    }

    // 「更新卡片」的狀態文字（Card storage…／Stage…／Card update complete…）。
    // 它是多行、內容一直變的長文字，翻譯檔的規則對不上（長文字的換行會被正規化），所以在這裡逐行翻
    string updaterShown;
    void TranslateUpdaterStatus()
    {
        if (cardUpdText == null) return;
        string cur = cardUpdText.text;
        if (string.IsNullOrEmpty(cur) || cur == updaterShown || ReadLanguage() == "en") return;
        var lines = cur.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++) lines[i] = UpdaterLine(lines[i]);
        string zh = string.Join("\n", lines);
        if (ReadLanguage() == "cn") zh = ToSimplified(zh);
        updaterShown = zh;
        if (zh != cur) cardUpdText.text = zh;
    }

    static readonly string[][] UpdaterFixed = {
        new[] { "Checking metadata", "檢查卡片資料" },
        new[] { "Checking installed images", "檢查已安裝的圖片" },
        new[] { "Downloading new images", "下載新圖片" },
        new[] { "Update complete", "更新完成" },
        new[] { "Update already running...", "已經在更新中…" },
        new[] { "Card update is already running.", "卡片更新已在進行中。" },
        new[] { "No card updater is assigned.", "找不到卡片更新程式。" },
        new[] { "Card update complete. Card database reloaded; restart is not required.", "卡片更新完成，已重新載入卡片資料，不需要重開遊戲。" },
        new[] { "never", "從未更新" },
        new[] { "unavailable", "無法取得" },
        new[] { "unavailable (no updater assigned)", "無法取得（沒有卡片更新程式）" },
        new[] { "loading...", "讀取中…" },
        new[] { "unknown", "未知" },
    };

    static string UpdaterValue(string v)
    {
        string t = v.Trim();
        foreach (var p in UpdaterFixed) if (t == p[0]) return p[1];
        return v;
    }

    static string UpdaterLine(string s)
    {
        foreach (var p in UpdaterFixed) if (s == p[0]) return p[1];
        var m = System.Text.RegularExpressions.Regex.Match(s, @"^(Card storage|Last card update run|Card repo latest commit): (.*)$");
        if (m.Success)
        {
            string label = m.Groups[1].Value == "Card storage" ? "卡片存放位置：" : m.Groups[1].Value == "Last card update run" ? "上次更新卡片：" : "卡片資料庫最新版本：";
            return label + UpdaterValue(m.Groups[2].Value);
        }
        m = System.Text.RegularExpressions.Regex.Match(s, @"^(Stage|Set): (.*?) \((\d+)%\)$");
        if (m.Success) return (m.Groups[1].Value == "Stage" ? "階段：" : "系列：") + UpdaterValue(m.Groups[2].Value) + "（" + m.Groups[3].Value + "%）";
        if (s.StartsWith("Update failed: ")) return "更新失敗：" + s.Substring(15);
        if (s.StartsWith("Card update completed, but the database could not be reloaded."))
            return "卡片更新完成，但無法重新載入卡片資料，請重開遊戲以載入新卡片。" + s.Substring(s.IndexOf("cards.") + 6);
        if (s.StartsWith("Large update detected: "))
            return "需要下載的系列太多（超過模擬器的安全上限），請依 README.txt 的初次安裝說明下載完整卡片資料，再用這裡更新。";
        return s;
    }

    // 這個檔案、CardText.cs、JpArt.cs 裡給玩家看的中文字的繁→簡對照（用 OpenCC 產生；新增文字時要補上）
    const string TradChars = "個側傷內動勝區問啟單圖場夾對幀庫張後從態戰戲換擇擊擬敗時暫會條構檔檢沒減灣為無狀獲畢確穩紀紅級組結經綠網線編與藍裝裡複視試語說請證譯讀變費資載輯這連進遊過選還錄長開間關階雲預題顏驗驟體黃";
    const string SimpChars = "个侧伤内动胜区问启单图场夹对帧库张后从态战戏换择击拟败时暂会条构档检没减湾为无状获毕确稳纪红级组结经绿网线编与蓝装里复视试语说请证译读变费资载辑这连进游过选还录长开间关阶云预题颜验骤体黄";
    internal static string ToSimplified(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s);
        for (int i = 0; i < sb.Length; i++) { int k = TradChars.IndexOf(sb[i]); if (k >= 0) sb[i] = SimpChars[k]; }
        return sb.ToString();
    }

    // 簡體中文模式時把我們自己顯示的文字轉成簡體
    static string Zh(string s) { return ReadLanguage() == "cn" ? ToSimplified(s) : s; }

    void BuildCardTextUI(object os, Type osType, Canvas cv, float H)
    {
        var upd = osType.GetField("updateCardsButton", Any).GetValue(os) as Button;
        var updText = osType.GetField("updateStatusText", Any).GetValue(os) as Text;
        if (upd == null) { Debug.Log("[WSZH] card text UI: update button not found"); return; }

        // 位置：「更新卡片」按鈕同一欄，Card storage 那幾行文字的下方
        Vector2 pBtn = ScreenPos(upd, cv);
        cardUpd = upd; cardUpdText = updText; cardCanvas = cv;
        float bottom = CardInfoBottom();

        var rt = MakeButton(upd, cv, "WSZH_CardTextButton", Zh("更新卡片翻譯"), OnCardTextUpdate, out cardBtnLabel);
        var btn = rt.GetComponent<Button>();
        cardBtnRt = rt;
        jpBtnRt = MakeButton(upd, cv, "WSZH_JpArtButton", JpArtLabel(), OnJpArtToggle, out jpBtnLabel);
        Graphic pfLabel;
        prefetchRt = MakeButton(upd, cv, "WSZH_JpPrefetchButton", Zh("下載牌組卡圖"), OnPrefetch, out pfLabel);

        // 狀態文字
        Graphic tmpl = (Graphic)updText ?? upd.GetComponentInChildren<TMP_Text>(true);
        var stGo = MakeText(tmpl, cv.transform, "WSZH_CardTextStatus");
        cardStatus = stGo.GetComponent<Graphic>();
        cardStatus.raycastTarget = false;
        float size = updText != null ? updText.fontSize : 20f;
        SetSizeOf(cardStatus, size);
        Normalize(cardStatus, rt.rect.width * 3f, size);
        cardStatusRt = (RectTransform)stGo.transform;
        cardLastBottom = float.NaN;
        PlaceCardTextUI();
        StartCoroutine(FollowCardInfo());
        ShowCardTextInfo();
        Debug.Log("[WSZH] card text UI: orig=" + pBtn + " active=" + upd.gameObject.activeInHierarchy
            + " bottom=" + bottom + " new=" + ScreenPos(btn, cv) + " screenH=" + Screen.height
            + " label=" + (upd.GetComponentInChildren<TMP_Text>(true) != null ? upd.GetComponentInChildren<TMP_Text>(true).text : "?"));
    }

    void ShowCardTextInfo()
    {
        if (cardStatus == null) return;
        int n = 0;
        try { n = WszhCardText.LineCount(); } catch { }
        string when = WszhCardText.LastUpdated();
        string lang = ReadLanguage();
        string line1;
        if (lang == "en") line1 = "Card text translation: not used in English";
        else if (n == 0) line1 = "卡片翻譯：尚未下載";
        else line1 = "卡片翻譯：" + n + " 條";
        int cnt; long bytes;
        WszhJpArt.CacheInfo(out cnt, out bytes);
        line1 += "｜日版卡圖：" + (cnt > 0 ? "已存 " + cnt + " 張（" + (bytes / 1048576) + " MB）" : "尚未下載");
        // 第二行說明日版卡圖的運作方式（會用到網路）
        int pending = WszhJpArt.Pending;
        shownPending = pending;
        string line2 = !WszhJpArt.Enabled
            ? "日版卡圖開啟後，牌組編輯看到的卡會連網從日版官網下載；對戰中不下載"
            : pending > 0 ? "日版卡圖下載中：還剩 " + pending + " 張（對戰中會暫停）"
            : "日版卡圖已開啟：牌組編輯看到的卡會自動下載，對戰中只用已下載的圖";
        SetTextOf(cardStatus, Zh(line1 + "\n" + line2));
    }

    void OnCardTextUpdate()
    {
        if (cardBusy) return;
        cardBusy = true;
        StartCoroutine(RunCardTextUpdate());
    }

    IEnumerator RunCardTextUpdate()
    {
        string last = null;
        var e = WszhCardText.Download(ReadLanguage(), s => { last = s; if (cardStatus != null) SetTextOf(cardStatus, Zh(s)); });
        while (true)
        {
            object cur;
            try { if (!e.MoveNext()) break; cur = e.Current; }
            catch (Exception ex) { last = "更新失敗：" + ex.Message; if (cardStatus != null) SetTextOf(cardStatus, Zh(last)); break; }
            yield return cur;
        }
        cardBusy = false;
    }

    // 新建一個文字物件，只沿用範本的字型、顏色與樣式
    // （不直接複製範本：範本上可能掛著遊戲自己的元件，會把位置改回原處）
    static GameObject MakeText(Graphic tmpl, Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tt = tmpl as TMP_Text;
        if (tt != null)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = tt.font; t.fontSharedMaterial = tt.fontSharedMaterial; t.color = tt.color;
            t.fontStyle = tt.fontStyle; t.fontSize = tt.fontSize;
        }
        else
        {
            var ut = (Text)tmpl;
            var u = go.AddComponent<Text>();
            u.font = ut.font; u.color = ut.color; u.fontStyle = ut.fontStyle; u.fontSize = ut.fontSize;
        }
        return go;
    }

    static void Normalize(Graphic g, float width, float size)
    {
        var rt = (RectTransform)g.transform;
        Vector3 keep = rt.position;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, size * 1.6f);
        rt.position = keep;
        var t = g as TMP_Text;
        if (t != null) { t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; }
        var u = g as Text;
        if (u != null) { u.alignment = TextAnchor.MiddleCenter; u.horizontalOverflow = HorizontalWrapMode.Overflow; u.verticalOverflow = VerticalWrapMode.Overflow; }
        foreach (var f in g.GetComponents<ContentSizeFitter>()) f.enabled = false;
    }

    static float SizeOf(Graphic g)
    {
        var t = g as TMP_Text; if (t != null) return t.fontSize;
        var u = g as Text; return u != null ? u.fontSize : 0;
    }

    static void SetSizeOf(Graphic g, float size)
    {
        var t = g as TMP_Text; if (t != null) { t.enableAutoSizing = false; t.fontSize = size; return; }
        var u = g as Text; if (u != null) { u.resizeTextForBestFit = false; u.fontSize = Mathf.RoundToInt(size); }
    }

    static void SetTextOf(Graphic g, string text)
    {
        var t = g as TMP_Text; if (t != null) { t.text = text; return; }
        var u = g as Text; if (u != null) u.text = text;
    }

    void OnPick(int i)
    {
        if (i < 0 || i >= Codes.Length || Codes[i] == current) return;
        string err;
        try { err = Switch(Codes[i]); }
        catch (Exception ex) { err = (ex.InnerException ?? ex).Message; }
        if (err == null) { current = Codes[i]; Status(Codes[i] == "en" ? "Switched to English" : "已切換為 " + Names[i]); RefreshCardTextLabels(); }
        else Status("切換失敗：" + err);
    }

    void Status(string msg)
    {
        if (status == null) return;
        SetTextOf(status, msg); statusShown = msg.Length > 0;
        statusUntil = Time.realtimeSinceStartup + 3f;
    }

    // 每一幀檢查：進到設定頁、而且還沒建立過選單時就建立（設定頁每次進入都是新的物件）
    GameObject built;
    bool building;
    // ---- 戰紀錄／重播畫面：翻譯外掛翻不到的文字 ----
    // 重播狀態列每一幀都重寫（翻譯外掛會因為變動太頻繁放棄翻譯），在 LateUpdate 遊戲寫完之後再翻；
    // 戰紀錄的「<b>Started:</b>\n日期」是標籤＋日期組成的一段文字，翻譯檔對不上，定時掃描替換。
    object replayCtl; Type replayCtlType; System.Reflection.FieldInfo replayStatusField; float replayLookAt, logScanAt;
    TextMeshProUGUI replayStatus; Color replayColor;

    void LateUpdate()
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try { LateUpdateCore(); }
        finally { WszhPerf.Add(3, t0); }
    }

    void LateUpdateCore()
    {
        try
        {
            string lang = null;
            // 只在重播畫面、戰紀錄畫面做事；其他畫面（尤其對戰中）什麼都不做，避免拖慢遊戲
            string scene = SceneManager.GetActiveScene().name;
            if (scene == "DeckEditorRedo" || scene == "Duel" || scene == "ReplayDuel") JpNamePreview(scene);
            bool inReplay = scene == "ReplayDuel", inLogs = scene == "GameLogs";
            if (!inReplay && !inLogs && replayCtl == null) return;
            if (inReplay && Time.realtimeSinceStartup >= replayLookAt && replayCtl == null)
            {
                replayLookAt = Time.realtimeSinceStartup + 1f;
                if (replayCtlType == null) replayCtlType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GameLogReplayController")).FirstOrDefault(x => x != null);
                if (replayCtlType != null) { replayCtl = FindObjectOfType(replayCtlType); replayStatusField = replayCtlType.GetField("statusText", Any); }
            }
            if (replayCtl != null && (UnityEngine.Object)replayCtl == null) { replayCtl = null; godBtn = null; godPanel = null; godImgs.Clear(); replayStatus = null; }   // 離開重播畫面
            if (replayCtl != null) GodView();
            if (replayCtl != null && replayStatusField != null)
            {
                // 遊戲每一幀都重寫狀態列，直接改它的文字會和遊戲（及翻譯外掛）互相覆蓋，播放中看不到。
                // 改成：原本的狀態列設成透明，另外疊一個我們自己的文字顯示翻譯後的內容
                var tg = replayStatusField.GetValue(replayCtl) as TMP_Text;
                if (tg != null)
                {
                    bool en = (lang = ReadLanguage()) == "en";
                    if (replayStatus == null && !en)
                    {
                        var go = MakeText(tg, tg.transform.parent, "WSZH_ReplayStatus");
                        replayStatus = go.GetComponent<TextMeshProUGUI>();
                        var rt = (RectTransform)go.transform; var src = tg.rectTransform;
                        rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
                        rt.sizeDelta = src.sizeDelta; rt.anchoredPosition = src.anchoredPosition;
                        var le = go.AddComponent<LayoutElement>(); le.ignoreLayout = true;
                        // 原本的字型沒有中文字：改用按鈕列上已經顯示中文的按鈕字型
                        var pb = replayCtl.GetType().GetField("perspectiveButton", Any).GetValue(replayCtl) as Button;
                        var pl = pb == null ? null : pb.GetComponentInChildren<TMP_Text>(true);
                        if (pl != null && pl.font != null) replayStatus.font = pl.font;
                        replayStatus.alignment = tg.alignment;
                        replayStatus.color = tg.color;
                        // 狀態列只有 14 像素高，中文行高比較高，允許超出、不換行
                        replayStatus.overflowMode = TextOverflowModes.Overflow;
                        replayStatus.enableWordWrapping = false;
                        replayStatus.raycastTarget = false;
                        replayColor = tg.color;
                    }
                    if (replayStatus != null)
                    {
                        replayStatus.gameObject.SetActive(!en);
                        var c = tg.color; c.a = en ? replayColor.a : 0f; tg.color = c;
                        string zh = en ? "" : Zh(ReplayStatusLine(tg.text ?? ""));
                        if (replayStatus.text != zh) replayStatus.text = zh;
                    }
                }
            }
            if (inLogs && Time.realtimeSinceStartup >= logScanAt)
            {
                logScanAt = Time.realtimeSinceStartup + 0.5f;
                if ((lang ?? (lang = ReadLanguage())) != "en")
                    foreach (var t in FindObjectsOfType<TMP_Text>())
                    {
                        string s = t.text;
                        if (string.IsNullOrEmpty(s) || s.IndexOf(":</b>", StringComparison.Ordinal) < 0) continue;
                        string zh = Zh(LogHeader(s));
                        if (zh != s) t.text = zh;
                    }
            }
        }
        catch { }
    }

    // ---- 重播的上帝視角：在畫面上方顯示對手手牌的卡圖 ----
    // 重播時對手的手牌在鏡頭外（畫面最上方），所以另外用一排小卡圖顯示；滑鼠移上去會在左邊預覽。
    // 只讀卡片的圖與資料，不改任何遊戲狀態，所以不影響重播驗證。
    bool godOn;
    RectTransform godBtn, godPanel;
    Graphic godLabel, godTitle;
    readonly List<RawImage> godImgs = new List<RawImage>();
    readonly List<Component> godShown = new List<Component>();
    static System.Reflection.FieldInfo fGame, fFront, fContents;

    string GodLabel()
    {
        if (ReadLanguage() == "en") return godOn ? "God view: On" : "God view: Off";
        return Zh(godOn ? "上帝視角：開" : "上帝視角：關");
    }

    void GodView()
    {
        var ctlType = replayCtl.GetType();
        Button persp = null, exit = null;
        if (godBtn == null)
        {
            persp = ctlType.GetField("perspectiveButton", Any).GetValue(replayCtl) as Button;
            exit = ctlType.GetField("exitButton", Any).GetValue(replayCtl) as Button;
            if (persp == null || exit == null) return;
            Canvas cv = persp.GetComponentInParent<Canvas>().rootCanvas;
            // 按鈕列沒有自動排版，新按鈕放在最左邊「暫停」的左側、間距和其他按鈕一樣
            var play = ctlType.GetField("playButton", Any).GetValue(replayCtl) as Button;
            godBtn = MakeButton(persp, cv, "WSZH_GodViewButton", GodLabel(), () => { godOn = !godOn; if (godLabel != null) SetTextOf(godLabel, GodLabel()); }, out godLabel, persp.transform.parent);
            var pr = (RectTransform)persp.transform; var er = (RectTransform)exit.transform;
            var lr = play != null ? (RectTransform)play.transform : er;
            godBtn.anchorMin = lr.anchorMin; godBtn.anchorMax = lr.anchorMax; godBtn.pivot = lr.pivot;
            godBtn.sizeDelta = lr.sizeDelta;
            godBtn.anchoredPosition = play != null ? lr.anchoredPosition - (er.anchoredPosition - pr.anchoredPosition)
                                                   : er.anchoredPosition + (er.anchoredPosition - pr.anchoredPosition);
            var lt = persp.GetComponentInChildren<TMP_Text>(true);
            if (godLabel != null && lt != null) { SetSizeOf(godLabel, lt.fontSize); var gl = godLabel as TMP_Text; if (gl != null) { gl.enableAutoSizing = lt.enableAutoSizing; gl.fontSizeMin = lt.fontSizeMin; gl.fontSizeMax = lt.fontSizeMax; } }

            // 對手手牌的顯示區（畫布最上層，按鈕列下方置中）
            var pgo = new GameObject("WSZH_GodHand", typeof(RectTransform));
            pgo.transform.SetParent(cv.transform, false);
            godPanel = (RectTransform)pgo.transform;
            godPanel.anchorMin = godPanel.anchorMax = new Vector2(0f, 1f);
            godPanel.pivot = new Vector2(0f, 1f);
            var bg = pgo.AddComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.45f); bg.raycastTarget = false;
            var tgo = MakeText(lt != null ? (Graphic)lt : godLabel, godPanel, "Title");
            godTitle = tgo.GetComponent<Graphic>(); godTitle.raycastTarget = false; godTitle.color = Color.white;
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(0f, 26f); trt.anchoredPosition = new Vector2(0f, -2f);
            var tt = godTitle as TMP_Text; if (tt != null) { tt.alignment = TextAlignmentOptions.Center; tt.enableAutoSizing = false; tt.fontSize = 20; }
            pgo.SetActive(false);
        }
        if (godPanel == null) return;

        var cards = new List<Component>();
        Component local = null;
        if (godOn)
        {
            if (fGame == null) fGame = ctlType.GetField("game", Any);
            var game = fGame == null ? null : fGame.GetValue(replayCtl) as Component;
            local = game == null ? null : game.GetType().GetField("localPlayer", Any).GetValue(game) as Component;
            var opp = local == null ? null : local.GetType().GetProperty("opponent", Any).GetValue(local, null) as Component;
            var hand = opp == null ? null : opp.GetType().GetField("hand", Any).GetValue(opp) as Component;
            if (hand != null)
            {
                if (fContents == null) fContents = hand.GetType().GetField("contents", Any);
                var list = fContents == null ? null : fContents.GetValue(hand) as IEnumerable;
                if (list != null) foreach (var o in list) { var c = o as Component; if (c != null) cards.Add(c); }
            }
        }
        godPanel.gameObject.SetActive(godOn);
        if (!godOn) return;

        // 放在左邊卡片預覽區右側的空白處（上方的提示訊息下面），兩欄往下排；大小依畫布縮放
        var prt = (RectTransform)godPanel.parent;
        float scale = prt.rect.height / 1080f, sx = prt.rect.width / 1920f;
        float cw = 82f * scale, ch = cw * 1.395f, gap = 5f * scale, top = 28f * scale;
        int n = cards.Count, cols = 2, rows = Mathf.Max(1, (n + cols - 1) / cols);
        godPanel.sizeDelta = new Vector2(cols * (cw + gap) + gap, top + rows * (ch + gap) + gap);
        godPanel.anchoredPosition = new Vector2(468f * sx, -215f * scale);
        SetTextOf(godTitle, ReadLanguage() == "en" ? "Opponent's hand " + n : Zh("對手手牌 " + n + " 張"));
        if (godLabel != null) { string gl = GodLabel(); if (!(godLabel is TMP_Text) || ((TMP_Text)godLabel).text != gl) SetTextOf(godLabel, gl); }
        while (godImgs.Count < n)
        {
            var go = new GameObject("Card", typeof(RectTransform));
            go.transform.SetParent(godPanel, false);
            var img = go.AddComponent<RawImage>();
            int idx = godImgs.Count;
            var trig = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            e.callback.AddListener(_ => PreviewGodCard(idx));
            trig.triggers.Add(e);
            godImgs.Add(img);
        }
        godShown.Clear(); godShown.AddRange(cards);
        for (int i = 0; i < godImgs.Count; i++)
        {
            var img = godImgs[i];
            bool on = i < n;
            img.gameObject.SetActive(on);
            if (!on) continue;
            var rt = (RectTransform)img.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(cw, ch);
            rt.anchoredPosition = new Vector2(gap + (i % cols) * (cw + gap), -top - (i / cols) * (ch + gap));
            img.texture = CardTexture(cards[i]);
        }
    }

    static Texture CardTexture(Component card)
    {
        if (fFront == null) fFront = card.GetType().GetField("frontFace", Any);
        var go = fFront == null ? null : fFront.GetValue(card) as GameObject;
        var mr = go == null ? null : go.GetComponent<MeshRenderer>();
        return mr == null || mr.sharedMaterial == null ? null : mr.sharedMaterial.GetTexture("_MainTex");
    }

    void PreviewGodCard(int i)
    {
        try
        {
            if (i >= godShown.Count || replayCtl == null) return;
            var game = fGame.GetValue(replayCtl) as Component;
            var local = game.GetType().GetField("localPlayer", Any).GetValue(game) as Component;
            var ui = local.GetType().GetField("ui", Any).GetValue(local);
            var m = ui.GetType().GetMethod("PreviewCardInGame", Any);
            if (m != null) m.Invoke(ui, new object[] { godShown[i] });
        }
        catch (Exception ex) { Debug.Log("[WSZH] god view preview: " + ex.Message); }
    }

    // ---- 卡片預覽：遊戲資料沒有日文卡名的卡（例如 MARVEL），在說明最下面補上日文名（補充資料，見 Names.cs） ----
    // 遊戲本身只在 card.japaneseName 有值時才顯示；這裡在遊戲寫完說明之後補一行，只影響顯示
    string jpScene; float jpLookAt; Component[] jpUis = new Component[0];
    static System.Reflection.FieldInfo fViewing, fDesc, fShowJp;
    void JpNamePreview(string scene)
    {
        if (ReadLanguage() == "en") return;
        if (scene != jpScene || (jpUis.Length == 0 && Time.realtimeSinceStartup >= jpLookAt))
        {
            jpScene = scene; jpLookAt = Time.realtimeSinceStartup + 1f;
            Type t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PlayerUI")).FirstOrDefault(x => x != null);
            if (t == null) return;
            fViewing = t.GetField("viewingCard", Any); fDesc = t.GetField("cardDescriptionText", Any); fShowJp = t.GetField("showJPNames", Any);
            jpUis = FindObjectsOfType(t).Cast<Component>().ToArray();
        }
        if (fViewing == null || fDesc == null || fShowJp == null) return;
        foreach (var ui in jpUis)
        {
            if (ui == null || !(bool)fShowJp.GetValue(ui)) continue;
            var card = fViewing.GetValue(ui);
            if (card == null || WszhNames.HasGameJapaneseName(card)) continue;
            string jp = WszhNames.SupplementName(card.GetType().GetField("id").GetValue(card) as string);
            var desc = fDesc.GetValue(ui) as TMP_Text;
            if (string.IsNullOrEmpty(jp) || desc == null) continue;
            string s = desc.text ?? "";
            if (!s.EndsWith(jp)) desc.text = s + "\n" + jp;
        }
    }

    static readonly string[][] ReplayStates = {
        new[] { "Playing partial recording", "播放中（紀錄不完整）" },
        new[] { "Playing", "播放中" },
        new[] { "Finished", "已結束" },
        new[] { "End of partial recording (an input was not completed)", "不完整的紀錄已播放完畢（有操作沒有完成）" },
        new[] { "End of partial recording", "不完整的紀錄已播放完畢" },
        new[] { "Replay complete - draw", "重播完畢 - 平手" },
        new[] { "Replay ended without reproducing its recorded result", "重播結束，但結果和紀錄不同" },
        new[] { "Replay ended without reproducing its recorded result.", "重播結束，但結果和紀錄不同" },
        new[] { "Replay stopped before its recorded result", "重播在紀錄的結果之前停止" },
        new[] { "Replay stopped before its recorded result.", "重播在紀錄的結果之前停止" },
        new[] { "Different build; validating inputs/state", "模擬器版本不同，正在驗證操作與狀態" },
    };

    // 「Paused - Playing partial recording | Turn 0/6 | Verified: 0 | Player 1 | 0%」
    static string ReplayStatusLine(string s)
    {
        string head = "";
        var m = System.Text.RegularExpressions.Regex.Match(s, @"^(Paused - |Seeking turn (\d+) - )");
        if (m.Success) { head = m.Groups[2].Success ? "跳到第 " + m.Groups[2].Value + " 回合 - " : "已暫停 - "; s = s.Substring(m.Length); }
        var parts = s.Split(new[] { " | " }, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            // 「Input differs at #161 (EndPhase/accepted). Expected context …」：模擬器重播時操作和紀錄對不上
            p = System.Text.RegularExpressions.Regex.Replace(p, @"Input differs at #(\d+) \([^)]*\)\..*$", "第 $1 步的操作和紀錄不一致（模擬器重播功能的問題）");
            if (p.StartsWith("重播已停止：")) goto done;           // 翻譯外掛已經翻過前半段
            foreach (var r in ReplayStates) if (p == r[0]) { p = r[1]; goto done; }
            if ((m = System.Text.RegularExpressions.Regex.Match(p, @"^Turn (\d+)/(\d+)$")).Success) p = "回合 " + m.Groups[1].Value + "/" + m.Groups[2].Value;
            else if ((m = System.Text.RegularExpressions.Regex.Match(p, @"^Verified: (\d+)$")).Success) p = "已驗證：" + m.Groups[1].Value;
            else if ((m = System.Text.RegularExpressions.Regex.Match(p, @"^Player ([12])$")).Success) p = "玩家 " + m.Groups[1].Value;
            else if ((m = System.Text.RegularExpressions.Regex.Match(p, @"^Replay complete - Player ([12]) wins$")).Success) p = "重播完畢 - 玩家 " + m.Groups[1].Value + " 獲勝";
            else if (p.StartsWith("Replay stopped: ")) p = "重播已停止：" + p.Substring(16);
            else if (p.StartsWith("Replay unavailable: ")) p = "無法重播：" + p.Substring(20);
            else if ((m = System.Text.RegularExpressions.Regex.Match(p, @"^End of partial recording \((?:action|decision) #(\d+) was not completed\)$")).Success) p = "不完整的紀錄已播放完畢（第 " + m.Groups[1].Value + " 步沒有完成）";
            else if (p.StartsWith("Recording ended: ")) p = "紀錄到此結束（" + p.Substring(17).Replace(" (no match result)", "，沒有對戰結果") + "）";
        done:
            parts[i] = p;
        }
        return head + string.Join(" ｜ ", parts);
    }

    static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    // 「<b>Started:</b>\nOct 25, 2026 1:12 AM」「<b>Duration:</b> 0h 0m 0s」
    static string LogHeader(string s)
    {
        s = s.Replace("<b>Started:</b>", "<b>開始時間：</b>").Replace("<b>Ended:</b>", "<b>結束時間：</b>").Replace("<b>Duration:</b>", "<b>對戰時長：</b>")
             .Replace("<b>Mode:</b>", "<b>模式：</b>");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) (\d{1,2}), (\d{4}) (\d{1,2}):(\d{2}) (AM|PM)", m =>
        {
            if (m.Groups[3].Value == "1970") return "（沒有紀錄）";           // 對戰沒有正常結束時，結束時間是 0
            return m.Groups[3].Value + "/" + (Array.IndexOf(Months, m.Groups[1].Value) + 1) + "/" + m.Groups[2].Value + " "
                + (m.Groups[6].Value == "AM" ? "上午 " : "下午 ") + m.Groups[4].Value + ":" + m.Groups[5].Value;
        });
        return System.Text.RegularExpressions.Regex.Replace(s, @"\b(\d+)h (\d+)m (\d+)s\b", "$1 小時 $2 分 $3 秒");
    }

    // ---- 中文字型的查找順序（2026-10-27 找到的卡頓主因）----
    // 遊戲的 TMP 字型（LiberationSans SDF）找不到中文字時，會先去「LiberationSans SDF - Fallback」這個動態字型找；
    // 它本身沒有中文字，但每次都要用字型引擎讀字檔確認，才輪到翻譯外掛加進來的中文字型（arialuni）。
    // 一段 300 字的卡片說明排版一次要 10ms 以上，牌組編輯滑鼠一移動就頓。
    // 把中文字型插到每個字型的備用清單最前面，中文字一次就找到。
    float fontFixAt;
    readonly HashSet<int> fontFixed = new HashSet<int>();
    void FixFontFallbacks()
    {
        if (Time.realtimeSinceStartup < fontFixAt) return;
        fontFixAt = Time.realtimeSinceStartup + 2f;            // 新場景可能載入新字型，每 2 秒檢查一次（很便宜）
        TMP_FontAsset cjk = null;
        var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in all) if (f != null && f.name.IndexOf("arialuni", StringComparison.OrdinalIgnoreCase) >= 0) { cjk = f; break; }
        if (cjk == null) return;
        foreach (var f in all)
        {
            if (f == null || f == cjk || fontFixed.Contains(f.GetInstanceID())) continue;
            if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new List<TMP_FontAsset>();
            var fb = f.fallbackFontAssetTable;
            fb.Remove(cjk);
            fb.Insert(0, cjk);
            fontFixed.Add(f.GetInstanceID());
            Debug.Log("[WSZH] font fallback: " + cjk.name + " first for " + f.name);
        }
    }

    // 卡片預覽顯示日文卡名：遊戲本來就有（PlayerUI.showJPNames，按 Ctrl+J 切換），只是預設關閉；
    // 中文模式下每進一個畫面就幫它打開（之後按 Ctrl+J 仍可關掉）
    string jpNameScene;
    IEnumerator ShowJpNamesLater()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        try
        {
            Type t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PlayerUI")).FirstOrDefault(x => x != null);
            var f = t == null ? null : t.GetField("showJPNames", Any);
            if (f != null) foreach (var ui in FindObjectsOfType(t)) f.SetValue(ui, true);
        }
        catch { }
    }

    void Update()
    {
        WszhPerf.Frame();
        string sceneNow = SceneManager.GetActiveScene().name;
        if (sceneNow != jpNameScene)
        {
            jpNameScene = sceneNow;
            if (ReadLanguage() != "en") StartCoroutine(ShowJpNamesLater());
        }
        try { if (ReadLanguage() != "en") FixFontFallbacks(); } catch { }
        WszhJpArt.Tick();
        if (!building && built == null && SceneManager.GetActiveScene().name == "Options")
        {
            building = true;
            StartCoroutine(BuildLater());
        }
        if (status != null && statusShown && Time.realtimeSinceStartup > statusUntil) { SetTextOf(status, ""); statusShown = false; }

        // 開發用：Ctrl+Shift+F12 匯出所有卡片的效果文字（給卡片翻譯用，見 CardText.cs）
        if (Input.GetKeyDown(KeyCode.F12) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            string err;
            try { err = WszhCardText.Dump(); }
            catch (Exception ex) { err = (ex.InnerException ?? ex).Message; }
            Debug.Log("[WSZH] card text dump " + (err == null ? "done" : "failed: " + err));
        }
    }
}
