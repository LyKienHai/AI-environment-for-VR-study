// Editor/SetupXRSimulator.cs
// Adds a menu item: Tools → VR Testing → Setup XR Device Simulator
// Run this once to add the simulator prefab to your scene.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

public static class SetupXRSimulator
{
    [MenuItem("Tools/VR Testing/Setup XR Device Simulator")]
    static void AddSimulator()
    {
        // Check if one already exists
        var existing = Object.FindObjectOfType<XRDeviceSimulator>();
        if (existing != null)
        {
            Debug.Log("[SetupXRSimulator] XR Device Simulator already in scene.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Create simulator object
        var go = new GameObject("XR Device Simulator");
        go.AddComponent<XRDeviceSimulator>();
        Undo.RegisterCreatedObjectUndo(go, "Add XR Device Simulator");
        Selection.activeGameObject = go;

        Debug.Log("[SetupXRSimulator] XR Device Simulator added to scene.\n" +
                  "Controls in Play mode:\n" +
                  "  WASD       — move\n" +
                  "  Mouse      — look\n" +
                  "  Left Shift — hold to move controllers\n" +
                  "  G          — toggle grip\n" +
                  "  T          — toggle trigger\n" +
                  "  B          — toggle primary button");
    }

    [MenuItem("Tools/VR Testing/Add Spectator Camera")]
    static void AddSpectatorCamera()
    {
        var existing = Object.FindObjectOfType<SpectatorCamera>();
        if (existing != null)
        {
            Debug.Log("[SetupXRSimulator] Spectator Camera already in scene.");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("SpectatorCamera");
        go.AddComponent<SpectatorCamera>();
        Undo.RegisterCreatedObjectUndo(go, "Add Spectator Camera");
        Selection.activeGameObject = go;

        Debug.Log("[SetupXRSimulator] Spectator Camera added. Press Play to activate.");
    }

    [MenuItem("Tools/VR Testing/Setup Both (Simulator + Spectator)")]
    static void SetupBoth()
    {
        AddSimulator();
        AddSpectatorCamera();
        Debug.Log("[SetupXRSimulator] Both XR Device Simulator and Spectator Camera added.");
    }
}
#endif
