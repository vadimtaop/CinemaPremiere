@echo off
chcp 65001 > nul
echo.
echo ======================================================
echo    Развертывание базы данных: CinemaPremiereDb
echo ======================================================
echo.

set SCRIPT_NAME="CinemaPremiereDb_Script (TestData).sql"

echo [1/2] Проверка наличия утилиты sqlcmd...
where sqlcmd >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ОШИБКА] Утилита sqlcmd не найдена. Убедитесь, что MS SQL Server установлен.
    pause
    exit /b
)

echo [2/2] Запуск SQL-скрипта...

sqlcmd -S . -E -i %SCRIPT_NAME%
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Попытка подключения к .\SQLEXPRESS...
    sqlcmd -S .\SQLEXPRESS -E -i %SCRIPT_NAME%
)

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] ОШИБКА: Не удалось создать базу данных. 
    echo Проверьте:
    echo 1. Запущена ли служба SQL Server.
    echo 2. Есть ли у вас права администратора.
    echo.
) else (
    echo.
    echo [+] УСПЕХ: База данных CinemaPremiereDb готова к работе!
    echo.
)

pause