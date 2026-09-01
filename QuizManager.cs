using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// QuizManager v3 — A2 German medical quiz with B1 challenge every 10 correct answers.
///
/// Question types:
///   • A2 Vocabulary  — "Was ist das?" object naming
///   • A2 Grammar     — fill-in-the-blank in doctor/reception scenario
///   • B1 Challenge   — harder grammar or vocab, always medical context
///                      appears once every 10 correct answers
///
/// Questions loop and shuffle so the quiz never ends.
/// </summary>
public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (Instance != null) return;
        var go = new GameObject("QuizManager");
        DontDestroyOnLoad(go);
        go.AddComponent<QuizManager>();
    }

    // ── question data ─────────────────────────────────────────────────────────
    public enum QType { Vocab, Grammar, ChallengeB1 }

    public class Q
    {
        public QType  type;
        public string context;
        public string stem;
        public string[] choices;   // exactly 4
        public int correct;        // 0-based index
        public string tip;
    }

    // ══ A2 VOCABULARY — object naming in the doctor's office ═════════════════
    static readonly Q[] A2_VOCAB = {
        new Q {
            type    = QType.Vocab,
            context = "🩺  The doctor uses this to listen to your heart and lungs.",
            stem    = "Was ist das?",
            choices = new[]{ "das Thermometer", "das Stethoskop", "die Spritze", "das Rezept" },
            correct = 1,
            tip     = "das Stethoskop = stethoscope  (neuter — das)"
        },
        new Q {
            type    = QType.Vocab,
            context = "🌡️  You put this under your tongue or in your ear to check your temperature.",
            stem    = "Was ist das?",
            choices = new[]{ "das Stethoskop", "das Blutdruckgerät", "das Pflaster", "das Thermometer" },
            correct = 3,
            tip     = "das Thermometer = thermometer  (neuter — das)"
        },
        new Q {
            type    = QType.Vocab,
            context = "📋  The doctor writes this so you can collect medicine from the pharmacy.",
            stem    = "Was ist das?",
            choices = new[]{ "die Krankenakte", "die Überweisung", "das Rezept", "die Quittung" },
            correct = 2,
            tip     = "das Rezept = prescription  (neuter — das)"
        },
        new Q {
            type    = QType.Vocab,
            context = "💳  You show this at every doctor visit in Germany — it proves you have insurance.",
            stem    = "Was ist das?",
            choices = new[]{ "der Ausweis", "die Krankenversicherungskarte", "das Rezept", "die Quittung" },
            correct = 1,
            tip     = "die Krankenversicherungskarte = health insurance card  (feminine — die)"
        },
        new Q {
            type    = QType.Vocab,
            context = "🩹  You put this sticky strip on a small cut to protect it.",
            stem    = "Was ist das?",
            choices = new[]{ "die Spritze", "das Stethoskop", "das Pflaster", "der Verband" },
            correct = 2,
            tip     = "das Pflaster = plaster / band-aid  (neuter — das)"
        },
        new Q {
            type    = QType.Vocab,
            context = "🪑  You sit here and wait until the doctor is ready for you.",
            stem    = "Wie heißt dieser Raum?   (What is this room called?)",
            choices = new[]{ "das Behandlungszimmer", "das Wartezimmer", "das Büro", "das Badezimmer" },
            correct = 1,
            tip     = "das Wartezimmer = waiting room  (neuter — das)"
        },
        new Q {
            type    = QType.Vocab,
            context = "💉  The nurse uses this to give you a vaccination or take blood.",
            stem    = "Was ist das?",
            choices = new[]{ "der Verband", "die Pille", "die Spritze", "das Pflaster" },
            correct = 2,
            tip     = "die Spritze = syringe / injection  (feminine — die)"
        },
        new Q {
            type    = QType.Vocab,
            context = "📄  This note from your doctor allows you to stay home from work when you are sick.",
            stem    = "Was ist das?",
            choices = new[]{ "das Rezept", "die Überweisung", "die Krankmeldung", "die Rechnung" },
            correct = 2,
            tip     = "die Krankmeldung = sick note  (feminine — die)"
        },
        new Q {
            type    = QType.Vocab,
            context = "📑  This document sends you from your GP to a specialist doctor.",
            stem    = "Was ist das?",
            choices = new[]{ "die Überweisung", "das Rezept", "die Krankenakte", "der Befund" },
            correct = 0,
            tip     = "die Überweisung = referral  (feminine — die)"
        },
        new Q {
            type    = QType.Vocab,
            context = "🩸  The doctor does this to check your heart speed — they count beats per minute.",
            stem    = "Was misst der Arzt?   (What does the doctor measure?)",
            choices = new[]{ "den Blutdruck", "die Temperatur", "den Puls", "den Blutzucker" },
            correct = 2,
            tip     = "der Puls = pulse  (masculine — der)"
        },
        new Q {
            type    = QType.Vocab,
            context = "🩺  The doctor puts a cuff around your arm and pumps it up to read this.",
            stem    = "Was misst der Arzt?",
            choices = new[]{ "den Puls", "den Blutdruck", "die Temperatur", "das Gewicht" },
            correct = 1,
            tip     = "der Blutdruck = blood pressure  (masculine — der)"
        },
        new Q {
            type    = QType.Vocab,
            context = "🌸  A green leafy plant sits in a terracotta pot in the corner of the waiting room.",
            stem    = "Was ist das?",
            choices = new[]{ "der Blumentopf", "die Zimmerpflanze", "der Tisch", "die Tür" },
            correct = 1,
            tip     = "die Zimmerpflanze = houseplant  (feminine — die)"
        },
    };

    // ══ A2 GRAMMAR — fill in the blank, doctor/reception scenario ═════════════
    static readonly Q[] A2_GRAMMAR = {
        new Q {
            type    = QType.Grammar,
            context = "You want to tell the doctor how you feel today.",
            stem    = "\"Ich ___ Kopfschmerzen.\"   (I have a headache.)",
            choices = new[]{ "hat", "habe", "haben", "hast" },
            correct = 1,
            tip     = "ich habe = I have  →  1st person singular of 'haben'"
        },
        new Q {
            type    = QType.Grammar,
            context = "You are telling the doctor how long you have been sick.",
            stem    = "\"Ich ___ seit drei Tagen krank.\"   (I have been sick for three days.)",
            choices = new[]{ "ist", "sind", "bist", "bin" },
            correct = 3,
            tip     = "ich bin = I am  →  1st person singular of 'sein'"
        },
        new Q {
            type    = QType.Grammar,
            context = "The receptionist asks you about your appointment.",
            stem    = "\"Haben Sie ___ Termin?\"   (Do you have an appointment?)",
            choices = new[]{ "eine", "ein", "einen", "einem" },
            correct = 2,
            tip     = "der Termin is masculine → accusative 'einen' after 'haben'"
        },
        new Q {
            type    = QType.Grammar,
            context = "You are explaining your symptoms to the doctor.",
            stem    = "\"Wo ___ es weh?\"   (Where does it hurt?)",
            choices = new[]{ "tue", "tun", "tuest", "tut" },
            correct = 3,
            tip     = "es tut weh = it hurts  →  3rd person singular: 'tut'"
        },
        new Q {
            type    = QType.Grammar,
            context = "You need to explain that you must see the doctor urgently.",
            stem    = "\"Ich ___ sofort zum Arzt gehen.\"   (I must go to the doctor immediately.)",
            choices = new[]{ "müssen", "musst", "muss", "müsst" },
            correct = 2,
            tip     = "ich muss = I must  →  1st person singular of 'müssen'"
        },
        new Q {
            type    = QType.Grammar,
            context = "The receptionist politely asks you to wait.",
            stem    = "\"Bitte ___ Sie im Wartezimmer.\"   (Please wait in the waiting room.)",
            choices = new[]{ "warte", "wartest", "wartet", "warten" },
            correct = 3,
            tip     = "Formal imperative (Sie form): verb stays as infinitive → 'warten'"
        },
        new Q {
            type    = QType.Grammar,
            context = "You want to make an appointment at the reception desk.",
            stem    = "\"Ich möchte einen Termin ___.\"   (I would like to make an appointment.)",
            choices = new[]{ "vereinbare", "vereinbarten", "vereinbart", "vereinbaren" },
            correct = 3,
            tip     = "'möchte' + infinitive at the end: 'vereinbaren'"
        },
        new Q {
            type    = QType.Grammar,
            context = "The doctor explains what the nurse will do.",
            stem    = "\"Die Schwester ___ Ihnen gleich Blut ab.\"   (The nurse will take your blood shortly.)",
            choices = new[]{ "nehmen", "nimmst", "nehmt", "nimmt" },
            correct = 3,
            tip     = "sie nimmt = she takes  →  3rd person singular of 'nehmen' (strong verb)"
        },
        new Q {
            type    = QType.Grammar,
            context = "You tell the receptionist you are sick and need help.",
            stem    = "\"Ich bin krank ___ brauche einen Arzt.\"   (I am sick and need a doctor.)",
            choices = new[]{ "aber", "weil", "und", "oder" },
            correct = 2,
            tip     = "'und' = and — connects two main clauses; verb stays in position 2"
        },
        new Q {
            type    = QType.Grammar,
            context = "The doctor asks if you have a fever.",
            stem    = "\"___ Sie Fieber?\"   (Do you have a fever?)",
            choices = new[]{ "Haben", "Hat", "Hast", "Habe" },
            correct = 0,
            tip     = "Formal question with 'Sie': 'Haben Sie' (verb first, then 'Sie')"
        },
        new Q {
            type    = QType.Grammar,
            context = "You explain how long you have had the pain.",
            stem    = "\"Die Schmerzen ___ schon seit gestern.\"   (The pain has been there since yesterday.)",
            choices = new[]{ "bin", "sind", "habe", "haben" },
            correct = 1,
            tip     = "'die Schmerzen' is plural → 'sind' (3rd person plural of 'sein')"
        },
        new Q {
            type    = QType.Grammar,
            context = "The receptionist checks the schedule for you.",
            stem    = "\"Der Arzt ___ um 14 Uhr Zeit.\"   (The doctor has time at 2 pm.)",
            choices = new[]{ "hast", "haben", "habe", "hat" },
            correct = 3,
            tip     = "er/sie hat = he/she has  →  3rd person singular of 'haben'"
        },
    };

    // ══ B1 CHALLENGE — harder, always medical context ═════════════════════════
    static readonly Q[] B1_CHALLENGE = {
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nThe doctor gives you advice. Choose the correct subordinate clause.",
            stem    = "\"Der Arzt empfiehlt, dass ich mehr Wasser ___ soll.\"   (The doctor recommends that I should drink more water.)",
            choices = new[]{ "trinke", "trinken", "trinkt", "trank" },
            correct = 0,
            tip     = "After 'dass', use Konjunktiv I for reported speech: 'trinke' (1st person)"
        },
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nYou explain why you came to the doctor. Choose the correct verb form.",
            stem    = "\"Ich bin zum Arzt gegangen, ___ ich starke Schmerzen hatte.\"",
            choices = new[]{ "und", "aber", "weil", "obwohl" },
            correct = 2,
            tip     = "'weil' (because) introduces a causal clause — verb goes to the END"
        },
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nThe doctor says the surgery went well. Choose the correct genitive.",
            stem    = "\"Das Ergebnis ___ Untersuchung ist positiv.\"   (The result of the examination is positive.)",
            choices = new[]{ "den", "dem", "der", "des" },
            correct = 2,
            tip     = "'die Untersuchung' is feminine → genitive: 'der Untersuchung'"
        },
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nYou describe your symptoms politely. Which is most appropriate?",
            stem    = "\"Ich ___ seit einer Woche starke Rückenschmerzen haben.\"   (I have had severe back pain for a week.)",
            choices = new[]{ "könnte", "würde", "hätte", "wäre" },
            correct = 2,
            tip     = "'hätte' = Konjunktiv II of 'haben' — used for polite/reported statements"
        },
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nChoose the correct preposition for this medical phrase.",
            stem    = "\"Ich leide ___ chronischen Rückenschmerzen.\"   (I suffer from chronic back pain.)",
            choices = new[]{ "bei", "unter", "an", "mit" },
            correct = 2,
            tip     = "'leiden an + Dativ' = to suffer from (illness) — always 'an'"
        },
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nThe receptionist asks about your insurance. Choose the right adjective ending.",
            stem    = "\"Haben Sie eine gültig___ Versicherungskarte?\"   (Do you have a valid insurance card?)",
            choices = new[]{ "-er", "-em", "-es", "-e" },
            correct = 3,
            tip     = "Feminine accusative after 'eine': adjective ends in '-e' → 'gültige'"
        },
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nYou want to describe when the pain started. Choose the right connector.",
            stem    = "\"___ ich aufgewacht bin, hatte ich sofort Fieber.\"   (As soon as I woke up, I had a fever immediately.)",
            choices = new[]{ "Bevor", "Nachdem", "Als", "Während" },
            correct = 2,
            tip     = "'Als' = when (single event in the past) — verb moves to the end of clause"
        },
        new Q {
            type    = QType.ChallengeB1,
            context = "🏆 B1 Challenge!\nThe doctor says he referred you. Choose the correct passive form.",
            stem    = "\"Sie ___ an einen Spezialisten überwiesen.\"   (You have been referred to a specialist.)",
            choices = new[]{ "hat", "sind", "haben", "wurden" },
            correct = 3,
            tip     = "Passive past (Präteritum): 'wurden + Partizip II' → 'wurden überwiesen'"
        },
    };

    // ── state ─────────────────────────────────────────────────────────────────
    private List<Q> _deck        = new List<Q>();   // shuffled A2 pool
    private int     _deckIdx     = 0;
    private int     _totalAsked  = 0;
    private int     _totalCorrect= 0;
    private int     _correctStreak = 0;             // since last B1 challenge
    private int     _b1Idx       = 0;
    private bool    _nextIsB1    = false;
    private bool    _answered    = false;
    private Q       _current;

    // ── UI refs ───────────────────────────────────────────────────────────────
    private Canvas      _canvas;
    private GameObject  _panel;
    private CanvasGroup _panelGroup;
    private Text       _typeText, _progressText, _scoreText;
    private Text       _contextText, _stemText, _feedbackText;
    private Image[]    _choiceBg  = new Image[4];
    private Text[]     _choiceTxt = new Text[4];

    static Color BG         = new Color(0.04f, 0.07f, 0.13f, 0.93f);
    static Color BG_CHOICE  = new Color(0.14f, 0.20f, 0.30f, 1f);
    static Color BG_CTX     = new Color(0.09f, 0.13f, 0.20f, 1f);
    static Color BG_CHAL    = new Color(0.18f, 0.10f, 0.05f, 1f);
    static Color C_CORRECT  = new Color(0.15f, 0.72f, 0.32f, 1f);
    static Color C_WRONG    = new Color(0.76f, 0.16f, 0.16f, 1f);
    static Color C_NEUTRAL  = new Color(0.88f, 0.92f, 1f,   1f);
    static Color C_GOLD     = new Color(1f,    0.87f, 0.25f, 1f);
    static Color C_ORANGE   = new Color(1f,    0.55f, 0.10f, 1f);
    static Color C_TIP      = new Color(0.60f, 0.92f, 1f,   1f);
    static Color C_DIM      = new Color(0.58f, 0.62f, 0.68f, 1f);

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake() { Instance = this; BuildUI(); }
    void Start()  { RebuildDeck(); NextQuestion(); }

    void Update()
    {
        if (_answered) return;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Answer(0);
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Answer(1);
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Answer(2);
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Answer(3);
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Answer(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Answer(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Answer(2);
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) Answer(3);
#endif
    }

    // ── question flow ─────────────────────────────────────────────────────────

    void RebuildDeck()
    {
        _deck.Clear();
        // Mix vocab and grammar into one pool
        foreach (var q in A2_VOCAB)    _deck.Add(q);
        foreach (var q in A2_GRAMMAR)  _deck.Add(q);
        // Shuffle
        for (int i = _deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = _deck[i]; _deck[i] = _deck[j]; _deck[j] = tmp;
        }
        _deckIdx = 0;
    }

    void NextQuestion()
    {
        if (_nextIsB1)
        {
            _nextIsB1 = false;
            _current  = B1_CHALLENGE[_b1Idx % B1_CHALLENGE.Length];
            _b1Idx++;
        }
        else
        {
            if (_deckIdx >= _deck.Count) RebuildDeck();
            _current = _deck[_deckIdx++];
        }
        PopulateUI();
    }

    void PopulateUI()
    {
        _answered = false;
        var q = _current;
        bool isB1 = q.type == QType.ChallengeB1;

        _panel.GetComponent<Image>().color = isB1 ? BG_CHAL : BG;

        string typeLabel = isB1              ? "🏆 B1 CHALLENGE"
                         : q.type == QType.Grammar ? "📝 A2 Grammatik"
                         :                           "🏥 A2 Wortschatz";
        _typeText.text     = typeLabel;
        _typeText.color    = isB1 ? C_ORANGE : C_GOLD;
        _progressText.text = $"Richtig: {_totalCorrect}";
        _scoreText.text    = $"Gesamt: {_totalAsked}";
        _contextText.text  = q.context;
        _stemText.text     = q.stem;
        _feedbackText.text  = "Drücke  [1]  [2]  [3]  [4]  um zu antworten";
        _feedbackText.color = C_DIM;

        for (int i = 0; i < 4; i++)
        {
            _choiceTxt[i].text  = $"[{i + 1}]  {q.choices[i]}";
            _choiceBg[i].color  = BG_CHOICE;
            _choiceTxt[i].color = C_NEUTRAL;
        }
    }

    void Answer(int chosen)
    {
        if (_answered) return;
        _answered = true;
        _totalAsked++;

        bool ok = chosen == _current.correct;
        if (ok)
        {
            _totalCorrect++;
            if (_current.type != QType.ChallengeB1)
            {
                _correctStreak++;
                if (_correctStreak >= 10)
                {
                    _correctStreak = 0;
                    _nextIsB1      = true;
                }
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (i == _current.correct)  { _choiceBg[i].color = C_CORRECT; _choiceTxt[i].color = Color.white; }
            else if (i == chosen)       { _choiceBg[i].color = C_WRONG;   _choiceTxt[i].color = Color.white; }
        }
        _feedbackText.color = ok ? C_CORRECT : C_WRONG;
        _feedbackText.text  = ok
            ? (_current.type == QType.ChallengeB1 ? "🏆 Ausgezeichnet! B1 geschafft!" : "✓ Richtig!")
            : $"✗ Falsch — richtig: [{_current.correct + 1}]";
        _scoreText.text     = $"Gesamt: {_totalAsked}";

        // Show next-B1 hint
        if (_nextIsB1 && ok)
            _feedbackText.text += "  ⚡ Nächste: B1 Herausforderung!";
        else if (!_nextIsB1 && _current.type != QType.ChallengeB1)
        {
            int remaining = 10 - _correctStreak;
            if (remaining <= 3 && ok)
                _feedbackText.text += $"  (noch {remaining} bis B1!)";
        }

        StartCoroutine(Advance(_current.tip));
    }

    IEnumerator Advance(string tip)
    {
        yield return new WaitForSeconds(0.9f);
        _feedbackText.color = C_TIP;
        _feedbackText.text  = $"💡 {tip}";
        yield return new WaitForSeconds(2.4f);
        yield return StartCoroutine(FadePanel(1f, 0f, 0.5f));
        NextQuestion();
        yield return StartCoroutine(FadePanel(0f, 1f, 0.3f));
    }

    IEnumerator FadePanel(float from, float to, float duration)
    {
        if (_panelGroup == null) yield break;
        float t = 0f;
        _panelGroup.alpha = from;
        while (t < duration)
        {
            t += Time.deltaTime;
            _panelGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        _panelGroup.alpha = to;
    }

    // ── UI builder ────────────────────────────────────────────────────────────

    void BuildUI()
    {
        var cgo = new GameObject("QuizCanvas");
        DontDestroyOnLoad(cgo);
        cgo.transform.SetParent(transform, false);
        _canvas = cgo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;
        var sc = cgo.AddComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight  = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        _panel = MakeGO(cgo, "QuizPanel", new Vector2(0f, 0f), new Vector2(1f, 0.46f));
        _panel.AddComponent<Image>().color = BG;
        _panelGroup = _panel.AddComponent<CanvasGroup>();

        var header = MakeGO(_panel, "Header", new Vector2(0f, 0.90f), new Vector2(1f, 1f));
        header.AddComponent<Image>().color = new Color(0.07f, 0.11f, 0.19f, 1f);
        _typeText     = MakeText(header, "Type",     0f, 1f, 0.01f, 0.55f, 30, C_GOLD, TextAnchor.MiddleLeft, bold: true);
        _progressText = MakeText(header, "Progress", 0f, 1f, 0.56f, 0.78f, 26, C_DIM,  TextAnchor.MiddleCenter);
        _scoreText    = MakeText(header, "Score",    0f, 1f, 0.79f, 0.99f, 26, C_DIM,  TextAnchor.MiddleRight);

        var ctx = MakeGO(_panel, "Ctx", new Vector2(0f, 0.73f), new Vector2(1f, 0.90f));
        ctx.AddComponent<Image>().color = BG_CTX;
        _contextText = MakeText(ctx, "CtxT", 0f, 1f, 0f, 1f, 28, C_NEUTRAL, TextAnchor.MiddleCenter);

        _stemText = MakeText(_panel, "Stem", 0.57f, 0.73f, 0f, 1f, 32, Color.white, TextAnchor.MiddleCenter, bold: true);

        float[] x0 = { 0.005f, 0.255f, 0.505f, 0.755f };
        float[] x1 = { 0.245f, 0.495f, 0.745f, 0.995f };
        for (int i = 0; i < 4; i++)
        {
            var c = MakeGO(_panel, $"C{i}", new Vector2(x0[i], 0.10f), new Vector2(x1[i], 0.56f));
            _choiceBg[i]       = c.AddComponent<Image>();
            _choiceBg[i].color = BG_CHOICE;
            _choiceTxt[i]      = MakeText(c, $"CT{i}", 0f, 1f, 0f, 1f, 30, C_NEUTRAL, TextAnchor.MiddleCenter);
        }

        _feedbackText = MakeText(_panel, "Feedback", 0f, 0.10f, 0f, 1f, 26, C_DIM, TextAnchor.MiddleCenter);
    }

    static GameObject MakeGO(GameObject parent, string name, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static Text MakeText(GameObject parent, string name,
                         float yMin, float yMax, float xMin, float xMax,
                         int size, Color color, TextAnchor anchor,
                         bool bold = false, float pad = 8f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = new Vector2(pad, 2f);    rt.offsetMax = new Vector2(-pad, -2f);
        var t = go.AddComponent<Text>();
        t.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize           = size;
        t.color              = color;
        t.alignment          = anchor;
        t.supportRichText    = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        if (bold) t.fontStyle = FontStyle.Bold;
        return t;
    }
}
