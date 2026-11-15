using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The panel at the very end of the cinematic: "The End", a closing line, and Play again / Main menu / Quit.
/// Call EndPanel.Show() when the last shot is on screen; it fades in over the picture.
/// </summary>
public class EndPanel : MonoBehaviour
{
    CanvasGroup group;
    RectTransform card;

    public static void Show(string line = "What was broken is whole again.")
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        KUI.EnsureEventSystem();
        var go = new GameObject("Kayyan End Panel");
        go.AddComponent<EndPanel>().Build(line);
    }

    void Build(string line)
    {
        var canvas = KUI.MakeCanvas("End Canvas", 300);
        canvas.transform.SetParent(transform, false);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var dim = KUI.Img(canvas.transform, "Dim", new Color(0.05f, 0.04f, 0.1f, 0.6f), null, true);
        KUI.Stretch(dim.rectTransform);

        card = KUI.Card(canvas.transform, "End Card", 940f, 760f);

        KUI.Place(KUI.Label(card, "KAYYAN", 26, KUI.Ink2, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform, 0, 54, 940, 34);

        var title = KUI.Label(card, "The End", 170, KUI.Sand, TextAnchor.MiddleCenter, true);
        KUI.Place(title.rectTransform, 0, 96, 940, 210);
        var o = title.gameObject.AddComponent<Outline>();
        o.effectColor = KUI.Ink; o.effectDistance = new Vector2(7f, -7f);
        var sh = title.gameObject.AddComponent<Shadow>();
        sh.effectColor = KUI.Ink; sh.effectDistance = new Vector2(0f, -16f);

        KUI.Place(KUI.Label(card, line, 36, KUI.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform, 0, 326, 940, 50);
        KUI.Place(KUI.Label(card, "Thank you for playing.", 28, KUI.Ink2, TextAnchor.MiddleCenter).rectTransform, 0, 382, 940, 40);

        var again = KUI.MakeButton(card, "Play again", KUI.Grass, KUI.Cream, 560, 88, 38, () => Load(PauseMenu.FirstScene));
        KUI.Place((RectTransform)again.transform, 190, 462, 560, 88);
        var menu = KUI.MakeButton(card, "Main menu", KUI.Paper2, KUI.Ink, 270, 78, 32, () => Load(PauseMenu.MenuScene));
        KUI.Place((RectTransform)menu.transform, 190, 580, 270, 78);
        var quit = KUI.MakeButton(card, "Quit", KUI.ClaySoft, KUI.Ink, 270, 78, 32, Quit);
        KUI.Place((RectTransform)quit.transform, 480, 580, 270, 78);

        KUI.Select(again.gameObject);
        UISound.Open();
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        const float time = 1.4f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            group.alpha = k;
            card.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        card.localScale = Vector3.one;
    }

    static void Load(string scene)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
