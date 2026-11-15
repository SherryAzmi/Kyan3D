using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Kayyan UI kit: colours, fonts and small widgets (cards, buttons, sliders, switches, controller glyphs)
/// drawn in code, so every menu looks the same and needs no prefabs or sprite files.
/// Look: warm paper cards, brown ink outlines, chunky "toy" shadows, Fredoka text, Bagel Fat One titles.
/// </summary>
public static class KUI
{
    // ------------------------------------------------------------------ colours
    public static readonly Color Paper = Hex("f7ecd3");
    public static readonly Color Paper2 = Hex("ecd9b2");
    public static readonly Color Cream = Hex("fffaf0");
    public static readonly Color Ink = Hex("3b2a1b");
    public static readonly Color Ink2 = Hex("7a6249");
    public static readonly Color Grass = Hex("3f8f2e");
    public static readonly Color Grass2 = Hex("276b22");
    public static readonly Color Bloom = Hex("f3c730");
    public static readonly Color Clay = Hex("c4553a");
    public static readonly Color ClaySoft = Hex("f1c6b4");
    public static readonly Color Sand = Hex("d4c092");
    public static readonly Color Night = Hex("141833");
    public static readonly Color PadBlue = Hex("3a63c8");
    public static readonly Color PadPink = Hex("c24d8f");
    public static readonly Color PadRed = Hex("c4553a");
    public static readonly Color PadGreen = Hex("2f9a62");

