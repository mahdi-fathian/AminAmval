@echo off
setlocal EnableExtensions
title AminAmval - Asset Management System
chcp 65001 >nul
cd /d "%~dp0"

echo.
echo  ================================================================
echo   AminAmval - Asset Management System  (v1.2.0)
echo   Starting... please wait a few seconds.
echo  ================================================================
echo.

rem ---------------------------------------------------------------
rem  0) the files must be really extracted (not opened inside the ZIP)
rem ---------------------------------------------------------------
if not exist "%~dp0server.js" goto :err_noserver

rem ---------------------------------------------------------------
rem  1) remove the "downloaded from internet" block that Windows puts
rem     on files from a ZIP (this is what blocks node.exe silently)
rem ---------------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -Command "try{Get-ChildItem -LiteralPath '%~dp0runtime' -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue}catch{}" >nul 2>&1

rem ---------------------------------------------------------------
rem  2) find a usable Node runtime (bundled one first)
rem ---------------------------------------------------------------
set "NODE="
if exist "%~dp0runtime\node.exe" set "NODE=%~dp0runtime\node.exe"
if defined NODE (
  "%NODE%" --version >nul 2>&1
  if errorlevel 1 set "NODE="
)
if not defined NODE (
  node --version >nul 2>&1
  if not errorlevel 1 set "NODE=node"
)
if not defined NODE goto :err_nonode

rem ---------------------------------------------------------------
rem  3) install dependencies only if they are missing
rem ---------------------------------------------------------------
if not exist "%~dp0node_modules\xlsx" call :installdeps

rem ---------------------------------------------------------------
rem  4) settings (edit here if you need to)
rem ---------------------------------------------------------------
if "%PORT%"=="" set "PORT=8080"
set "HOST=127.0.0.1"
rem   ^^^ for access from other computers on the LAN change it to 0.0.0.0
set "PREVIEW_MODE=true"
set "LOG_FILE=%~dp0startup-log.txt"
set "OPEN_BROWSER=1"

rem  first-run mandatory accounts (used only while the database is empty)
if "%ADMIN_PASSWORD%"=="" set "ADMIN_PASSWORD=Admin1289@@@"
if "%CUSTODIAN_PASSWORD%"=="" set "CUSTODIAN_PASSWORD=Jamdar1289@@@"

rem ---------------------------------------------------------------
rem  5) run the server
rem ---------------------------------------------------------------
echo  Starting on:  http://127.0.0.1:%PORT%
echo  (the browser opens by itself; to stop: close this window or Ctrl+C)
echo  Log file: %LOG_FILE%
echo.
echo  First login:  admin / Admin1289@@@   (system manager)
echo                jamdar / Jamdar1289@@@  (asset custodian)
echo.

"%NODE%" "%~dp0server.js"
set "RC=%ERRORLEVEL%"

echo.
echo  ================================================================
echo   The server has stopped (exit code %RC%).
echo   Details were saved to: %LOG_FILE%
echo   If this keeps happening, send that file to support.
echo  ================================================================
echo.
pause
exit /b %RC%

rem ===============================================================
rem  helper routines
rem ===============================================================
:installdeps
echo  [setup] dependencies are missing - installing (needs internet, one time)...
set "NPMCLI=%~dp0runtime\node_modules\npm\bin\npm-cli.js"
if exist "%NPMCLI%" goto :dep_bundled
set "NPMCLI=%~dp0npm-cli.js"
if exist "%NPMCLI%" goto :dep_bundled
echo  [setup] using the system npm...
call npm install --omit=dev --no-audit --no-fund
if errorlevel 1 goto :err_nodeps
goto :eof

:dep_bundled
"%NODE%" "%NPMCLI%" install --omit=dev --no-audit --no-fund
if errorlevel 1 goto :err_nodeps
goto :eof

:err_noserver
echo  [ERROR] server.js was not found next to this file.
echo.
echo  This usually means the ZIP was opened directly instead of extracted.
echo  Fix: right-click the ZIP  ^>  "Extract All..."  ^>  open the extracted
echo  folder (for example C:\AminAmval) and run start-windows.bat from there.
echo.
pause
exit /b 1

:err_nonode
echo  [ERROR] no usable Node.js runtime was found.
echo.
echo  The package should contain:  runtime\node.exe
echo  If it is missing, either re-extract the full ZIP, or install Node.js
echo  version 22.5 or newer from  https://nodejs.org  and run this file again.
echo.
pause
exit /b 1

:err_nodeps
echo  [ERROR] installing the dependencies failed.
echo.
echo  Check the internet connection and run this file again.
echo  If the computer has no internet at all, make sure the folder
echo  "node_modules" from the ZIP was extracted next to this file.
echo.
pause
exit /b 1
