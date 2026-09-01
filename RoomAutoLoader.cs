using System;
using System.IO;
using UnityEngine;

/// <summary>
/// RoomAutoLoader — polls room_config.json every second and hot-reloads
/// whenever the file changes. Polling is more reliable than FileSystemWatcher
/// on Windows (FSW can miss events depending on how the file is written).
/// </summary>
public class RoomAutoLoader : MonoBehaviour
{
    [Header("Blank = auto-detect Unity project root")]
    public string outputFolder  = "";
    public string configFileName = "room_config.json";

    [Tooltip("How often to check for file changes (seconds).")]
    public float pollInterval = 1f;

    // ── internal state ────────────────────────────────────────────────────────
    private string   _configPath;
    private DateTime _lastWriteTime = DateTime.MinValue;
    private float    _nextPoll      = 0f;

    // Cached component refs
    private RoomConfigLoader _loader;
    private AIRoomBox        _roomBox;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (string.IsNullOrEmpty(outputFolder))
            outputFolder = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, ".."));

        _configPath = Path.Combine(outputFolder, configFileName);
        _loader     = GetComponent<RoomConfigLoader>();
        _roomBox    = GetComponent<AIRoomBox>();

        if (!Directory.Exists(outputFolder))
        {
            Debug.LogWarning($"[RoomAutoLoader] Folder not found: {outputFolder}");
            return;
        }

        // Seed the timestamp so we don't reload on first frame
        if (File.Exists(_configPath))
            _lastWriteTime = File.GetLastWriteTimeUtc(_configPath);

        // Apply current config immediately — DoctorEmptyRoom.Init() may have run
        // before GameBootstrap created this loader, giving walls the wrong color.
        if (_loader != null && _loader.HasConfig)
            OnRoomConfigReloaded();

        Debug.Log($"[RoomAutoLoader] Polling {_configPath} every {pollInterval}s.");
    }

    void Update()
    {
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + pollInterval;

        if (!File.Exists(_configPath)) return;

        DateTime wt = File.GetLastWriteTimeUtc(_configPath);
        if (wt <= _lastWriteTime) return;

        Debug.Log($"[RoomAutoLoader] Config changed — reloading…");
        ApplyNewConfig();

        // Only advance the timestamp if the config actually loaded.
        // If the file was empty (write race condition), HasConfig stays false
        // and we leave _lastWriteTime unchanged so the next poll retries.
        if (_loader != null && _loader.HasConfig)
            _lastWriteTime = wt;
    }

    // ── reload logic ──────────────────────────────────────────────────────────

    void ApplyNewConfig()
    {
        if (_loader != null) _loader.Reload();

        if (_roomBox != null && _loader != null && _loader.HasConfig)
        {
            _roomBox.SendMessage("ApplyPaletteColors", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[RoomAutoLoader] Room palette updated ✓");
        }

        OnRoomConfigReloaded();
    }

    protected virtual void OnRoomConfigReloaded()
    {
        // ── Direct: update DoctorEmptyRoom walls ─────────────────────────────
        if (_loader != null && _loader.HasConfig)
        {
            var doctorRoom = GameObject.Find("DoctorEmptyRoom");
            if (doctorRoom != null)
            {
                int updated = 0;
                foreach (Transform child in doctorRoom.transform)
                {
                    var rend = child.GetComponent<Renderer>();
                    if (rend == null) continue;
                    string n = child.name;
                    Color c = n.StartsWith("Wall") ? _loader.WallColor
                            : n == "Floor"         ? _loader.FloorColor
                            : n == "Ceiling"       ? _loader.CeilingColor
                            : rend.material.color;
                    rend.material.color = c;
                    rend.material.SetColor("_BaseColor", c);
                    if (rend.material.HasProperty("_Color"))
                        rend.material.SetColor("_Color", c);
                    updated++;
                }
                Debug.Log($"[RoomAutoLoader] DoctorEmptyRoom: {updated} surfaces recoloured ✓");
            }
        }

        // ── Broadcast: RefreshFromConfig / ReloadPictures on other scripts ───
        var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;
            var m1 = mb.GetType().GetMethod("RefreshFromConfig",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (m1 != null) m1.Invoke(mb, null);

            var m2 = mb.GetType().GetMethod("ReloadPictures",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (m2 != null) m2.Invoke(mb, null);
        }
    }
}
