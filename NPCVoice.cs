using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// NPCVoice — calls tts_server.py (port 7866) and plays the returned audio
/// through an AudioSource on this GameObject.
///
/// SETUP: Add this component to the NPC_Doctor GameObject.
///        Then call Speak() from NPCDoctor whenever the doctor says something.
///
/// USAGE:
///   GetComponent&lt;NPCVoice&gt;().Speak("Guten Tag, wie geht es Ihnen?", "de");
///   GetComponent&lt;NPCVoice&gt;().Speak("Good afternoon!", "en");
///
/// WAV  (pyttsx3 offline)  → decoded directly in memory.
/// MP3  (edge-tts neural)  → written to a temp file and loaded via Unity's
///                           built-in MP3 decoder (works on Windows/Android).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class NPCVoice : MonoBehaviour
{
    [Header("TTS Server")]
    public string ttsServerUrl = "http://localhost:7866/speak";

    [Header("Voice override (leave blank to use server default)")]
    [Tooltip("e.g. de-DE-KatjaNeural  or  de-DE-ConradNeural")]
    public string voiceDe = "";
    [Tooltip("e.g. en-US-JennyNeural")]
    public string voiceEn = "";

    [Header("Behaviour")]
    [Tooltip("Ignore new Speak() calls while audio is still playing.")]
    public bool skipWhileSpeaking = false;
    [Tooltip("Volume of the doctor's voice (0-1).")]
    [Range(0f, 1f)]
    public float volume = 1f;

    // ── state ─────────────────────────────────────────────────────────────────
    private AudioSource _audio;
    public  bool IsSpeaking => _audio != null && _audio.isPlaying;

    // temp file path for MP3
    private string _mp3TmpPath;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake  = false;
        _audio.spatialBlend = 1f;   // 3-D audio positioned at the NPC
        _audio.volume       = volume;
        _mp3TmpPath = Path.Combine(Path.GetTempPath(), "npc_doctor_voice.mp3");
    }

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>Fetch audio from the TTS server and play it. Non-blocking.</summary>
    public void Speak(string text, string lang = "de")
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (skipWhileSpeaking && IsSpeaking) return;

        StopAllCoroutines();
        _audio.Stop();
        _audio.volume = volume;
        StartCoroutine(FetchAndPlay(text, lang));
    }

    /// <summary>Stop the doctor's voice immediately.</summary>
    public void StopSpeaking() => _audio.Stop();

    // ── internals ─────────────────────────────────────────────────────────────

    IEnumerator FetchAndPlay(string text, string lang)
    {
        // Build JSON body
        string voice  = lang == "de" ? voiceDe : voiceEn;
        string voiceJson = string.IsNullOrEmpty(voice) ? "" : voice;
        string body   = $"{{\"text\":\"{EscapeJson(text)}\","
                      + $"\"lang\":\"{lang}\","
                      + $"\"voice\":\"{voiceJson}\"}}";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

        // POST to TTS server
        using var req = new UnityWebRequest(ttsServerUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 20;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[NPCVoice] TTS server error: {req.error}  "
                           + "(Is tts_server.py running on port 7866?)");
            yield break;
        }

        byte[] audioData = req.downloadHandler.data;
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogWarning("[NPCVoice] TTS server returned empty audio.");
            yield break;
        }

        string ct = req.GetResponseHeader("Content-Type") ?? "";

        if (ct.Contains("wav"))
        {
            // ── WAV (pyttsx3 offline fallback) ────────────────────────────────
            AudioClip clip = WavDecoder.Decode(audioData, "npc_tts");
            if (clip == null) { Debug.LogWarning("[NPCVoice] WAV decode failed."); yield break; }
            _audio.clip = clip;
            _audio.Play();
            Debug.Log($"[NPCVoice] Playing WAV TTS ({lang}): \"{Truncate(text, 60)}\"");
        }
        else
        {
            // ── MP3 (edge-tts neural voice) ───────────────────────────────────
            // Unity can load MP3 from a local file on Windows and Android.
            File.WriteAllBytes(_mp3TmpPath, audioData);

            string fileUrl = "file:///" + _mp3TmpPath.Replace("\\", "/");
            using var req2 = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG);
            yield return req2.SendWebRequest();

            if (req2.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[NPCVoice] MP3 load failed: {req2.error}");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(req2);
            if (clip == null) { Debug.LogWarning("[NPCVoice] MP3 AudioClip is null."); yield break; }
            _audio.clip = clip;
            _audio.Play();
            Debug.Log($"[NPCVoice] Playing MP3 TTS ({lang}): \"{Truncate(text, 60)}\"");
        }
    }

    // ── tiny helpers ──────────────────────────────────────────────────────────

    static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\")
         .Replace("\"", "\\\"")
         .Replace("\n", "\\n")
         .Replace("\r", "\\r");

    static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";
}

// ── Minimal WAV decoder ───────────────────────────────────────────────────────
// Handles 16-bit and 8-bit PCM WAV, mono or stereo.
// Used for pyttsx3 output; edge-tts output is MP3 (handled above).
public static class WavDecoder
{
    public static AudioClip Decode(byte[] wav, string clipName)
    {
        if (wav == null || wav.Length < 44) return null;
        try
        {
            int channels   = wav[22] | (wav[23] << 8);
            int sampleRate = wav[24] | (wav[25] << 8) | (wav[26] << 16) | (wav[27] << 24);
            int bitDepth   = wav[34] | (wav[35] << 8);

            // Find "data" chunk (header may be > 44 bytes)
            int dataOffset = 44;
            for (int i = 12; i < wav.Length - 8; i++)
            {
                if (wav[i]=='d' && wav[i+1]=='a' && wav[i+2]=='t' && wav[i+3]=='a')
                { dataOffset = i + 8; break; }
            }

            int bytesPerSample   = bitDepth / 8;
            int totalSamples     = (wav.Length - dataOffset) / bytesPerSample;
            int samplesPerChannel = totalSamples / Mathf.Max(1, channels);
            float[] pcm = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                int off = dataOffset + i * bytesPerSample;
                if (bitDepth == 16)
                {
                    short s = (short)(wav[off] | (wav[off + 1] << 8));
                    pcm[i] = s / 32768f;
                }
                else if (bitDepth == 8)
                {
                    pcm[i] = (wav[off] - 128) / 128f;
                }
            }

            AudioClip clip = AudioClip.Create(clipName, samplesPerChannel, channels, sampleRate, false);
            clip.SetData(pcm, 0);
            return clip;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[WavDecoder] {ex.Message}");
            return null;
        }
    }
}
