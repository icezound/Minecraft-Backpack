@echo off
chcp 936 >nul
setlocal
set "HERE=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto nocsc
echo 正在编译, 请稍等...
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"%HERE%app.ico" /out:"%HERE%我的世界背包.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll "%HERE%源码\*.cs"
if errorlevel 1 goto failed
echo.
echo 编译完成: 我的世界背包.exe
goto end
:failed
echo.
echo 编译失败, 请看上面的报错信息。
goto end
:nocsc
echo 找不到 .NET Framework 的编译器 csc.exe, 没法重新编译。
echo 请确认系统是 Windows 10 或 11, 它们自带 .NET Framework 4.8。
:end
echo.
pause
