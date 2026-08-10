using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// AIDepthSkybox — Option 1: Depth-warped panoramic sphere.
///
/// Creates a high-resolution sphere that you stand INSIDE. Each vertex is
/// displaced inward toward the camera based on the depth map value:
///   - Bright (far) pixels → stay near sphere surface
///   - Dark  (near) pixels → pushed toward the camera
///
/// This gives real parallax as the user rotates the camera — walls, desks,
/// chairs all appear at different depths. Much more "3D" than a flat skybox.
///
/// SETUP:
///   1. Create an empty GameObject, name it "DepthScene"
///   2. Attach this script to it
///   3. Run Play — it will build the sphere and load the images automatically
///   4. Generate a new image in Python — Unity updates in real time
/// </summary>
public class AIDepthSkybox : MonoBehaviour
{
    [Header("File Paths")]
    public string colorImagePath = @"E:\ai_generator\output\scene_360.png";
    public string depthImagePath = @"E:\ai_generator\output\scene_360_depth.png";

    [Header("Sphere Settings")]
    [Tooltip("Radius of the environment sphere. Camera must be at origin (0,0,0).")]
    public float sphereRadius = 50f;

    [Tooltip("Max vertex displacement toward camera. Higher = more exaggerated depth.")]
    public float maxDisplacement = 12f;

    [Tooltip("Horizontal segments. Higher = smoother depth warp. 120 is a good default.")]
    public int segments = 120;

    [Tooltip("Vertical rings. Higher = smoother depth warp. 60 is a good default.")]
    public int rings = 60;

    // ── internals ────────────────────────────────────────────────────────────
    private Mesh           _mesh;
    private MeshRenderer   _renderer;
    private Vector3[]      _baseVertices;   // undisplaced sphere vertices
    private Vector2[]      _uvs;
    private FileSystemWatcher _watcher;
    private bool           _pendingUpdate = false;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        BuildSphere();

        if (File.Exists(colorImagePath) && File.Exists(depthImagePath))
            ApplyImages();
        else if (File.Exists(colorImagePath))
            ApplyColorOnly();

        // Watch for new files from the Python generator
        string dir  = Path.GetDirectoryName(colorImagePath);
        string file = Path.GetFileName(colorImagePath);
        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (s, e) => _pendingUpdate = true;
        _watcher.EnableRaisingEvents = true;

