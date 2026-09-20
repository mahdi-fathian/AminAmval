# AminAmval v2 — معماری جدید + همه قابلیت‌ها

## ساختار

```
src/
├── AminAmval.Domain/          # دامنه غنی (DDD) — استاندارد NavaKit
├── AminAmval.Application/     # CQRS + Validation
├── AminAmval.Infrastructure/  # EF + Repositoryها (مسیر Clean)
└── AminAmval.Api/             # میزبان کامل سامانه
    ├── Program.cs             # همه Endpointها
    ├── Endpoints/             # Auth, Assets, Operations, Users, Reports, Print
    ├── Services/              # Excel, Backup, Core, AssetService, Session
    ├── Models/ + Data/        # مدل و DbContext عملیاتی
    ├── Middleware/            # Exception handling (Clean)
    └── wwwroot/               # UI کامل
```

## قابلیت‌های فعال در Api

- احراز هویت / Session / CSRF / تغییر رمز
- اموال: CRUD، تخصیص، انتقال، عودت، وضعیت
- Disposition (درخواست / تأیید / رد / لغو / بازگردانی)
- کاربران و نقش‌ها
- دسته‌بندی و واحد سازمانی
- گزارش‌ها و خروجی
- Excel
- Backup
- چاپ
- UI کامل (wwwroot)

## اجرا

```bash
cd src/AminAmval.Api
dotnet restore
dotnet run
```

لایه‌های Domain/Application/Infrastructure برای توسعه استاندارد NavaKit آماده هستند.
