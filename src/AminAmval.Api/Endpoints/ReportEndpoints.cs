using System.Text.RegularExpressions;
using AminAmval.Data;
using AminAmval.Models;
using AminAmval.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace AminAmval.Endpoints;
public static class ReportEndpoints
{
    public static void MapReportsAndSystem(this WebApplication app)
    {
        app.MapGet("/api/dashboard", async (AppDb db, HttpContext c, BackupService backups) => {
            var assets = db.Assets.Where(x => !x.Deleted);
            if (!c.IsStaff()) assets = assets.Where(x => x.Assignments.Any(a => a.UserId == c.UserId() && a.EndedAt == null));
            var counts = await assets.GroupBy(x => x.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync();
            var categories = await assets.GroupBy(x => new { x.CategoryId, x.Category.Name }).Select(g => new { id = g.Key.CategoryId, name = g.Key.Name, count = g.Count() }).OrderByDescending(x => x.count).ToListAsync();
            var total = await assets.CountAsync(); var unlabeled = await assets.CountAsync(x => !x.HasLabel);
            var cost = c.IsStaff() ? await assets.SumAsync(x => (double)(x.PurchaseCost ?? 0)) : 0;
            var recentQuery = db.Assignments.AsNoTracking().Where(x => x.EndedAt == null && !x.Asset.Deleted);
            if (!c.IsStaff()) recentQuery = recentQuery.Where(x => x.UserId == c.UserId());
            var recent = await recentQuery.OrderByDescending(x => x.StartedAt).Take(5).Select(x => new { x.Id, x.AssetId, assetName = x.Asset.Name, assetCode = x.Asset.Code, imageId = x.Asset.ImageId, recipient = x.RecipientName, department = x.DepartmentName, x.StartedAt }).ToListAsync();
            var activities = c.IsStaff() ? await db.Audit.AsNoTracking().Where(x => !x.Action.StartsWith("view") && (c.IsAdmin() || x.EntityType == "Asset")).OrderByDescending(x => x.At).Take(6).Select(x => new { x.Id, x.At, x.Action, x.Description, x.ActorName, x.AssetId, x.EntityType }).ToListAsync() : [];
            var setup = c.IsStaff() ? new { categories = await db.Categories.CountAsync(), departments = await db.Departments.CountAsync(), employees = await db.Users.CountAsync(x => x.Role == Roles.Employee), assets = total } : null;
            var users = c.IsStaff() ? await db.Users.CountAsync(x => x.Active) : 0;
            db.Log(c, "view.dashboard", "System", null, "مشاهدهٔ داشبورد اختصاصی نقش"); await db.SaveChangesAsync();
            return Results.Ok(new { pendingOperations = c.IsStaff() ? await db.DispositionRequests.CountAsync(x => x.State == "Pending") : 0, total, counts, categories, unlabeled, purchaseValue = cost, recent, activities, setup, activeUsers = users, backup = c.IsAdmin() ? backups.Status() : null });
        }).RequireAuthorization();
        app.MapGet("/api/audit", async (AppDb db, HttpContext c) => {
            var q = AuditQuery(db, c.Request.Query); var (page, size) = Core.Paging(c.Request.Query); var total = await q.CountAsync();
            var items = await q.OrderByDescending(x => x.At).ThenByDescending(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync();
            db.Log(c, "view.audit", "System", null, "مشاهدهٔ گزارش ممیزی", after: new { filters = Core.FilterSnapshot(c.Request.Query), total }); await db.SaveChangesAsync();
            return Results.Ok(new { items, total, page, pageSize = size });
        }).RequireAuthorization("Admin");
        app.MapGet("/api/reports/{kind}/export", async (string kind, AppDb db, HttpContext c) => {
            byte[] bytes; int count;
            if (kind == "audit") {
                if (!c.IsAdmin()) throw new ApiException(403, "خروجی کل سوابق فقط برای مدیر مجاز است.");
                var q = AuditQuery(db, c.Request.Query); count = await q.CountAsync(); Limit(count); bytes = ExcelService.ExportAudit(await q.OrderByDescending(x => x.At).ToListAsync(), c.Request.QueryString.Value);
            } else if (kind == "users") {
                var q = UserEndpoints.QueryUsers(db, c.Request.Query); count = await q.CountAsync(); Limit(count); var users = await q.ToListAsync();
                bytes = ExcelService.Workbook("کارکنان", ["کد پرسنلی", "نام", "نام خانوادگی", "کد ملی", "واحد سازمانی", "نام کاربری", "نقش", "فعال", "زمان ثبت (UTC)"], users.Select(x => new object?[] { x.PersonnelCode, x.FirstName, x.LastName, x.NationalId, x.Department?.Name, x.Username, x.Role == Roles.Admin ? "مدیر سامانه" : x.Role == Roles.Custodian ? "جمعدار اموال" : "کاربر عادی", x.Active, x.CreatedAt }), c.Request.QueryString.Value);
            } else if (kind is "assets" or "current" or "unlabeled") {
                var dict = c.Request.Query.ToDictionary(x => x.Key, x => x.Value); if (kind != "assets") dict["report"] = new StringValues(kind);
                var q = AssetService.Query(db, new QueryCollection(dict), c); count = await q.CountAsync(); Limit(count); bytes = ExcelService.ExportAssets(await q.ToListAsync(), c.Request.QueryString.Value + "&report=" + kind);
            } else throw new ApiException(404, "گزارش پیدا نشد.");
            db.Log(c, "export", kind == "users" ? "User" : kind == "audit" ? "System" : "Asset", null, "دریافت خروجی Excel «" + kind + "»", after: new { filters = Core.FilterSnapshot(c.Request.Query), count }); await db.SaveChangesAsync();
            return Results.File(bytes, ExcelService.Mime, "amin-" + kind + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".xlsx");
        }).RequireAuthorization("Staff").RequireRateLimiting("export");
        app.MapGet("/api/assets/{id}/history/export", async (string id, AppDb db, HttpContext c) => {
            var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && (!x.Deleted || c.IsAdmin())) ?? throw new ApiException(404, "اموال پیدا نشد.");
            var q = db.Audit.AsNoTracking().Where(x => x.AssetId == id); var count = await q.CountAsync(); Limit(count);
            var bytes = ExcelService.ExportAudit(await q.OrderByDescending(x => x.At).ToListAsync(), "asset=" + asset.Code);
            db.Log(c, "export.history", "Asset", id, "خروجی کامل تاریخچهٔ اموال «" + asset.Code + "»", after: new { count }); await db.SaveChangesAsync();
            return Results.File(bytes, ExcelService.Mime, "amin-history-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".xlsx");
        }).RequireAuthorization("Staff").RequireRateLimiting("export");
        app.MapGet("/api/import/{kind}/template", async (string kind, AppDb db, HttpContext c) => {
            var bytes = ExcelService.Template(kind); db.Log(c, "template", "System", null, "دریافت قالب Excel «" + kind + "»"); await db.SaveChangesAsync();
            return Results.File(bytes, ExcelService.Mime, "amin-template-" + kind + ".xlsx");
        }).RequireAuthorization("Staff");
        app.MapPost("/api/import/{kind}", async (string kind, HttpContext c, ExcelService excel) => {
            if (!c.Request.HasFormContentType) throw new ApiException(400, "فایل Excel را انتخاب کنید."); var form = await c.Request.ReadFormAsync();
            var f = form.Files.GetFile("file") ?? throw new ApiException(400, "فایل انتخاب نشده است.");
            if (!f.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.Length > 20 * 1024 * 1024) throw new ApiException(400, "فقط فایل XLSX تا ۲۰ مگابایت پذیرفته می‌شود.");
            using var ms = new MemoryStream(); await f.CopyToAsync(ms); var result = await excel.Import(kind, ms.ToArray(), c.Request.Query["commit"] == "true", c); return Results.Ok(result);
        }).RequireAuthorization("Staff").RequireRateLimiting("bulk");
        app.MapGet("/api/system", async (AppDb db, Storage s, BackupService backups, HttpContext c, IConfiguration cfg) => {
            db.Log(c, "view.settings", "System", null, "مشاهدهٔ تنظیمات و وضعیت سامانه"); await db.SaveChangesAsync();
            long databaseBytes = 0;
            var connStr = cfg.GetConnectionString("DefaultConnection") ?? "";
            if (!string.IsNullOrEmpty(connStr)) {
                try {
                    var dbName = new SqlConnectionStringBuilder(connStr).InitialCatalog;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT SUM(size) * 8192 FROM sys.database_files WHERE type IN (0,1) AND name = @dbName";
                    cmd.Parameters.AddWithValue("@dbName", dbName);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value) databaseBytes = Convert.ToInt64(result);
                } catch { }
            }
            return Results.Ok(new { version = "1.1.0", runtime = ".NET 8 / ASP.NET Core", database = "SQL Server (LocalDB)", databaseBytes, uploadBytes = Directory.EnumerateFiles(s.Uploads).Sum(x => new FileInfo(x).Length), auditCount = await db.Audit.CountAsync(), assets = await db.Assets.CountAsync(x => !x.Deleted), users = await db.Users.CountAsync(), backup = backups.Status(), backups = backups.Files().Select(x => new { name = x.Name, size = x.Length, createdAt = x.LastWriteTimeUtc }) });
        }).RequireAuthorization("Admin");
        app.MapPost("/api/backups", async (BackupService backups, HttpContext c) => Results.Ok(new { name = await backups.Create(c), message = "نسخهٔ پشتیبان با موفقیت تهیه شد." })).RequireAuthorization("Admin").RequireRateLimiting("backup");
        app.MapGet("/api/backups/{name}", async (string name, AppDb db, Storage storage, HttpContext c) => {
            if (!Regex.IsMatch(name, "^amin-[0-9]{8}-[0-9]{6}-[a-f0-9]{6}\\.zip$")) throw new ApiException(404, "نسخهٔ پشتیبان پیدا نشد.");
            var path = Path.Combine(storage.Backups, name); if (!File.Exists(path)) throw new ApiException(404, "نسخهٔ پشتیبان پیدا نشد.");
            db.Log(c, "backup.download", "System", null, "دریافت نسخهٔ پشتیبان حساس", after: new { filename = name }); await db.SaveChangesAsync(); return Results.File(path, "application/zip", name);
        }).RequireAuthorization("Admin").RequireRateLimiting("backup");
    }
    private static void Limit(int count) { if (count > 10000) throw new ApiException(400, "هر خروجی حداکثر ۱۰٬۰۰۰ رکورد دارد. بازه یا فیلتر را محدودتر کنید؛ هیچ ردیفی بی‌اطلاع حذف نمی‌شود."); }
    public static IQueryable<AuditEvent> AuditQuery(AppDb db, IQueryCollection p)
    {
        var q = db.Audit.AsNoTracking().AsQueryable(); string term = Core.Clean(p["q"].ToString()), action = p["action"].ToString(), type = p["entityType"].ToString(), actor = p["actorId"].ToString(), scope = p["scope"].ToString();
        if (term != "") q = q.Where(x => x.Description.Contains(term) || x.ActorName.Contains(term) || x.Ip.Contains(term) || x.CorrelationId.Contains(term));
        if (action != "") q = q.Where(x => x.Action == action); if (type != "") q = q.Where(x => x.EntityType == type); if (actor != "") q = q.Where(x => x.ActorId == actor);
        var ids = p["entityIds"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray(); if (ids.Length > 200 || ids.Any(x => !Regex.IsMatch(x, "^[a-f0-9]{32}$"))) throw new ApiException(400, "شناسه‌های انتخاب‌شده معتبر نیستند؛ حداکثر ۲۰۰ مورد انتخاب کنید.");
        if (ids.Length > 0) q = scope switch { "assets" => q.Where(x => x.AssetId != null && ids.Contains(x.AssetId)), "users" => q.Where(x => (x.TargetUserId != null && ids.Contains(x.TargetUserId)) || (x.ActorId != null && ids.Contains(x.ActorId))), _ => q.Where(x => x.EntityId != null && ids.Contains(x.EntityId)) };
        if (!string.IsNullOrEmpty(p["from"])) { if (!DateTime.TryParse(p["from"], out var from)) throw new ApiException(400, "تاریخ شروع معتبر نیست."); q = q.Where(x => x.At >= from.Date.AddHours(-3.5)); }
        if (!string.IsNullOrEmpty(p["to"])) { if (!DateTime.TryParse(p["to"], out var to)) throw new ApiException(400, "تاریخ پایان معتبر نیست."); var end = to.Date.AddDays(1).AddHours(-3.5); q = q.Where(x => x.At < end); }
        if (DateTime.TryParse(p["from"], out var f) && DateTime.TryParse(p["to"], out var t) && f > t) throw new ApiException(400, "تاریخ شروع نباید بعد از تاریخ پایان باشد.");
        return q;
    }
}
