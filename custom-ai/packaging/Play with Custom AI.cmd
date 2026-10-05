@echo off
setlocal
if not exist "%~dp0Scam With Your Friends.exe" (
    echo Extract the entire ZIP into the game folder first.
    echo This file must be next to Scam With Your Friends.exe.
    echo Press any key to continue . . .
    pause >nul
    exit /b 1
)
"%~dp0CustomAI\package\installer\SWYF.CustomAI.Installer.exe" launch "%~dp0." "%~dp0CustomAI\package"
if errorlevel 1 (
    echo Could not launch. Read the error above. Wait for Steam updates to finish and close the game before retrying.
    echo Press any key to continue . . .
    pause >nul
    exit /b 1
)
