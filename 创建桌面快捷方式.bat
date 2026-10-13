@echo off
chcp 936 >nul
setlocal
set "HERE=%~dp0"
if not exist "%HERE%我的世界背包.exe" goto missing
powershell -NoProfile -ExecutionPolicy Bypass -Command "$h='%HERE%'; $e=Join-Path $h '我的世界背包.exe'; $d=[Environment]::GetFolderPath('Desktop'); $l=Join-Path $d '我的世界背包.lnk'; $w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut($l); $s.TargetPath=$e; $s.WorkingDirectory=$h; $s.IconLocation=$e+',0'; $s.Description='仿 Minecraft 背包界面的快捷启动器'; $s.Save(); Write-Host ('已创建桌面快捷方式: ' + $l)"
echo.
pause
exit /b 0
:missing
echo 没找到 我的世界背包.exe, 请把这个脚本放在程序所在的文件夹里再运行。
pause
exit /b 1
