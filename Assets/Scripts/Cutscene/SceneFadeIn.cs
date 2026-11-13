using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fades the next loaded scene in from black. Call SceneFadeIn.Request() right before
/// SceneManager.LoadScene(); the target scene needs no setup.
/// </summary>
public class SceneFadeIn : MonoBehaviour
{
    public float duration = 1.5f;

    static bool pending;

    public static void Request() => pending = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        pending = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pending) return;
        pending = false;

        var go = new GameObject("SceneFadeIn");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        go.AddComponent<SceneFadeIn>().image = img;
    }

    Image image;
    float t;

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (image) image.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / duration));
        if (t >= duration) Destroy(gameObject);
    }
}
