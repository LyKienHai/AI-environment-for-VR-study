using UnityEngine;

/// <summary>
/// ObjectAnchorPlacer — spawns placeholder objects at fixed anchor positions
/// inside the AI room, reads German labels from RoomConfigLoader.
///
/// SETUP:
///   Attach to the same GameObject as AIRoomBox and RoomConfigLoader.
///   On Play, all objects appear with floating German labels.
///
/// ROOM LAYOUT (floorSize=10, wallHeight=5):
///   Floor Y = -2.5   |   Wall Z front = +5   |   Wall X left = -5
///
///   Corner1 (back-left)   (-4, floor, -4)  → flower pot / plant
///   Corner2 (back-right)  (+4, floor, -4)  → plant / shelf
///   Corner3 (front-left)  (-4, floor, +3)  → water cooler
///   Corner4 (front-right) (+4, floor, +3)  → trash bin
///   Table                 ( 0, floor, -1.5) → desk
///   Chair Doctor          ( 0, floor, -2.8) → behind table
///   Chair Patient         ( 0, floor, -0.3) → in front of table
///   Wall pic Left         (-4.9, +0.5, 0)  → on left wall
///   Wall pic Back         ( 0, +0.5, -4.9) → on back wall
///   Wall pic Right        (+4.9, +0.5, 0)  → on right wall
/// </summary>
[RequireComponent(typeof(RoomConfigLoader))]
public class ObjectAnchorPlacer : MonoBehaviour
{
    [Header("Room dimensions — must match AIRoomBox")]
    public float floorSize  = 10f;
    public float wallHeight = 5f;

    [Header("Show anchor markers (editor only)")]
    public bool showAnchors = true;

    private float _floorY;

    void Start()
    {
        _floorY = -(wallHeight / 2f);   // e.g. -2.5
        PlaceAll();
    }

    void PlaceAll()
    {
        var cfg = RoomConfigLoader.Instance;

        // ── Corners ───────────────────────────────────────────────────────────
        PlaceCornerObject("Corner1", new Vector3(-4, _floorY, -4),
            cfg?.Corner1Type   ?? "flower_pot",
            cfg?.Corner1German ?? "der Blumentopf", "flower pot");

        PlaceCornerObject("Corner2", new Vector3(+4, _floorY, -4),
            cfg?.Corner2Type   ?? "plant",
            cfg?.Corner2German ?? "die Zimmerpflanze", "plant");

        PlaceCornerObject("Corner3", new Vector3(-4, _floorY, +3f),
            cfg?.Corner3Type   ?? "water_cooler",
            cfg?.Corner3German ?? "der Wasserspender", "water cooler");

        PlaceCornerObject("Corner4", new Vector3(+4, _floorY, +3f),
            cfg?.Corner4Type   ?? "trash_bin",
            cfg?.Corner4German ?? "der Mülleimer", "bin");

        // ── Table (desk) ──────────────────────────────────────────────────────
        PlaceDesk(cfg?.TableGerman ?? "der Schreibtisch");

        // ── Chairs ────────────────────────────────────────────────────────────
        PlaceChair("ChairDoctor",  new Vector3(0, _floorY, -2.8f), 180f,
            cfg?.ChairDoctorGerman  ?? "der Arztstuhl",  "doctor's chair");

        PlaceChair("ChairPatient", new Vector3(0, _floorY, -0.3f), 0f,
            cfg?.ChairPatientGerman ?? "der Patientenstuhl", "patient's chair");

        Debug.Log("[ObjectAnchorPlacer] All objects placed.");
    }

    // ── corner object builder ─────────────────────────────────────────────────

    void PlaceCornerObject(string objName, Vector3 pos, string type, string german, string english)
    {
        var parent = new GameObject(objName);
        parent.transform.SetParent(transform, false);
        parent.transform.localPosition = pos;

        switch (type)
        {
            case "flower_pot":   BuildFlowerPot(parent);  break;
            case "plant":        BuildPlant(parent);       break;
            case "water_cooler": BuildWaterCooler(parent); break;
            case "trash_bin":    BuildTrashBin(parent);    break;
            default:             BuildGenericBox(parent, Color.gray, 0.3f, 0.5f, 0.3f); break;
        }

        // label floats above the object
        float h = GetApproxHeight(type);
        GermanLabel.Attach(parent, german, english, h + 0.15f);
    }

    float GetApproxHeight(string type)
    {
        switch (type)
        {
            case "flower_pot":   return 0.35f;
            case "plant":        return 0.8f;
            case "water_cooler": return 1.1f;
            case "trash_bin":    return 0.45f;
            default:             return 0.5f;
        }
    }

    // ── desk ──────────────────────────────────────────────────────────────────

    void PlaceDesk(string german)
    {
        var parent = new GameObject("Desk");
        parent.transform.SetParent(transform, false);
        parent.transform.localPosition = new Vector3(0, _floorY, -1.5f);

        // tabletop
        var top = CreatePrim(PrimitiveType.Cube, parent,
            Vector3.zero, new Vector3(1.6f, 0.06f, 0.8f),
            new Color(0.65f, 0.45f, 0.25f));   // wood brown

        // 4 legs
        float legH = 0.72f;
        float legW = 0.05f;
        Vector3[] legOffsets = {
            new Vector3(-0.75f, -legH/2f - 0.03f, -0.35f),
            new Vector3(+0.75f, -legH/2f - 0.03f, -0.35f),
            new Vector3(-0.75f, -legH/2f - 0.03f, +0.35f),
            new Vector3(+0.75f, -legH/2f - 0.03f, +0.35f),
        };
        foreach (var offset in legOffsets)
            CreatePrim(PrimitiveType.Cube, parent, offset,
                new Vector3(legW, legH, legW), new Color(0.5f, 0.33f, 0.15f));

        GermanLabel.Attach(parent, german, "desk", 0.5f);
    }

