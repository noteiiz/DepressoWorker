@echo off
rem Creates a shortcut so CatTimer starts automatically when you log in.
cd /d "%~dp0"
powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Startup')+'\CatTimer.lnk'); $s.TargetPath='%~dp0CatTimer.exe'; $s.WorkingDirectory='%~dp0'; $s.Save()"
echo CatTimer will now start automatically when you log in.
echo (To undo: press Win+R, type shell:startup, delete CatTimer.lnk)
pause
