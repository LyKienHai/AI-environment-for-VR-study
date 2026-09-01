using UnityEngine;

/// <summary>
/// GermanLabel — attaches a floating German text label above any GameObject.
///
/// USAGE (from code):
///   GermanLabel.Attach(myGameObject, "der Schreibtisch");
///
/// The label floats above the object, always faces the camera (billboard),
/// shows the German word with article in yellow + a small English hint below.
/// </summary>
public class GermanLabel : MonoBehaviour
{
    [Header("Text")]
    public string germanText  = "das Objekt";
    public string englishHint = "";          // optional English translation below

    [Header("Appearance")]
    public float  yOffset     = 0.4f;        // how far above the object's pivot
    public float  fontSize    = 14;
    public Color  germanColor  = new Color(1f, 0.95f, 0.2f);   // yellow
    public Color  englishColor = new Color(0.8f, 0.8f, 0.8f);  // light grey

    private TextMesh _germanMesh;
    private TextMesh _englishMesh;
    private Camera   _cam;

    void Start()
    {
        _cam = Camera.main;
        CreateLabels();
    }

    void LateUpdate()
    {
        // Billboard — always face camera
        if (_cam == null) return;
        if (_germanMesh  != null) _germanMesh.transform.rotation  = _cam.transform.rotation;
        if (_englishMesh != null) _englishMesh.transform.rotation = _cam.transform.rotation;
    }

    void CreateLabels()
    {
        // ── German label ──────────────────────────────────────────────────────
        var goG = new GameObject("Label_German");
        goG.transform.SetParent(transform, false);
        goG.transform.localPosition = new Vector3(0, yOffset, 0);

        _germanMesh = goG.AddComponent<TextMesh>();
        _germanMesh.text      = germanText;
        _germanMesh.fontSize  = (int)fontSize;
        _germanMesh.color     = germanColor;
        _germanMesh.anchor    = TextAnchor.LowerCenter;
        _germanMesh.alignment = TextAlignment.Center;
        _germanMesh.characterSize = 0.05f;
        _germanMesh.fontStyle = FontStyle.Bold;

        // ── English hint (smaller, below) ─────────────────────────────────────
        if (!string.IsNullOrEmpty(englishHint))
        {
            var goE = new GameObject("Label_English");
            goE.transform.SetParent(transform, false);
            goE.transform.localPosition = new Vector3(0, yOffset - 0.12f, 0);

            _englishMesh = goE.AddComponent<TextMesh>();
            _englishMesh.text      = englishHint;
            _englishMesh.fontSize  = (int)(fontSize * 0.7f);
            _englishMesh.color     = englishColor;
            _englishMesh.anchor    = TextAnchor.UpperCenter;
            _englishMesh.alignment = TextAlignment.Center;
            _englishMesh.characterSize = 0.05f;
        }
    }

    // ── static helper ─────────────────────────────────────────────────────────

    /// <summary>Add a GermanLabel component to any existing GameObject.</summary>
    public static GermanLabel Attach(GameObject target, string german, string english = "",
                                     float yOffset = 0.4f)
    {
        var label       = target.AddComponent<GermanLabel>();
        label.germanText  = german;
        label.englishHint = english;
        label.yOffset     = yOffset;
        return label;
    }
}
