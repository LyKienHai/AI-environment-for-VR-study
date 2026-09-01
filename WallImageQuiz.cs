using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// WallImageQuiz — loads AI-generated medical images onto the 3 wall frames.
/// No quiz logic — purely decorative. Images refresh from Pollinations.ai each session.
/// </summary>
public class WallImageQuiz : MonoBehaviour
{
    public static WallImageQuiz Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        var go = new GameObject("WallImageQuiz");
        DontDestroyOnLoad(go);
        go.AddComponent<WallImageQuiz>();
    }

    [Header("AI Output folder (leave blank to auto-detect quiz_images/ next to project)")]
    public string outputFolder = "";

    [Header("References (auto-found if blank)")]
    public DoctorRoomDecorator roomDecorator;

    struct MedItem
    {
        public string german;
        public MedItem(string g) { german = g; }
        public string ImageFileName => german.ToLower()
            .Replace(" ", "_").Replace("ä","ae").Replace("ö","oe")
            .Replace("ü","ue").Replace("ß","ss") + ".png";
    }

    static readonly MedItem[] _items = new MedItem[]
    {
        new MedItem("Stethoskop"), new MedItem("Thermometer"), new MedItem("Spritze"),
        new MedItem("Pflaster"),   new MedItem("Blutdruckgeraet"), new MedItem("Skalpell"),
        new MedItem("Verband"),    new MedItem("Roentgenbild"),    new MedItem("Tablette"),
        new MedItem("Rollstuhl"),  new MedItem("Kruecke"),         new MedItem("Krankenbett"),
        new MedItem("Handschuhe"), new MedItem("Maske"),           new MedItem("Otoskop"),
        new MedItem("Mikroskop"),  new MedItem("Wattebausch"),     new MedItem("Pipette"),
        new MedItem("Erste-Hilfe-Kasten"), new MedItem("Augentropfen"),
    };

    readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();

    void Start()
    {
        if (string.IsNullOrEmpty(outputFolder))
        {
            string root = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, ".."));
            outputFolder = System.IO.Path.Combine(root, "quiz_images");
        }

        Instance = this;
        if (roomDecorator == null) roomDecorator = FindFirstObjectByType<DoctorRoomDecorator>();

        StartCoroutine(WaitThenLoadFrames());
    }

    // Wait for DoctorRoomDecorator to build canvases, then populate them.
    IEnumerator WaitThenLoadFrames()
    {
        float timeout = 5f;
        while (timeout > 0f)
        {
            if (roomDecorator == null) roomDecorator = FindFirstObjectByType<DoctorRoomDecorator>();
            if (DoctorRoomDecorator.PictureCanvases != null &&
                DoctorRoomDecorator.PictureCanvases.Length >= 3 &&
                DoctorRoomDecorator.PictureCanvases[0] != null)
                break;
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (DoctorRoomDecorator.PictureCanvases == null ||
            DoctorRoomDecorator.PictureCanvases[0] == null)
        {
            Debug.LogWarning("[WallImageQuiz] Canvases not ready — skipping image load.");
            yield break;
        }

        // Pick 3 random items for the frames
        int[] idx = ShuffleIndices(_items.Length);
        for (int i = 0; i < 3; i++)
            StartCoroutine(LoadFrame(i, _items[idx[i]]));
    }

    // Try disk image first; fall back to live Pollinations fetch.
    IEnumerator LoadFrame(int frameIndex, MedItem item)
    {
        var rend = DoctorRoomDecorator.PictureCanvases[frameIndex];
        if (rend == null) yield break;

        // 1. Try disk
        string path = Path.Combine(outputFolder, item.ImageFileName);
        if (File.Exists(path))
        {
            byte[] data = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(data);
            ApplyTex(rend, tex);
            Debug.Log($"[WallImageQuiz] Frame {frameIndex+1}: {item.german} (disk)");
            yield break;
        }

        // 2. Fetch from Pollinations
        if (Application.internetReachability == NetworkReachability.NotReachable)
            yield break;

        int seed = Random.Range(1, 999999);
        string prompt = UnityWebRequest.EscapeURL(item.german.ToLower() +
            " medical object, white background, realistic photo");
        string url = $"https://image.pollinations.ai/prompt/{prompt}?width=512&height=384&nologo=true&seed={seed}";

        using var req = UnityWebRequestTexture.GetTexture(url);
        req.timeout = 30;
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            ApplyTex(rend, DownloadHandlerTexture.GetContent(req));
            Debug.Log($"[WallImageQuiz] Frame {frameIndex+1}: {item.german} (AI live)");
        }
    }

    void ApplyTex(MeshRenderer rend, Texture2D tex)
    {
        rend.material.mainTexture = tex;
        rend.material.SetTexture("_BaseMap", tex);
        rend.material.SetTexture("_MainTex",  tex);
        rend.material.SetColor("_BaseColor", Color.white);
        rend.material.SetColor("_Color",     Color.white);
        rend.material.color = Color.white;
    }

    int[] ShuffleIndices(int count)
    {
        var idx = new int[count];
        for (int i = 0; i < count; i++) idx[i] = i;
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = idx[i]; idx[i] = idx[j]; idx[j] = tmp;
        }
        return idx;
    }
}
