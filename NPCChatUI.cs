using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPCChatUI — lightweight bottom HUD (status bar + chat history).
/// No voice/compose area. Used for quiz feedback and NPC messages.
///
/// API:
///   NPCChatUI.Instance.AddDoctorMessage("Guten Tag!")
///   NPCChatUI.Instance.AddPlayerMessage("Hallo.")
///   NPCChatUI.Instance.SetStatus("…")
///   NPCChatUI.Instance.ClearChat()
/// </summary>
public class NPCChatUI : MonoBehaviour
{
    public static NPCChatUI Instance { get; private set; }

    [Header("Appearance")]
    [Range(0.10f, 0.30f)] public float panelHeightFraction = 0.18f;
    public int maxMessages = 8;
    public int fontSize     = 40;

    private Text _statusText;
    private Text _chatText;
    private readonly List<string> _lines = new List<string>();

    static readonly Color BgColor     = new Color(0f,    0f,    0f,   0.72f);
    static readonly Color StatusColor = new Color(1f,    0.85f, 0.30f, 1f);
    static readonly Color DoctorColor = new Color(0.45f, 0.85f, 1f,   1f);
    static readonly Color PlayerColor = new Color(0.80f, 1f,   0.55f, 1f);

    void Awake() { Instance = this; BuildUI(); }

    void Start()
    {
        string name = "Doctor";
        if (RoomConfigLoader.Instance != null && RoomConfigLoader.Instance.HasConfig)
            name = RoomConfigLoader.Instance.NpcName;
        AddDoctorMessage($"Willkommen! Ich bin {name}. Drücken Sie [1]–[4] um zu antworten.");
        SetStatus("Drücken Sie [1] [2] [3] [4] um eine Antwort zu wählen.");
    }

    public void AddDoctorMessage(string text)
    {
        string name = "Doctor";
        if (RoomConfigLoader.Instance != null && RoomConfigLoader.Instance.HasConfig)
            name = RoomConfigLoader.Instance.NpcName;
        AppendLine($"<color=#{Hex(DoctorColor)}>🩺 {name}:</color> {text}");
    }

    public void AddPlayerMessage(string text)
        => AppendLine($"<color=#{Hex(PlayerColor)}>Sie:</color> {text}");

    public void SetStatus(string msg)
    { if (_statusText != null) _statusText.text = msg; }

    // kept for compatibility — no-op since compose area removed
    public void SetComposeText(string _) { }

    public void ClearChat() { _lines.Clear(); RefreshChat(); }

    void AppendLine(string line)
    {
        _lines.Add(line);
        if (_lines.Count > maxMessages) _lines.RemoveAt(0);
        RefreshChat();
    }

    void RefreshChat()
    { if (_chatText != null) _chatText.text = string.Join("\n", _lines); }

    static string Hex(Color c)
        => $"{B(c.r):X2}{B(c.g):X2}{B(c.b):X2}";
    static int B(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255), 0, 255);

    void BuildUI()
    {
        var cgo = new GameObject("NPCChatCanvas");
        DontDestroyOnLoad(cgo);
        cgo.transform.SetParent(transform, false);

        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        var panelGO = new GameObject("ChatPanel");
        panelGO.transform.SetParent(cgo.transform, false);
        var prt = panelGO.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0f, 0f);
        prt.anchorMax = new Vector2(1f, panelHeightFraction);
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        panelGO.AddComponent<Image>().color = BgColor;

        // Status strip — top 20% of panel
        var sgo = new GameObject("Status");
        sgo.transform.SetParent(panelGO.transform, false);
        var srt = sgo.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0.80f); srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(12f, 0f);   srt.offsetMax = new Vector2(-12f, -2f);
        _statusText = sgo.AddComponent<Text>();
        _statusText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusText.fontSize  = Mathf.Max(12, fontSize - 4);
        _statusText.color     = StatusColor;
        _statusText.alignment = TextAnchor.UpperLeft;
        _statusText.supportRichText = true;
        _statusText.text = "Initialising…";

        // Divider
        var dgo = new GameObject("Div");
        dgo.transform.SetParent(panelGO.transform, false);
        var drt = dgo.AddComponent<RectTransform>();
        drt.anchorMin = new Vector2(0f, 0.79f); drt.anchorMax = new Vector2(1f, 0.80f);
        drt.offsetMin = new Vector2(8f, 0f); drt.offsetMax = new Vector2(-8f, 0f);
        dgo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

        // Chat history — bottom 79%
        var tgo = new GameObject("ChatText");
        tgo.transform.SetParent(panelGO.transform, false);
        var trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0.79f);
        trt.offsetMin = new Vector2(12f, 4f); trt.offsetMax = new Vector2(-12f, -2f);
        _chatText = tgo.AddComponent<Text>();
        _chatText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _chatText.fontSize  = fontSize;
        _chatText.color     = Color.white;
        _chatText.alignment = TextAnchor.LowerLeft;
        _chatText.supportRichText    = true;
        _chatText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _chatText.verticalOverflow   = VerticalWrapMode.Truncate;
    }
}
