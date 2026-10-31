@echo off
chcp 65001 >nul
cd /d "%~dp0"
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0release.ps1" %*
if errorlevel 1 (
    echo.
    echo CADDC 发布失败，请查看上方错误。
    echo 脚本已经结束，下面等待按键关闭窗口，不是卡死。
    pause
    exit /b 1
)

echo.
echo CADDC 发布完成，安装包位于 artifacts 目录。
echo 脚本已经结束，下面等待按键关闭窗口，不是卡死。
pause
