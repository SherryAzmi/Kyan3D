using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared Kayyan panels: the controls help (per player) and the settings (sound) panel.</summary>
public static class KPanels
{
    // ------------------------------------------------------------------ controls
    /// <summary>
    /// "How to play" for one player: action, controller button, keyboard keys.
    /// player 1 = Present (WASD + mouse), player 2 = Future (arrows).
    /// </summary>
    public static RectTransform Controls(Transform parent, int player, Action onClose)
    {
        bool p1 = player == 1;
        var card = KUI.Card(parent, "Controls P" + player, 860f, 790f);

        KUI.Place(KUI.Label(card, p1 ? "PLAYER 1  -  PRESENT" : "PLAYER 2  -  FUTURE", 22, p1 ? KUI.Hex("a8731f") : KUI.Grass2,
            TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 48, 38, 600, 30);
        KUI.Place(KUI.Label(card, "How to play", 58, KUI.Ink, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 48, 70, 600, 70);
        var close = KUI.IconButton(card, "cross", 64f, onClose);
        close.GetComponent<RectTransform>().anchorMin = close.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        close.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
        close.GetComponent<RectTransform>().anchoredPosition = new Vector2(-40f, -40f);

        float y0 = 168f;
        KUI.Place(KUI.Label(card, "ACTION", 18, KUI.Ink2, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 48, y0, 240, 26);
        KUI.Place(KUI.Label(card, "CONTROLLER", 18, KUI.Ink2, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 340, y0, 200, 26);
        KUI.Place(KUI.Label(card, "KEYBOARD", 18, KUI.Ink2, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 540, y0, 240, 26);

        // What this player uses right now: with one controller plugged in, Player 1 uses the controller and
        // Player 2 the keyboard and mouse; with two, each has a controller; with none, the keyboard layouts.
        var layout = PlayerInputReader.LayoutFor(player - 1, p1 ? KeyboardDebugLayout.WasdMouse : KeyboardDebugLayout.Arrows);
        bool hasPad = UnityEngine.InputSystem.Gamepad.all.Count >= player;
        string[,] rows;
        switch (layout)
        {
            case KeyboardDebugLayout.WasdMouse:
                rows = new[,]
                {
                    { "Move", "", "L", "W", "A", "S", "D" },
                    { "Look around", "Camera", "R", "Mouse", "", "", "" },
                    { "Jump", "", "cross", "Space", "", "", "" },
                    { "Pick up / use", "Press", "square", "E", "", "", "" },
                    { "Power", "Hold: dig, grow, join", "R2", "F", "", "", "" },
                };
                break;
            case KeyboardDebugLayout.Arrows:
                rows = new[,]
                {
                    { "Move", "", "L", "Arrow keys", "", "", "" },
                    { "Look around", "Camera", "R", ",", ".", "", "" },
                    { "Jump", "", "cross", "Enter", "", "", "" },
                    { "Pick up / use", "Press", "square", "R-Shift", "", "", "" },
                    { "Power", "Hold: dig, grow, join", "R2", "R-Ctrl", "", "", "" },
                };
                break;
            default: // controller only
                rows = new[,]
                {
                    { "Move", "", "L", "", "", "", "" },
                    { "Look around", "Camera", "R", "", "", "", "" },
                    { "Jump", "", "cross", "", "", "", "" },
                    { "Pick up / use", "Press", "square", "", "", "", "" },
                    { "Power", "Hold: dig, grow, join", "R2", "", "", "", "" },
                };
                break;
        }
        // which device the player is using, shown under the title
        string device = layout == KeyboardDebugLayout.None ? "Controller"
                      : hasPad ? "Controller or keyboard" : "Keyboard" + (layout == KeyboardDebugLayout.WasdMouse ? " and mouse" : "");
        KUI.Place(KUI.Label(card, device, 22, KUI.Ink2, TextAnchor.MiddleRight, false, FontStyle.Bold).rectTransform, 420, 100, 330, 30);

        float rowH = 96f, y = y0 + 34f;
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            var line = KUI.Img(card, "Line", new Color(KUI.Ink.r, KUI.Ink.g, KUI.Ink.b, 0.2f));
            KUI.Place(line.rectTransform, 48, y, 764, 2);

            bool hasSub = rows[i, 1].Length > 0;
            KUI.Place(KUI.Label(card, rows[i, 0], 31, KUI.Ink, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform,
                48, y + (hasSub ? 12f : 26f), 280, 40);
            if (hasSub)
                KUI.Place(KUI.Label(card, rows[i, 1], 19, KUI.Ink2, TextAnchor.MiddleLeft).rectTransform, 48, y + 52f, 280, 26);

            var pad = KUI.Row(card, "Pad", 10f);
            KUI.Place((RectTransform)pad.transform, 340, y, 190, rowH);
            if (hasPad || layout == KeyboardDebugLayout.None) KUI.PadGlyph(pad.transform, rows[i, 2], 62f);

            var keys = KUI.Row(card, "Keys", 8f);
            KUI.Place((RectTransform)keys.transform, 540, y, 280, rowH);
            for (int k = 3; k < 7; k++)
                if (rows[i, k].Length > 0) KUI.KeyCap(keys.transform, rows[i, k], 52f);
            y += rowH;
        }

        var tip = KUI.Box(card, "Tip", p1 ? KUI.Hex("ffefba") : KUI.Hex("dff4bf"), 24, 3.5f, 0f);
        KUI.Place(tip, 48, y + 14f, 764, 76);
        var tr = KUI.Row(tip, "Tip Row", 14f);
        KUI.Stretch((RectTransform)tr.transform, 22, 22, 0, 0);
        KUI.PadGlyph(tr.transform, "R2", 50f);
        var tt = KUI.Label(tr.transform, "Most puzzles need both players to hold Power at the same time.", 25, KUI.Ink,
            TextAnchor.MiddleLeft, false, FontStyle.Bold);
        var tle = tt.gameObject.AddComponent<LayoutElement>();
        tle.preferredWidth = 620f; tle.preferredHeight = 60f;
        tt.horizontalOverflow = HorizontalWrapMode.Wrap;
        return card;
    }

    // ------------------------------------------------------------------ settings
    /// <summary>
    /// Sound settings. In the main menu pass onRestart = null: one Back button. In a level pass onRestart
    /// (it should ask for confirmation) to also show "Restart game" next to "Resume".
    /// </summary>
    public static RectTransform Settings(Transform parent, Action onBack, Action onRestart)
    {
        var card = KUI.Card(parent, "Settings", 860f, 740f);
        KUI.Place(KUI.Label(card, "OPTIONS", 22, KUI.Ink2, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 48, 38, 400, 30);
        KUI.Place(KUI.Label(card, "Settings", 62, KUI.Ink, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 48, 68, 600, 76);
        var close = KUI.IconButton(card, "cross", 64f, () => { GameSettings.Save(); onBack?.Invoke(); });
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-40f, -40f);

        string[] names = { "Master", "Music", "Sound effects" };
        float[] values = { GameSettings.Master, GameSettings.Music, GameSettings.Sfx };
        float y = 176f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var line = KUI.Img(card, "Line", new Color(KUI.Ink.r, KUI.Ink.g, KUI.Ink.b, 0.2f));
            KUI.Place(line.rectTransform, 48, y, 764, 2);
            KUI.Place(KUI.Label(card, names[i], 32, KUI.Ink, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 48, y + 26, 260, 44);
            var val = KUI.Label(card, Mathf.RoundToInt(values[i] * 100f).ToString(), 32, KUI.Ink, TextAnchor.MiddleRight, false, FontStyle.Bold);
            KUI.Place(val.rectTransform, 740, y + 26, 72, 44);
            var slider = KUI.MakeSlider(card, 410f, values[i], v =>
            {
                val.text = Mathf.RoundToInt(v * 100f).ToString();
                if (idx == 0) GameSettings.Master = v;
                else if (idx == 1) GameSettings.Music = v;
                else GameSettings.Sfx = v;
            });
            KUI.Place((RectTransform)slider.transform, 320, y + 24, 410, 44);
            y += 94f;
        }
        {
            var line = KUI.Img(card, "Line", new Color(KUI.Ink.r, KUI.Ink.g, KUI.Ink.b, 0.2f));
            KUI.Place(line.rectTransform, 48, y, 764, 2);
            KUI.Place(KUI.Label(card, "Mute all", 32, KUI.Ink, TextAnchor.MiddleLeft, false, FontStyle.Bold).rectTransform, 48, y + 26, 260, 44);
            var sw = KUI.MakeSwitch(card, GameSettings.Muted, on => GameSettings.Muted = on);
            KUI.Place((RectTransform)sw.transform, 320, y + 24, 88, 46);
        }

        float by = 590f;
        if (onRestart != null)
        {
            KUI.Place((RectTransform)KUI.MakeButton(card, "Restart game", KUI.ClaySoft, KUI.Ink, 360, 84, 32, () => { GameSettings.Save(); onRestart(); }).transform, 48, by, 360, 84);
            KUI.Place((RectTransform)KUI.MakeButton(card, "Resume", KUI.Grass, KUI.Cream, 360, 84, 32, () => { GameSettings.Save(); onBack?.Invoke(); }).transform, 452, by, 360, 84);
        }
        else
        {
            KUI.Place((RectTransform)KUI.MakeButton(card, "Back", KUI.Grass, KUI.Cream, 360, 84, 32, () => { GameSettings.Save(); onBack?.Invoke(); }).transform, 250, by, 360, 84);
        }
        return card;
    }

    // ------------------------------------------------------------------ confirm
    public static RectTransform Confirm(Transform parent, string eyebrow, string title, string body, Action yes, Action no)
    {
        var card = KUI.Card(parent, "Confirm", 760f, 420f);
        KUI.Place(KUI.Label(card, eyebrow.ToUpperInvariant(), 22, KUI.Ink2, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform, 0, 52, 760, 30);
        KUI.Place(KUI.Label(card, title, 52, KUI.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform, 0, 92, 760, 70);
        KUI.Place(KUI.Label(card, body, 27, KUI.Ink2, TextAnchor.MiddleCenter).rectTransform, 0, 178, 760, 40);
        KUI.Place((RectTransform)KUI.MakeButton(card, "Cancel", KUI.Paper2, KUI.Ink, 300, 80, 30, no).transform, 70, 270, 300, 80);
        KUI.Place((RectTransform)KUI.MakeButton(card, "Yes", KUI.ClaySoft, KUI.Ink, 300, 80, 30, yes).transform, 390, 270, 300, 80);
        return card;
    }
}
