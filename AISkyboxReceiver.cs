using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Attach to any GameObject. Watches the output PNG from ai_generator
/// and auto-applies it as a panoramic skybox whenever it changes.
/// No networking or admin rights required.
/// </summary>
public class AISkyboxReceiver : MonoBehaviour
{
    [Header("Image Path")]
    public string watchPath = @"E:\ai_generator\output\scene_360.png";

    private Material _skyboxMat;
    private FileSystemWatcher _watcher;
    private bool _pendingUpdate = false;
    private float _lastModified = 0f;

    void Start()
    {
        // Create a Panoramic skybox material
        _skyboxMat = new Material(Shader.Find("Skybox/Panoramic"));
        RenderSettings.skybox = _skyboxMat;

        // Load existing image on start if it exists
        if (File.Exists(watchPath))
            ApplySkybox();

        // Watch for file changes
        string dir = Path.GetDirectoryName(watchPath);
        string file = Path.GetFileName(watchPath);

        _watcher = new FileSystemWatcher(dir, file);
        _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
        _watcher.Changed += (s, e) => { _pendingUpdate = true; };
        _watcher.EnableRaisingEvents = true;

        Debug.Log($"[AISkyboxReceiver] Watching: {watchPath}");
    }

    void Update()
    {
        if (_pendingUpdate)
        {
            _pendingUpdate = false;
            // Small delay to ensure file write is complete
            Invoke(nameof(ApplySkybox), 0.5f);
        }
    }

    void ApplySkybox()
    {
        if (!File.Exists(watchPath))
        {
            Debug.LogWarning($"[AISkyboxReceiver] File not found: {watchPath}");
            return;
        }

        byte[] data = File.ReadAllBytes(watchPath);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(data);
        tex.wrapMode = TextureWrapMode.Repeat;

        _skyboxMat.SetTexture("_MainTex", tex);
        _skyboxMat.SetFloat("_Mapping", 1); // Latitude Longitude 360
        RenderSettings.skybox = _skyboxMat;
        DynamicGI.UpdateEnvironment();

        Debug.Log($"[AISkyboxReceiver] Skybox updated from {watchPath}");
    }

    void OnDestroy()
    {
        _watcher?.Dispose();
    }
}
