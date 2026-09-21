@echo off
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
rem Never build over a running ClipLite. On a normal disk the compiler would fail with a locked
rem file; on a cloud-synced folder the write goes through and the running process then loads
rem garbage for every method it has not called yet (that is how tray Exit once broke).
tasklist /FI "IMAGENAME eq ClipLite.exe" 2>nul | find /I "ClipLite.exe" >nul
if not errorlevel 1 (echo ClipLite is running - exit it from the tray first. & exit /b 1)
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /platform:anycpu ^
  /out:bin\ClipLite.exe /win32manifest:src\app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /resource:assets\sounds\Capture.wav,ClipLite.Sounds.Capture.wav ^
  /resource:assets\sounds\Erase.wav,ClipLite.Sounds.Erase.wav ^
  /resource:assets\sounds\Ignore.wav,ClipLite.Sounds.Ignore.wav ^
  /resource:assets\sounds\Append.wav,ClipLite.Sounds.Append.wav ^
  src\*.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo Built bin\ClipLite.exe
