@echo off
chcp 65001 >nul
title Node-RED - HIDROCONTROL PTAP V2.2
color 0b

echo ==============================================================================
echo           HIDROCONTROL PTAP V2.2 - INICIANDO NODE-RED (PUERTO 1880)
echo ==============================================================================
echo.

call node-red.cmd
if %errorlevel% neq 0 (
    call node-red
)
pause
