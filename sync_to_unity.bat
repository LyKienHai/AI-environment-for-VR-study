@echo off
title Sync scripts to Unity project
color 0A

set UNITY=E:\AI_HDRI_Unity\Assets
set SRC=%~dp0

echo Copying C# scripts to Unity project...

for %%f in (
    AIDepthSkybox.cs
    AIRoomBox.cs
    AISkyboxReceiver.cs
    ChairCorner.cs
    DoctorRoomDecorator.cs
    DoorQuad.cs
    GameBootstrap.cs
    GermanLabel.cs
    NPCChatUI.cs
    NPCDoctor.cs
    NPCVoice.cs
    ObjectAnchorPlacer.cs
    QuizManager.cs
    RoomAutoLoader.cs
    RoomColorAccuracy.cs
    RoomConfig.cs
    RoomConfigLoader.cs
    SpectatorCamera.cs
    VRMouseLook.cs
    VoiceRecorder.cs
    WallImageQuiz.cs
    WallPictureLoader.cs
) do (
    if exist "%SRC%%%f" (
        copy /Y "%SRC%%%f" "%UNITY%\%%f" >nul
        echo   Copied %%f
    )
)

echo.
echo Copying Editor scripts...
if not exist "%UNITY%\Editor" mkdir "%UNITY%\Editor"
for %%f in (BuildScript.cs SetupXRSimulator.cs VRAutoDeployer.cs) do (
    if exist "%SRC%Editor\%%f" (
        copy /Y "%SRC%Editor\%%f" "%UNITY%\Editor\%%f" >nul
        echo   Copied Editor\%%f
    )
)

echo.
echo Done! Unity will recompile automatically.
pause
