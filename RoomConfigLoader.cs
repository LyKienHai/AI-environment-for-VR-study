using System;
using System.IO;
using UnityEngine;

/// <summary>
/// RoomConfigLoader — reads room_config.json from the output folder and exposes
/// the palette + object data to all other room scripts.
///
/// SETUP:
///   Attach to the same GameObject as AIRoomBox. It auto-reads on Start.
///   All other scripts call RoomConfigLoader.Instance to get data.
///
/// JSON format expected at E:\ai_generator\output\room_config.json:
/// {
///   "room_type": "Arztpraxis",
///   "palette": {
///     "wall":    "#D4E9F7",
///     "floor":   "#7A6652",
///     "ceiling": "#F0EEE8"
///   },
///   "objects": {
///     "corner1":           { "type": "flower_pot",    "german": "der Blumentopf" },
///     "corner2":           { "type": "plant",         "german": "die Zimmerpflanze" },
///     "corner3":           { "type": "water_cooler",  "german": "der Wasserspender" },
///     "corner4":           { "type": "trash_bin",     "german": "der Mülleimer" },
///     "table":             { "type": "desk",          "german": "der Schreibtisch" },
///     "chair_doctor":      { "type": "office_chair",  "german": "der Arztstuhl" },
///     "chair_patient":     { "type": "patient_chair", "german": "der Patientenstuhl" },
///     "door":              {                           "german": "die Tür" },
///     "wall_picture_left": { "unsplash_query": "medical diploma", "german": "das Diplom" },
///     "wall_picture_back": { "unsplash_query": "anatomy chart",   "german": "das Poster" },
///     "wall_picture_right":{ "unsplash_query": "hospital room",   "german": "das Bild" }
///   },
///   "npc": {
///     "name": "Dr. Müller",
///     "context": "Du bist Dr. Müller..."
///   }
/// }
/// </summary>
public class RoomConfigLoader : MonoBehaviour
{
    public static RoomConfigLoader Instance { get; private set; }

    [Header("Config file location")]
    public string outputFolder = "";
    public string configFileName = "room_config.json";

    // ── parsed palette ────────────────────────────────────────────────────────
    public bool   HasConfig    { get; private set; } = false;
    public Color  WallColor    { get; private set; } = new Color(0.83f, 0.91f, 0.97f);  // default clinic blue-white
    public Color  FloorColor   { get; private set; } = new Color(0.48f, 0.40f, 0.32f);  // default warm beige
    public Color  CeilingColor { get; private set; } = new Color(0.94f, 0.93f, 0.91f);  // default off-white

    // ── parsed objects ────────────────────────────────────────────────────────
    public string Corner1Type   { get; private set; } = "flower_pot";
    public string Corner1German { get; private set; } = "der Blumentopf";
    public string Corner2Type   { get; private set; } = "plant";
    public string Corner2German { get; private set; } = "die Zimmerpflanze";
    public string Corner3Type   { get; private set; } = "water_cooler";
    public string Corner3German { get; private set; } = "der Wasserspender";
    public string Corner4Type   { get; private set; } = "trash_bin";
    public string Corner4German { get; private set; } = "der Mülleimer";
    public string TableGerman         { get; private set; } = "der Schreibtisch";
    public string ChairDoctorGerman   { get; private set; } = "der Arztstuhl";
    public string ChairPatientGerman  { get; private set; } = "der Patientenstuhl";
    public string DoorGerman          { get; private set; } = "die Tür";

    public string PicLeftQuery  { get; private set; } = "medical diploma certificate";
    public string PicBackQuery  { get; private set; } = "anatomy human body chart";
    public string PicRightQuery { get; private set; } = "hospital modern interior";
    public string PicLeftGerman  { get; private set; } = "das Diplom";
    public string PicBackGerman  { get; private set; } = "das Poster";
    public string PicRightGerman { get; private set; } = "das Bild";

    public string NpcName    { get; private set; } = "Dr. Müller";
    public string NpcContext { get; private set; } = "";
    public string RoomType   { get; private set; } = "Arztpraxis";

    // ── raw JSON for advanced use ─────────────────────────────────────────────
    public string RawJson { get; private set; } = "";

