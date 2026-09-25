@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

rem ---------------------------------------------------------------
rem  AminAmval — اجرای یک‌دستی روی ویندوز (نیازی به نصب Node نیست)
rem  نسخهٔ نهایی v1.1.0
rem ---------------------------------------------------------------

rem 1) Node همراه بسته (runtime\node.exe) در اولویت است؛
rem    در غیر این صورت از Node نصب‌شده روی سیستم استفاده می‌شود.
set "NODE=%~dp0runtime\node.exe"
if not exist "%NODE%" set "NODE=node"
"%NODE%" --version >nul 2>&1
if errorlevel 1 (
  echo [خطا] Node.js پیدا نشد.
  echo بستهٔ کامل باید شامل runtime\node.exe باشد، یا Node 22.5+ را نصب کنید.
  echo دانلود: https://nodejs.org
  pause
  exit /b 1
)

rem 2) اگر وابستگی‌ها هنوز نصب نشده باشند، نصب می‌شوند.
if not exist "%~dp0node_modules" (
  echo [نصب] در حال نصب وابستگی‌ها ^(فقط بار اول٬ چند ثانیه^)...
  set "NPMCLI=%~dp0runtime\node_modules\npm\bin\npm-cli.js"
  if not exist "%NPMCLI%" set "NPMCLI=%~dp0npm-cli.js"
  "%NODE%" "%NPMCLI%" install --omit=dev --no-audit --no-fund
  if errorlevel 1 (
    echo [خطا] نصب وابستگی‌ها ناموفق بود. اتصال اینترنت را بررسی کنید.
    pause
    exit /b 1
  )
)

rem 3) تنظیمات (قابل تغییر):
if "%PORT%"=="" set "PORT=8080"
if "%PREVIEW_MODE%"=="" set "PREVIEW_MODE=true"

rem 4) رمزهای اولیهٔ اجباری (فقط در اولین اجرا اعمال می‌شوند):
if "%ADMIN_PASSWORD%"=="" set "ADMIN_PASSWORD=Admin1289@@@"
if "%CUSTODIAN_PASSWORD%"=="" set "CUSTODIAN_PASSWORD=Jamdar1289@@@"

rem 5) اجرای سرور
echo.
echo  ==============================================================
echo   امین اموال ناواکو — در حال اجرا روی http://localhost:%PORT%
echo   توقف: بستن همین پنجره یا Ctrl+C
echo  ==============================================================
echo.
"%NODE%" "%~dp0server.js"
pause
