using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Writes a text on screen like someone typing a letter: in a handwriting font, letter by letter,
/// with soft keyboard clicks (space bar and return too), a human typing rhythm, short pauses after
/// commas and full stops, and a longer one at the end of each line. Used for the Mirror Fragment notes and the final note.
/// The rest of the text is already laid out (invisible), so the words never jump around.
/// </summary>
public static class LetterWriter
{
    static Font hand;
    static bool looked;

    /// <summary>A handwriting font from the system (Ink Free / Segoe Print / Segoe Script), or null.</summary>
    public static Font Handwriting
    {
        get
        {
            if (looked) return hand;
            looked = true;
            var installed = Font.GetOSInstalledFontNames();
            foreach (var name in new[] { "Ink Free", "Segoe Print", "Segoe Script" })
                if (System.Array.IndexOf(installed, name) >= 0)
                {
                    hand = Font.CreateDynamicFontFromOSFont(name, 48);
                    break;
                }
            return hand;
        }
    }

    /// <summary>Switch a Text to the handwriting font (keeps the original if none is installed).</summary>
    public static void UseHandwriting(Text t, float sizeScale = 1.08f)
    {
        if (!t || !Handwriting || t.font == Handwriting) return;
        t.font = Handwriting;
        t.fontStyle = FontStyle.Normal; // script fonts are already slanted
        t.fontSize = Mathf.RoundToInt(t.fontSize * sizeScale);
        t.lineSpacing = 1.05f;
    }

    /// <summary>Write 'full' into 't' letter by letter. The Text should already be fully visible (alpha 1).</summary>
    public static IEnumerator Write(Text t, string full, float lettersPerSecond = 14f)
    {
        if (!t) yield break;
        t.supportRichText = true;
        float carry = 0f;
        for (int i = 0; i <= full.Length; i++)
        {
            t.text = i < full.Length
                ? full.Substring(0, i) + "<color=#00000000>" + full.Substring(i) + "</color>"
                : full;
            if (i == full.Length) break;

            char c = full[i];
            Key(c); // a keyboard key for every letter, the space bar, return at the end of a line

            // a human typing rhythm: never perfectly even, short thinking pauses at punctuation
            float wait = 1f / lettersPerSecond * Random.Range(0.7f, 1.35f);
            if (c == '\n') wait = 0.6f;
            else if (c == '.' || c == '!' || c == '?') wait = 0.4f;
            else if (c == ',' || c == ';' || c == ':') wait = 0.22f;
            else if (c == ' ') wait *= 1.3f;

            carry += wait;
            while (carry > 0f) { carry -= Time.unscaledDeltaTime; yield return null; }
        }
    }

    static void Key(char c)
    {
        var a = GameAudio.I;
        if (!a) return;
        AudioClip clip;
        if (c == '\n') clip = a.typeEnter;
        else if (c == ' ') clip = a.typeSpace;
        else clip = a.typeKeys != null && a.typeKeys.Length > 0 ? a.typeKeys[Random.Range(0, a.typeKeys.Length)] : null;
        if (!clip) return;
        GameAudio.Play(clip, -1, Random.Range(0.5f, 0.7f), Random.Range(0.94f, 1.08f));
    }
}