    void Awake()
    {
        Instance = this;
        if (string.IsNullOrEmpty(outputFolder))
            outputFolder = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, ".."));
        Reload();   // Load config immediately in Awake so HasConfig is true before AIRoomBox.Start() runs
    }

    /// <summary>Re-reads room_config.json. Call this after the file changes.</summary>
    public void Reload()
    {
        string path = Path.Combine(outputFolder, configFileName);
        if (!File.Exists(path))
        {
            Debug.Log($"[RoomConfigLoader] No config found at {path} — using defaults.");
            HasConfig = false;
            return;
        }

        try
        {
            RawJson = File.ReadAllText(path);
            ParseJson(RawJson);
            HasConfig = true;
            Debug.Log($"[RoomConfigLoader] Loaded config: {RoomType}, wall={ColorToHex(WallColor)}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomConfigLoader] Failed to parse {path}: {ex.Message}");
            HasConfig = false;
        }
    }

    // ── minimal JSON parser (no extra packages needed) ────────────────────────

    void ParseJson(string json)
    {
        RoomType = ExtractString(json, "room_type") ?? "Arztpraxis";

        // palette
        string paletteBlock = ExtractBlock(json, "palette");
        if (paletteBlock != null)
        {
            string wallHex    = ExtractString(paletteBlock, "wall");
            string floorHex   = ExtractString(paletteBlock, "floor");
            string ceilHex    = ExtractString(paletteBlock, "ceiling");
            if (wallHex  != null) WallColor    = HexToColor(wallHex);
            if (floorHex != null) FloorColor   = HexToColor(floorHex);
            if (ceilHex  != null) CeilingColor = HexToColor(ceilHex);
        }

        // objects
        string objBlock = ExtractBlock(json, "objects");
        if (objBlock != null)
        {
            Corner1Type   = ExtractNestedString(objBlock, "corner1",           "type")           ?? Corner1Type;
            Corner1German = ExtractNestedString(objBlock, "corner1",           "german")         ?? Corner1German;
            Corner2Type   = ExtractNestedString(objBlock, "corner2",           "type")           ?? Corner2Type;
            Corner2German = ExtractNestedString(objBlock, "corner2",           "german")         ?? Corner2German;
            Corner3Type   = ExtractNestedString(objBlock, "corner3",           "type")           ?? Corner3Type;
            Corner3German = ExtractNestedString(objBlock, "corner3",           "german")         ?? Corner3German;
            Corner4Type   = ExtractNestedString(objBlock, "corner4",           "type")           ?? Corner4Type;
            Corner4German = ExtractNestedString(objBlock, "corner4",           "german")         ?? Corner4German;
            TableGerman        = ExtractNestedString(objBlock, "table",            "german") ?? TableGerman;
            ChairDoctorGerman  = ExtractNestedString(objBlock, "chair_doctor",     "german") ?? ChairDoctorGerman;
            ChairPatientGerman = ExtractNestedString(objBlock, "chair_patient",    "german") ?? ChairPatientGerman;
            DoorGerman         = ExtractNestedString(objBlock, "door",             "german") ?? DoorGerman;
            PicLeftQuery  = ExtractNestedString(objBlock, "wall_picture_left",  "unsplash_query") ?? PicLeftQuery;
            PicBackQuery  = ExtractNestedString(objBlock, "wall_picture_back",  "unsplash_query") ?? PicBackQuery;
            PicRightQuery = ExtractNestedString(objBlock, "wall_picture_right", "unsplash_query") ?? PicRightQuery;
            PicLeftGerman  = ExtractNestedString(objBlock, "wall_picture_left",  "german") ?? PicLeftGerman;
            PicBackGerman  = ExtractNestedString(objBlock, "wall_picture_back",  "german") ?? PicBackGerman;
            PicRightGerman = ExtractNestedString(objBlock, "wall_picture_right", "german") ?? PicRightGerman;
        }

        // npc
        string npcBlock = ExtractBlock(json, "npc");
        if (npcBlock != null)
        {
            NpcName    = ExtractString(npcBlock, "name")    ?? NpcName;
            NpcContext = ExtractString(npcBlock, "context") ?? NpcContext;
        }
    }

    // ── tiny JSON helpers ─────────────────────────────────────────────────────

    /// Extracts the string value of a top-level key: "key": "value"
    static string ExtractString(string json, string key)
    {
        string search = $"\"{key}\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx + search.Length);
        if (colon < 0) return null;
        int q1 = json.IndexOf('"', colon + 1);
        if (q1 < 0) return null;
        int q2 = json.IndexOf('"', q1 + 1);
        if (q2 < 0) return null;
        return json.Substring(q1 + 1, q2 - q1 - 1);
    }

    /// Extracts the {...} block for a top-level key
    static string ExtractBlock(string json, string key)
    {
        string search = $"\"{key}\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return null;
        int brace = json.IndexOf('{', idx + search.Length);
        if (brace < 0) return null;
        int depth = 0, end = brace;
        for (int i = brace; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
        }
        return json.Substring(brace, end - brace + 1);
    }

    /// Extracts string from a nested object: objects.key.field
    static string ExtractNestedString(string objectsBlock, string key, string field)
    {
        string block = ExtractBlock(objectsBlock, key);
        if (block == null) return null;
        return ExtractString(block, field);
    }

    // ── color utilities ───────────────────────────────────────────────────────

    public static Color HexToColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            return new Color(r, g, b);
        }
        return Color.white;
    }

    public static string ColorToHex(Color c)
    {
        return $"#{(int)(c.r*255):X2}{(int)(c.g*255):X2}{(int)(c.b*255):X2}";
    }
}
