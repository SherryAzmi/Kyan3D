using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The main menu: Play, Settings (sound), Controls, Quit. Everything is built when the scene starts, so the
/// "MainMenu" scene only needs this component (Tools > Kayyan > Build Main Menu Scene creates it).
/// The background is the game itself in two halves: the desert at night (Present) and the meadow (Future),
/// split by the same mirror crack as in the levels.
/// </summary>
public class MainMenu : MonoBehaviour
{
    enum Page { Main, Settings, Controls }

    Canvas canvas;
    GameObject mainGroup, dim, settingsCard, controlsGroup;
    Page page = Page.Main;
    Button playButton;

    public static MainMenu Current { get; private set; }

    /// <summary>Open a page by name (main, settings, controls). Used by the screenshot tool.</summary>
    public void DebugPage(string name) =>
        ShowPage(name == "settings" ? Page.Settings : name == "controls" ? Page.Controls : Page.Main);

    void Start()
    {
        Current = this;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameSettings.Apply();

        if (!Camera.main && !FindFirstObjectByType<Camera>())
        {
            var cg = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cg.tag = "MainCamera";
            var c = cg.GetComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
        }
        KUI.EnsureEventSystem();
        canvas = KUI.MakeCanvas("Main Menu", 0);
        canvas.transform.SetParent(transform, false);

        BuildBackground();
        BuildMain();
        BuildPanels();
        ShowPage(Page.Main);
        UISound.Open(); // a soft chime as the menu appears

        music = gameObject.AddComponent<AudioSource>();
        music.clip = MenuMusic.Create();
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = 0f;
        music.Play();
    }

    AudioSource music;
    float musicFade;

    void LateUpdate()
    {
        if (!music) return;
        musicFade = Mathf.MoveTowards(musicFade, 1f, Time.unscaledDeltaTime / 3f); // fades in over 3 seconds
        music.volume = 0.55f * GameSettings.Music * musicFade;
    }

    // ---------------------------------------------------------------- build

    void BuildBackground()
    {
        var raw = KUI.Rect(canvas.transform, "Backdrop").gameObject.AddComponent<RawImage>();
        raw.texture = PaintBackdrop();
        raw.raycastTarget = false;
        KUI.Stretch(raw.rectTransform);

        var crack = KUI.Rect(canvas.transform, "Mirror Crack");
        KUI.Stretch(crack);
        var mc = crack.gameObject.AddComponent<MirrorCrackDivider>();
        mc.raycastTarget = false;
        mc.levelLight = 0.6f;

        var vig = KUI.Img(canvas.transform, "Vignette", new Color(0.12f, 0.07f, 0.02f, 0.28f));
        KUI.Stretch(vig.rectTransform);
    }

