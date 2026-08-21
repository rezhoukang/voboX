@echo off
rem voboX one-click package builder
rem 1) edit the $Version line in installer\package.ps1
rem 2) double-click this file to build the installer
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "installer\package.ps1"
echo.
pause
