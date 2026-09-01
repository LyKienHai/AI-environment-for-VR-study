using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Disables ALL colour-shifting post-processing overrides on every Global
/// Volume so that Unlit room-wall materials display the exact hex values
/// from the Room AI. Non-colour-shifting effects (Bloom, Vignette,
/// DepthOfField, MotionBlur, etc.) are left active.
///
/// Profiles are cloned at runtime — saved assets on disk are never modified.
/// </summary>
[DefaultExecutionOrder(-40)]
public class RoomColorAccuracy : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("[RoomColorAccuracy]");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<RoomColorAccuracy>();
    }

    void Start() => Apply();

    static void Apply()
    {
        foreach (var vol in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (!vol.isGlobal || vol.profile == null) continue;

            // Clone so the saved asset on disk is never modified
            vol.profile = Instantiate(vol.profile);

            int disabled = 0;

            foreach (var comp in vol.profile.components)
            {
                // Keep non-colour-shifting effects active
                if (comp is Bloom
                 || comp is Vignette
                 || comp is DepthOfField
                 || comp is MotionBlur
                 || comp is FilmGrain
                 || comp is PaniniProjection
                 || comp is LensDistortion
                 || comp is ChromaticAberration
                 ) // ScreenSpaceAmbientOcclusion is a RendererFeature, not a VolumeComponent
                    continue;

                // Disable everything else (all colour grading)
                if (comp.active)
                {
                    comp.active = false;
                    disabled++;
                    Debug.Log($"[RoomColorAccuracy] Disabled '{comp.GetType().Name}' on '{vol.name}'.");
                }
            }

            if (disabled > 0)
                Debug.Log($"[RoomColorAccuracy] '{vol.name}' — {disabled} colour effect(s) disabled. Room wall hex values should now match exactly.");
            else
                Debug.Log($"[RoomColorAccuracy] '{vol.name}' — no colour-shifting effects found.");
        }
    }
}
