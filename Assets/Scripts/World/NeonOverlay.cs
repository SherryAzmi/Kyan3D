using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps the desert dark but its neon lights bright (the desert's grey shade and colour grading skip them).
/// The desert's grey mood shade (DesertShade) used to sit on a screen overlay, on top of everything,
/// so it greyed the neon guide beam too. Now:
///  - the shade is drawn by the desert camera itself (a camera-space canvas just in front of it),
///  - everything on the "Neon" layer (guide beams, their core / halo / ground ring) is left out of the
///    desert camera and drawn by an overlay camera stacked on top, after the shade, using the same depth,
///    so the beams stay hidden behind dunes but are never greyed.
/// Installs itself in any scene with the desert camera; no scene changes needed.
/// </summary>
public class NeonOverlay : MonoBehaviour
{
    public const int NeonLayer = 30;
    const string LeftCam = "Camera_Light_Left";

    Camera baseCam, overlayCam;
    Canvas refCanvas;
    CanvasScaler shadeScaler;

    // follow window resizes the way the old screen canvas did
    void LateUpdate()
    {
        if (refCanvas && shadeScaler) shadeScaler.scaleFactor = refCanvas.scaleFactor;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Install();

    static void Install()
    {
        var cam = GameObject.Find(LeftCam);
        if (!cam || cam.GetComponentInChildren<NeonOverlay>()) return;
        var go = new GameObject("Neon Overlay Camera");
        go.transform.SetParent(cam.transform, false);
        go.AddComponent<NeonOverlay>().baseCam = cam.GetComponent<Camera>();
    }

    // Start: after every AfterSceneLoad installer (DesertMood makes the shade there).
    void Start()
    {
        if (!baseCam) { Destroy(gameObject); return; }
        var baseData = baseCam.GetUniversalAdditionalCameraData();

        overlayCam = gameObject.AddComponent<Camera>();
        overlayCam.cullingMask = 1 << NeonLayer;
        overlayCam.clearFlags = CameraClearFlags.Nothing;
        var od = overlayCam.GetUniversalAdditionalCameraData();
        od.renderType = CameraRenderType.Overlay;
        // keep the base camera's depth so dunes and players still hide the beams (no public setter)
        typeof(UniversalAdditionalCameraData).GetField("m_ClearDepth",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(od, false);
        od.renderShadows = false;
        // no post on this layer: the desert's grey colour grading runs before it, so the neon keeps full colour
        od.renderPostProcessing = false;
        baseCam.cullingMask &= ~(1 << NeonLayer);
        baseData.cameraStack.Add(overlayCam);
        Sync();

        MoveShadeUnderNeon();
        RenderPipelineManager.beginCameraRendering += OnBegin;
    }

    void OnDestroy() => RenderPipelineManager.beginCameraRendering -= OnBegin;

    void OnBegin(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam == overlayCam) Sync();
    }

    // Cinemachine drives the base camera's lens; the overlay copies it every frame.
    void Sync()
    {
        overlayCam.fieldOfView = baseCam.fieldOfView;
        overlayCam.nearClipPlane = baseCam.nearClipPlane;
        overlayCam.farClipPlane = baseCam.farClipPlane;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    void MoveShadeUnderNeon()
    {
        var shade = FindAnyObjectByType<DesertShade>();
        if (!shade) return;
        var oldCanvas = shade.canvas;

        var cgo = new GameObject("Desert Shade Canvas", typeof(RectTransform));
        cgo.transform.SetParent(oldCanvas ? oldCanvas.transform.parent : null, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = baseCam;
        canvas.planeDistance = baseCam.nearClipPlane + 0.05f;
        // same canvas units as before (1920 x 1080 reference over the whole screen)
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = oldCanvas ? oldCanvas.scaleFactor : Screen.width / 1920f;

        refCanvas = oldCanvas;
        shadeScaler = scaler;
        shade.transform.SetParent(cgo.transform, false);
        // the canvas covers only the desert (left) half; the shade's rect is twice as wide, so its
        // centre (where it fades out) is still the mirror crack at the right edge of this half
        var rt = (RectTransform)shade.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(2f, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        shade.SetVerticesDirty();
    }
}
