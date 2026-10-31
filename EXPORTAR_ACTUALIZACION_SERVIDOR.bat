@echo off
chcp 65001 >nul
title EXPORTAR PAQUETE DE ACTUALIZACIÓN AL SERVIDOR DE OFICINA
color 0a

python "%~dp0scripts\exportar_actualizacion.py"
pause
