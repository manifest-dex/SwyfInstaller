@echo off
setlocal
if not exist "%~dp0Scam With Your Friends.exe" (
    echo Run this file from the game folder, next to Scam With Your Friends.exe.
    echo Press any key to continue . . .
    pause >nul
    exit /b 1
)
"%~dp0CustomAI\package\installer\SWYF.CustomAI.Installer.exe" uninstall "%~dp0."
set "uninstallResult=%errorlevel%"
if not "%uninstallResult%"=="0" echo Uninstall failed. Read the error above.
echo Press any key to continue . . .
pause >nul
exit /b %uninstallResult%