    void BuildMain()
    {
        var g = KUI.Rect(canvas.transform, "Main Group");
        KUI.Stretch(g);
        mainGroup = g.gameObject;

        foreach (int side in new[] { 0, 1 })
        {
            var chip = KUI.Box(g, "Chip", side == 0 ? KUI.Hex("ffe3a1") : KUI.Hex("cdeeaa"), 22, 3.5f, 5f);
            chip.anchorMin = chip.anchorMax = new Vector2(side == 0 ? 0f : 1f, 1f);
            chip.pivot = new Vector2(side == 0 ? 0f : 1f, 1f);
            chip.anchoredPosition = new Vector2(side == 0 ? 44f : -44f, -40f);
            chip.sizeDelta = new Vector2(230f, 46f);
            KUI.Stretch(KUI.Label(chip, side == 0 ? "PRESENT" : "FUTURE", 22, KUI.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform);
        }

        var title = KUI.Label(g, "Kayyan", 250, KUI.Sand, TextAnchor.MiddleCenter, true);
        KUI.Center(title.rectTransform, 1400, 300, 0, 300);
        var o = title.gameObject.AddComponent<Outline>();
        o.effectColor = KUI.Ink; o.effectDistance = new Vector2(9f, -9f);
        var sh = title.gameObject.AddComponent<Shadow>();
        sh.effectColor = KUI.Ink; sh.effectDistance = new Vector2(0f, -22f);

        var tag = KUI.Label(g, "What was broken can be made whole again.", 42, KUI.Hex("fff4d2"), TextAnchor.MiddleCenter, false, FontStyle.Bold);
        KUI.Center(tag.rectTransform, 1200, 60, 0, 66);
        var to = tag.gameObject.AddComponent<Outline>();
        to.effectColor = KUI.Ink; to.effectDistance = new Vector2(3f, -3f);

        playButton = KUI.MakeButton(g, "Play", KUI.Grass, KUI.Cream, 560, 92, 40, Play);
        KUI.Center((RectTransform)playButton.transform, 560, 92, 0, -40);
        var s = KUI.MakeButton(g, "Settings", KUI.Paper, KUI.Ink, 560, 92, 40, () => ShowPage(Page.Settings));
        KUI.Center((RectTransform)s.transform, 560, 92, 0, -156);
        var c = KUI.MakeButton(g, "Controls", KUI.Paper, KUI.Ink, 560, 92, 40, () => ShowPage(Page.Controls));
        KUI.Center((RectTransform)c.transform, 560, 92, 0, -272);
        var q = KUI.MakeButton(g, "Quit", KUI.ClaySoft, KUI.Ink, 560, 92, 40, Quit);
        KUI.Center((RectTransform)q.transform, 560, 92, 0, -388);

        // footer hint
        var foot = KUI.Row(g, "Footer", 12f);
        var frt = (RectTransform)foot.transform;
        frt.anchorMin = frt.anchorMax = new Vector2(0f, 0f);
        frt.pivot = new Vector2(0f, 0f);
        frt.anchoredPosition = new Vector2(44f, 34f);
        frt.sizeDelta = new Vector2(500f, 56f);
        KUI.PadGlyph(foot.transform, "cross", 50f);
        var ft = KUI.Label(foot.transform, "Select", 30, KUI.Hex("fff4d2"), TextAnchor.MiddleLeft, false, FontStyle.Bold);
        ft.gameObject.AddComponent<Outline>().effectColor = KUI.Ink;
        var fle = ft.gameObject.AddComponent<LayoutElement>(); fle.preferredWidth = 200; fle.preferredHeight = 50;
    }

    void BuildPanels()
    {
        var d = KUI.Img(canvas.transform, "Dim", new Color(0.1f, 0.06f, 0.02f, 0.5f), null, true);
        KUI.Stretch(d.rectTransform);
        dim = d.gameObject;

        settingsCard = KPanels.Settings(canvas.transform, () => ShowPage(Page.Main), null).gameObject;

        var cg = KUI.Rect(canvas.transform, "Controls Group");
        KUI.Stretch(cg);
        controlsGroup = cg.gameObject;
        RebuildControls();
    }

    /// <summary>Both players' controls, built fresh so they match the controllers plugged in right now.</summary>
    void RebuildControls()
    {
        for (int i = controlsGroup.transform.childCount - 1; i >= 0; i--)
        {
            var old = controlsGroup.transform.GetChild(i).gameObject;
            old.SetActive(false); // Destroy happens at the end of the frame
            Destroy(old);
        }
        for (int side = 0; side < 2; side++)
        {
            var card = KPanels.Controls(controlsGroup.transform, side + 1, () => ShowPage(Page.Main));
            card.anchorMin = card.anchorMax = new Vector2(side == 0 ? 0.25f : 0.75f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.localScale = Vector3.one * 0.9f;
        }
    }

    // ---------------------------------------------------------------- pages

    void ShowPage(Page p)
    {
        if (p != page) { if (p == Page.Main) UISound.Close(); else UISound.Open(); }
        page = p;
        if (p == Page.Controls) RebuildControls();
        mainGroup.SetActive(true);
        dim.SetActive(p != Page.Main);
        settingsCard.SetActive(p == Page.Settings);
        controlsGroup.SetActive(p == Page.Controls);
        mainGroup.SetActive(p == Page.Main);
        GameObject focus = null;
        if (p == Page.Main && playButton) focus = playButton.gameObject;
        else if (p == Page.Settings) { var s = settingsCard.GetComponentInChildren<Selectable>(true); if (s) focus = s.gameObject; }
        else if (p == Page.Controls) { var s = controlsGroup.GetComponentInChildren<Selectable>(false); if (s) focus = s.gameObject; }
        KUI.Select(focus);
    }

    void Update()
    {
        if (page == Page.Main) return;
        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool back = false;
        foreach (var pad in Gamepad.all) if (pad.buttonEast.wasPressedThisFrame) back = true;
        if (esc || back) ShowPage(Page.Main);
    }

    void Play() => SceneManager.LoadScene(PauseMenu.FirstScene);

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- the backdrop

    static Texture2D PaintBackdrop()
    {
        const int W = 960, H = 540;
        int half = W / 2;
        float gy = H * 0.42f;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[W * H];

        Color nightA = KUI.Hex("272c4c"), nightB = KUI.Hex("171b3a"), nightC = KUI.Hex("0b0e24"), nightD = KUI.Hex("03040d");
        Color dayA = KUI.Hex("8fcf9f"), dayB = KUI.Hex("a6dcc0"), dayC = KUI.Hex("cdeedb");

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color c;
                if (x < half)
                {
                    if (y < gy)
                    {
                        float t = y / gy;
                        c = Color.Lerp(KUI.Hex("2b2d49"), KUI.Hex("3d4062"), t);
                        float rp = Mathf.Sin(x * 0.05f + y * 0.32f) * 0.5f + 0.5f;
                        c = Color.Lerp(c, c * 0.78f, rp * 0.3f * (1f - t));
                        if (y > gy - 3f) c = KUI.Hex("4a4d72");
                    }
                    else
                    {
                        float d1 = gy + H * 0.09f * (0.5f + 0.5f * Mathf.Sin(x * 0.009f + 0.5f));
                        float d2 = gy + H * 0.05f * (0.5f + 0.5f * Mathf.Sin(x * 0.016f + 2.2f));
                        if (y < d2) c = KUI.Hex("2a2c4a");
                        else if (y < d1) c = KUI.Hex("34365a");
                        else
                        {
                            float t = (y - gy) / (H - gy);
                            c = t < 0.3f ? Color.Lerp(nightA, nightB, t / 0.3f) : t < 0.65f ? Color.Lerp(nightB, nightC, (t - 0.3f) / 0.35f) : Color.Lerp(nightC, nightD, (t - 0.65f) / 0.35f);
                        }
                    }
                }
                else
                {
                    int mx = x - half;
                    if (y < gy)
                    {
                        float t = y / gy;
                        c = Color.Lerp(KUI.Hex("3f8f2e"), KUI.Hex("4a9a35"), t);
                        float g = Mathf.Sin(mx * 0.9f + y * 0.3f) * 0.5f + 0.5f;
                        c = Color.Lerp(c, c * 0.86f, g * 0.25f);
                        if (y > gy - 3f) c = KUI.Hex("2f7a25");
                    }
                    else
                    {
                        float h1 = gy + H * 0.12f * (0.5f + 0.5f * Mathf.Sin(mx * 0.008f + 0.4f));
                        float h2 = gy + H * 0.075f * (0.5f + 0.5f * Mathf.Sin(mx * 0.013f + 2.6f));
                        if (y < h2) c = KUI.Hex("57a85e");
                        else if (y < h1) c = KUI.Hex("6db873");
                        else
                        {
                            float t = (y - gy) / (H - gy);
                            c = t < 0.2f ? Color.Lerp(dayA, dayB, t / 0.2f) : Color.Lerp(dayB, dayC, Mathf.Clamp01((t - 0.2f) / 0.8f));
                        }
                    }
                }
                px[y * W + x] = c;
            }

        var rng = new System.Random(5);
        float R() => (float)rng.NextDouble();
        void Blend(int x, int y, Color col, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Color b = px[y * W + x];
            px[y * W + x] = Color.Lerp(b, col, Mathf.Clamp01(a));
        }
        void Disc(float cx, float cy, float r, Color col, float a = 1f)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= cx + r + 1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    Blend(x, y, col, a * Mathf.Clamp01(r - d + 0.5f));
                }
        }