        Debug.Log($"[AIDepthSkybox] Ready. Watching {colorImagePath}");
    }

    void Update()
    {
        if (_pendingUpdate)
        {
            _pendingUpdate = false;
            Invoke(nameof(ApplyImages), 0.6f); // wait for Python to finish writing both files
        }
    }

    void OnDestroy()
    {
        _watcher?.Dispose();
    }

    // ── sphere construction ───────────────────────────────────────────────────

    void BuildSphere()
    {
        // Add mesh components
        var mf = gameObject.AddComponent<MeshFilter>();
        _renderer = gameObject.AddComponent<MeshRenderer>();

        // Try URP first, then fall back to Built-in pipeline shaders
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Standard");

        if (shader == null)
            Debug.LogError("[AIDepthSkybox] Could not find any usable shader!");

        var mat = new Material(shader);
        // Cull Off = render both sides, so we see the inside of the sphere
        mat.SetInt("_Cull", 0);
        // Ensure full white base color so texture shows at full brightness
        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_Color", Color.white);
        _renderer.material = mat;

        // Build mesh
        _mesh = CreateInsideSphere(rings, segments, sphereRadius);
        mf.mesh = _mesh;

        // Store base (undisplaced) vertices for reuse on updates
        _baseVertices = (Vector3[])_mesh.vertices.Clone();
        _uvs = _mesh.uv;
    }

    /// <summary>
    /// Creates a sphere with inward-facing normals and correct UV mapping for
    /// equirectangular (panoramic) textures.
    /// </summary>
    static Mesh CreateInsideSphere(int rings, int segments, float radius)
    {
        var mesh = new Mesh
        {
            indexFormat = IndexFormat.UInt32,   // supports >65k vertices
        };

        int vertCount = (rings + 1) * (segments + 1);
        var vertices  = new Vector3[vertCount];
        var uvs       = new Vector2[vertCount];
        var normals   = new Vector3[vertCount];

        for (int r = 0; r <= rings; r++)
        {
            float phi = Mathf.PI * r / rings;          // 0 → π  (top to bottom)
            for (int s = 0; s <= segments; s++)
            {
                float theta = 2f * Mathf.PI * s / segments; // 0 → 2π
                int idx = r * (segments + 1) + s;

                float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = Mathf.Cos(phi);
                float z = Mathf.Sin(phi) * Mathf.Sin(theta);

                vertices[idx] = new Vector3(x, y, z) * radius;
                uvs[idx]      = new Vector2((float)s / segments, 1f - (float)r / rings);
                normals[idx]  = new Vector3(-x, -y, -z); // inward normals
            }
        }

        // Triangles wound for inside view
        var triangles = new int[rings * segments * 6];
        int ti = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r       * (segments + 1) + s;
                int b = r       * (segments + 1) + s + 1;
                int c = (r + 1) * (segments + 1) + s;
                int d = (r + 1) * (segments + 1) + s + 1;

                // Reversed winding so faces point inward
                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
                triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        mesh.vertices  = vertices;
        mesh.uv        = uvs;
        mesh.normals   = normals;
        mesh.triangles = triangles;
        return mesh;
    }

    // ── image loading & vertex displacement ──────────────────────────────────

    void ApplyColorOnly()
    {
        var colorTex = LoadTexture(colorImagePath);
        if (colorTex == null) return;

        _renderer.material.mainTexture = colorTex;
        _renderer.material.SetTexture("_BaseMap", colorTex);
        _renderer.material.SetTexture("_MainTex", colorTex);
        Debug.Log("[AIDepthSkybox] Color texture applied (no depth map yet).");
    }

    void ApplyImages()
    {
        // ── 1. Color texture ──────────────────────────────────────────────
        var colorTex = LoadTexture(colorImagePath);
        if (colorTex == null) return;

        colorTex.wrapMode = TextureWrapMode.Repeat;
        _renderer.material.mainTexture = colorTex;
        // URP Unlit uses _BaseMap; Built-in uses _MainTex; mainTexture covers both
        _renderer.material.SetTexture("_BaseMap", colorTex);
        _renderer.material.SetTexture("_MainTex", colorTex);

        // ── 2. Depth texture ──────────────────────────────────────────────
        if (!File.Exists(depthImagePath))
        {
            Debug.LogWarning("[AIDepthSkybox] Depth map not found, applying color only.");
            return;
        }

        var depthTex = LoadTexture(depthImagePath);
        if (depthTex == null) return;

        // ── 3. Displace vertices ──────────────────────────────────────────
        var vertices = (Vector3[])_baseVertices.Clone();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 dir = vertices[i].normalized; // unit direction from center

            float u = _uvs[i].x;
            float v = _uvs[i].y;

            // Bilinear sample the depth map
            float depth = SampleDepth(depthTex, u, v); // 0 = close, 1 = far

            // Near things (low depth) → pushed toward camera (reduce radius)
            // Far things  (high depth) → stay near sphere surface
            float displacement = (1f - depth) * maxDisplacement;
            vertices[i] = dir * (sphereRadius - displacement);
        }

        _mesh.vertices = vertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        DynamicGI.UpdateEnvironment();
        Debug.Log("[AIDepthSkybox] Depth-warped sphere updated!");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    static Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[AIDepthSkybox] File not found: {path}");
            return null;
        }
        byte[] data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data);
        return tex;
    }

    /// <summary>Bilinear sample normalized [0,1] at UV coordinates.</summary>
    static float SampleDepth(Texture2D tex, float u, float v)
    {
        float fx = u * (tex.width  - 1);
        float fy = v * (tex.height - 1);

        int x0 = Mathf.Clamp((int)fx, 0, tex.width  - 1);
        int y0 = Mathf.Clamp((int)fy, 0, tex.height - 1);
        int x1 = Mathf.Clamp(x0 + 1,  0, tex.width  - 1);
        int y1 = Mathf.Clamp(y0 + 1,  0, tex.height - 1);

        float tx = fx - x0;
        float ty = fy - y0;

        float d00 = tex.GetPixel(x0, y0).r;
        float d10 = tex.GetPixel(x1, y0).r;
        float d01 = tex.GetPixel(x0, y1).r;
        float d11 = tex.GetPixel(x1, y1).r;

        return Mathf.Lerp(
            Mathf.Lerp(d00, d10, tx),
            Mathf.Lerp(d01, d11, tx),
            ty
        );
    }
}
