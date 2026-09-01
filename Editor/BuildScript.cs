/// <summary>
/// BuildScript — Unity Editor build helper for Meta Quest (Android).
///
/// USAGE (command line / deploy_vr.bat):
///   Unity.exe -batchmode -quit -projectPath "C:\path\to\project" ^
///             -executeMethod BuildScript.BuildAndroid ^
///             -logFile build.log
///
/// OUTPUT:
///   Builds to  Builds\GermanVR.apk  (relative to project root).
///   Edit APK_PATH below to change the output location.
///
/// REQUIREMENTS inside Unity:
///   • Edit → Project Settings → Player → Android tab:
///       - Package Name:  com.yourname.germanvr
///       - Minimum API:   29 (Quest 2 / Pro / 3)
///       - Target API:    32+ (recommended)
///       - Scripting Backend: IL2CPP
///       - Target Architectures: ARM64 ✓
///   • Edit → Project Settings → XR Plug-in Management → Android:
///       - OpenXR ✓  (or Oculus XR Plugin ✓)
///   • Build Settings: Platform = Android, already switched
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public static class BuildScript
{
    // ── config ────────────────────────────────────────────────────────────────

    /// Output APK path (relative to Unity project root)
    private const string APK_PATH = @"Builds\GermanVR.apk";

    /// Scenes to include — add/remove as needed, in load order
    private static readonly string[] SCENES = new[]
    {
        "Assets/Scenes/Main.unity",   // ← adjust to your actual scene name(s)
    };

    // ── entry points ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Unity CLI (-executeMethod BuildScript.BuildAndroid).
    /// Builds a release APK for Meta Quest and exits Unity.
    /// </summary>
    public static void BuildAndroid()
    {
        string apkPath = Path.GetFullPath(APK_PATH);
        string outDir  = Path.GetDirectoryName(apkPath);
        if (!Directory.Exists(outDir))
            Directory.CreateDirectory(outDir);

        Debug.Log($"[BuildScript] Building Android APK → {apkPath}");

        var options = new BuildPlayerOptions
        {
            scenes           = GetEnabledScenes(),
            locationPathName = apkPath,
            target           = BuildTarget.Android,
            options          = BuildOptions.None,
        };

        BuildReport  report  = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] ✓ Build succeeded ({summary.totalSize / 1024 / 1024} MB) → {apkPath}");
        }
        else
        {
            Debug.LogError($"[BuildScript] ✗ Build FAILED: {summary.result}");
            // Exit with non-zero so deploy_vr.bat can detect failure
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Same as BuildAndroid but forces Development Build + Script Debugging.
    /// Useful for deploying a debug build to the Quest for testing.
    /// </summary>
    public static void BuildAndroidDev()
    {
        string apkPath = Path.GetFullPath(APK_PATH.Replace(".apk", "_dev.apk"));
        string outDir  = Path.GetDirectoryName(apkPath);
        if (!Directory.Exists(outDir))
            Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes           = GetEnabledScenes(),
            locationPathName = apkPath,
            target           = BuildTarget.Android,
            options          = BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        BuildReport  report  = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] ✓ Dev build succeeded → {apkPath}");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BuildScript] ✗ Dev build FAILED");
            EditorApplication.Exit(1);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// Returns scenes from SCENES[] if they exist on disk;
    /// falls back to scenes enabled in Build Settings.
    private static string[] GetEnabledScenes()
    {
        // Prefer the explicit list if the files exist
        bool allExist = true;
        foreach (var s in SCENES)
            if (!File.Exists(Path.GetFullPath(s))) { allExist = false; break; }

        if (allExist && SCENES.Length > 0)
            return SCENES;

        // Fallback: use Build Settings enabled scenes
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
            if (scene.enabled) scenes.Add(scene.path);

        if (scenes.Count == 0)
            Debug.LogWarning("[BuildScript] No scenes found! Add scenes to Build Settings.");

        return scenes.ToArray();
    }
}
#endif
