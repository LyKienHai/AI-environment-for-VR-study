# AI_HDRI_Unity — GenAI 3D Virtual Reality Augmented

A VR medical German-language learning environment built with Unity 6 and Python AI services.

---

## Quick Start (New Computer)

### 1. Clone the repo
```bash
git clone <your-repo-url> AI_HDRI_Unity
cd AI_HDRI_Unity
```

### 2. Run the auto-setup (Windows only)
Right-click `setup.bat` → **Run as administrator**

This installs automatically:
- Git
- Python 3.10
- Unity Hub
- Unity 6000.4.3f1 (with Android/Quest build support)
- All Python packages (`requirements.txt`)

> ⏱ First run takes 15–30 min (Unity download is ~2 GB)

### 3. Open the Unity project
1. Open **Unity Hub**
2. Click **Add** → select the cloned folder
3. Wait for asset import (~5 min first time)

### 4. Start the web app
```bash
python gradio_app_v2.py
```
Open browser → http://localhost:7860

### 5. (Optional) Start the Whisper voice server
```bash
python whisper_server.py
```
Runs on http://localhost:7864

---

## Project Structure

```
AI_HDRI_Unity/
├── Assets/
│   ├── DoctorRoomDecorator.cs   # Doctor room scene setup
│   ├── QuizManager.cs           # German vocabulary quiz
│   ├── WallImageQuiz.cs         # Image-based wall quiz
│   ├── NPCChatUI.cs             # HUD chat interface
│   └── VoiceRecorder.cs         # Mic → Whisper transcription
├── gradio_app_v2.py             # Main web UI (Builder + Learner mode)
├── whisper_server.py            # FastAPI voice transcription server
├── requirements.txt             # Python dependencies
├── setup.bat                    # Auto-install all prerequisites
└── README.md                    # This file
```

---

## Requirements (installed by setup.bat)

| Tool | Version |
|------|---------|
| Unity | 6000.4.3f1 |
| Python | 3.10+ |
| Git | latest |
| CUDA (optional) | 11.8+ for GPU Whisper |

---

## VR Headset (Varjo)

If using a Varjo XR headset, install **Varjo Base 4.16+**:
- Download from https://varjo.com/downloads
- Requires ~4 GB free on C: drive

---

## Common Issues

**`ModuleNotFoundError`** — run `pip install -r requirements.txt`

**Unity can't find Android SDK** — in Unity Hub → Installs → your version → Add Modules → Android

**Whisper is slow** — install PyTorch with CUDA for GPU acceleration:
```bash
pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu118
```
