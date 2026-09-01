using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// VoiceRecorder v2 — unified input + compose + review system
/// ===========================================================
///
/// MODE SWITCH (arrow keys):
///   ↑  Up Arrow    → TYPE mode   (keyboard chars → compose area)
///   ↓  Down Arrow  → VOICE mode  (mic → Whisper → compose area)  [default]
///
/// COMPOSE AREA (both modes):
///   Text accumulates before sending. Shown in the gold HUD strip.
///   [Delete]        → remove last word
///   [Shift+Delete]  → clear all  (speak/type again)
///   [Enter]         → send compose to NPC doctor
///
/// VOICE MODE extras:
///   Hold [Space]    → record mic
///   Release         → Whisper transcribes → text appended to compose
///   (hold Space multiple times to build a longer sentence before sending)
///
/// TYPE MODE extras (PC testing):
///   Keyboard chars  → typed into compose area
///   [Backspace]     → remove last character
///
/// Attach to any always-active GameObject (e.g. the AIRoomBox or Main Camera).
/// Requires: NPCChatUI in scene, whisper_server.py running on port 7864.
/// </summary>
public class VoiceRecorder : MonoBehaviour
{
    // ── inspector ─────────────────────────────────────────────────────────────
    [Header("Server")]
    public string whisperUrl = "http://localhost:7864/transcribe";

    [Header("Recording")]
    [Range(2, 30)] public int maxRecordSeconds = 8;
    public int sampleRate = 16000;

    [Header("Feedback")]
    public bool playBeeps = true;

    // ── mode ──────────────────────────────────────────────────────────────────
    public enum InputMode { Voice, Type }
    [HideInInspector] public InputMode currentMode = InputMode.Voice;

    // ── compose state ─────────────────────────────────────────────────────────
    private string _compose   = "";
    private bool   _recording  = false;
    private bool   _processing = false;
    private AudioClip   _clip;
    private AudioSource _beepSource;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _beepSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _beepSource.playOnAwake = false;

        if (Microphone.devices.Length == 0)
            Debug.LogWarning("[VoiceRecorder] No microphone found.");
        else
            Debug.Log($"[VoiceRecorder] Mic: {Microphone.devices[0]}");