    public static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        return c;
    }

    // ------------------------------------------------------------------ fonts
    static Font body, title;

    /// <summary>Fredoka: the font for all text in the game.</summary>
    public static Font Body
    {
        get
        {
            if (!body) body = Resources.Load<Font>("Fonts/Fredoka");
            if (!body) body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return body;
        }
    }

    /// <summary>Bagel Fat One: titles only.</summary>
    public static Font Title
    {
        get
        {
            if (!title) title = Resources.Load<Font>("Fonts/BagelFatOne");
            if (!title) title = Body;
            return title;
        }
    }

    // ------------------------------------------------------------------ canvas
    public static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        return c;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current || UnityEngine.Object.FindFirstObjectByType<EventSystem>()) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    public static void Select(GameObject go)
    {
        if (EventSystem.current && go) EventSystem.current.SetSelectedGameObject(go);
    }

    // ------------------------------------------------------------------ layout helpers
    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
        return rt;
    }

    /// <summary>Top-left anchored placement: x to the right, y downwards.</summary>
    public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static RectTransform Center(RectTransform rt, float w, float h, float x = 0, float y = 0)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    // ------------------------------------------------------------------ sprites
    static readonly Dictionary<int, Sprite> rounds = new Dictionary<int, Sprite>();
    static readonly Dictionary<string, Sprite> symbols = new Dictionary<string, Sprite>();
    static Sprite circle;

    /// <summary>A white rounded rectangle for 9-slicing (corner radius in pixels).</summary>
    public static Sprite Round(int r)
    {
        r = Mathf.Max(2, r);
        if (rounds.TryGetValue(r, out var s) && s) return s;
        int size = r * 2 + 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px_ = Mathf.Abs(x + 0.5f - half) - (half - r);
                float py_ = Mathf.Abs(y + 0.5f - half) - (half - r);
                float d = new Vector2(Mathf.Max(px_, 0f), Mathf.Max(py_, 0f)).magnitude + Mathf.Min(Mathf.Max(px_, py_), 0f) - r;
                px[y * size + x] = (Color32)new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        rounds[r] = s;
        return s;
    }

    public static Sprite Circle
    {
        get
        {
            if (circle) return circle;
            circle = Paint(128, p => p.magnitude - 1f);
            return circle;
        }
    }

    /// <summary>Paints a 128px white symbol from a signed distance function over -1..1 (negative = inside).</summary>
    static Sprite Paint(int size, Func<Vector2, float> sdf)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float d = sdf(p) * size * 0.5f;
                px[y * size + x] = (Color32)new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    static float Box(Vector2 p, Vector2 half, float r)
    {
        var q = new Vector2(Mathf.Abs(p.x) - half.x + r, Mathf.Abs(p.y) - half.y + r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    /// <summary>Named symbol sprites (white, tint with Image.color): cross, square, ring, triangle, gear, pad.</summary>
    public static Sprite Symbol(string kind)
    {
        if (symbols.TryGetValue(kind, out var s) && s) return s;
        switch (kind)
        {
            case "cross":
                s = Paint(128, p => Mathf.Min(Capsule(p, new Vector2(-0.46f, -0.46f), new Vector2(0.46f, 0.46f), 0.13f),
                                              Capsule(p, new Vector2(-0.46f, 0.46f), new Vector2(0.46f, -0.46f), 0.13f)));
                break;
            case "square":
                s = Paint(128, p => Mathf.Abs(Box(p, new Vector2(0.52f, 0.52f), 0.12f) + 0.1f) - 0.1f);
                break;
            case "ring":
                s = Paint(128, p => Mathf.Abs(p.magnitude - 0.5f) - 0.12f);
                break;
            case "triangle":
                s = Paint(128, p =>
                {
                    var a = new Vector2(0f, 0.58f); var b = new Vector2(-0.58f, -0.4f); var c = new Vector2(0.58f, -0.4f);
                    return Mathf.Min(Capsule(p, a, b, 0.1f), Mathf.Min(Capsule(p, b, c, 0.1f), Capsule(p, c, a, 0.1f)));
                });
                break;
            case "gear":
                s = Paint(128, p =>
                {
                    float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
                    float tooth = Mathf.Clamp01(Mathf.Cos(a * 8f) * 2.2f + 0.5f);
                    float outer = r - (0.66f + 0.2f * tooth);
                    return Mathf.Max(outer, -(r - 0.26f));
                });
                break;
            case "pad":
                s = Paint(128, p =>
                {
                    float body = Box(p - new Vector2(0f, 0.12f), new Vector2(0.86f, 0.4f), 0.28f);
                    float gl = (p - new Vector2(-0.58f, -0.3f)).magnitude - 0.3f;
                    float gr = (p - new Vector2(0.58f, -0.3f)).magnitude - 0.3f;
                    float d = Mathf.Min(body, Mathf.Min(gl, gr));
                    float dpad = Mathf.Min(Capsule(p, new Vector2(-0.62f, 0.14f), new Vector2(-0.32f, 0.14f), 0.075f),
                                           Capsule(p, new Vector2(-0.47f, -0.01f), new Vector2(-0.47f, 0.29f), 0.075f));
                    float btn = Mathf.Min(Mathf.Min((p - new Vector2(0.47f, 0.28f)).magnitude, (p - new Vector2(0.47f, 0f)).magnitude) - 0.075f,
                                          Mathf.Min((p - new Vector2(0.33f, 0.14f)).magnitude, (p - new Vector2(0.61f, 0.14f)).magnitude) - 0.075f);
                    return Mathf.Max(d, -Mathf.Min(dpad, btn));
                });
                break;
            default:
                s = Circle;
                break;
        }
        symbols[kind] = s;
        return s;
    }

    // ------------------------------------------------------------------ primitives
    public static Image Img(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
    {
        var rt = Rect(parent, name);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        if (sprite)
        {
            img.sprite = sprite;
            img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    public static Text Label(Transform parent, string text, int size, Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter, bool titleFont = false, FontStyle style = FontStyle.Normal)
    {
        var rt = Rect(parent, "Text");
        var t = rt.gameObject.AddComponent<Text>();
        t.font = titleFont ? Title : Body;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    /// <summary>
    /// A paper-style box: a hard shadow below, an ink border and a fill. Put content into the returned rect.
    /// The fill is at box.Find("Fill").
    /// </summary>
    public static RectTransform Box(Transform parent, string name, Color fill, int radius, float border = 4f, float shadow = 8f)
    {
        var root = Rect(parent, name);
        if (shadow > 0f)
        {
            var sh = Img(root, "Shadow", Ink, Round(radius));
            sh.rectTransform.anchorMin = Vector2.zero; sh.rectTransform.anchorMax = Vector2.one;
            sh.rectTransform.offsetMin = new Vector2(0f, -shadow); sh.rectTransform.offsetMax = new Vector2(0f, -shadow);
        }
        var b = Img(root, "Border", Ink, Round(radius));
        Stretch(b.rectTransform);
        var f = Img(root, "Fill", fill, Round(Mathf.Max(2, radius - Mathf.RoundToInt(border))), true);
        Stretch(f.rectTransform, border, border, border, border);
        return root;
    }

    public static Image FillOf(RectTransform box) => box.Find("Fill").GetComponent<Image>();

    /// <summary>A paper card with a dashed-looking inner line is overkill in uGUI: a plain card with a soft inner border.</summary>
    public static RectTransform Card(Transform parent, string name, float w, float h)
    {
        var box = Box(parent, name, Paper, 34, 6f, 12f);
        var inner = Img(box, "Inner", new Color(Ink.r, Ink.g, Ink.b, 0.16f), Round(26));
        Stretch(inner.rectTransform, 14, 14, 14, 14);
        var innerFill = Img(box, "InnerFill", Paper, Round(24));
        Stretch(innerFill.rectTransform, 16, 16, 16, 16);
        return Center(box, w, h);
    }

    // ------------------------------------------------------------------ buttons
    public static Button MakeButton(Transform parent, string label, Color fill, Color textColor, float w, float h,
        int fontSize, Action onClick)
    {
        var box = Box(parent, "Button " + label, fill, Mathf.RoundToInt(h * 0.3f), 4f, 7f);
        box.sizeDelta = new Vector2(w, h);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h;
        var btn = box.gameObject.AddComponent<Button>();
        btn.targetGraphic = FillOf(box);
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.96f, 0.84f);
        cb.selectedColor = new Color(1f, 0.96f, 0.84f);
        cb.pressedColor = new Color(0.8f, 0.78f, 0.7f);
        cb.fadeDuration = 0.06f;
        btn.colors = cb;
        var t = Label(box, label, fontSize, textColor, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        Stretch(t.rectTransform);
        box.gameObject.AddComponent<ButtonFX>();
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        return btn;
    }

    public static Button IconButton(Transform parent, string symbol, float size, Action onClick)
    {
        var box = Box(parent, "Icon " + symbol, Paper, Mathf.RoundToInt(size * 0.5f), 4f, 6f);
        box.sizeDelta = new Vector2(size, size);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size; le.preferredHeight = size;
        var btn = box.gameObject.AddComponent<Button>();
        btn.targetGraphic = FillOf(box);
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.93f, 0.7f); cb.selectedColor = new Color(1f, 0.93f, 0.7f);
        cb.pressedColor = new Color(0.8f, 0.76f, 0.6f); cb.fadeDuration = 0.06f;
        btn.colors = cb;
        var ic = Img(box, "Symbol", Ink, Symbol(symbol));
        Center(ic.rectTransform, size * 0.56f, size * 0.56f);
        box.gameObject.AddComponent<ButtonFX>();
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        return btn;
    }

    // ------------------------------------------------------------------ slider and switch
    public static Slider MakeSlider(Transform parent, float w, float value, Action<float> onChange)
    {
        var root = Rect(parent, "Slider");
        root.sizeDelta = new Vector2(w, 44f);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = 44f;

        var track = Box(root, "Track", Cream, 11, 3f, 0f);
        track.anchorMin = new Vector2(0f, 0.5f); track.anchorMax = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(0f, -11f); track.offsetMax = new Vector2(0f, 11f);

        var fillArea = Rect(root, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(5f, -8f); fillArea.offsetMax = new Vector2(-5f, 8f);
        var fill = Img(fillArea, "Fill", Grass, Round(8));
        fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

        var slideArea = Rect(root, "Handle Slide Area");
        // Slider stretches the handle over this area's height, so the area is exactly as tall as the handle
        slideArea.anchorMin = new Vector2(0f, 0.5f); slideArea.anchorMax = new Vector2(1f, 0.5f);
        slideArea.offsetMin = new Vector2(20f, -20f); slideArea.offsetMax = new Vector2(-20f, 20f);
        var handle = Box(slideArea, "Handle", Bloom, 20, 4f, 4f);
        handle.sizeDelta = new Vector2(40f, 0f);

        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle;
        s.targetGraphic = FillOf(handle);
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f;
        s.value = value;
        s.onValueChanged.AddListener(v => { UISound.Tick(); onChange?.Invoke(v); });
        return s;
    }

    public static SwitchToggle MakeSwitch(Transform parent, bool on, Action<bool> onChange)
    {
        var box = Box(parent, "Switch", Hex("d6c6a4"), 22, 4f, 5f);
        box.sizeDelta = new Vector2(88f, 46f);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 88f; le.preferredHeight = 46f;
        var knob = Box(box, "Knob", Color.white, 15, 3f, 0f);
        Center(knob, 30f, 30f);
        var sw = box.gameObject.AddComponent<SwitchToggle>();
        sw.knob = knob;
        sw.track = FillOf(box);
        var btn = box.gameObject.AddComponent<Button>();
        btn.targetGraphic = FillOf(box);
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => sw.Set(!sw.On, true));
        sw.onChanged = onChange;
        sw.Set(on, false);
        return sw;
    }

    // ------------------------------------------------------------------ controller glyphs
    /// <summary>
    /// A controller button glyph: "cross", "square", "circle", "triangle", or text ones like "R2", "L", "R", "Start".
    /// </summary>
    public static RectTransform PadGlyph(Transform parent, string kind, float size)
    {
        RectTransform box;
        float w = size;
        switch (kind)
        {
            case "cross":
            case "square":
            case "circle":
            case "triangle":
            {
                Color c = kind == "cross" ? PadBlue : kind == "square" ? PadPink : kind == "circle" ? PadRed : PadGreen;
                box = Box(parent, "Glyph " + kind, Cream, Mathf.RoundToInt(size * 0.5f), 3.5f, 4f);
                var ic = Img(box, "Symbol", c, Symbol(kind == "circle" ? "ring" : kind));
                Center(ic.rectTransform, size * 0.66f, size * 0.66f);
                break;
            }
            case "R2":
            case "L2":
            case "R1":
            case "L1":
                w = size * 1.45f;
                box = Box(parent, "Glyph " + kind, Cream, Mathf.RoundToInt(size * 0.3f), 3.5f, 4f);
                Stretch(Label(box, kind, Mathf.RoundToInt(size * 0.5f), Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform);
                break;
            default: // "L" / "R" sticks and anything else
                box = Box(parent, "Glyph " + kind, Cream, Mathf.RoundToInt(size * 0.5f), 3.5f, 4f);
                var inner = Img(box, "Cap", Paper2, Circle);
                Center(inner.rectTransform, size * 0.68f, size * 0.68f);
                Stretch(Label(box, kind, Mathf.RoundToInt(size * 0.42f), Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform);
                break;
        }
        box.sizeDelta = new Vector2(w, size);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = size;
        return box;
    }

    /// <summary>A keyboard key cap.</summary>
    public static RectTransform KeyCap(Transform parent, string label, float h)
    {
        float w = Mathf.Max(h, h * 0.42f + label.Length * h * 0.36f);
        var box = Box(parent, "Key " + label, Cream, Mathf.RoundToInt(h * 0.25f), 3f, 5f);
        box.sizeDelta = new Vector2(w, h);
        Stretch(Label(box, label, Mathf.RoundToInt(h * 0.46f), Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h;
        return box;
    }

    public static HorizontalLayoutGroup Row(Transform parent, string name, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var rt = Rect(parent, name);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = align;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        return h;
    }
}

/// <summary>Hover and keyboard/pad focus: the button grows a little.</summary>
public class ButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
{
    float target = 1f;
    bool hover, selected;

    public void OnPointerEnter(PointerEventData e) { hover = true; Refresh(); if (EventSystem.current) EventSystem.current.SetSelectedGameObject(gameObject); }
    public void OnPointerExit(PointerEventData e) { hover = false; Refresh(); }
    public void OnSelect(BaseEventData e) { selected = true; Refresh(); UISound.Hover(); }
    public void OnPointerClick(PointerEventData e) => UISound.Click();
    public void OnSubmit(BaseEventData e) => UISound.Click();
    public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
    void Refresh() => target = hover || selected ? 1.05f : 1f;
    void OnEnable() { transform.localScale = Vector3.one; target = 1f; hover = selected = false; }

    void Update()
    {
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 1.2f);
        transform.localScale = new Vector3(s, s, 1f);
    }
}

/// <summary>An on/off switch with a sliding knob.</summary>
public class SwitchToggle : MonoBehaviour
{
    public RectTransform knob;
    public Image track;
    public Action<bool> onChanged;
    public bool On { get; private set; }
    float pos;

    public void Set(bool on, bool notify)
    {
        On = on;
        if (!notify) pos = on ? 1f : 0f;
        if (notify) { UISound.Click(); onChanged?.Invoke(on); }
        Apply();
    }

    void Update()
    {
        pos = Mathf.MoveTowards(pos, On ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    void Apply()
    {
        if (knob) knob.anchoredPosition = new Vector2(Mathf.Lerp(-18f, 18f, pos), 0f);
        if (track) track.color = Color.Lerp(KUI.Hex("d6c6a4"), KUI.Clay, pos);
    }
}
