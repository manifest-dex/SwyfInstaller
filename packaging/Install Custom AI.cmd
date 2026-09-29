@echo off
setlocal
if not exist "%~dp0Scam With Your Friends.exe" (
    echo Extract the entire ZIP into the game folder first.
    echo This file must be next to Scam With Your Friends.exe.
    pause
    exit /b 1
)
"%~dp0CustomAI\package\installer\SWYF.CustomAI.Installer.exe" install "%~dp0." "%~dp0CustomAI\package"
set "installResult=%errorlevel%"
if not "%installResult%"=="0" echo Installation failed. Read the error above. Check that ASP.NET Core Runtime 10 x64 is installed.
pause
exit /b %installResult%
