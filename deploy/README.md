# AminAmval — بستهٔ اجرا (امین اموال ناواکو)

این پوشه یک **میزبان مستقل Node.js** است که API کامل سامانهٔ امین اموال
(نسخهٔ ASP.NET Core موجود در `src/`) را اجرا می‌کند و همان رابط کاربری اصلی
(`src/AminAmval.Api/wwwroot`) را سرو می‌کند. از آنجا که SQL Server / LocalDB
بر روی لینوکس در دسترس نیست، لایهٔ داده با **SQLite** (ماژول داخلی
`node:sqlite` — بدون هیچ وابستگی باینری) پیاده‌سازی شده و رفتارِ دقیق
قرارداد API نسخهٔ C# (شامل احراز هویت، CSRF، قفل ورود، کنترل هم‌زمانی،
نقش‌ها و همهٔ endpointها) را بازتولید می‌کند.

## پیش‌نیاز

- Node.js **22.5+** (برای `node:sqlite`)
- `npm install` (سه وابستگی: `xlsx`، `qrcode`، `jszip`)

## اجرا

```bash
cd deploy
npm install
ADMIN_PASSWORD='Admin1289@@@' CUSTODIAN_PASSWORD='Jamdar1289@@@' node server.js
```

بعد از اجرا:

- UI در آدرس `http://localhost:8080` (متغیر `PORT` را برای تغییر تنظیم کنید).
- health-check در `GET /api/health`.

### متغیرهای محیطی

| متغیر | پیش‌فرض | توضیح |
|---|---|---|
| `PORT` | `8080` | پورت HTTP |
| `HOST` | `0.0.0.0` | بایند آدرس |
| `PREVIEW_MODE` | `true` | وقتی پشت پراکسی HTTPS هستید روی `true` بگذارید (کوکی Secure + SameSite=None) |
| `DATA_DIR` | `src/AminAmval.Api/runtime-data` | محل دیتابیس، آپلودها، کلیدها و پشتیبان‌ها |
| `ADMIN_PASSWORD` | (تصادفی قوی) | گذرواژهٔ اولیهٔ حساب مدیر (`admin`) |
| `CUSTODIAN_PASSWORD` | (تصادفی قوی) | گذرواژهٔ اولیهٔ حساب جمعدار (`jamdar`) |
| `BACKUP_HOUR_UTC` / `BACKUP_MINUTE_UTC` | `22` / `30` | زمان پشتیبان‌گیری خودکار روزانه |
| `BACKUP_RETENTION_DAYS` | `14` | نگهداری نسخه‌های پشتیبان |

## bootstrap دیتابیس

در اولین اجرا، دیتابیس **کاملاً خالی** ساخته می‌شود و **فقط دو حساب اجباری**
ایجاد می‌شوند (هیچ دادهٔ نمونه، دسته‌بندی، واحد سازمانی، کاربر یا اموال دیگری
وجود ندارد):

| نقش | نام کاربری | PersonnelCode |
|---|---|---|
| مدیر سامانه (Admin) | `admin` | `SYS-ADMIN` |
| جمعدار اموال (Custodian) | `jamdar` | `SYS-JAMDAR` |

گذرواژه‌ها از متغیرهای محیطی خوانده می‌شوند؛ اگر خالی باشند یک گذرواژهٔ
تصادفی قوی تولید می‌شود و به همراه لاگِ راه‌اندازی چاپ می‌گردد و در فایل
`runtime-data/initial-credentials.txt` (دسترسی ۶۰۰) ذخیره می‌شود — این فایل را
پس از تحویل امن باید حذف کنید.

**توجه:** هر دو حساب با پرچم «تغییر اجباری گذرواژه در اولین ورود» ساخته
می‌شوند؛ پس از ورود اول، سامانه کاربر را مجبور به تغییر گذرواژه می‌کند
(مطابق رفتار نسخهٔ اصلی).

## قرارداد API

همهٔ endpointهای نسخهٔ C# پیاده شده‌اند:

- `POST /api/auth/*` (login/logout/me/password/sessions, CSRF)
- `GET/POST/PUT/DELETE /api/assets*` (CRUD، تخصیص، انتقال، عودت، وضعیت، بایگانی، بازگردانی)
- `POST /api/assets/{id}/disposition-requests` و `POST /api/operations/{id}/{approve|reject|cancel}`
- `GET/POST/PUT/DELETE /api/users*`، `/api/references/{categories|departments}*`
- `/api/lookups`، `/api/dashboard`، `/api/audit`، `/api/reports/{kind}/export`
- `/api/import/{kind}/template` و `POST /api/import/{kind}?commit=(true|false)`
- `/api/assets/{id}/label` و `/api/assignments/{id}/receipt` (چاپ/PDF)
- `/api/system` و `POST /api/backups` و `GET /api/backups/{name}`

قرارداد پیمانهٔ احراز هویت/cookie برابر نسخهٔ اصلی است:
کوکی `Amin.Session` (HttpOnly + Secure + SameSite=None در حالت preview) و
کوکی `Amin.Csrf` + هدر `X-CSRF-TOKEN` برای درخواست‌های تغییردهنده.

## امنیت پیاده‌سازی‌شده

- هش گذرواژه با قطعه‌بندی و الگوریتم سازگار با ASP.NET Identity V3 (PBKDF2-HMAC-SHA256).
- ابطال نشست بر اساس SecurityStamp (بعد از تغییر رمز یا ویرایش کاربر، همهٔ نشست‌ها باطل می‌شوند).
- قفل موقت ۵ تلاش ناموفق / ۱۵ دقیقه.
- کنترل هم‌زمانی (Version) برای assets/users/references/disposition.
- Rate limiting (login، upload، export، password، backup، bulk، audit).
- هدرهای امنیتی CSP / nosniff / Referrer-Policy / Permissions-Policy.
- Audit کامل (before/after، IP، correlationId) و ماسک کد ملی در لاگ.

## پشتیبان‌گیری

پشتیبان‌گیری روزانه (و دستی از صفحهٔ تنظیمات) یک فایل ZIP شامل یک snapshot
سازگار از SQLite (از طریق `VACUUM INTO`)، فایل‌های آپلود و کلیدهای نشست، به
همراه `manifest.json` (هش SHA-256 هر فایل) می‌سازد. بازیابی آفلاین است: توقف
سرویس، جایگذاری `amin.db` و پوشه‌های `uploads`/`keys`.

## تست

```bash
# سرور را اجرا کنید، سپس:
python3 smoke-test.py     # ۳۵ سناریوی انتها-به-انتها (مدیر، جمعدار، اموال، Excel، بکاپ، ...)
```

## تفاوت‌ها با نسخهٔ C# (مستند)

- پایگاه داده: SQLite به جای SQL Server (بدون تغییر در قرارداد API).
- تصویر شناور داخل فایل XLSX در حالت وارد کردن اموال استخراج نمی‌شود (قالب
  Excel پیچیده است)؛ به جای آن «شناسهٔ تصویر از قبل بارگذاری‌شده» که در راهنمای
  قالب هم مجاز است استفاده می‌شود.
- خروجی Excel با SheetJS (بدون ClosedXML) تولید می‌شود؛ محتوا و ترتیب ستون‌ها
  یکسان است.
