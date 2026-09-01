using UnityEngine;

/// <summary>
/// AIRoomBox — builds a 6-sided room (4 walls + floor + ceiling) with solid
/// palette colors from room_config.json. Thin black cube strips run along all
/// 12 room edges so each face is clearly separated from any camera angle.
/// </summary>
public class AIRoomBox : MonoBehaviour
{
    [Header("Room Size (1 Unity unit = 1 meter)")]
    [Tooltip("Floor width and depth in meters.")]
    public float floorSize = 10f;

    [Tooltip("Wall height in meters. Camera sits at Y=0 (mid-height).")]
    public float wallHeight = 5f;

    [Header("Edge Lines")]
    [Tooltip("Thickness of the black edge strips between faces (meters).")]
    public float edgeLineWidth = 0.05f;

    // face order: front, back, left, right, floor, ceiling
    private static readonly string[] FaceLabels =
        { "Front Wall", "Back Wall", "Left Wall", "Right Wall", "Floor", "Ceiling" };

    private GameObject[] _faceObjects = new GameObject[6];

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        BuildRoom();
        BuildEdgeLines();   // black cube strips along all 12 edges

        if (RoomConfigLoader.Instance != null && RoomConfigLoader.Instance.HasConfig)
            ApplyPaletteColors();
        else
            Debug.Log("[AIRoomBox] No config yet — walls white. Hot-reload applies colors when room_config.json is saved.");

        Debug.Log($"[AIRoomBox] Room built. floorSize={floorSize} wallHeight={wallHeight}");
    }

    // ── geometry ──────────────────────────────────────────────────────────────

    void BuildRoom()
    {
        float hf = floorSize  / 2f;
        float hw = wallHeight / 2f;

        Vector3[] positions =
        {
            new Vector3( 0,  0,  hf),   // front  wall  +Z
            new Vector3( 0,  0, -hf),   // back   wall  -Z
            new Vector3(-hf, 0,   0),   // left   wall  -X
            new Vector3( hf, 0,   0),   // right  wall  +X
            new Vector3( 0, -hw,   0),  // floor        -Y
            new Vector3( 0,  hw,   0),  // ceiling      +Y
        };

        Vector3[] rotations =
        {
            new Vector3(  0, 180, 0),   // front  — normal -Z
            new Vector3(  0,   0, 0),   // back   — normal +Z
            new Vector3(  0,  90, 0),   // left   — normal +X
            new Vector3(  0, -90, 0),   // right  — normal -X
            new Vector3(-90,   0, 0),   // floor  — normal +Y
            new Vector3( 90,   0, 0),   // ceiling — normal -Y
        };

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

        if (shader == null) { Debug.LogError("[AIRoomBox] No usable shader!"); return; }

        for (int i = 0; i < 6; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = FaceLabels[i];
            go.transform.SetParent(transform, false);
            go.transform.localPosition    = positions[i];
            go.transform.localEulerAngles = rotations[i];
            go.transform.localScale       = scales[i];

            var col = go.GetComponent<MeshCollider>();
            if (col != null) Destroy(col);

            var mat = new Material(shader);
            mat.SetInt("_Cull", 0);         // double-sided
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color",     Color.white);
            go.GetComponent<MeshRenderer>().material = mat;
            _faceObjects[i] = go;
        }
    }

    // ── palette colors ────────────────────────────────────────────────────────

    /// Sets solid Material colors on all 6 faces from room_config.json palette.
    public void ApplyPaletteColors()
    {
        var cfg = RoomConfigLoader.Instance;
        if (cfg == null) return;

        Color[] faceColors = {
            cfg.WallColor,    // front
            cfg.WallColor,    // back
            cfg.WallColor,    // left
            cfg.WallColor,    // right
            cfg.FloorColor,   // floor
            cfg.CeilingColor  // ceiling
        };

        for (int i = 0; i < 6; i++)
        {
            if (_faceObjects[i] == null) continue;
            var mat = _faceObjects[i].GetComponent<MeshRenderer>().material;
            mat.mainTexture = null;
            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex",  null);
            mat.SetColor("_BaseColor", faceColors[i]);
            mat.SetColor("_Color",     faceColors[i]);
        }

        Debug.Log($"[AIRoomBox] Colors applied — wall:{RoomConfigLoader.ColorToHex(cfg.WallColor)} " +
                  $"floor:{RoomConfigLoader.ColorToHex(cfg.FloorColor)} " +
                  $"ceiling:{RoomConfigLoader.ColorToHex(cfg.CeilingColor)}");
    }

    // ── edge lines ────────────────────────────────────────────────────────────

    /// Builds thin black cube strips along all 12 room edges.
    /// Uses Cube primitives (not LineRenderer) so they're visible from any camera angle.
    void BuildEdgeLines()
    {
        float hf = floorSize  / 2f;
        float hw = wallHeight / 2f;

        // 8 corner points
        Vector3 ftl = new Vector3(-hf,  hw,  hf);  // front-top-left
        Vector3 ftr = new Vector3( hf,  hw,  hf);  // front-top-right
        Vector3 fbl = new Vector3(-hf, -hw,  hf);  // front-bottom-left
        Vector3 fbr = new Vector3( hf, -hw,  hf);  // front-bottom-right
        Vector3 btl = new Vector3(-hf,  hw, -hf);  // back-top-left
        Vector3 btr = new Vector3( hf,  hw, -hf);  // back-top-right
        Vector3 bbl = new Vector3(-hf, -hw, -hf);  // back-bottom-left
        Vector3 bbr = new Vector3( hf, -hw, -hf);  // back-bottom-right

        // 12 edges of a box
        Vector3[][] edges = {
            // 4 vertical corner edges
            new[] { fbl, ftl }, new[] { fbr, ftr },
            new[] { bbl, btl }, new[] { bbr, btr },
            // 4 top edges (wall-ceiling seams)
            new[] { ftl, ftr }, new[] { btl, btr },
            new[] { ftl, btl }, new[] { ftr, btr },
            // 4 bottom edges (wall-floor seams)
            new[] { fbl, fbr }, new[] { bbl, bbr },
            new[] { fbl, bbl }, new[] { fbr, bbr },
        };

        foreach (var edge in edges)
            MakeEdgeCube(edge[0], edge[1]);
    }

    /// Creates a thin black cube between two points in local space.
    void MakeEdgeCube(Vector3 a, Vector3 b)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "EdgeLine";
        go.transform.SetParent(transform, false);

        Vector3 dir    = b - a;
        float   length = dir.magnitude;

        go.transform.localPosition = (a + b) * 0.5f;
        // Align Y-axis of cube to the edge direction
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        go.transform.localScale    = new Vector3(edgeLineWidth, length, edgeLineWidth);

        // Remove collider
        var col = go.GetComponent<BoxCollider>();
        if (col != null) Destroy(col);

        // Black unlit material
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", Color.black);
        mat.SetColor("_Color",     Color.black);
        go.GetComponent<MeshRenderer>().material = mat;
    }
}
