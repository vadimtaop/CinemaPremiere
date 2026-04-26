@echo off
cd /d "%~dp0"

chcp 65001 > nul

set SCRIPT_NAME="CinemaPremiereDb_Script.sql"

where sqlcmd >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    exit /b 1
)

sqlcmd -S . -E -i %SCRIPT_NAME%
if %ERRORLEVEL% NEQ 0 (
    sqlcmd -S .\SQLEXPRESS -E -i %SCRIPT_NAME%
)

exit /b %ERRORLEVEL%