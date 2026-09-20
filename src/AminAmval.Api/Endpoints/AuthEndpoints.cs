using AminAmval.Data;
using AminAmval.Models;
using AminAmval.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Endpoints;
public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (HttpContext c, IAntiforgery a) => Results.Ok(new { token = a.GetAndStoreTokens(c).RequestToken }));
        app.MapPost("/api/auth/login", async (LoginInput input, AppDb db, HttpContext c) => {
            var username = Core.Digits(input.Username).ToLowerInvariant();
            if (string.IsNullOrEmpty(input.Password) || input.Password.Length > 128) throw new ApiException(400, "نام کاربری و گذرواژه را وارد کنید.");
            var u = await db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Username == username);
            var hasher = new PasswordHasher<AppUser>();
            bool valid = false;
            if (u != null) valid = hasher.VerifyHashedPassword(u, u.PasswordHash, input.Password) != PasswordVerificationResult.Failed;
            else { var dummy = new AppUser(); hasher.HashPassword(dummy, input.Password); }
            if (u == null || !valid || !u.Active || u.LockoutUntil > DateTime.UtcNow)
            {
                if (u is { Active: true } && u.LockoutUntil <= DateTime.UtcNow) { u.LockoutUntil = null; u.FailedLogins = 0; }
                if (u is { Active: true } && u.LockoutUntil == null) { u.FailedLogins++; if (u.FailedLogins >= 5) u.LockoutUntil = DateTime.UtcNow.AddMinutes(15); u.Version++; }
                db.Log(c, "login.failed", "User", u?.Id, "ورود ناموفق یا حساب غیرفعال / موقتاً قفل‌شده: " + Core.Clean(username, 80));
                await db.SaveChangesAsync();
                throw new ApiException(401, "اطلاعات ورود نادرست است یا حساب غیرفعال / موقتاً قفل شده است. پس از ۵ تلاش ناموفق، ۱۵ دقیقه صبر کنید.");
            }
            u.FailedLogins = 0; u.LockoutUntil = null;
            var session = SessionService.Create(db, u, c);
            await c.SignInAsync("Cookies", Core.Principal(u, session.Id), new AuthenticationProperties { IsPersistent = false });
            c.User = Core.Principal(u, session.Id);
            db.Log(c, "login", "User", u.Id, "ورود موفق به سامانه");
            await db.SaveChangesAsync();
            return Results.Ok(Core.PublicUser(u));
        }).RequireRateLimiting("login");
        app.MapGet("/api/auth/sessions", async (AppDb db, HttpContext c) => {
            var sid = c.User.FindFirst("sid")?.Value; var stamp = c.User.FindFirst("stamp")?.Value; var now = DateTime.UtcNow;
            var items = await db.Sessions.AsNoTracking().Where(x => x.UserId == c.UserId() && x.RevokedAt == null && x.ExpiresAt > now && x.Stamp == stamp).OrderByDescending(x => x.LastSeenAt).Select(x => new { x.Id, x.CreatedAt, x.LastSeenAt, x.ExpiresAt, x.Ip, x.UserAgent, current = x.Id == sid }).ToListAsync();
            db.Log(c, "session.view", "User", c.UserId(), "مشاهدهٔ نشست‌های ورود خود"); await db.SaveChangesAsync();
            return Results.Ok(new { items });
        }).RequireAuthorization();
        app.MapPost("/api/auth/sessions/{id}/revoke", async (string id, AppDb db, HttpContext c) => {
            var session = await db.Sessions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == c.UserId()) ?? throw new ApiException(404, "نشست پیدا نشد.");
            if (id == c.User.FindFirst("sid")?.Value) throw new ApiException(400, "برای پایان نشست فعلی از خروج استفاده کنید.");
            session.RevokedAt = DateTime.UtcNow; db.Log(c, "session.revoke", "User", c.UserId(), "ابطال یکی از نشست‌های دیگر حساب", after: new { session.Ip, session.UserAgent }); await db.SaveChangesAsync();
            return Results.Ok(new { message = "نشست انتخاب‌شده باطل شد." });
        }).RequireAuthorization();
        app.MapGet("/api/auth/me", async (AppDb db, HttpContext c) => {
            var u = await db.Users.Include(x => x.Department).SingleAsync(x => x.Id == c.UserId());
            return Results.Ok(Core.PublicUser(u));
        }).RequireAuthorization();
        app.MapPost("/api/auth/logout", async (AppDb db, HttpContext c) => {
            var sid = c.User.FindFirst("sid")?.Value; var session = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sid && x.UserId == c.UserId()); if (session != null) session.RevokedAt = DateTime.UtcNow;
            db.Log(c, "logout", "User", c.UserId(), "خروج از سامانه"); await db.SaveChangesAsync();
            await c.SignOutAsync("Cookies"); return Results.Ok(new { message = "با موفقیت خارج شدید." });
        }).RequireAuthorization();
        app.MapPost("/api/auth/password", async (PasswordInput input, AppDb db, HttpContext c) => {
            Core.StrongPassword(input.NewPassword);
            var u = await db.Users.Include(x => x.Department).SingleAsync(x => x.Id == c.UserId());
            var hasher = new PasswordHasher<AppUser>();
            if (string.IsNullOrEmpty(input.CurrentPassword) || input.CurrentPassword.Length > 128 || hasher.VerifyHashedPassword(u, u.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed) {
                db.Log(c, "password.failed", "User", u.Id, "تلاش ناموفق برای تغییر گذرواژه"); await db.SaveChangesAsync(); throw new ApiException(400, "گذرواژهٔ فعلی صحیح نیست.");
            }
            if (input.NewPassword == input.CurrentPassword || input.NewPassword == u.NationalId) throw new ApiException(400, "گذرواژهٔ جدید باید با گذرواژهٔ فعلی و کد ملی متفاوت باشد.");
            u.PasswordHash = hasher.HashPassword(u, input.NewPassword); u.MustChangePassword = false; u.SecurityStamp = Guid.NewGuid().ToString("N"); u.Version++;
            await SessionService.RevokeAll(db, u.Id); var session = SessionService.Create(db, u, c);
            db.Log(c, "password.change", "User", u.Id, "تغییر گذرواژه و ابطال نشست‌های قبلی"); await db.SaveChangesAsync();
            await c.SignInAsync("Cookies", Core.Principal(u, session.Id));
            return Results.Ok(Core.PublicUser(u));
        }).RequireAuthorization().RequireRateLimiting("password");
    }
}
