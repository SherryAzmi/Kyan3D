#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Editor-only screenshot helper. When the file Temp/kayyan_shots.txt exists (line 1 = job, line 2 = output folder),
/// the Play mode started by KayyanShots drives the UI, saves PNGs of the Game view and stops Play mode.
/// Jobs: menu, puzzle, note, end.
/// </summary>
public class ShotDriver : MonoBehaviour
{
    public const string Request = "Temp/kayyan_shots.txt";
    string job, dir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!File.Exists(Request)) return;
        var lines = File.ReadAllLines(Request);
        if (lines.Length < 2) return;
        var go = new GameObject("Shot Driver");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<ShotDriver>();
        d.job = lines[0].Trim();
        d.dir = lines[1].Trim();
    }

    IEnumerator Start()
    {
        Debug.Log("[Shots] start: " + job);
        Directory.CreateDirectory(dir);
        yield return new WaitForSecondsRealtime(5f);
        switch (job)
        {
            case "menu": yield return Menu(); break;
            case "puzzle": yield return Puzzle(); break;
            case "note": yield return Note(); break;
            case "end": yield return End(); break;
            case "video": yield return gameObject.AddComponent<Showreel>().Run(Path.Combine(dir, "kayyan_gameplay.avi")); break;
        }
        Debug.Log("[Shots] done: " + job);
        File.Delete(Request);
        UnityEditor.EditorApplication.isPlaying = false;
    }

    IEnumerator Menu()
    {
        yield return Shot("1_menu");
        MainMenu.Current.DebugPage("settings"); yield return new WaitForSecondsRealtime(0.7f); yield return Shot("2_settings");
        MainMenu.Current.DebugPage("controls"); yield return new WaitForSecondsRealtime(0.7f); yield return Shot("3_controls");
    }

    IEnumerator Puzzle()
    {
        var hud = FindFirstObjectByType<PuzzleHUD>();
        if (hud) hud.ShowBoth("The sand hides something... follow the light.", 60f);
        yield return new WaitForSecondsRealtime(1f);
        yield return Shot("4_hud_message");

        // walk the Present next to a mound so its prompt shows
        PlayerInteractor present = null;
        foreach (var p in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None)) if (p.hudSide == 0) present = p;
        var mound = FindFirstObjectByType<SandMound>();
        if (present && mound)
        {
            var cc = present.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            present.transform.position = mound.transform.position + new Vector3(-1.4f, 0.3f, 0f);
            if (cc) cc.enabled = true;
        }
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Shot("5_prompt");

        PauseMenu.DebugShow("help1"); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("6_help_p1");
        PauseMenu.DebugShow("help2"); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("7_help_p2");
        PauseMenu.DebugShow("settings"); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("8_settings");
        PauseMenu.DebugShow("pause"); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("9_pause");
        PauseMenu.DebugShow("none");
    }

    IEnumerator Note()
    {
        var fc = FindFirstObjectByType<FragmentCutscene>();
        if (!fc) { Debug.LogWarning("[Shots] no FragmentCutscene in this scene"); yield break; }
        fc.Play();
        yield return new WaitForSecondsRealtime(13f);
        yield return Shot("10_note_typing");
        yield return new WaitForSecondsRealtime(8f);
        yield return Shot("11_note_done");
    }

    IEnumerator End()
    {
        EndPanel.Show();
        yield return new WaitForSecondsRealtime(2.5f);
        yield return Shot("12_end_panel");
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
        yield return null;
    }
}
#endif
