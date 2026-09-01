using UnityEngine;

/// <summary>
/// GameBootstrap — auto-creates the VR room and camera setup at runtime.
///
/// HOW IT WORKS:
///   [RuntimeInitializeOnLoadMethod] fires automatically every time Play starts.
///   It destroys any stale AIRoomBox from the scene (wrong position / old values)
///   and creates a fresh one at origin using values from RoomConfig.cs.
///
/// FOR NEW USERS (other computers / OS):
///   1. Drop GameBootstrap.cs, AIRoomBox.cs, VRMouseLook.cs, NPCDoctor.cs
///      and RoomConfig.cs into any Unity project's Assets folder.
///   2. Edit RoomConfig.cs — change OutputFolder to your local path.
///   3. Press Play — the room builds itself. Done.
///
/// CROSS-PLATFORM:
///   Works on Windows, Mac, Linux. Works on Unity 2020+ through Unity 6+.
/// </summary>
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        UnityEngine.Debug.Log("[GameBootstrap] Initializing VR room...");
        SetupCamera();
        SetupRoom();
        UnityEngine.Debug.Log("[GameBootstrap] Done. Room is ready.");
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    static void SetupCamera()
    {
        UnityEngine.Camera cam = UnityEngine.Camera.main;
        if (cam == null)
        {
            UnityEngine.Debug.LogWarning("[GameBootstrap] No Main Camera found.");
            return;
        }

        // Place camera at room center (0,0,0) looking straight ahead
        cam.transform.position = UnityEngine.Vector3.zero;
        cam.transform.rotation = UnityEngine.Quaternion.identity;

        // Black background
        cam.clearFlags      = UnityEngine.CameraClearFlags.SolidColor;
        cam.backgroundColor = UnityEngine.Color.black;

        // Add VRMouseLook if missing
        if (cam.GetComponent<VRMouseLook>() == null)
        {
            cam.gameObject.AddComponent<VRMouseLook>();
            UnityEngine.Debug.Log("[GameBootstrap] VRMouseLook added to Main Camera.");
        }
    }

    // ── Room ──────────────────────────────────────────────────────────────────

    static void SetupRoom()
    {
        // Destroy any existing AIRoomBox immediately — it may have a wrong
        // position or stale serialized values saved from a previous session.
        AIRoomBox existing = UnityEngine.Object.FindAnyObjectByType<AIRoomBox>();
        if (existing != null)
        {
            UnityEngine.Debug.Log("[GameBootstrap] Removing stale AIRoomBox from scene.");
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        // Always create a fresh AIRoomBox at world origin
        var roomObj = new UnityEngine.GameObject("AIRoomBox");
        roomObj.transform.position = UnityEngine.Vector3.zero;

        // Attach script — values must be set BEFORE Start() runs
        AIRoomBox room = roomObj.AddComponent<AIRoomBox>();
        room.floorSize  = RoomConfig.FloorSize;
        room.wallHeight = RoomConfig.WallHeight;

        // Attach config loader — must run before RoomAutoLoader so _loader ref is valid
        RoomConfigLoader loader = roomObj.AddComponent<RoomConfigLoader>();
        loader.outputFolder   = RoomConfig.OutputFolder;
        loader.configFileName = "room_config.json";

        // Attach auto-reloader — watches room_config.json and hot-reloads colours
        RoomAutoLoader autoLoader = roomObj.AddComponent<RoomAutoLoader>();
        autoLoader.outputFolder   = RoomConfig.OutputFolder;
        autoLoader.configFileName = "room_config.json";

        UnityEngine.Debug.Log(
            $"[GameBootstrap] AIRoomBox created at origin. " +
            $"Size={room.floorSize}x{room.wallHeight}  " +
            $"Folder={RoomConfig.OutputFolder}");
    }
}
