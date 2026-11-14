using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Kayyan > UI > Add Hold Prompts To Open Scene
/// Builds the two circular hold-to-interact prompts (Void = cream, left half; Light = blue,
/// right half) under the Canvas of the scene that is currently open. Running it again
/// replaces the previous pair, so it is safe to re-run. Ctrl+Z undoes it.
/// </summary>
public static class HoldPromptBuilder
{
    const string Dir = "Assets/UI/HoldPrompt/";
    const float Size = 120f;      // prompt diameter in canvas units
    const float TexSize = 256f;   // the PNGs are 256 px
    const float RingMidRadius = 109f; // px, centre line of the ring in the PNG
    const float RingWidth = 18f;      // px

    [MenuItem("Kayyan/UI/Add Hold Prompts To Open Scene")]
    public static void Build()
    {
        var bg = LoadSprite("prompt_bg.png");
        var ring = LoadSprite("prompt_ring.png");
        var cap = LoadSprite("prompt_cap.png");
        var check = LoadSprite("prompt_check.png");
        if (!bg || !ring || !cap || !check)
        {
            EditorUtility.DisplayDialog("Hold Prompts",
                "Could not find the prompt images in " + Dir + ".", "OK");
            return;
        }

        var canvas = FindOrCreateCanvas();

        var voidPrompt = BuildPrompt(canvas.transform, "HoldPrompt_Void", 0.25f,
            Hex("#EFE8D8"), bg, ring, cap, check, Key.X, 0);
        BuildPrompt(canvas.transform, "HoldPrompt_Light", 0.75f,
            Hex("#7FD3EA"), bg, ring, cap, check, Key.M, 1);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = voidPrompt;
        Debug.Log("[HoldPromptBuilder] Added HoldPrompt_Void and HoldPrompt_Light under '" + canvas.name +
                  "'. Press Play and hold X (left) or M (right) to test.");
    }

    static GameObject BuildPrompt(Transform canvas, string name, float anchorX, Color ringColor,
        Sprite bg, Sprite ring, Sprite cap, Sprite check, Key demoKey, int demoPad)
    {
        var old = canvas.Find(name);
        if (old) Undo.DestroyObjectImmediate(old.gameObject);

        var root = Child(name, canvas, new Vector2(Size, Size));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add Hold Prompts");
        root.anchorMin = root.anchorMax = new Vector2(anchorX, 0f);
        root.anchoredPosition = new Vector2(0f, 140f);

        AddImage(Child("Background", root, new Vector2(Size, Size)), bg, Color.white);

        var ringImg = AddImage(Child("Ring", root, new Vector2(Size, Size)), ring, ringColor);
        ringImg.type = Image.Type.Filled;
        ringImg.fillMethod = Image.FillMethod.Radial360;
        ringImg.fillOrigin = (int)Image.Origin360.Top;
        ringImg.fillClockwise = true;
        ringImg.fillAmount = 0f;

        float scale = Size / TexSize;
        float capSize = RingWidth * scale;
        float capRadius = RingMidRadius * scale;

        var startCap = AddImage(Child("StartCap", root, new Vector2(capSize, capSize)), cap, ringColor);
        startCap.rectTransform.anchoredPosition = new Vector2(0f, capRadius);

        var pivot = Child("EndCapPivot", root, Vector2.zero);
        var endCap = AddImage(Child("EndCap", pivot, new Vector2(capSize, capSize)), cap, ringColor);
        endCap.rectTransform.anchoredPosition = new Vector2(0f, capRadius);

        var keyRt = Child("Key", root, new Vector2(Size, Size));
        var key = keyRt.gameObject.AddComponent<TextMeshProUGUI>();
        key.text = "X";
        key.fontSize = 44f;
        key.fontStyle = FontStyles.Bold;
        key.alignment = TextAlignmentOptions.Center;
        key.color = Hex("#EFE8D8");
        key.raycastTarget = false;

        var checkImg = AddImage(Child("Check", root, new Vector2(Size, Size)), check, ringColor);

        var ui = root.gameObject.AddComponent<HoldPromptUI>();
        ui.ring = ringImg;
        ui.startCap = startCap;
        ui.endCap = endCap;
        ui.endCapPivot = pivot;
        ui.keyLabel = key;
        ui.check = checkImg;
        ui.ringColor = ringColor;
        ui.SetProgress(0f);

        var demo = root.gameObject.AddComponent<HoldPromptDemo>();
        demo.holdKey = demoKey;
        demo.gamepadIndex = demoPad;

        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(demo);
        return root.gameObject;
    }

    static RectTransform Child(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }

    static Image AddImage(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Canvas FindOrCreateCanvas()
    {
        var active = EditorSceneManager.GetActiveScene();
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas && c.gameObject.scene == active)
                return c;

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        Undo.RegisterCreatedObjectUndo(go, "Add Hold Prompts");
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>Loads a prompt PNG as a sprite, switching its import settings to Sprite first if needed.</summary>
    static Sprite LoadSprite(string file)
    {
        string path = Dir + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            importer.mipmapEnabled ||
            settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
