using UnityEngine;

/// <summary>
/// SpectatorCamera — mirrors the VR headset view to the monitor.
///
/// HOW TO USE:
///   1. Create an empty GameObject in your scene, name it "SpectatorCamera"
///   2. Attach this script to it
///   3. Press Play — a second camera follows the VR camera and shows its view
///      on a 16:9 window in the bottom-right corner of the Game view.
///
/// When someone wears the headset, whoever watches the monitor sees
/// exactly the same view (left eye).
/// </summary>
public class SpectatorCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Leave empty — auto-finds the Main Camera (VR headset camera)")]
    public Transform vrCameraOverride;

    [Header("Smoothing")]
    [Tooltip("How smoothly the spectator cam follows. 0 = instant, 10 = very smooth")]
    public float smoothSpeed = 8f;

    [Header("Viewport (0-1 normalized screen coords)")]
    public Rect viewport = new Rect(0f, 0f, 1f, 1f); // full screen by default

    // ── private ──────────────────────────────────────────────
    Camera      _spectatorCam;
    Transform   _vrCam;

    void Start()
    {
        // Find VR camera
        _vrCam = vrCameraOverride;
        if (_vrCam == null)
        {
            var main = Camera.main;
            if (main != null) _vrCam = main.transform;
        }
        if (_vrCam == null)
        {
            Debug.LogWarning("[SpectatorCamera] Could not find VR camera. Assign vrCameraOverride manually.");
            enabled = false;
            return;
        }

        // Build spectator camera
        _spectatorCam = gameObject.AddComponent<Camera>();
        _spectatorCam.CopyFrom(Camera.main != null ? Camera.main : _vrCam.GetComponent<Camera>());
        _spectatorCam.rect          = viewport;
        _spectatorCam.depth         = 10;           // render on top
        _spectatorCam.stereoTargetEye = StereoTargetEyeMask.None; // flat output

        // Copy FOV from VR cam
        var vrCamComp = _vrCam.GetComponent<Camera>();
        if (vrCamComp) _spectatorCam.fieldOfView = vrCamComp.fieldOfView;

        Debug.Log("[SpectatorCamera] Spectator view active — following " + _vrCam.name);
    }

    void LateUpdate()
    {
        if (_vrCam == null) return;

        // Smooth follow
        transform.position = Vector3.Lerp(
            transform.position,
            _vrCam.position,
            Time.deltaTime * smoothSpeed * 10f);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            _vrCam.rotation,
            Time.deltaTime * smoothSpeed * 10f);
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        // Small label so you know which window is the spectator view
        GUI.Label(new Rect(10, 10, 300, 25),
            "<color=yellow><b>[Spectator View]</b></color>",
            new GUIStyle { richText = true, fontSize = 14 });
    }
#endif
}