        // night: stars, moon and its glow
        for (int i = 0; i < 110; i++)
        {
            int sx = Mathf.FloorToInt(R() * (half - 4)) + 2, sy = Mathf.FloorToInt(gy + 24 + R() * (H - gy - 30));
            float b = 0.35f + R() * 0.6f;
            Blend(sx, sy, Color.white, b);
            if (R() < 0.25f) { Blend(sx + 1, sy, Color.white, b * 0.6f); Blend(sx, sy + 1, Color.white, b * 0.6f); }
        }
        float mx0 = half * 0.64f, my0 = H * 0.80f;
        for (int y = (int)(my0 - 90); y < my0 + 90; y++)
            for (int x = (int)(mx0 - 90); x < mx0 + 90; x++)
            {
                float d = Mathf.Sqrt((x - mx0) * (x - mx0) + (y - my0) * (y - my0));
                if (d < 90f) Blend(x, y, KUI.Hex("8c99cc"), 0.35f * (1f - d / 90f) * (1f - d / 90f));
            }
        Disc(mx0, my0, 26f, KUI.Hex("f1eedd"));

        // night cacti
        foreach (float cx in new[] { 0.14f, 0.78f })
        {
            float bx = half * cx, by = gy * 0.82f, h = 70f;
            Color cc = KUI.Hex("1d2a3a");
            for (int y = (int)by; y < by + h; y++) for (int x = (int)bx - 6; x < bx + 6; x++) Blend(x, y, cc, 1f);
            Disc(bx, by + h, 6f, cc);
            for (int y = (int)(by + 22); y < by + 48; y++) for (int x = (int)bx - 20; x < bx - 12; x++) Blend(x, y, cc, 1f);
            for (int x = (int)bx - 20; x < bx - 4; x++) for (int y = (int)(by + 20); y < by + 28; y++) Blend(x, y, cc, 1f);
            Disc(bx - 16, by + 48, 4f, cc);
        }

