using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause, settings and controls help for every scene except the main menu. Installs itself (no scene setup).
///  - Esc or the controller's Start button: pause panel (Continue, Restart puzzle, Exit to menu).
///  - In the split-screen puzzles each player gets two buttons at the top-left of their own half:
///    a controller "?" that shows THEIR controls (controller buttons and keyboard keys) and a gear for the
///    sound settings and Restart game. Keyboard: F1 / F2. Controller: Select / Share on pad 1 / pad 2.
///  - Circle (B) or Esc closes whatever is open. The game is frozen while anything is open.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public const string MenuScene = "MainMenu";
    public const string FirstScene = "Cutscene";

    public static bool IsPaused { get; private set; }

    enum Panel { None, Pause, Settings, Help1, Help2 }

    Canvas canvas;
    GameObject dim, pauseCard, settingsCard, confirmCard;
    GameObject[] helpCards = new GameObject[2];
    Text timeText, confirmTitle, confirmBody;
    System.Action confirmYes;
    Panel open = Panel.None;
    float prevScale = 1f;
    CursorLockMode prevLock;
    bool prevCursorVisible;
    bool split;

    // ---------------------------------------------------------------- install

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Install(s);

    static void Install(Scene s)
    {
        IsPaused = false;
        if (s.name == MenuScene) return;
        if (FindFirstObjectByType<PauseMenu>()) return;
        new GameObject("Kayyan Pause").AddComponent<PauseMenu>();
    }

    // ---------------------------------------------------------------- build

    static PauseMenu current;

    /// <summary>Open a panel by name (pause, settings, help1, help2, none). Used by the screenshot tool.</summary>
    public static void DebugShow(string name)
    {
        if (!current) return;
        switch (name)
        {
            case "pause": current.Show(Panel.Pause); break;
            case "settings": current.Show(Panel.Settings); break;
            case "help1": current.Show(Panel.Help1); break;
            case "help2": current.Show(Panel.Help2); break;
            default: current.Show(Panel.None); break;
        }
    }

    void Awake()
    {
        current = this;
        KUI.EnsureEventSystem();
        canvas = KUI.MakeCanvas("Kayyan UI", 500);
        canvas.transform.SetParent(transform, false);
        split = GameObject.Find("SplitScreenUI") != null;

        var dimImg = KUI.Img(canvas.transform, "Dim", new Color(0.1f, 0.06f, 0.02f, 0.55f), null, true);
        KUI.Stretch(dimImg.rectTransform);
        dim = dimImg.gameObject;

        BuildPause();
        settingsCard = KPanels.Settings(canvas.transform, () => Show(Panel.None), AskRestartGame).gameObject;

        confirmCard = KPanels.Confirm(canvas.transform, "Are you sure?", "Please confirm", "", () => { var y = confirmYes; confirmYes = null; y?.Invoke(); }, CancelConfirm).gameObject;
        // the card's last children are: eyebrow, title, body, Cancel, Yes
        confirmTitle = confirmCard.transform.GetChild(confirmCard.transform.childCount - 4).GetComponent<Text>();
        confirmBody = confirmCard.transform.GetChild(confirmCard.transform.childCount - 3).GetComponent<Text>();

        if (split) BuildHud();

        dim.SetActive(false);
        pauseCard.SetActive(false);
        settingsCard.SetActive(false);
        confirmCard.SetActive(false);
    }

    void BuildPause()
    {
        var card = KUI.Card(canvas.transform, "Pause", 640f, 700f);
        KUI.Place(KUI.Label(card, "PAUSED", 24, KUI.Ink2, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform, 0, 52, 640, 30);
        KUI.Place(KUI.Label(card, "Take a breath", 66, KUI.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform, 0, 90, 640, 84);
        timeText = KUI.Label(card, "", 24, KUI.Ink2, TextAnchor.MiddleCenter, false, FontStyle.Bold);
        KUI.Place(timeText.rectTransform, 0, 186, 640, 34);

        var cont = KUI.MakeButton(card, "Continue", KUI.Grass, KUI.Cream, 480, 92, 36, () => Show(Panel.None));
        KUI.Place((RectTransform)cont.transform, 80, 260, 480, 92);
        var rest = KUI.MakeButton(card, "Restart puzzle", KUI.Paper2, KUI.Ink, 480, 92, 36, () =>
            Ask("Restart", "Restart this puzzle?", "Progress in this puzzle will be lost.", RestartPuzzle));
        KUI.Place((RectTransform)rest.transform, 80, 384, 480, 92);
        var exit = KUI.MakeButton(card, "Exit to menu", KUI.ClaySoft, KUI.Ink, 480, 92, 36, () =>
            Ask("Exit", "Back to the main menu?", "Progress in this puzzle will be lost.", ExitToMenu));
        KUI.Place((RectTransform)exit.transform, 80, 508, 480, 92);
        pauseCard = card.gameObject;
    }

    void BuildHud()
    {
        for (int side = 0; side < 2; side++)
        {
            int n = side + 1;
            var row = KUI.Row(canvas.transform, "Player " + n + " corner", 14f);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(side == 0 ? 0f : 0.5f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(420f, 76f);

            var help = KUI.IconButton(row.transform, "pad", 76f, () => Toggle(n == 1 ? Panel.Help1 : Panel.Help2));
            var q = KUI.Box(help.transform, "Badge", KUI.Grass, 17, 3f, 0f);
            q.anchorMin = q.anchorMax = new Vector2(1f, 1f);
            q.sizeDelta = new Vector2(34f, 34f);
            q.anchoredPosition = new Vector2(2f, 2f);
            KUI.Stretch(KUI.Label(q, "?", 24, KUI.Cream, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform);
            KUI.IconButton(row.transform, "gear", 76f, () => Toggle(Panel.Settings));

            var chip = KUI.Box(row.transform, "Chip", n == 1 ? KUI.Hex("ffe3a1") : KUI.Hex("cdeeaa"), 20, 3.5f, 5f);
            var cle = chip.gameObject.AddComponent<LayoutElement>();
            cle.preferredWidth = 190f; cle.preferredHeight = 42f;
            KUI.Stretch(KUI.Label(chip, n == 1 ? "P1  Present" : "P2  Future", 22, KUI.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold).rectTransform);

            MakeHelp(side);
        }
    }

    /// <summary>
    /// (Re)builds a player's controls panel, under their corner buttons and inside their half of the screen.
    /// It is rebuilt every time it opens, so it always shows the device that player uses right now
    /// (controller, or keyboard and mouse when only one controller is plugged in).
    /// </summary>
    void MakeHelp(int side)
    {
        if (helpCards[side]) Destroy(helpCards[side]);
        var card = KPanels.Controls(canvas.transform, side + 1, () => Show(Panel.None));
        card.anchorMin = card.anchorMax = new Vector2(side == 0 ? 0f : 0.5f, 1f);
        card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = new Vector2(50f, -118f);
        card.localScale = Vector3.one * 0.88f;
        card.gameObject.SetActive(false);
        helpCards[side] = card.gameObject;
    }

    // ---------------------------------------------------------------- state

    void Toggle(Panel p) => Show(open == p ? Panel.None : p);

    void Show(Panel p)
    {
        if (p != Panel.None && open == Panel.None) { Freeze(); UISound.Open(); }
        else if (p == Panel.None && open != Panel.None) { Unfreeze(); UISound.Close(); }
        else if (p != open) UISound.Open();
        open = p;

        if (split && p == Panel.Help1) MakeHelp(0);
        if (split && p == Panel.Help2) MakeHelp(1);
        dim.SetActive(p != Panel.None);
        pauseCard.SetActive(p == Panel.Pause);
        settingsCard.SetActive(p == Panel.Settings);
        confirmCard.SetActive(false);
        for (int i = 0; i < 2; i++) if (helpCards[i]) helpCards[i].SetActive(p == (i == 0 ? Panel.Help1 : Panel.Help2));

        if (p == Panel.Pause) FocusFirst(pauseCard);
        else if (p == Panel.Settings) FocusFirst(settingsCard);
        else if (p == Panel.Help1) FocusFirst(helpCards[0]);
        else if (p == Panel.Help2) FocusFirst(helpCards[1]);
    }

    static void FocusFirst(GameObject go)
    {
        if (!go) return;
        var s = go.GetComponentInChildren<Selectable>(true);
        if (s) KUI.Select(s.gameObject);
    }

    void Freeze()
    {
        IsPaused = true;
        prevScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        prevLock = Cursor.lockState;
        prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Unfreeze()
    {
        IsPaused = false;
        Time.timeScale = prevScale <= 0f ? 1f : prevScale;
        AudioListener.pause = false;
        Cursor.lockState = prevLock;
        Cursor.visible = prevCursorVisible;
    }

    void OnDestroy()
    {
        if (open != Panel.None) { Time.timeScale = prevScale <= 0f ? 1f : prevScale; AudioListener.pause = false; }
        IsPaused = false;
    }

    // ---------------------------------------------------------------- confirm + actions

    Panel beforeConfirm;

    void Ask(string eyebrow, string title, string body, System.Action yes)
    {
        beforeConfirm = open;
        confirmYes = yes;
        confirmTitle.text = title;
        confirmBody.text = body;
        pauseCard.SetActive(false);
        settingsCard.SetActive(false);
        confirmCard.SetActive(true);
        FocusFirst(confirmCard);
    }

    void CancelConfirm()
    {
        confirmYes = null;
        confirmCard.SetActive(false);
        Show(beforeConfirm == Panel.None ? Panel.Pause : beforeConfirm);
    }

    void AskRestartGame() =>
        Ask("Restart", "Restart the game?", "You will start again from the beginning.", () => LoadAfterUnfreeze(FirstScene));

    void RestartPuzzle() => LoadAfterUnfreeze(SceneManager.GetActiveScene().name);
    void ExitToMenu() => LoadAfterUnfreeze(MenuScene);

    void LoadAfterUnfreeze(string scene)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
        open = Panel.None;
        SceneManager.LoadScene(scene);
    }

    // ---------------------------------------------------------------- input

    void Update()
    {
        var kb = Keyboard.current;
        bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
        bool start = false, back = false;
        foreach (var pad in Gamepad.all)
        {
            if (pad.startButton.wasPressedThisFrame) start = true;
            if (pad.buttonEast.wasPressedThisFrame) back = true;
        }

        if (split)
        {
            var pads = Gamepad.all;
            if ((kb != null && kb.f1Key.wasPressedThisFrame) || (pads.Count > 0 && pads[0].selectButton.wasPressedThisFrame)) Toggle(Panel.Help1);
            if ((kb != null && kb.f2Key.wasPressedThisFrame) || (pads.Count > 1 && pads[1].selectButton.wasPressedThisFrame)) Toggle(Panel.Help2);
        }

        if (confirmCard.activeSelf)
        {
            if (esc || back) CancelConfirm();
            return;
        }

        if (esc || start) Show(open == Panel.None ? Panel.Pause : Panel.None);
        else if (back && open != Panel.None) Show(Panel.None);

        if (open == Panel.Pause && timeText)
        {
            int t = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            timeText.text = SceneLabel(SceneManager.GetActiveScene().name) + "     TIME " + (t / 60) + ":" + (t % 60).ToString("00");
        }
    }

    static string SceneLabel(string scene)
    {
        switch (scene)
        {
            case "Cutscene": return "PROLOGUE";
            case "SplitCamera": return "PUZZLE 1 / 3";
            case "Puzzle2": return "PUZZLE 2 / 3";
            case "Puzzle3": return "PUZZLE 3 / 3";
            case "CafeEnding": return "EPILOGUE";
            default: return scene.ToUpperInvariant();
        }
    }
}
