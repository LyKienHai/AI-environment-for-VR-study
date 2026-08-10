using System.IO;
using UnityEngine;

/// <summary>
/// NPCDoctor — a 2D billboard sprite of an AI-generated doctor.
///
/// The doctor stands 1.5 m in front of the camera on the floor,
/// always rotating to face the camera (Y-axis billboard only so feet stay grounded).
/// Texture is loaded from npc_doctor.png in the output folder.
/// When npc_doctor.png changes on disk, the sprite auto-reloads.
///
/// SETUP:
///   1. In the same scene as AIRoomBox, create an empty GameObject → name it "NPC_Doctor"
///   2. Attach this script to it.
///   3. Press Play — the doctor appears automatically.
///
/// DIALOG (future):
///   Attach NPCDialog.cs to the same GameObject to add speech bubbles/conversation.
/// </summary>
public class NPCDoctor : MonoBehaviour
{
    [Header("Output folder (same as AIRoomBox)")]
    public string outputFolder = @"E:\ai_generator\output";

    [Header("NPC Appearance")]
    [Tooltip("Height of the sprite in meters. 1.8 = normal person height.")]
    public float npcHeight = 1.8f;

    [Tooltip("Width of the sprite in meters. Adjust to match generated image aspect ratio.")]
    public float npcWidth  = 0.8f;

    [Header("Room Dimensions (must match AIRoomBox)")]
    [Tooltip("Half the wallHeight from AIRoomBox. Camera is at y=0, floor is at y=-halfWallHeight.")]
    public float halfWallHeight = 1.35f;   // wallHeight 2.7 / 2

    [Tooltip("Distance from camera center to place the NPC (meters).")]
    public float distanceFromCamera = 1.5f;

    // ── internals ─────────────────────────────────────────────────────────────
    private GameObject    _quad;
    private Material      _mat;
    private FileSystemWatcher _watcher;
    private bool          _pendingReload = false;

    // ── NPC position helpers ──────────────────────────────────────────────────
    // Camera is at world y=0 (mid-room). Floor is at y=-halfWallHeight.
    // Bottom of NPC is at the floor. Center of NPC quad is at floor+npcHeight/2.
    float CenterY  => -halfWallHeight + npcHeight / 2f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        BuildQuad();
        LoadTexture();
        StartWatcher();
    }

    void Update()
    {
        // Billboard: keep facing camera on Y axis only (feet stay on ground)
        if (_quad != null && Camera.main != null)
        {
            Vector3 camPos  = Camera.main.transform.position;
            Vector3 dir     = _quad.transform.position - camPos;
            dir.y           = 0f;   // ignore vertical — Y-axis rotation only
            if (dir.sqrMagnitude > 0.0001f)
                _quad.transform.rotation = Quaternion.LookRotation(-dir);   // face TOWARD camera
        }

        if (_pendingReload)
        {
            _pendingReload = false;
            Invoke(nameof(LoadTexture), 0.5f);   // small delay so file write finishes
        }
    }

    void OnDestroy()
    {
        _watcher?.Dispose();
    }

    // ── build the sprite quad ─────────────────────────────────────────────────

    void BuildQuad()
    {
        _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quad.name = "DoctorSprite";
        _quad.transform.SetParent(transform, false);

        // Position: 1.5 m in front of camera, vertically centered on the sprite
        _quad.transform.localPosition = new Vector3(0f, CenterY, distanceFromCamera);
        _quad.transform.localScale    = new Vector3(npcWidth, npcHeight, 1f);

        // Remove collider (not needed for 2D NPC sprite)
        var col = _quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Transparent unlit material — works in both Built-in and URP
        Shader shader = Shader.Find("Unlit/Transparent")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Standard");

        _mat = new Material(shader);

        // URP: set surface type to Transparent
        _mat.SetFloat("_Surface",  1f);           // 1 = transparent
        _mat.SetFloat("_Blend",    0f);           // 0 = alpha blending
        _mat.SetFloat("_AlphaClip", 0f);
        _mat.SetInt("_SrcBlend",   (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend",   (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_ZWrite",     0);
        _mat.renderQueue           = 3000;         // transparent queue
        _mat.SetColor("_BaseColor", Color.white);
        _mat.SetColor("_Color",     Color.white);

        // Cull Off so the sprite renders from both sides
        _mat.SetInt("_Cull", 0);

        _quad.GetComponent<MeshRenderer>().material = _mat;

        Debug.Log($"[NPCDoctor] Sprite quad built at z={distanceFromCamera}m, y-center={CenterY:F2}m");
    }

    // ── texture loading ───────────────────────────────────────────────────────

    void LoadTexture()
    {
        string path = Path.Combine(outputFolder, "npc_doctor.png");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[NPCDoctor] npc_doctor.png not found at: {path}\n" +
                             "Run: python generate_npc.py");
            return;
        }

        byte[]    data = File.ReadAllBytes(path);
        Texture2D tex  = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data);

        _mat.mainTexture         = tex;
        _mat.SetTexture("_BaseMap", tex);  // URP
        _mat.SetTexture("_MainTex", tex);  // Built-in

        Debug.Log($"[NPCDoctor] Doctor sprite loaded ({tex.width}x{tex.height}).");
    }

    // ── file watcher — auto-reload when generate_npc.py reruns ───────────────

    void StartWatcher()
    {
        if (!Directory.Exists(outputFolder)) return;

        _watcher = new FileSystemWatcher(outputFolder, "npc_doctor.png")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (s, e) => _pendingReload = true;
        _watcher.EnableRaisingEvents = true;
    }
}