        // meadow: trees and flowers
        foreach (float tx in new[] { 0.12f, 0.82f })
        {
            float bx = half + half * tx, by = gy * 0.95f, h = tx < 0.5f ? 62f : 82f;
            for (int y = (int)by; y < by + h; y++) for (int x = (int)bx - 3; x < bx + 3; x++) Blend(x, y, KUI.Hex("6b4a2a"), 1f);
            for (int k = 0; k < 4; k++)
                Disc(bx + (k - 1.5f) * 7f, by + h + 18f - Mathf.Abs(k - 1.5f) * 8f, 34f - k % 2 * 4f, Color.Lerp(KUI.Hex("7bc060"), KUI.Hex("3f8f2e"), 0.3f + 0.2f * k));
        }
        for (int i = 0; i < 46; i++)
        {
            float fx = half + 12 + R() * (half - 24), fy = 8 + R() * (gy - 30);
            float scale = 0.7f + (1f - fy / gy) * 0.5f;
            for (int y = (int)fy; y < fy + 14 * scale; y++) Blend((int)fx, y, KUI.Hex("2f6e22"), 1f);
            float cy = fy + 15f * scale;
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f;
                Disc(fx + Mathf.Cos(a) * 4.5f * scale, cy + Mathf.Sin(a) * 4.5f * scale, 3.6f * scale, KUI.Hex("f3c730"));
            }
            Disc(fx, cy, 2.8f * scale, KUI.Hex("d9871a"));
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
