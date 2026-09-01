using UnityEngine;

/// <summary>
/// DoctorEmptyRoom — Bare doctor's room shell + NPC, no furniture.
/// Use the Room AI Generator web UI to add objects freely one at a time.
///
/// Switch modes in the Builder tab → Room Canvas section:
///   🏗️ Empty Room   → this file is copied to Assets, DoctorRoomDecorator removed
///   🏥 Existing Room → DoctorRoomDecorator copied to Assets, this file removed
///
/// Unity auto-recompiles on file change; re-enter Play mode to see the switch.
/// </summary>
[DefaultExecutionOrder(-50)]
public class DoctorEmptyRoom : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        // Guard: don't double-spawn if walls already built.
        // An empty DoctorEmptyRoom in the scene (no children) means the walls
        // haven't been created yet — reuse that GameObject instead of skipping.
        var existing = GameObject.Find("DoctorEmptyRoom");
        if (existing != null && existing.transform.childCount > 0) return;

        var root = existing ?? new GameObject("DoctorEmptyRoom");

        // Walls are placed 0.3 units inside the AIRoomBox boundary so they
        // clearly win depth testing and avoid Z-fighting with AIRoomBox quads.
        const float W = 4.7f;   // wall centre inset from ±5 boundary
        const float H = 2.45f;  // floor/ceiling inset from ±2.5 boundary

        // ── Floor ────────────────────────────────────────────────────────────
        Prim(root, PrimitiveType.Cube, "Floor",
            new Vector3(0f, -H, 0f),
            new Vector3(10f, 0.05f, 10f),
            new Color(0.80f, 0.76f, 0.70f));

        // ── Ceiling ──────────────────────────────────────────────────────────
        Prim(root, PrimitiveType.Cube, "Ceiling",
            new Vector3(0f, H, 0f),
            new Vector3(10f, 0.05f, 10f),
            new Color(0.96f, 0.96f, 0.96f));

        // ── Walls ────────────────────────────────────────────────────────────
        Prim(root, PrimitiveType.Cube, "WallBack",
            new Vector3(0f, 0f, W),
            new Vector3(10f, 5f, 0.1f),
            new Color(0.92f, 0.89f, 0.84f));

        Prim(root, PrimitiveType.Cube, "WallFront",
            new Vector3(0f, 0f, -W),
            new Vector3(10f, 5f, 0.1f),
            new Color(0.92f, 0.89f, 0.84f));

        Prim(root, PrimitiveType.Cube, "WallLeft",
            new Vector3(-W, 0f, 0f),
            new Vector3(0.1f, 5f, 10f),
            new Color(0.90f, 0.87f, 0.82f));

        Prim(root, PrimitiveType.Cube, "WallRight",
            new Vector3(W, 0f, 0f),
            new Vector3(0.1f, 5f, 10f),
            new Color(0.90f, 0.87f, 0.82f));

        // NPC doctor is handled by NPCDoctor.cs — no capsule placeholder needed.

        Debug.Log("[DoctorEmptyRoom] Empty canvas loaded — add objects via Room AI Generator.");

        // Apply config colors immediately if available
        var loader = RoomConfigLoader.Instance;
        if (loader != null && loader.HasConfig)
            ApplyColors(root, loader.WallColor, loader.FloorColor, loader.CeilingColor);
    }

    /// <summary>Called by RoomAutoLoader.OnRoomConfigReloaded() when room_config.json changes.</summary>
    public void RefreshFromConfig()
    {
        var loader = RoomConfigLoader.Instance;
        if (loader == null || !loader.HasConfig) return;
        var root = GameObject.Find("DoctorEmptyRoom");
        if (root == null) return;
        ApplyColors(root, loader.WallColor, loader.FloorColor, loader.CeilingColor);
        Debug.Log($"[DoctorEmptyRoom] Colors refreshed — wall:{RoomConfigLoader.ColorToHex(loader.WallColor)}");
    }

    static void ApplyColors(GameObject root, Color wall, Color floor, Color ceiling)
    {
        foreach (Transform child in root.transform)
        {
            var rend = child.GetComponent<Renderer>();
            if (rend == null) continue;
            string n = child.name;
            Color c = n.StartsWith("Wall") ? wall
                    : n == "Floor"         ? floor
                    : n == "Ceiling"       ? ceiling
                    : rend.material.color;
            // Set both property names — URP Unlit uses _BaseColor, legacy Unlit/Color uses _Color
            rend.material.color = c;
            rend.material.SetColor("_BaseColor", c);
            if (rend.material.HasProperty("_Color"))
                rend.material.SetColor("_Color", c);
        }
    }

    static void Prim(GameObject parent, PrimitiveType type, string name,
                     Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (parent != null) go.transform.SetParent(parent.transform);

        // Use Unlit so wall colours exactly match the hex values (Lit picks up
        // HDRI ambient/reflection and makes cream walls look teal).
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat == null || mat.shader == null)
            mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = color;
        go.GetComponent<Renderer>().material = mat;
    }
}
