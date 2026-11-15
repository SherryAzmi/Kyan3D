using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts the little quote that appears after a puzzle (the note written letter by letter when the flower
/// blooms, and the last note) on a paper panel with a small heading, in the Kayyan font.
/// A cutscene calls NotePanel.Replace(ref header, ref note, ref backdrop) once in Awake; its own fades and
/// the letter-by-letter writing then act on the panel's texts, so nothing else in the cutscene changes.
/// </summary>
public static class NotePanel
{
    public static void Replace(ref Text header, ref Text note, ref Image backdrop)
    {
        if (!note) return;

        var canvas = KUI.MakeCanvas("Note Panel", 400);

        var card = KUI.Card(canvas.transform, "Note Card", 1280f, 380f);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
        card.pivot = new Vector2(0.5f, 0f);
        card.anchoredPosition = new Vector2(0f, 90f);
        var group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var h = KUI.Label(card, header ? header.text : "", 28, KUI.Ink2, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        KUI.Place(h.rectTransform, 60, 40, 1160, 36);

        var n = KUI.Label(card, note.text, 42, KUI.Ink, TextAnchor.UpperCenter, false, FontStyle.Bold);
        n.horizontalOverflow = HorizontalWrapMode.Wrap;
        n.verticalOverflow = VerticalWrapMode.Overflow;
        n.lineSpacing = 1.15f;
        n.supportRichText = true;
        KUI.Place(n.rectTransform, 70, 100, 1140, 200);

        // the cutscene fades this image's alpha; the fit component copies it to the whole card
        var proxy = KUI.Img(card, "Fade", new Color(1f, 1f, 1f, 0f));
        proxy.enabled = false;

        var fit = card.gameObject.AddComponent<NoteCardFit>();
        fit.card = card; fit.note = n; fit.proxy = proxy; fit.group = group;

        // the original loose text and dark band are no longer used
        if (header) header.enabled = false;
        note.enabled = false;
        if (backdrop) backdrop.enabled = false;

        header = h;
        note = n;
        backdrop = proxy;
    }
}

/// <summary>Sizes the note panel to its text and follows the cutscene's fade.</summary>
public class NoteCardFit : MonoBehaviour
{
    public RectTransform card;
    public Text note;
    public Image proxy;
    public CanvasGroup group;
    float peak = 0.01f;

    void LateUpdate()
    {
        if (!card || !note) return;
        float h = Mathf.Max(60f, note.preferredHeight);
        note.rectTransform.sizeDelta = new Vector2(note.rectTransform.sizeDelta.x, h + 6f);
        card.sizeDelta = new Vector2(card.sizeDelta.x, 100f + h + 64f);

        float a = proxy ? proxy.color.a : 0f;
        peak = Mathf.Max(peak, a);
        group.alpha = Mathf.Clamp01(a / peak);
    }
}
