using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gives every piece of text in the game the Kayyan font (Fredoka), in every scene, without touching the scenes:
/// legacy UI Text, TextMeshPro and 3D TextMesh. Titles that already use Bagel Fat One are left alone.
/// Installs itself at startup and re-checks about twice a second, so text created later is covered too.
/// </summary>
public class FontApplier : MonoBehaviour
{
    static FontApplier instance;
    static TMP_FontAsset tmpBody;

    float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance) return;
        var go = new GameObject("Kayyan Fonts");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FontApplier>();
        SceneManager.sceneLoaded += (s, m) => instance.Apply();
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.5f;
        Apply();
    }

    void Apply()
    {
        var body = KUI.Body;
        var title = KUI.Title;
        if (!body) return;

        foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.font != body && t.font != title) t.font = body;

        if (!tmpBody) tmpBody = TMP_FontAsset.CreateFontAsset(body);
        if (tmpBody)
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.font != tmpBody && !(t.font && t.font.name.Contains("Bagel"))) t.font = tmpBody;

        foreach (var t in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font == body) continue;
            t.font = body;
            var r = t.GetComponent<MeshRenderer>();
            if (r) r.sharedMaterial = body.material;
        }
    }
}
