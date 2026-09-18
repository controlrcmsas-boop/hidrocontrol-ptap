@echo off
title Dashboard SCADA - HIDROCONTROL PTAP
cd /d "%~dp0dashboard\PTAPControl"
dotnet run --urls "http://0.0.0.0:5088"
pause
