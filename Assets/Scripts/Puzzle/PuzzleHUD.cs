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

    public void ShowMessage(int side, string text, float seconds = 4f)
    {
        if (!messages[side]) return;
        messages[side].text = text;
        messageUntil[side] = Time.time + seconds;
    }

    public void ShowBoth(string text, float seconds = 4f)
    {
        ShowMessage(0, text, seconds);
        ShowMessage(1, text, seconds);
    }

    void Update()
    {
        for (int i = 0; i < 2; i++)
        {
            if (!messages[i]) continue;
            float left = messageUntil[i] - Time.time;
            var c = messages[i].color;
            c.a = Mathf.Clamp01(left / 0.6f);
            messages[i].color = c;
        }
    }
}
