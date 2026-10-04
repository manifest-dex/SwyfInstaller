@echo off
setlocal
if not exist "%~dp0Scam With Your Friends.exe" (
    echo Extract the entire ZIP into the game folder first.
    echo This file must be next to Scam With Your Friends.exe.
    echo Press any key to continue . . .
    pause >nul
    exit /b 1
)
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0CustomAI\package\prerequisites.ps1" -PackageDir "%~dp0CustomAI\package"
set "prerequisiteResult=%errorlevel%"
if not "%prerequisiteResult%"=="0" (
    echo Prerequisites are not ready. The mod was not installed. Read the message above.
    echo Press any key to continue . . .
    pause >nul
    exit /b %prerequisiteResult%
)
echo If you disable Kolkata, press F8 to configure and enable your AI provider.
echo Kolkata accounts, credits and community cloud features will be unavailable. Steam and networking are unaffected.
choice /C YN /N /M "Disable the Kolkata API? [Y]es / [N]o: "
set "kolkataChoice=%errorlevel%"
set "disableKolkata="
if "%kolkataChoice%"=="1" set "disableKolkata=true"
if "%kolkataChoice%"=="2" set "disableKolkata=false"
if not defined disableKolkata (
    echo Selection canceled. The mod was not installed.
    echo Press any key to continue . . .
    pause >nul
    exit /b 1
)
"%~dp0CustomAI\package\installer\SWYF.CustomAI.Installer.exe" install "%~dp0." "%~dp0CustomAI\package" "--disable-kolkata=%disableKolkata%"
set "installResult=%errorlevel%"
if not "%installResult%"=="0" echo Installation failed. Read the error above.
echo Press any key to continue . . .
pause >nul
exit /b %installResult%
