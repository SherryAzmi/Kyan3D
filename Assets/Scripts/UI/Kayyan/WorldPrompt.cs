using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The button prompt that floats above whatever a player can use (a sand mound, the river, a seed, the mirror...).
/// It shows the controller icon (R2, Square...) or, with no controller plugged in, the keyboard key, plus a short
/// text: "Hold [R2] to dig". One bubble per player, drawn only inside that player's half of the split screen.
/// PlayerInteractor feeds it every frame; it installs itself.
/// </summary>
public class WorldPrompt : MonoBehaviour
{
    static WorldPrompt inst;

    class Bubble
    {
        public RectTransform wrap, box, content;
        public CanvasGroup group;
        public string key = "";
        public float alpha;
        // the latest request
        public int frame = -10;
        public Vector3 point;
        public string prompt;
        public PlayerInputReader input;
    }

    readonly Bubble[] bubbles = { new Bubble(), new Bubble() };
    Canvas canvas;
    RectTransform canvasRect;

    // ---------------------------------------------------------------- install

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Make();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Make();

    static void Make()
    {
        if (inst) return;
        if (!FindFirstObjectByType<PlayerInteractor>()) return;
        inst = new GameObject("Kayyan Prompts").AddComponent<WorldPrompt>();
    }

    void Awake()
    {
        canvas = KUI.MakeCanvas("Prompt Canvas", 50);
        canvas.transform.SetParent(transform, false);
        canvasRect = (RectTransform)canvas.transform;
    }

    // ---------------------------------------------------------------- api

    /// <summary>Show (or keep showing) a prompt for a player's half. Call every frame while it applies.</summary>
    public static void Set(int side, Vector3 worldPoint, string prompt, PlayerInputReader input)
    {
        if (!inst || side < 0 || side > 1 || string.IsNullOrEmpty(prompt)) return;
        var b = inst.bubbles[side];
        b.frame = Time.frameCount;
        b.point = worldPoint;
        b.prompt = prompt;
        b.input = input;
    }

    // ---------------------------------------------------------------- update

    void LateUpdate()
    {
        if (!canvasRect) return;
        for (int side = 0; side < 2; side++)
        {
            var b = bubbles[side];
            bool want = Time.frameCount - b.frame <= 1 && !PauseMenu.IsPaused && !PauseMenu.UiHidden && !string.IsNullOrEmpty(b.prompt);
            Camera cam = want ? CameraFor(side) : null;
            Vector3 sp = Vector3.zero;
            if (cam)
            {
                sp = cam.WorldToScreenPoint(b.point);
                if (sp.z <= 0f) want = false;
            }
            else want = false;

            if (want)
            {
                string key = b.prompt + "|" + (b.input && b.input.Pad != null) + "|" + (b.input ? b.input.ActiveLayout.ToString() : "");
                if (b.wrap == null || b.key != key) Build(b, side, key);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
                float bob = Mathf.Sin(Time.unscaledTime * 3f + side) * 6f;
                b.wrap.anchoredPosition = local + new Vector2(0f, bob);
            }

            b.alpha = Mathf.MoveTowards(b.alpha, want ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (b.group) b.group.alpha = b.alpha;
            if (b.wrap) b.wrap.gameObject.SetActive(b.alpha > 0.01f);
        }
    }

    static Camera CameraFor(int side)
    {
        Camera fallback = null;
        foreach (var c in Camera.allCameras)
        {
            if (!c.enabled || c.targetTexture) continue;
            var r = c.rect;
            if (r.width > 0.6f) { if (!fallback) fallback = c; continue; }
            if (side == 0 && r.x < 0.25f) return c;
            if (side == 1 && r.x >= 0.25f) return c;
        }
        return fallback ? fallback : Camera.main;
    }

    // ---------------------------------------------------------------- build the bubble

    void Build(Bubble b, int side, string key)
    {
        if (b.wrap) Destroy(b.wrap.gameObject);
        b.key = key;

        b.wrap = KUI.Rect(canvas.transform, "Prompt " + (side + 1));
        b.wrap.anchorMin = b.wrap.anchorMax = new Vector2(0.5f, 0.5f);
        b.wrap.pivot = new Vector2(0.5f, 0f);
        b.wrap.sizeDelta = Vector2.zero;
        b.group = b.wrap.gameObject.AddComponent<CanvasGroup>();
        b.group.blocksRaycasts = false;
        b.group.interactable = false;

        // the little tail that points at the object
        var tailOuter = KUI.Img(b.wrap, "Tail", KUI.Ink, null);
        KUI.Center(tailOuter.rectTransform, 26f, 26f, 0f, 20f);
        tailOuter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var tailInner = KUI.Img(b.wrap, "TailInner", KUI.Paper, null);
        KUI.Center(tailInner.rectTransform, 17f, 17f, 0f, 21f);
        tailInner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        b.box = KUI.Box(b.wrap, "Bubble", KUI.Paper, 30, 4f, 6f);
        b.box.anchorMin = b.box.anchorMax = new Vector2(0.5f, 0f);
        b.box.pivot = new Vector2(0.5f, 0f);
        b.box.anchoredPosition = new Vector2(0f, 24f);

        var row = KUI.Row(b.box, "Content", 12f, TextAnchor.MiddleCenter);
        row.padding = new RectOffset(26, 26, 10, 10);
        b.content = (RectTransform)row.transform;
        b.content.anchorMin = b.content.anchorMax = b.content.pivot = new Vector2(0.5f, 0.5f);
        var fit = b.content.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Split(b.prompt, out string pre, out char tok, out string post);
        if (pre.Length > 0) AddText(row.transform, pre);
        if (tok != '\0') AddGlyph(row.transform, tok, b.input);
        if (post.Length > 0) AddText(row.transform, post);

        LayoutRebuilder.ForceRebuildLayoutImmediate(b.content);
        b.box.sizeDelta = new Vector2(Mathf.Max(120f, b.content.sizeDelta.x), Mathf.Max(64f, b.content.sizeDelta.y));

        foreach (var g in b.wrap.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    static void AddText(Transform parent, string s)
    {
        var t = KUI.Label(parent, s, 32, KUI.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    static void AddGlyph(Transform parent, char tok, PlayerInputReader input)
    {
        bool power = tok == 'P';
        bool pad = input == null || input.Pad != null || input.ActiveLayout == KeyboardDebugLayout.None;
        if (pad)
        {
            KUI.PadGlyph(parent, power ? "R2" : "square", 56f);
            return;
        }
        bool arrows = input.ActiveLayout == KeyboardDebugLayout.Arrows;
        string k = power ? (arrows ? "R-Ctrl" : "F") : (arrows ? "R-Shift" : "E");
        KUI.KeyCap(parent, k, 52f);
    }

    /// <summary>"Hold {P} to dig" becomes ("Hold", 'P', "to dig").</summary>
    static void Split(string s, out string pre, out char tok, out string post)
    {
        tok = '\0';
        int p = s.IndexOf("{P}"), i = s.IndexOf("{I}");
        int k = p >= 0 && i >= 0 ? Mathf.Min(p, i) : (p >= 0 ? p : i);
        if (k < 0) { pre = s.Trim(); post = ""; return; }
        tok = s[k + 1];
        pre = s.Substring(0, k).Trim();
        post = s.Substring(k + 3).Trim();
    }
}
