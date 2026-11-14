using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// When this player walks into the invisible edge of their world (the "Bounds" walls), a red warning
/// appears on their half of the split screen and fades out shortly after they turn back.
/// Only the Bounds walls trigger it: trees, pillars, altars and puzzle objects never do.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BoundaryWarning : MonoBehaviour
{
    [TextArea] public string message = "You can't go any further\nthis is the edge of your world";
    public Color color = new Color(1f, 0.18f, 0.15f);
    [Tooltip("0 = left half of the screen, 1 = right half. -1 = work it out from the world the player is in.")]
    public int side = -1;
    public float showTime = 1.6f;
    public float forestMinX = 150f;

    static Canvas canvas;
    Text text;
    float visibleUntil = -99f;

    void Start()
    {
        if (side < 0)
        {
            var pi = GetComponent<PlayerInteractor>();
            side = pi ? pi.hudSide : (transform.position.x >= forestMinX ? 1 : 0);
        }
        text = MakeText(side);
        SetAlpha(0f);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var p = hit.collider ? hit.collider.transform.parent : null;
        if (p && p.name == "Bounds" && Mathf.Abs(hit.normal.y) < 0.5f) visibleUntil = Time.time + showTime;
    }

    void Update()
    {
        if (!text) return;
        float left = visibleUntil - Time.time;
        float a = left > 0f ? Mathf.Clamp01(left / 0.4f) : 0f;
        // a gentle pulse while it's showing
        SetAlpha(a * (0.85f + 0.15f * Mathf.Sin(Time.time * 9f)));
    }

    void SetAlpha(float a)
    {
        var c = color;
        c.a = a;
        text.color = c;
        text.enabled = a > 0.001f;
    }

    Text MakeText(int half)
    {
        if (!canvas)
        {
            var go = new GameObject("BoundaryWarningUI");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        var t = new GameObject(half == 0 ? "Warning Left" : "Warning Right").AddComponent<Text>();
        t.transform.SetParent(canvas.transform, false);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 40;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = message;
        var outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        var rt = t.rectTransform;
        float x0 = half == 0 ? 0f : 0.5f;
        rt.anchorMin = new Vector2(x0, 0.5f);
        rt.anchorMax = new Vector2(x0 + 0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(-60f, 160f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        return t;
    }
}
