@echo off
rem Builds CatTimer.exe using the C# compiler that ships with Windows (.NET Framework 4).
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. Please enable .NET Framework 4.x in Windows Features.
  pause
  exit /b 1
)
"%CSC%" /nologo /target:winexe /win32icon:Cat.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:CatTimer.exe CatTimer.cs
if errorlevel 1 (
  echo Build FAILED.
) else (
  echo Build OK: %~dp0CatTimer.exe
)
pause
