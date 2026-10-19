@echo off
if exist "%~dp0Builds\Windows\EscapeFromIrkutsk.exe" (
    start "" "%~dp0Builds\Windows\EscapeFromIrkutsk.exe"
) else (
    echo Open Assets/Scenes/EscapePrototype.unity in Unity 6000.6.6f1 and press Play.
    pause
)
