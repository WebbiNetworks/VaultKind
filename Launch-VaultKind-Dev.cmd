@echo off
setlocal
title VaultKind Development Launcher

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-vaultkind-dev.ps1"
if errorlevel 1 (
	echo.
	echo VaultKind could not be started. See the error above.
	pause
)