    // ── chair ─────────────────────────────────────────────────────────────────

    void PlaceChair(string objName, Vector3 pos, float yRot, string german, string english)
    {
        var parent = new GameObject(objName);
        parent.transform.SetParent(transform, false);
        parent.transform.localPosition = pos;
        parent.transform.localEulerAngles = new Vector3(0, yRot, 0);

        Color seatColor = new Color(0.18f, 0.35f, 0.60f);   // medical blue

        // seat
        CreatePrim(PrimitiveType.Cube, parent,
            new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.06f, 0.5f), seatColor);
        // back
        CreatePrim(PrimitiveType.Cube, parent,
            new Vector3(0, 0.75f, -0.22f), new Vector3(0.5f, 0.6f, 0.06f), seatColor);
        // legs
        float legH = 0.42f;
        Vector3[] lp = {
            new Vector3(-0.22f, 0.22f, -0.22f), new Vector3(+0.22f, 0.22f, -0.22f),
            new Vector3(-0.22f, 0.22f, +0.22f), new Vector3(+0.22f, 0.22f, +0.22f),
        };
        foreach (var lpp in lp)
            CreatePrim(PrimitiveType.Cube, parent, lpp,
                new Vector3(0.04f, legH, 0.04f), new Color(0.3f, 0.3f, 0.3f));

        GermanLabel.Attach(parent, german, english, 1.2f);
    }

    // ── corner object shapes ──────────────────────────────────────────────────

    void BuildFlowerPot(GameObject parent)
    {
        // pot (truncated cone approximated with cylinder)
        CreatePrim(PrimitiveType.Cylinder, parent,
            new Vector3(0, 0.12f, 0), new Vector3(0.15f, 0.12f, 0.15f),
            new Color(0.7f, 0.35f, 0.15f));
        // soil top
        CreatePrim(PrimitiveType.Cylinder, parent,
            new Vector3(0, 0.25f, 0), new Vector3(0.13f, 0.02f, 0.13f),
            new Color(0.3f, 0.2f, 0.1f));
        // small plant
        CreatePrim(PrimitiveType.Sphere, parent,
            new Vector3(0, 0.38f, 0), new Vector3(0.18f, 0.18f, 0.18f),
            new Color(0.2f, 0.6f, 0.2f));
    }

    void BuildPlant(GameObject parent)
    {
        // pot
        CreatePrim(PrimitiveType.Cylinder, parent,
            new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.15f, 0.2f),
            new Color(0.6f, 0.3f, 0.1f));
        // stem
        CreatePrim(PrimitiveType.Cylinder, parent,
            new Vector3(0, 0.55f, 0), new Vector3(0.04f, 0.4f, 0.04f),
            new Color(0.25f, 0.5f, 0.15f));
        // foliage
        CreatePrim(PrimitiveType.Sphere, parent,
            new Vector3(0, 0.95f, 0), new Vector3(0.4f, 0.5f, 0.4f),
            new Color(0.15f, 0.55f, 0.15f));
        CreatePrim(PrimitiveType.Sphere, parent,
            new Vector3(-0.15f, 0.85f, 0), new Vector3(0.25f, 0.3f, 0.25f),
            new Color(0.2f, 0.6f, 0.2f));
        CreatePrim(PrimitiveType.Sphere, parent,
            new Vector3(+0.15f, 0.85f, 0), new Vector3(0.25f, 0.3f, 0.25f),
            new Color(0.2f, 0.6f, 0.2f));
    }

    void BuildWaterCooler(GameObject parent)
    {
        // body
        CreatePrim(PrimitiveType.Cube, parent,
            new Vector3(0, 0.55f, 0), new Vector3(0.35f, 1.0f, 0.35f),
            new Color(0.85f, 0.87f, 0.9f));
        // water bottle (blue sphere on top)
        CreatePrim(PrimitiveType.Sphere, parent,
            new Vector3(0, 1.2f, 0), new Vector3(0.28f, 0.4f, 0.28f),
            new Color(0.4f, 0.65f, 0.9f));
        // spigot
        CreatePrim(PrimitiveType.Cube, parent,
            new Vector3(0, 0.45f, 0.18f), new Vector3(0.12f, 0.08f, 0.06f),
            new Color(0.2f, 0.4f, 0.8f));
    }

    void BuildTrashBin(GameObject parent)
    {
        CreatePrim(PrimitiveType.Cylinder, parent,
            new Vector3(0, 0.22f, 0), new Vector3(0.25f, 0.22f, 0.25f),
            new Color(0.5f, 0.5f, 0.5f));
        // lid
        CreatePrim(PrimitiveType.Cylinder, parent,
            new Vector3(0, 0.46f, 0), new Vector3(0.27f, 0.04f, 0.27f),
            new Color(0.3f, 0.3f, 0.3f));
    }

    void BuildGenericBox(GameObject parent, Color color, float w, float h, float d)
    {
        CreatePrim(PrimitiveType.Cube, parent,
            new Vector3(0, h/2f, 0), new Vector3(w, h, d), color);
    }

    // ── primitive factory ─────────────────────────────────────────────────────

    static GameObject CreatePrim(PrimitiveType type, GameObject parent,
                                  Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;

        // remove collider
        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // apply color
        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color",     color);
            rend.material = mat;
        }
        return go;
    }
}
