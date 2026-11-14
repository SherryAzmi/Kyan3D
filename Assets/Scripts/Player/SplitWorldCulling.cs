using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps each player from seeing the other player's world. The worlds are 300 m apart (and the
/// level 3 ending room 600 m), so every gameplay camera is limited to <see cref="MaxView"/> metres.
/// Cinemachine cameras set the real camera's far clip from their Lens every frame, so the Lens is
/// what has to be limited. Installs itself in every scene; no scene or builder changes needed.
/// </summary>
public static class SplitWorldCulling
{
    public const float MaxView = 120f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Apply();

    static void Apply()
    {
        int n = 0;
        foreach (var vc in Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            var lens = vc.Lens;
            if (lens.FarClipPlane > MaxView) { lens.FarClipPlane = MaxView; vc.Lens = lens; n++; }
        }
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.farClipPlane > MaxView) { cam.farClipPlane = MaxView; n++; }
        if (n > 0) Debug.Log($"[SplitWorldCulling] {SceneManager.GetActiveScene().name}: limited {n} camera(s) to {MaxView} m so the other world stays hidden.");
    }
}