        ShowStatus();
        NPCChatUI.Instance?.SetComposeText("");
    }

    void Update()
    {
        HandleModeSwitch();

        if (!_processing)
        {
            HandleDeleteClear();
            HandleSend();
            if (currentMode == InputMode.Type)  HandleTyping();
            else                                HandleVoiceKeys();
        }
    }

    // ── mode switch ───────────────────────────────────────────────────────────

    void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) && currentMode != InputMode.Type)
        {
            if (_recording) { Microphone.End(null); _recording = false; _processing = false; }
            currentMode = InputMode.Type;
            ShowStatus();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) && currentMode != InputMode.Voice)
        {
            currentMode = InputMode.Voice;
            ShowStatus();
        }
    }

    void ShowStatus()
    {
        if (NPCChatUI.Instance == null) return;
        if (currentMode == InputMode.Voice)
            NPCChatUI.Instance.SetStatus(
                "↓ VOICE  |  [Space]=record  [Del]=−word  [Shift+Del]=clear  [Enter]=send  |  ↑=switch to Type");
        else
            NPCChatUI.Instance.SetStatus(
                "↑ TYPE   |  type below  [Backspace]=−char  [Del]=−word  [Shift+Del]=clear  [Enter]=send  |  ↓=switch to Voice");
    }

    // ── delete / clear ────────────────────────────────────────────────────────

    void HandleDeleteClear()
    {
        bool del   = Input.GetKeyDown(KeyCode.Delete);
        bool back  = Input.GetKeyDown(KeyCode.Backspace);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (del && shift)
        {
            // Shift + Delete → clear all, prompt to retry
            _compose = "";
            NPCChatUI.Instance?.SetComposeText("");
            string retryHint = currentMode == InputMode.Voice
                ? "Cleared — hold [Space] to speak again"
                : "Cleared — type again";
            NPCChatUI.Instance?.SetStatus(retryHint);
        }
        else if (del)
        {
            // Delete → remove last word
            _compose = DropLastWord(_compose);
            NPCChatUI.Instance?.SetComposeText(_compose);
        }
        else if (back && currentMode == InputMode.Type && _compose.Length > 0)
        {
            // Backspace in Type mode → remove last character
            _compose = _compose.Substring(0, _compose.Length - 1);
            NPCChatUI.Instance?.SetComposeText(_compose);
        }
    }

    static string DropLastWord(string s)
    {
        s = s.TrimEnd();
        int i = s.LastIndexOf(' ');
        return i < 0 ? "" : s.Substring(0, i);
    }

    // ── send ──────────────────────────────────────────────────────────────────

    void HandleSend()
    {
        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
        string text = _compose.Trim();
        if (string.IsNullOrEmpty(text)) return;

        NPCChatUI.Instance?.AddPlayerMessage(text);
        ForwardToNPC(text);

        _compose = "";
        NPCChatUI.Instance?.SetComposeText("");
        ShowStatus();
    }

    // ── type mode ─────────────────────────────────────────────────────────────

    void HandleTyping()
    {
        foreach (char c in Input.inputString)
        {
            if (c == '\r' || c == '\n' || c == '\b') continue;
            _compose += c;
        }
        if (Input.inputString.Length > 0)
            NPCChatUI.Instance?.SetComposeText(_compose);
    }

    // ── voice mode ────────────────────────────────────────────────────────────

    void HandleVoiceKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !_recording)
            BeginRecording();
        else if (Input.GetKeyUp(KeyCode.Space) && _recording)
            EndRecording();
    }

    void BeginRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            NPCChatUI.Instance?.SetStatus("⚠ No microphone detected.");
            return;
        }
        _recording = true;
        _clip = Microphone.Start(null, false, maxRecordSeconds, sampleRate);
        PlayTone(880f, 0.08f);
        NPCChatUI.Instance?.SetStatus("🔴 Recording… release [Space] to transcribe");
    }

    void EndRecording()
    {
        _recording  = false;
        _processing = true;
        int position = Microphone.GetPosition(null);
        Microphone.End(null);
        PlayTone(440f, 0.08f);

        if (position < sampleRate * 0.3f)
        {
            _processing = false;
            NPCChatUI.Instance?.SetStatus("Too short — hold [Space] longer to speak");
            return;
        }

        NPCChatUI.Instance?.SetStatus("⏳ Transcribing…");
        StartCoroutine(PostToWhisper(_clip, position));
    }

    IEnumerator PostToWhisper(AudioClip clip, int samples)
    {
        byte[] wav = EncodeWav(clip, samples);

        var form = new WWWForm();
        form.AddBinaryData("audio", wav, "voice.wav", "audio/wav");

        var req = UnityWebRequest.Post(whisperUrl, form);
        req.timeout = 25;
        yield return req.SendWebRequest();

        _processing = false;

        if (req.result != UnityWebRequest.Result.Success)
        {
            NPCChatUI.Instance?.SetStatus($"⚠ Cannot reach Whisper — is start_whisper.bat running?");
            Debug.LogError($"[VoiceRecorder] {req.error}");
            yield break;
        }

        TranscribeResp resp;
        try   { resp = JsonUtility.FromJson<TranscribeResp>(req.downloadHandler.text); }
        catch { resp = new TranscribeResp { text = req.downloadHandler.text.Trim('"') }; }

        string t = resp.text?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            NPCChatUI.Instance?.SetStatus("(nothing heard) — try again  |  Hold [Space]");
            yield break;
        }

        // Append to compose with a space separator
        if (_compose.Length > 0 && !_compose.EndsWith(" ")) _compose += " ";
        _compose += t;

        NPCChatUI.Instance?.SetComposeText(_compose);
        NPCChatUI.Instance?.SetStatus(
            "[Enter]=send  |  [Space]=add more  |  [Del]=−word  |  [Shift+Del]=clear & retry");

        Debug.Log($"[VoiceRecorder] Transcript: {t}");
    }

    // ── forward to NPC ────────────────────────────────────────────────────────

    void ForwardToNPC(string text)
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            var m = mb.GetType().GetMethod("AskQuestion",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (m != null) { m.Invoke(mb, new object[] { text }); return; }

            var m2 = mb.GetType().GetMethod("ReceivePlayerInput",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (m2 != null) { m2.Invoke(mb, new object[] { text }); return; }
        }
        NPCChatUI.Instance?.AddDoctorMessage(
            "(NPC not connected — add AskQuestion(string) to NPCDoctor.cs)");
    }

    // ── beep ──────────────────────────────────────────────────────────────────

    void PlayTone(float freq, float dur)
    {
        if (!playBeeps || _beepSource == null) return;
        int n = (int)(44100 * dur);
        var clip = AudioClip.Create("beep", n, 1, 44100, false);
        var d = new float[n];
        for (int i = 0; i < n; i++)
            d[i] = 0.35f * Mathf.Sin(2f * Mathf.PI * freq * i / 44100f);
        clip.SetData(d, 0);
        _beepSource.PlayOneShot(clip);
    }

    // ── WAV encoder ───────────────────────────────────────────────────────────

    static byte[] EncodeWav(AudioClip clip, int sampleCount)
    {
        int ch   = clip.channels;
        int rate = clip.frequency;
        var raw  = new float[sampleCount * ch];
        clip.GetData(raw, 0);

        var mono = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float s = 0f;
            for (int c = 0; c < ch; c++) s += raw[i * ch + c];
            s /= ch;
            mono[i] = (short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767);
        }

        int dataBytes = mono.Length * 2;
        var wav = new byte[44 + dataBytes];
        int o = 0;

        void Str(string s) { foreach (char c in s) wav[o++] = (byte)c; }
        void I32(int v)    { Array.Copy(BitConverter.GetBytes(v), 0, wav, o, 4); o += 4; }
        void I16(short v)  { Array.Copy(BitConverter.GetBytes(v), 0, wav, o, 2); o += 2; }

        Str("RIFF"); I32(36 + dataBytes);
        Str("WAVE"); Str("fmt "); I32(16);
        I16(1); I16(1); I32(rate); I32(rate * 2); I16(2); I16(16);
        Str("data"); I32(dataBytes);
        foreach (short s in mono) I16(s);

        return wav;
    }

    [Serializable] private class TranscribeResp { public string text; }
}
