using System.IO;
using UnityEngine;

/// <summary>
/// DoorQuad — places a flat textured door on the front wall of the AI room.
///
/// SETUP:
///   1. Attach this script to the same GameObject as AIRoomBox (or any child).
///   2. Drop a door PNG into E:\ai_generator\output\door.png
///      (or generate one with the Gradio app / Poly Haven / etc.)
///   3. Hit Play — door appears centered on the front wall.
///
/// The door sits just inside the front wall face so it's always visible.
/// Default size: 1.2 m wide × 2.4 m tall (typical interior door).
/// </summary>
public class DoorQuad : MonoBehaviour
{
    [Header("Texture")]
    [Tooltip("Full path to door PNG. If file not found, door shows a solid brown placeholder.")]
    public string doorTexturePath = @"E:\ai_generator\output\door.png";

    [Header("Door Size (meters)")]
    public float doorWidth  = 1.2f;
    public float doorHeight = 2.4f;

    [Header("Room Settings — must match AIRoomBox values")]
    public float floorSize  = 10f;
    public float wallHeight = 5f;

    [Header("Horizontal offset from wall center (0 = center)")]
    public float horizontalOffset = 0f;

    private GameObject _doorGO;

    void Start()
    {
        SpawnDoor();
    }

    void SpawnDoor()
    {
        float hf = floorSize  / 2f;   // 5  — front wall Z position
        float hw = wallHeight / 2f;   // 2.5 — floor is at -hw in Y

        // Door sits just inside the front wall (z slightly < hf so it renders in front)
        float doorZ  = hf - 0.02f;

        // Bottom of door = floor level (-hw), center = bottom + half height
        float doorY  = -hw + doorHeight / 2f;

        _doorGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _doorGO.name = "DoorQuad";
        _doorGO.transform.SetParent(transform, false);
        _doorGO.transform.localPosition    = new Vector3(horizontalOffset, doorY, doorZ);
        _doorGO.transform.localEulerAngles = new Vector3(0, 180, 0);   // face inward like front wall
        _doorGO.transform.localScale       = new Vector3(doorWidth, doorHeight, 1f);

        // Remove collider
        var col = _doorGO.GetComponent<MeshCollider>();
        if (col != null) Destroy(col);

        // Shader — matches AIRoomBox
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Standard");

        var mat = new Material(shader);
        mat.SetInt("_Cull", 0);

        Texture2D tex = LoadTexture(doorTexturePath);
        if (tex != null)
        {
            mat.mainTexture = tex;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex",  tex);
            Debug.Log($"[DoorQuad] Texture loaded from {doorTexturePath}");
        }
        else
        {
            // Placeholder — solid brown so you know the door is there
            Color brown = new Color(0.45f, 0.25f, 0.08f);
            mat.SetColor("_BaseColor", brown);
            mat.SetColor("_Color",     brown);
            Debug.LogWarning($"[DoorQuad] Texture not found at {doorTexturePath} — showing placeholder. Drop a door.png there and re-run.");
        }

        _doorGO.GetComponent<MeshRenderer>().material = mat;

        Debug.Log($"[DoorQuad] Door placed at local pos ({_doorGO.transform.localPosition}), " +
                  $"size {doorWidth}x{doorHeight}m");
    }

    static Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path)) return null;
        byte[] data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(data);
        return tex;
    }
}
