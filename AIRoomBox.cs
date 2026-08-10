using System.IO;
using UnityEngine;

/// <summary>
/// AIRoomBox — builds a 6-sided room (4 walls + floor + ceiling) and textures
/// each face from a separate AI-generated image. Camera stands in the middle.
///
/// Expects these 6 files in outputFolder:
///   wall_front.png, wall_back.png, wall_left.png, wall_right.png,
///   floor.png, ceiling.png
///
/// SETUP:
///   1. Create an empty GameObject, name it "AIRoomBox"
///   2. Attach this script to it — keep it at position (0,0,0)
///   3. Put VRMouseLook.cs on Main Camera, position camera at (0,0,0)
///   4. Run Play — room builds automatically and loads images
/// </summary>
public class AIRoomBox : MonoBehaviour
{
    [Header("Output Folder (must match Python OUTPUT_DIR)")]
    public string outputFolder = @"E:\ai_generator\output";

    [Header("Room Size (1 Unity unit = 1 meter)")]
    [Tooltip("Floor width and depth in meters.")]
    public float floorSize = 10f;

    [Tooltip("Wall height in meters. Camera sits at Y=0 (mid-height).")]
    public float wallHeight = 5f;

    [Header("Debug")]
    [Tooltip("Show solid colors instead of AI textures (for geometry testing)")]
    public bool debugColors = false;

    // face order: front, back, left, right, floor, ceiling
    private static readonly string[] FaceFiles   = { "wall_front", "wall_back", "wall_left", "wall_right", "floor", "ceiling" };
    private static readonly string[] FaceLabels  = { "Front Wall", "Back Wall", "Left Wall", "Right Wall", "Floor", "Ceiling" };
    private static readonly Color[]  DebugColors = {
        Color.red,      // front  wall
        Color.blue,     // back   wall
        Color.green,    // left   wall
        Color.yellow,   // right  wall
        new Color(1f, 0.5f, 0f),   // floor  (orange)
        Color.cyan,     // ceiling
    };

    private GameObject[] _faceObjects = new GameObject[6];
    private FileSystemWatcher _watcher;
    private bool _pendingUpdate = false;

    void Start()
    {
        BuildRoom();
        if (!debugColors)
            LoadAllTextures();
        StartWatcher();
        Debug.Log($"[AIRoomBox] Room built. floorSize={floorSize} wallHeight={wallHeight}. Watching {outputFolder}");
    }

    void Update()
    {
        if (_pendingUpdate)
        {
            _pendingUpdate = false;
            Invoke(nameof(LoadAllTextures), 1.0f);
        }
    }

    void OnDestroy()
    {
        _watcher?.Dispose();
    }

    // ── geometry ──────────────────────────────────────────────────────────────

    void BuildRoom()
    {
        float hf = floorSize  / 2f;   // half floor size  (e.g. 5 for 10m room)
        float hw = wallHeight / 2f;   // half wall height (e.g. 2.5 for 5m walls)

        // All positions are LOCAL (relative to this GameObject at 0,0,0)
        Vector3[] positions =
        {
            new Vector3( 0,  0,  hf),   // front  wall  +Z
            new Vector3( 0,  0, -hf),   // back   wall  -Z
            new Vector3(-hf, 0,   0),   // left   wall  -X
            new Vector3( hf, 0,   0),   // right  wall  +X
            new Vector3( 0, -hw,   0),  // floor        -Y
            new Vector3( 0,  hw,   0),  // ceiling      +Y
        };

        // Rotations so the INWARD face of each quad points toward the room center
        Vector3[] rotations =
        {
            new Vector3(  0, 180, 0),   // front  wall — normal points -Z (toward camera)
            new Vector3(  0,   0, 0),   // back   wall — normal points +Z
            new Vector3(  0,  90, 0),   // left   wall — normal points +X
            new Vector3(  0, -90, 0),   // right  wall — normal points -X
            new Vector3(-90,   0, 0),   // floor  — normal points +Y (up toward camera)
            new Vector3( 90,   0, 0),   // ceiling — normal points -Y (down toward camera)
        };

        // walls: floorSize wide × wallHeight tall | floor/ceiling: floorSize × floorSize
        Vector3[] scales =
        {
            new Vector3(floorSize, wallHeight, 1f),
            new Vector3(floorSize, wallHeight, 1f),
            new Vector3(floorSize, wallHeight, 1f),
            new Vector3(floorSize, wallHeight, 1f),
            new Vector3(floorSize, floorSize,  1f),
            new Vector3(floorSize, floorSize,  1f),
        };

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("[AIRoomBox] Could not find any usable shader!");
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = FaceLabels[i];
            go.transform.SetParent(transform, false);
            go.transform.localPosition    = positions[i];
            go.transform.localEulerAngles = rotations[i];
            go.transform.localScale       = scales[i];

            // Remove unneeded collider
            var col = go.GetComponent<MeshCollider>();
            if (col != null) Destroy(col);

            var mat = new Material(shader);
            mat.SetInt("_Cull", 0);   // double-sided — visible from inside

            if (debugColors)
            {
                mat.SetColor("_BaseColor", DebugColors[i]);
                mat.SetColor("_Color",     DebugColors[i]);
            }
            else
            {
                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_Color",     Color.white);
            }

            go.GetComponent<MeshRenderer>().material = mat;
            _faceObjects[i] = go;

            Vector3 wp = go.transform.position;
            Vector3 ws = go.transform.lossyScale;
            Debug.Log($"[AIRoomBox] Built {FaceLabels[i]}  pos=({wp.x:F2},{wp.y:F2},{wp.z:F2})  scale=({ws.x:F2},{ws.y:F2},{ws.z:F2})");
        }
    }

    // ── texturing ─────────────────────────────────────────────────────────────

    void LoadAllTextures()
    {
        int loaded = 0;
        for (int i = 0; i < 6; i++)
        {
            string path = Path.Combine(outputFolder, FaceFiles[i] + ".png");
            var tex = LoadTexture(path);
            if (tex == null)
            {
                Debug.LogWarning($"[AIRoomBox] Missing: {path}");
                continue;
            }
            var mat = _faceObjects[i].GetComponent<MeshRenderer>().material;
            mat.mainTexture = tex;
            mat.SetTexture("_BaseMap",  tex);   // URP Lit / Unlit
            mat.SetTexture("_MainTex",  tex);   // Built-in
            loaded++;
        }
        Debug.Log($"[AIRoomBox] Textures loaded: {loaded}/6");
    }

    static Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path)) return null;
        byte[] data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data);
        return tex;
    }

    void StartWatcher()
    {
        if (!Directory.Exists(outputFolder))
        {
            Debug.LogWarning($"[AIRoomBox] Output folder not found: {outputFolder}");
            return;
        }
        _watcher = new FileSystemWatcher(outputFolder, "wall_front.png")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (s, e) => _pendingUpdate = true;
        _watcher.EnableRaisingEvents = true;
    }
}
