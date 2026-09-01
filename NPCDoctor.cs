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
    [Header("Output folder (leave blank to auto-detect quiz_images/ in project root)")]
    public string outputFolder = "";

    [Header("NPC Appearance")]
    [Tooltip("Height of the sprite in meters. 3.2 = prominently visible in a 5m room.")]
    public float npcHeight = 3.2f;

    [Tooltip("Width of the sprite in meters. Adjust to match generated image aspect ratio.")]
    public float npcWidth  = 1.6f;

    [Header("Room Dimensions (must match AIRoomBox)")]
    [Tooltip("Half the wallHeight from AIRoomBox. Camera is at y=0, floor is at y=-halfWallHeight.")]
    public float halfWallHeight = 2.5f;    // wallHeight 5 / 2  (AIRoomBox Wall Height = 5)

    [Tooltip("Distance in front of NPC_Doctor parent to place the sprite quad (meters).")]
    public float distanceFromCamera = 1.0f;

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
        // Auto-detect output folder: quiz_images/ next to the Unity project root.
        if (string.IsNullOrEmpty(outputFolder))
        {
            string projectRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, ".."));
            outputFolder = System.IO.Path.Combine(projectRoot, "quiz_images");
        }

        // Force room dimensions to match AlRoomBox (Wall Height = 5 → halfWallHeight = 2.5)
        // This overrides any stale Inspector value so the doctor stands on the floor.
        halfWallHeight = 2.5f;

        // Parent at Z=0.5, quad local Z=1.0 → doctor world Z=1.5:
        //   Desk back edge at Z=1.4, chair at Z=2.1
        //   Doctor stands at Z=1.5 = in front of chair, just behind desk ✓
        transform.position = new Vector3(0f, 0f, 0.5f);

        // Destroy any orphaned DoctorSprite quad from a previous domain reload
        var orphan = transform.Find("DoctorSprite");
        if (orphan != null) Destroy(orphan.gameObject);

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
        if (_quad != null) Destroy(_quad);   // prevent orphaned sprite on domain reload
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

        // Transparent unlit material — prefer URP/Unlit, fallback to legacy
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Transparent")
                     ?? Shader.Find("Standard");

        _mat = new Material(shader);

        // URP: enable transparency keywords so the texture renders with alpha
        _mat.SetFloat("_Surface",   1f);   // 1 = transparent in URP
        _mat.SetFloat("_Blend",     0f);   // 0 = Alpha (not Premultiply)
        _mat.SetFloat("_AlphaClip", 0f);
        _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        _mat.SetOverrideTag("RenderType", "Transparent");
        _mat.SetInt("_SrcBlend",   (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend",   (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_ZWrite",     0);
        _mat.renderQueue           = 3000;   // transparent queue
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
            Debug.LogWarning($"[NPCDoctor] npc_doctor.png not found at: {path} — showing placeholder.");
            ApplyPlaceholder();
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

    // ── placeholder doctor silhouette ────────────────────────────────────────

    void ApplyPlaceholder()
    {
        int w = 256, h = 512;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px  = new Color[w * h];

        Color bg        = new Color(0, 0, 0, 0);               // transparent
        Color coat      = new Color(0.04f, 0.64f, 0.72f, 1f);  // TEAL coat — visible against any wall
        Color shirt     = new Color(1.00f, 1.00f, 1.00f, 1f);  // white shirt
        Color skin      = new Color(0.95f, 0.80f, 0.65f, 1f);  // face/hands
        Color hairCol   = new Color(0.18f, 0.12f, 0.08f, 1f);  // dark hair
        Color pants     = new Color(0.12f, 0.16f, 0.26f, 1f);  // dark navy trousers
        Color shoes     = new Color(0.10f, 0.08f, 0.06f, 1f);  // dark shoes
        Color stetho    = new Color(0.90f, 0.80f, 0.20f, 1f);  // gold stethoscope
        Color coatLap   = new Color(0.03f, 0.50f, 0.58f, 1f);  // darker teal lapel

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)w;   // 0-1
                float ny = y / (float)h;   // 0-1 (0=bottom, 1=top)

                Color c = bg;

                // ── Shoes (bottom 5%) ─────────────────────────────────────────
                if (ny < 0.05f && nx > 0.26f && nx < 0.74f) c = shoes;

                // ── Trousers (5-40%) ─────────────────────────────────────────
                bool leftLeg  = nx > 0.27f && nx < 0.47f;
                bool rightLeg = nx > 0.53f && nx < 0.73f;
                if (ny > 0.05f && ny < 0.40f && (leftLeg || rightLeg)) c = pants;

                // ── White coat body (38-78%) ──────────────────────────────────
                if (ny > 0.38f && ny < 0.78f && nx > 0.18f && nx < 0.82f) c = coat;

                // ── Shirt visible in V-neck (60-72%, centre) ─────────────────
                float vNeck = Mathf.Abs(nx - 0.5f);
                if (ny > 0.60f && ny < 0.72f && vNeck < (ny - 0.60f) * 1.2f + 0.06f) c = shirt;

                // ── Coat lapels (shadow band each side of V-neck) ────────────
                if (ny > 0.62f && ny < 0.78f)
                {
                    float lapelW = 0.07f;
                    float vEdge  = (ny - 0.60f) * 1.2f + 0.06f;
                    if (vNeck > vEdge && vNeck < vEdge + lapelW) c = coatLap;
                }

                // ── Coat arms (38-68%) ────────────────────────────────────────
                bool leftArm  = nx > 0.06f && nx < 0.23f;
                bool rightArm = nx > 0.77f && nx < 0.94f;
                if (ny > 0.38f && ny < 0.68f && (leftArm || rightArm)) c = coat;

                // ── Stethoscope around neck ───────────────────────────────────
                float sty = ny - 0.71f;
                float stx = nx - 0.5f;
                float stRadius = Mathf.Sqrt(stx * stx * 8f + sty * sty * 8f);
                if (ny > 0.62f && ny < 0.70f && stRadius < 0.18f && stRadius > 0.12f) c = stetho;
                // Chest piece
                if (ny > 0.62f && ny < 0.65f && Mathf.Abs(nx - 0.42f) < 0.025f) c = stetho;

                // ── Neck (76-80%) ─────────────────────────────────────────────
                if (ny > 0.76f && ny < 0.80f && nx > 0.43f && nx < 0.57f) c = skin;

                // ── Head (80-94%) ─────────────────────────────────────────────
                float hx = (nx - 0.5f) * 2f;       // -1 to +1
                float hy = (ny - 0.87f) * 6f;       // scaled head space
                if (hx * hx + hy * hy < 1f) c = skin;

                // ── Hair (top of head 88-94%) ─────────────────────────────────
                float hxH = (nx - 0.5f) * 2f;
                float hyH = (ny - 0.915f) * 7.5f;
                if (hxH * hxH + hyH * hyH < 1f && ny > 0.875f) c = hairCol;
                // Side hair
                if (ny > 0.84f && ny < 0.90f && (nx < 0.30f || nx > 0.70f))
                {
                    float hsq = (nx - 0.5f) * (nx - 0.5f) * 4f + (ny - 0.87f) * (ny - 0.87f) * 36f;
                    if (hsq < 1f) c = hairCol;
                }

                // ── Eyes (83-85%) ─────────────────────────────────────────────
                if (ny > 0.831f && ny < 0.851f)
                {
                    if (Mathf.Abs(nx - 0.38f) < 0.045f) c = new Color(0.15f, 0.25f, 0.50f);
                    if (Mathf.Abs(nx - 0.62f) < 0.045f) c = new Color(0.15f, 0.25f, 0.50f);
                }

                // ── Mouth / smile (80-82%) ────────────────────────────────────
                if (ny > 0.800f && ny < 0.818f && Mathf.Abs(nx - 0.5f) < 0.07f)
                    c = new Color(0.72f, 0.42f, 0.38f);

                // ── Coat pocket & pen (43-52%, left side) ────────────────────
                if (ny > 0.43f && ny < 0.52f && nx > 0.58f && nx < 0.73f)
                {
                    if (ny < 0.435f || (nx > 0.58f && nx < 0.595f) || (nx > 0.715f && nx < 0.73f))
                        c = new Color(0.85f, 0.85f, 0.85f); // pocket border
                }
                // Pen clip
                if (ny > 0.44f && ny < 0.52f && Mathf.Abs(nx - 0.63f) < 0.008f)
                    c = new Color(0.10f, 0.30f, 0.70f);

                px[y * w + x] = (c.a > 0f) ? c : bg;
            }
        }

        // ── Outline pass: add dark border around the figure ────────────────────
        // Any transparent pixel adjacent to a solid pixel becomes a dark outline.
        Color outline = new Color(0.05f, 0.05f, 0.05f, 1f);
        var finalPx = new Color[w * h];
        System.Array.Copy(px, finalPx, w * h);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a < 0.5f)   // transparent pixel
                {
                    bool adj = false;
                    if (x > 0   && px[y*w+(x-1)].a > 0.5f) adj = true;
                    if (x < w-1 && px[y*w+(x+1)].a > 0.5f) adj = true;
                    if (y > 0   && px[(y-1)*w+x].a > 0.5f) adj = true;
                    if (y < h-1 && px[(y+1)*w+x].a > 0.5f) adj = true;
                    if (adj) finalPx[y*w+x] = outline;
                }
            }
        }

        tex.SetPixels(finalPx);
        tex.Apply();

        _mat.mainTexture         = tex;
        _mat.SetTexture("_BaseMap", tex);
        _mat.SetTexture("_MainTex", tex);
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
