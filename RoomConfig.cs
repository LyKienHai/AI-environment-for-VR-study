/// <summary>
/// RoomConfig — the only file users need to edit for their machine.
///
/// SETUP FOR A NEW USER:
///   1. Change OutputFolder to wherever your Python AI generator saves images.
///   2. Optionally adjust room size.
///   3. Press Play in Unity — everything else is automatic.
///
/// FINDING YOUR OUTPUT FOLDER:
///   - Windows default: C:\ai_generator\output  OR  E:\ai_generator\output
///   - Mac default:     /Users/yourname/ai_generator/output
///   - Linux default:   /home/yourname/ai_generator/output
///   Look for these files inside: wall_front.png, wall_back.png, wall_left.png,
///   wall_right.png, floor.png, ceiling.png
/// </summary>
public static class RoomConfig
{
    // =====================================================================
    //  EDIT THESE VALUES FOR YOUR MACHINE
    // =====================================================================

    /// <summary>
    /// Folder where your Python pipeline saves the 6 wall/floor/ceiling images.
    /// Must contain: wall_front.png, wall_back.png, wall_left.png,
    ///               wall_right.png, floor.png, ceiling.png
    /// </summary>
    public static string OutputFolder = "";

    // =====================================================================
    //  ROOM DIMENSIONS  (1 Unity unit = 1 real-world meter)
    // =====================================================================

    /// <summary>Floor width and depth in meters. Default: 10m x 10m.</summary>
    public static float FloorSize = 10f;

    /// <summary>Wall height in meters. Camera stands at Y=0 (mid-height).</summary>
    public static float WallHeight = 5f;

    // =====================================================================
    //  DEBUG
    // =====================================================================

    /// <summary>
    /// TRUE  = solid colors (Red/Blue/Green/Yellow/Orange/Cyan) — test geometry.
    /// FALSE = real AI-generated textures from OutputFolder.
    /// </summary>
    public static bool DebugColors = false;
}
