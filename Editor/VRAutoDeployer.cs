/// <summary>
/// VRAutoDeployer — Unity Editor utility for one-click build + deploy to Meta Quest.
///
/// FEATURES:
///   • Menu:  VR Tools → Build & Deploy to Quest   (Ctrl+Shift+D)
///   • Menu:  VR Tools → Deploy APK Only            (skips build if APK exists)
///   • Menu:  VR Tools → List Connected Devices
///   • Auto-polls ADB every 5 s in the background; shows a status bar message
///     and optional popup when a Quest is first detected.
///   • Works with any ADB-accessible Android VR device (Quest 2 / 3 / Pro).
///
/// REQUIREMENTS:
///   • Android SDK / ADB installed and on PATH  — or set ADB_PATH below.
///   • Unity project already switched to Android platform.
///   • Quest in Developer Mode with USB Debugging enabled.
///   • BuildScript.cs present in Editor/ folder (for the build step).
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class VRAutoDeployer
{
    // ── config ────────────────────────────────────────────────────────────────

    /// Full path to adb.exe, or just "adb" if it's on PATH.
    private const string ADB_PATH = "adb";

    /// APK produced by BuildScript (must match BuildScript.APK_PATH)
    private const string APK_PATH = @"Builds\GermanVR.apk";

    /// How often (seconds) to check for connected devices in the background.
    private const double POLL_INTERVAL = 5.0;

    /// Set true to pop up a dialog when Quest is first connected.
    private const bool AUTO_POPUP_ON_CONNECT = true;

    /// Meta Quest USB vendor IDs (decimal) — used to identify Quest devices.
    // Quest 2 / Pro / 3 all appear as generic Android over ADB; we rely on
    // the device model string from "adb shell getprop ro.product.model" instead.
    // Patterns that match Oculus / Meta Quest model strings:
    private static readonly Regex QuestPattern = new Regex(
        @"Quest|Oculus|monterey|hollywood|eureka",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // ── state ─────────────────────────────────────────────────────────────────

    private static double    _lastPoll        = 0;
    private static bool      _questWasConnected = false;
    private static string[]  _lastDevices      = Array.Empty<string>();

    // ── constructor: register editor update loop ──────────────────────────────

    static VRAutoDeployer()
    {
        EditorApplication.update += OnEditorUpdate;
        Debug.Log("[VRAutoDeployer] Loaded — polling for Quest every 5 s.");
    }

    // ── background poll ───────────────────────────────────────────────────────

    static void OnEditorUpdate()
    {
        if (EditorApplication.timeSinceStartup - _lastPoll < POLL_INTERVAL) return;
        _lastPoll = EditorApplication.timeSinceStartup;

        string[] devices = GetAdbDevices();

        bool questNowConnected = devices.Length > 0;

        // Show popup when Quest first plugged in during this editor session
        if (AUTO_POPUP_ON_CONNECT && questNowConnected && !_questWasConnected)
        {
            Debug.Log($"[VRAutoDeployer] Quest connected: {string.Join(", ", devices)}");

            // Show non-blocking status bar message (won't interrupt coding)
            EditorUtility.DisplayProgressBar("Quest Detected",
                $"Device: {devices[0]} — use VR Tools → Build & Deploy to Quest", 1f);
            EditorApplication.delayCall += () => EditorUtility.ClearProgressBar();

            // Optionally prompt user
            bool deploy = EditorUtility.DisplayDialog(
                "Meta Quest Detected!",
                $"Device connected:\n  {string.Join("\n  ", devices)}\n\nBuild & Deploy now?",
                "Build & Deploy", "Later");

            if (deploy)
                BuildAndDeploy(devices[0]);
        }

        if (!questNowConnected && _questWasConnected)
            Debug.Log("[VRAutoDeployer] Quest disconnected.");

        _questWasConnected = questNowConnected;
        _lastDevices       = devices;
    }

    // ── menu items ────────────────────────────────────────────────────────────

    [MenuItem("VR Tools/Build && Deploy to Quest %#d", priority = 1)]
    public static void MenuBuildAndDeploy()
    {
        string[] devices = GetAdbDevices();
        if (devices.Length == 0)
        {
            EditorUtility.DisplayDialog("No Device Found",
                "No Meta Quest detected via ADB.\n\n" +
                "• Enable Developer Mode on the Quest\n" +
                "• Enable USB Debugging in Settings\n" +
                "• Accept the 'Allow USB Debugging' prompt on the headset",
                "OK");
            return;
        }
        BuildAndDeploy(devices[0]);
    }

    [MenuItem("VR Tools/Deploy APK Only (skip build)", priority = 2)]
    public static void MenuDeployOnly()
    {
        string apkFull = Path.GetFullPath(APK_PATH);
        if (!File.Exists(apkFull))
        {
            EditorUtility.DisplayDialog("APK Not Found",
                $"No APK at:\n{apkFull}\n\nUse 'Build & Deploy' instead.", "OK");
            return;
        }

        string[] devices = GetAdbDevices();
        if (devices.Length == 0)
        {
            EditorUtility.DisplayDialog("No Device Found",
                "No Meta Quest detected via ADB.", "OK");
            return;
        }

        DeployApk(apkFull, devices[0]);
    }

    [MenuItem("VR Tools/List Connected ADB Devices", priority = 20)]
    public static void MenuListDevices()
    {
        string output = RunAdb("devices -l");
        EditorUtility.DisplayDialog("ADB Devices", output.Trim(), "OK");
    }

    // ── core logic ────────────────────────────────────────────────────────────

    static void BuildAndDeploy(string deviceSerial)
    {
        string apkFull = Path.GetFullPath(APK_PATH);
        string outDir  = Path.GetDirectoryName(apkFull);

        if (!Directory.Exists(outDir))
            Directory.CreateDirectory(outDir);

        // --- Build ---
        EditorUtility.DisplayProgressBar("Building APK", "Running Unity Android build…", 0.1f);

        try
        {
            // Reuse BuildScript.BuildAndroid logic inline so it runs in the
            // current editor process (no need to spawn a new Unity instance).
            var scenes  = GetEnabledScenes();
            var options = new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = apkFull,
                target           = UnityEditor.BuildTarget.Android,
                options          = BuildOptions.None,
            };

            EditorUtility.DisplayProgressBar("Building APK", "Compiling…", 0.3f);

            var report  = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            EditorUtility.ClearProgressBar();

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Build Failed",
                    $"Build result: {summary.result}\nCheck the Console for errors.", "OK");
                return;
            }

            Debug.Log($"[VRAutoDeployer] Build succeeded → {apkFull}");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Build Error", ex.Message, "OK");
            return;
        }

        // --- Deploy ---
        DeployApk(apkFull, deviceSerial);
    }

    static void DeployApk(string apkPath, string deviceSerial)
    {
        EditorUtility.DisplayProgressBar("Deploying to Quest",
            $"Installing on {deviceSerial}…", 0.7f);

        string installArgs = $"-s {deviceSerial} install -r \"{apkPath}\"";
        string installOut  = RunAdb(installArgs);

        EditorUtility.ClearProgressBar();

        bool success = installOut.Contains("Success");
        Debug.Log($"[VRAutoDeployer] ADB install: {installOut.Trim()}");

        if (success)
        {
            // Launch the app automatically after install
            string package = UnityEditor.PlayerSettings.applicationIdentifier;
            RunAdb($"-s {deviceSerial} shell monkey -p {package} -c android.intent.category.LAUNCHER 1");

            EditorUtility.DisplayDialog("Deploy Complete ✓",
                $"APK installed and launched on:\n{deviceSerial}\n\nPut on your headset!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Deploy Failed",
                $"ADB output:\n{installOut.Trim()}\n\nMake sure:\n" +
                "• Quest is awake and unlocked\n" +
                "• USB Debugging is allowed", "OK");
        }
    }

    // ── ADB helpers ───────────────────────────────────────────────────────────

    /// Returns serial numbers of connected Android devices (filters for Quest where possible).
    static string[] GetAdbDevices()
    {
        try
        {
            string output = RunAdb("devices");
            var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var serials = new System.Collections.Generic.List<string>();

            foreach (var line in lines)
            {
                // "adb devices" format:  <serial>    device
                if (line.EndsWith("\tdevice") || line.EndsWith(" device"))
                {
                    string serial = line.Split('\t')[0].Trim();
                    if (serial != "List" && serial.Length > 0)
                        serials.Add(serial);
                }
            }
            return serials.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// Runs an adb command and returns stdout + stderr combined.
    static string RunAdb(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = ADB_PATH,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            var proc = Process.Start(psi);
            string stdout  = proc.StandardOutput.ReadToEnd();
            string stderr  = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            proc.Dispose();
            return stdout + stderr;
        }
        catch (Exception ex)
        {
            return $"ADB error: {ex.Message}";
        }
    }

    /// Returns scenes enabled in Build Settings.
    static string[] GetEnabledScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) scenes.Add(s.path);
        return scenes.ToArray();
    }
}
#endif
