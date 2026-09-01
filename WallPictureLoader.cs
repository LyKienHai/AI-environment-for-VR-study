using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// WallPictureLoader — places picture frames on 3 walls (left, back, right)
/// and loads images from Unsplash (or cached local PNG files).
///
/// SETUP:
///   Attach to the same GameObject as AIRoomBox and RoomConfigLoader.
///   Reads query strings from RoomConfigLoader and fetches images via Unsplash API.
///
/// IMAGE SOURCE:
///   1. First checks for local cached file: E:\ai_generator\output\pic_left.png etc.
///   2. If not cached, fetches from Unsplash via the public source URL.
///   3. Saves fetched image to cache for offline use.
///
/// NOTE: Unsplash source.unsplash.com returns a random CC0 image for the query.
///       No API key required for this endpoint.
/// </summary>
public class WallPictureLoader : MonoBehaviour
{
    [Header("Room dimensions — must match AIRoomBox")]
    public float floorSize  = 10f;
    public float wallHeight = 5f;

    [Header("Picture frame size (meters)")]
    public float frameWidth  = 1.2f;
    public float frameHeight = 0.9f;

    [Header("Cache folder (local PNG files)")]
    public string cacheFolder = @"E:\ai_generator\output";

    // Unsplash source URL — no API key needed, returns a random image for the query
    private const string UnsplashBase = "https://source.unsplash.com/800x600/?";

    private float _wallOffset = 0.05f;   // distance from wall surface

    void Start()
    {
        StartCoroutine(PlaceAllPictures());
    }

    IEnumerator PlaceAllPictures()
    {
        var cfg = RoomConfigLoader.Instance;

        float hf = floorSize / 2f;
        float hw = wallHeight / 2f;
        float pictureY = hw * 0.4f;   // hang at ~60% of wall height

        // Left wall: x=-hf, facing +X (rotation 0,-90,0 ... but picture faces inward = 0,90,0)
        yield return StartCoroutine(PlacePicture(
            "PicLeft",
            new Vector3(-hf + _wallOffset, pictureY, 0f),
            new Vector3(0f, 90f, 0f),
            "pic_left",
            cfg?.PicLeftQuery  ?? "medical diploma certificate",
            cfg?.PicLeftGerman ?? "das Diplom"
        ));

        // Back wall: z=-hf, facing +Z
        yield return StartCoroutine(PlacePicture(
            "PicBack",
            new Vector3(0f, pictureY, -hf + _wallOffset),
            new Vector3(0f, 0f, 0f),
            "pic_back",
            cfg?.PicBackQuery  ?? "anatomy human body chart",
            cfg?.PicBackGerman ?? "das Poster"
        ));

        // Right wall: x=+hf, facing -X
        yield return StartCoroutine(PlacePicture(
            "PicRight",
            new Vector3(hf - _wallOffset, pictureY, 0f),
            new Vector3(0f, -90f, 0f),
            "pic_right",
            cfg?.PicRightQuery  ?? "hospital modern interior",
            cfg?.PicRightGerman ?? "das Bild"
        ));

        Debug.Log("[WallPictureLoader] All wall pictures placed.");
    }

    IEnumerator PlacePicture(string objName, Vector3 localPos, Vector3 localRot,
                              string cacheFile, string query, string german)
    {
        // ── Build frame ───────────────────────────────────────────────────────
        var frameGO = new GameObject(objName + "_Frame");
        frameGO.transform.SetParent(transform, false);
        frameGO.transform.localPosition    = localPos;
        frameGO.transform.localEulerAngles = localRot;

        // Outer frame border (dark wood)
        float borderW = 0.06f;
        BuildFrameBorder(frameGO, borderW);

        // Inner picture quad
        var pictureGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pictureGO.name = "Picture";
        pictureGO.transform.SetParent(frameGO.transform, false);
        pictureGO.transform.localPosition = new Vector3(0, 0, -0.01f);
        pictureGO.transform.localScale    = new Vector3(frameWidth - borderW*2,
                                                         frameHeight - borderW*2, 1f);

        var col = pictureGO.GetComponent<MeshCollider>();
        if (col != null) Destroy(col);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", Color.white);
        pictureGO.GetComponent<MeshRenderer>().material = mat;

        // ── Load image ────────────────────────────────────────────────────────
        string cachePath = Path.Combine(cacheFolder, cacheFile + ".png");
        Texture2D tex = null;

        if (File.Exists(cachePath))
        {
            // Load from local cache
            byte[] data = File.ReadAllBytes(cachePath);
            tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            Debug.Log($"[WallPictureLoader] Loaded from cache: {cachePath}");
        }
        else
        {
            // Fetch from Unsplash
            string url = UnsplashBase + UnityWebRequest.EscapeURL(query);
            Debug.Log($"[WallPictureLoader] Fetching: {url}");

            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    tex = DownloadHandlerTexture.GetContent(req);

                    // Cache it for next time
                    try
                    {
                        if (!Directory.Exists(cacheFolder))
                            Directory.CreateDirectory(cacheFolder);
                        File.WriteAllBytes(cachePath, tex.EncodeToPNG());
                        Debug.Log($"[WallPictureLoader] Cached to {cachePath}");
                    }
                    catch { /* cache write optional */ }
                }
                else
                {
                    Debug.LogWarning($"[WallPictureLoader] Could not fetch image for '{query}': {req.error}");
                    // Show grey placeholder
                    tex = MakeColorTexture(new Color(0.7f, 0.7f, 0.75f));
                }
            }
        }

        if (tex != null)
        {
            mat.mainTexture = tex;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex",  tex);
        }

        // ── German label under the frame ──────────────────────────────────────
        GermanLabel.Attach(frameGO, german, "", -(frameHeight / 2f + 0.05f));
    }

    void BuildFrameBorder(GameObject parent, float border)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");

        Color woodColor = new Color(0.35f, 0.22f, 0.08f);

        // top / bottom / left / right border strips
        CreateFrameStrip(parent, shader, woodColor,
            new Vector3(0,  frameHeight/2f, 0),
            new Vector3(frameWidth, border, border));
        CreateFrameStrip(parent, shader, woodColor,
            new Vector3(0, -frameHeight/2f, 0),
            new Vector3(frameWidth, border, border));
        CreateFrameStrip(parent, shader, woodColor,
            new Vector3(-frameWidth/2f, 0, 0),
            new Vector3(border, frameHeight, border));
        CreateFrameStrip(parent, shader, woodColor,
            new Vector3( frameWidth/2f, 0, 0),
            new Vector3(border, frameHeight, border));
    }

    void CreateFrameStrip(GameObject parent, Shader shader, Color color,
                           Vector3 localPos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;

        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color",     color);
        go.GetComponent<MeshRenderer>().material = mat;
    }

    static Texture2D MakeColorTexture(Color color)
    {
        var tex = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
