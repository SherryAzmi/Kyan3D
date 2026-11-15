using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Split-screen HUD: index 0 = left half (Present), 1 = right half (Future).
/// Each half has a prompt line + hold bar at the bottom and a message line near the top.
/// </summary>
public class PuzzleHUD : MonoBehaviour
{
    public Text[] prompts = new Text[2];
    public GameObject[] barRoots = new GameObject[2];
    public RectTransform[] barFills = new RectTransform[2];
    public Text[] messages = new Text[2];

    readonly float[] messageUntil = new float[2];

    public void SetPrompt(int side, string text, float progress)
    {
        if (prompts[side])
        {
            prompts[side].enabled = !string.IsNullOrEmpty(text);
            prompts[side].text = text;
        }
        if (barRoots[side]) barRoots[side].SetActive(progress >= 0f);
        if (barFills[side] && progress >= 0f) barFills[side].anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    [Tooltip("Every message stays at least this long, so there is time to read it.")]
    public float minMessageSeconds = 10f;

    public void ShowMessage(int side, string text, float seconds = 4f)
    {
        if (!messages[side]) return;
        bool isNew = messages[side].text != text || Time.time > messageUntil[side];
        messages[side].text = text;
        messageUntil[side] = Time.time + Mathf.Max(seconds, minMessageSeconds);
        // a soft chime on that player's side: "something new to read"
        if (isNew && !string.IsNullOrEmpty(text) && GameAudio.I) GameAudio.Play(GameAudio.I.message, side, 0.8f);
    }

    public void ShowBoth(string text, float seconds = 4f)
    {
        ShowMessage(0, text, seconds);
        ShowMessage(1, text, seconds);
    }

    // Each message sits on a paper panel (same look as the menus) instead of loose white text.
    readonly RectTransform[] cards = new RectTransform[2];
    readonly CanvasGroup[] cardGroups = new CanvasGroup[2];

    void BuildCard(int i)
    {
        var text = messages[i];
        var tr = text.rectTransform;
        var card = KUI.Box(text.transform.parent, "Message Panel " + (i + 1), KUI.Paper, 26, 4f, 7f);
        card.SetSiblingIndex(text.transform.GetSiblingIndex()); // behind the text
        card.anchorMin = card.anchorMax = new Vector2((tr.anchorMin.x + tr.anchorMax.x) * 0.5f, tr.anchorMax.y);
        card.pivot = new Vector2(0.5f, 1f);
        foreach (var g in card.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false;
        cardGroups[i] = card.gameObject.AddComponent<CanvasGroup>();
        cardGroups[i].blocksRaycasts = false;
        cards[i] = card;

        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        foreach (var fx in text.GetComponents<UnityEngine.UI.Shadow>()) fx.enabled = false; // Shadow and Outline
    }

    void Update()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!messages[i]) continue;
            if (!cards[i]) BuildCard(i);

            float left = messageUntil[i] - Time.time;
            float a = Mathf.Clamp01(left / 0.6f);
            bool has = !string.IsNullOrEmpty(messages[i].text) && a > 0.001f;
            messages[i].color = new Color(KUI.Ink.r, KUI.Ink.g, KUI.Ink.b, a);
            cardGroups[i].alpha = a;
            cards[i].gameObject.SetActive(has);
            if (!has) continue;

            var tr = messages[i].rectTransform;
            float maxW = Mathf.Max(200f, tr.rect.width - 40f);
            float w = Mathf.Min(messages[i].preferredWidth, maxW);
            float h = messages[i].preferredHeight;
            cards[i].sizeDelta = new Vector2(w + 90f, h + 40f);
            cards[i].anchoredPosition = new Vector2(tr.anchoredPosition.x, tr.anchoredPosition.y + 20f);
        }
    }
}
