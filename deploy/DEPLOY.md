# DEPLOY — استقرار دائمی و همیشه‌بالای «امین اموال ناواکو»

این سند راه‌های استقرار **دائمی** مطمئن را می‌دهد که به هیچ درگاه آزمایشی
(مانند پیش‌نمایش آنلاین) وابسته نباشند. با هرکدام، سامانه روی زیرساختِ واقعیِ
خود سازمان (سرور داخلی، VPS، یا یک میزبان کانتینری) بالا می‌آید و برای
همیشه — حتی پس از ریبوت هاست یا کرش فرایند — زنده می‌ماند.

برنامه یک پروسهٔ خودکفاست: Node.js + SQLite + رابط کاربری در یکجا.
وضعیت تولید را از این آدرس‌ها بشناسید:

- رابط کاربری: `http://SERVER:PORT/`
- سلامت: `GET /api/health` → `{"status":"ok","version":"1.1.0",...}`

> 🔐 **قبل از هر استقرار تولیدی، رمزهای اولیه را عوض کنید** و بعد از تحویل،
> فایل `initial-credentials.txt` را حذف کنید.

---

## گزینهٔ ۱ — Docker (توصیه‌شده، ساده‌ترین راهِ «همیشه بالا»)

پیش‌نیاز: Docker با پشتیبانی Docker Compose.

```bash
# 1) بیلد و اجرا (از ریشهٔ ریپو، جایی که deploy/ و src/ هستند)
docker compose -f deploy/docker-compose.yml up -d --build

# 2) بررسی
docker compose -f deploy/docker-compose.yml ps
curl http://localhost:8080/api/health
```

`restart: unless-stopped` باعث می‌شود کانتینر بعد از ریبوت هاست یا کرش به‌صورت
خودکار بالا بیاید. داده‌ها در volume مستقلِ `aminamval-data` ذخیره می‌شوند، پس
با بازسازی تصویر، اطلاعات از بین نمی‌رود.

پورت و رمزها را قبل از اجرا در `deploy/docker-compose.yml` تنظیم کنید
(یا از فایل `.env` برای کامپوز استفاده کنید).

---

## گزینهٔ ۲ — systemd روی سرور لینوکسی (بدون Docker)

پیش‌نیاز: سرور لینوکسی با Node.js **۲۲.۵+** و npm.

```bash
# 1) بسته را قرار دهید
sudo mkdir -p /opt/AminAmval
sudo cp -r /home/user/AminAmval/. /opt/AminAmval          # مسیر را متناسب کنید

# 2) وابستگی‌ها
cd /opt/AminAmval/deploy
sudo npm install --omit=dev

# 3) تنظیمات و رمزها
sudo cp env.example /etc/aminamval.env
sudo chmod 600 /etc/aminamval.env
sudo nano /etc/aminamval.env          # ← رمزها را عوض کنید

# 4) کاربر سرویس
sudo useradd --system --home /opt/AminAmval --shell /usr/sbin/nologin aminamval
sudo chown -R aminamval:aminamval /opt/AminAmval

# 5) نصب و فعال‌سازی سرویس (همیشه بالا، حتی بعد از ریبوت)
sudo cp aminamval.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now aminamval

# 6) بررسی
sudo systemctl status aminamval
curl http://localhost:8080/api/health
```

`Restart=always` در `aminamval.service` تضمین می‌کند اگر فرایند هر لحظه بیفتد،
systemd بلافاصله آن را دوباره اجرا کند و `enable` یعنی بعد از هر ریبوت سرور،
سامانه خودکار بالا می‌آید.

---

## گزینهٔ ۳ — اسکریپت ساده با watchdog (توسعه/دمو)

```bash
cd deploy
chmod +x run.sh
PORT=8080 ./run.sh          # پورت دلخواه: PORT=8097 ./run.sh
```

`run.sh` سرور را اجرا می‌کند و اگر بیفتد، به‌صورت خودکار و بی‌نهایت دوباره
آن را بالا می‌آورد (تأخیر فزاینده تا سقف ۳۰ ثانیه).

---

## 🔓 انتشار روی پورت 443 با HTTPS

برای اینکه سامانه روی دامنهٔ واقعی و HTTPS بالا بیاید، سرور را پشت یک
پروکسی معکوس قرار دهید:

- **Nginx / Caddy / Traefik** در همان هاست، یا
- خود Docker Compose با افزودن سرویس Caddy.

نکتهٔ کلیدی: برنامه کوکی‌های **Secure + SameSite=None** می‌سازد، پس حتماً
با HTTPS سرو شود. متغیر `PREVIEW_MODE=true` همین رفتار را فعال می‌کند.

نمونهٔ سادهٔ Caddyfile:

```
amin.example.com {
    reverse_proxy 127.0.0.1:8080
}
```

پس از راه‌اندازی، لینک نهایی شما روی دامنهٔ خودتان (مثلاً
`https://amin.example.com`) است — بدون هیچ درگاه واسطهٔ آزمایشی.

---

## 🔐 حساب‌های اجباری (bootstrap)

| نقش | نام کاربری | گذرواژهٔ اولیه (پیش‌فرض نمونه — حتماً عوض کنید) |
|---|---|---|
| مدیر سامانه (Admin) | `admin` | `Admin1289@@@` |
| جمعدار اموال (Custodian) | `jamdar` | `Jamdar1289@@@` |

هر دو با پرچم «تغییر اجباری گذرواژه در اولین ورود» ساخته می‌شوند. اگر در
رشتهٔ bootstrap رمزها مشخص نشوند، به‌صورت تصادفی قوی تولید و در
`initial-credentials.txt` ذخیره می‌شوند.

---

## 🧪 صحت استقرار (بعد از بالا آمدن)

```bash
curl -s http://localhost:8080/api/health               # {"status":"ok",...}
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8080/   # 200
```

و برای اطمینان انتها-به-انتها، روی یک دیتابیسِ جدا (تا دادهٔ تحویلی دست
نخورد) sue تست ۳۵ سناریویی اجرا شود:

```bash
cd deploy
PORT=8098 DATA_DIR=/tmp/amin-smoke \
  ADMIN_PASSWORD='Admin1289@@@' CUSTODIAN_PASSWORD='Jamdar1289@@@' node server.js &
# سپس در پنجرهٔ دیگر:
sed 's#http://localhost:8097#http://localhost:8098#' smoke-test.py > /tmp/t.py
python3 /tmp/t.py        # انتظار: 35 passed, 0 failed
```

---

## 📦 ساختار فایل‌های استقرار

| فایل | نقش |
|---|---|
| `Dockerfile` | تصویر تولیدی (Node 22 slim، غیر-root، healthcheck) |
| `docker-compose.yml` | اجرای همیشه‌بالا با volume ماندگار |
| `aminamval.service` | سرویس systemd (Restart=always + enable) |
| `env.example` | الگوی متغیرهای محیطی با رمزهای نمونه |
| `run.sh` | اجرای ساده با watchdog (بدون Docker) |
| `smoke-test.py` | ۳۵ آزمون انتها-به-انتها |
