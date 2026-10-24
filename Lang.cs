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
    }

    // ---- XUnity 存取 ----
    static Assembly Xu()
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "XUnity.AutoTranslator.Plugin.Core");
    }

    static Type XuType(string name)
    {
        var a = Xu();
        return a == null ? null : a.GetType("XUnity.AutoTranslator.Plugin.Core." + name);
    }

    static string ReadLanguage()
    {
        try
        {
            var s = XuType("Configuration.Settings");
            return s == null ? null : (string)s.GetField("Language", Any).GetValue(null);
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
        Place((RectTransform)titleGo.transform, new Vector2(cx, by - H * 0.085f), cv);

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
        ddRt.sizeDelta = new Vector2(backRt.rect.width * 1.05f, backRt.rect.height);
        Place(ddRt, new Vector2(cx, by - H * 0.155f), cv);

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
        Place((RectTransform)stGo.transform, new Vector2(cx, by - H * 0.215f), cv);
        StartCoroutine(Replace());
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
        if (err == null) { current = Codes[i]; Status(Codes[i] == "en" ? "Switched to English" : "已切換為 " + Names[i]); }
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
    void Update()
    {
        if (!building && built == null && SceneManager.GetActiveScene().name == "Options")
        {
            building = true;
            StartCoroutine(BuildLater());
        }
        if (status != null && statusShown && Time.realtimeSinceStartup > statusUntil) { SetTextOf(status, ""); statusShown = false; }
    }
}
