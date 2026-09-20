#!/usr/bin/env bash
# AminAmval — یک‌دستور اجرای همیشه‌بالا (بدون Docker)
# سرور را اجرا می‌کند و اگر هر لحظه بیفتد، خودکار دوباره بلندش می‌کند (watchdog).
#
#   ./run.sh                  → پورت پیش‌فرض 8080
#   PORT=8097 ./run.sh        → پورت دلخواه
#
# وابستگی‌ها: node >= 22.5  و  npm install

set -u
cd "$(dirname "$0")"

if [ ! -d node_modules ]; then
  echo ">> نصب وابستگی‌ها (npm install)…"
  npm install --omit=dev || { echo "npm install شکست خورد"; exit 1; }
fi

export PORT="${PORT:-8080}"
export HOST="${HOST:-0.0.0.0}"
export PREVIEW_MODE="${PREVIEW_MODE:-true}"

# اگر رمزها در env نباشند، همان رمزهای استاندارد تحویل (فقط برای bootstrap اول)
export ADMIN_PASSWORD="${ADMIN_PASSWORD:-Admin1289@@@}"
export CUSTODIAN_PASSWORD="${CUSTODIAN_PASSWORD:-Jamdar1289@@@}"

echo ">> شروع AminAmval روی ${HOST}:${PORT} (حالت همیشه‌بالا با راه‌اندازی مجدد خودکار)…"

restart_delay=2
while true; do
  node server.js
  code=$?
  echo ">> پروسهٔ سرور متوقف شد (کد $code) — راه‌اندازی مجدد خودکار تا ${restart_delay} ثانیهٔ دیگر…"
  sleep "$restart_delay"
  [ "$restart_delay" -lt 30 ] && restart_delay=$((restart_delay + 2))
done
