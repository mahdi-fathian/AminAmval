using AminAmval.Data;
using AminAmval.Models;
using AminAmval.Services;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Endpoints;
public static class AssetEndpoints
{
    public static void MapAssets(this WebApplication app)
    {
        app.MapGet("/api/assets", async (AppDb db, HttpContext c) => {
            var q = AssetService.Query(db, c.Request.Query, c); var (page, size) = Core.Paging(c.Request.Query);
            var total = await q.CountAsync(); var data = await q.Skip((page - 1) * size).Take(size).ToListAsync();
            db.Log(c, "view.list", "Asset", null, "مشاهدهٔ فهرست اموال", after: new { filters = Core.FilterSnapshot(c.Request.Query), total }); await db.SaveChangesAsync();
            return Results.Ok(new { items = data.Select(a => Core.AssetView(a, c.IsStaff())), total, page, pageSize = size });
        }).RequireAuthorization();
        app.MapGet("/api/assets/{id}", async (string id, AppDb db, HttpContext c) => {
            var a = await AssetService.Load(db, id, c.IsAdmin());
            if (!c.IsStaff() && !a.Assignments.Any(x => x.UserId == c.UserId() && x.EndedAt == null)) throw new ApiException(404, "اموال مورد نظر پیدا نشد.");
            var history = c.IsStaff() ? await db.Audit.AsNoTracking().Where(x => x.AssetId == id).OrderByDescending(x => x.Id).Take(200).ToListAsync() : [];
            var historyTotal = c.IsStaff() ? await db.Audit.CountAsync(x => x.AssetId == id) : 0;
            db.Log(c, "view.detail", "Asset", id, "مشاهدهٔ شناسنامهٔ اموال «" + a.Name + "»"); await db.SaveChangesAsync();
            return Results.Ok(new { asset = Core.AssetView(a, c.IsStaff()), assignments = c.IsStaff() ? a.Assignments.OrderByDescending(x => x.StartedAt).Select(Core.AssignmentView) : [], history, historyTotal });
        }).RequireAuthorization();
        app.MapGet("/api/assets/{id}/history", async (string id, AppDb db, HttpContext c) => {
            if (!await db.Assets.AnyAsync(x => x.Id == id && (!x.Deleted || c.IsAdmin()))) throw new ApiException(404, "اموال پیدا نشد.");
            var (page, size) = Core.Paging(c.Request.Query); var q = db.Audit.AsNoTracking().Where(x => x.AssetId == id);
            if (long.TryParse(c.Request.Query["beforeId"], out var cursor)) q = q.Where(x => x.Id < cursor);
            return Results.Ok(new { items = await q.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size).ToListAsync(), total = await q.CountAsync(), page, pageSize = size });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/assets", async (AssetInput input, AppDb db, HttpContext c) => {
            var a = new Asset(); await AssetService.Apply(db, a, input); db.Assets.Add(a);
            db.Log(c, "create", "Asset", a.Id, "ثبت اموال «" + a.Name + "» با کد " + a.Code, after: Core.AssetSnapshot(a)); await db.SaveChangesAsync();
            return Results.Created("/api/assets/" + a.Id, new { a.Id, a.Code, a.Version, message = "اموال با موفقیت ثبت شد." });
        }).RequireAuthorization("Staff");
        app.MapPut("/api/assets/{id}", async (string id, AssetInput input, AppDb db, HttpContext c) => {
            var a = await AssetService.Load(db, id); Core.CheckVersion(a.Version, input.Version); AssetService.CheckEditable(a); var before = Core.AssetSnapshot(a);
            await AssetService.Apply(db, a, input); a.Version++;
            db.Log(c, "update", "Asset", a.Id, "ویرایش اموال «" + a.Name + "»", before, Core.AssetSnapshot(a)); await db.SaveChangesAsync();
            return Results.Ok(new { a.Id, a.Version, message = "تغییرات ذخیره شد." });
        }).RequireAuthorization("Staff");
        app.MapDelete("/api/assets/{id}", async (string id, HttpContext c, AppDb db) => {
            var input = await c.Request.ReadFromJsonAsync<DeleteInput>() ?? throw new ApiException(400, "علت حذف الزامی است.");
            var a = await AssetService.Load(db, id); Core.CheckVersion(a.Version, input.Version); AssetService.CheckEditable(a);
            if (a.Status == States.Assigned) throw new ApiException(409, "ابتدا اموال را عودت دهید؛ اموال تخصیص‌یافته قابل حذف نیست.");
            var before = Core.AssetSnapshot(a); a.Deleted = true; a.Version++; a.UpdatedAt = DateTime.UtcNow;
            db.Log(c, "delete", "Asset", id, "بایگانی اموال؛ علت: " + Core.Required(input.Reason, "علت حذف", 1000), before, Core.AssetSnapshot(a)); await db.SaveChangesAsync();
            return Results.Ok(new { message = "اموال بایگانی شد؛ سوابق برای حسابرسی محفوظ است." });
        }).RequireAuthorization("Admin");
        app.MapPost("/api/assets/{id}/assign", (string id, AssignmentInput input, AppDb db, HttpContext c) => Assign(id, input, db, c, false)).RequireAuthorization("Staff");
        app.MapPost("/api/assets/{id}/transfer", (string id, AssignmentInput input, AppDb db, HttpContext c) => Assign(id, input, db, c, true)).RequireAuthorization("Staff");
        app.MapPost("/api/assets/{id}/return", async (string id, ReturnInput input, AppDb db, HttpContext c) => {
            var a = await AssetService.Load(db, id); Core.CheckVersion(a.Version, input.Version); AssetService.CheckEditable(a);
            var current = a.Assignments.SingleOrDefault(x => x.EndedAt == null);
            if (a.Status != States.Assigned || current == null) throw new ApiException(409, "اموال تخصیص جاری ندارد.");
            var date = Core.ValidDate(input.Date, "تاریخ عودت"); Core.CheckChronology(a, date);
            if (date < current.StartedAt) throw new ApiException(400, "تاریخ عودت نمی‌تواند قبل از شروع تخصیص باشد.");
            var before = new { asset = Core.AssetSnapshot(a), assignment = Core.AssignmentView(current) };
            current.EndedAt = date; current.EndReason = Core.Required(input.Reason, "علت عودت", 1000); current.EndedBy = c.User.Identity?.Name ?? ""; current.EndReference = Core.Clean(input.Reference);
            a.Status = States.Available; a.LastOperationDate = date; a.Location = Core.Clean(input.Location); a.Version++; a.UpdatedAt = DateTime.UtcNow;
            db.Log(c, "return", "Asset", id, "عودت «" + a.Name + "» از " + (current.UserId == null ? current.DepartmentName : current.RecipientName), before, new { asset = Core.AssetSnapshot(a), assignment = Core.AssignmentView(current), reference = Core.Clean(input.Reference) }, targetUser: current.UserId);
            await db.SaveChangesAsync(); return Results.Ok(new { message = "عودت ثبت شد و اموال به انبار بازگشت." });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/assets/{id}/status", async (string id, StatusInput input, AppDb db, HttpContext c) => {
            var a = await AssetService.Load(db, id); Core.CheckVersion(a.Version, input.Version); AssetService.CheckEditable(a);
            var (date, reason, reference) = AssetService.ValidateTransition(a, input);
            if (States.Terminal.Contains(input.Status)) throw new ApiException(409, "برای فروش، اسقاط یا خروج ابتدا درخواست مجوز ثبت و تأیید مدیر دیگر را دریافت کنید.");
            var before = Core.AssetSnapshot(a); a.Status = input.Status; a.LastOperationDate = date; a.Version++; a.UpdatedAt = DateTime.UtcNow;
            db.Log(c, "status", "Asset", id, "تغییر وضعیت «" + a.Name + "» به «" + States.Fa(input.Status) + "»؛ " + reason, before, new { asset = Core.AssetSnapshot(a), effectiveDate = date, reason, reference, amount = input.Status == States.Sold ? input.Amount : null });
            await db.SaveChangesAsync(); return Results.Ok(new { message = "تغییر وضعیت با ثبت مجوز و سوابق انجام شد." });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/files", async (HttpContext c, AppDb db, Storage storage) => {
            if (!c.Request.HasFormContentType) throw new ApiException(400, "فایل تصویر را انتخاب کنید.");
            var f = (await c.Request.ReadFormAsync()).Files.GetFile("file") ?? throw new ApiException(400, "فایل تصویر انتخاب نشده است.");
            if (f.Length > 5 * 1024 * 1024) throw new ApiException(400, "حداکثر اندازهٔ تصویر ۵ مگابایت است.");
            using var ms = new MemoryStream(); await f.CopyToAsync(ms); var image = await AssetService.StoreImage(storage, db, ms.ToArray(), c.UserId());
            db.Log(c, "upload", "Image", image.Id, "بارگذاری تصویر اموال", after: new { image.ContentType, image.Size }); await db.SaveChangesAsync();
            return Results.Ok(new { image.Id, url = "/api/files/" + image.Id });
        }).RequireAuthorization("Staff").RequireRateLimiting("upload");
        app.MapGet("/api/files/{id}", async (string id, AppDb db, HttpContext c, Storage storage) => {
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-f0-9]{32}\\.(png|jpg|webp)$")) throw new ApiException(404, "تصویر پیدا نشد.");
            var image = await db.Images.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "تصویر پیدا نشد.");
            if (!c.IsStaff() && !await db.Assets.AnyAsync(x => !x.Deleted && x.ImageId == id && x.Assignments.Any(a => a.UserId == c.UserId() && a.EndedAt == null))) throw new ApiException(404, "تصویر پیدا نشد.");
            var path = Path.Combine(storage.Uploads, id); if (!File.Exists(path)) throw new ApiException(404, "فایل تصویر در دسترس نیست.");
            return Results.File(path, image.ContentType);
        }).RequireAuthorization();
    }
    private static async Task<IResult> Assign(string id, AssignmentInput input, AppDb db, HttpContext c, bool transfer)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await AssetService.Load(db, id); Core.CheckVersion(a.Version, input.Version); AssetService.CheckEditable(a);
        if ((!transfer && a.Status != States.Available) || (transfer && a.Status != States.Assigned)) throw new ApiException(409, transfer ? "فقط اموال در حال بهره‌برداری قابل انتقال است." : "فقط اموال موجود در انبار قابل تخصیص است.");
        var date = Core.ValidDate(input.StartedAt, "تاریخ شروع تخصیص"); Core.CheckChronology(a, date);
        AppUser? user = null;
        if (!string.IsNullOrEmpty(input.UserId)) {
            user = await db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == input.UserId && x.Active) ?? throw new ApiException(400, "شخص انتخاب‌شده معتبر یا فعال نیست.");
            if (user.Role != Roles.Employee) throw new ApiException(400, "تخصیص اموال فقط به کارکنان سازمان مجاز است.");
        }
        var deptId = user != null ? user.DepartmentId : input.DepartmentId;
        if (string.IsNullOrEmpty(deptId)) throw new ApiException(400, "واحد سازمانی تحویل‌گیرنده مشخص نیست.");
        var dept = await db.Departments.FindAsync(deptId) ?? throw new ApiException(400, "برای تخصیص به شخص، واحد سازمانی او باید مشخص باشد؛ برای تخصیص به واحد، واحد را انتخاب کنید.");
        var current = a.Assignments.SingleOrDefault(x => x.EndedAt == null);
        var before = new { asset = Core.AssetSnapshot(a), assignment = current == null ? null : Core.AssignmentView(current) };
        if (current != null) {
            if (date < current.StartedAt) throw new ApiException(400, "تاریخ انتقال قبل از تخصیص جاری است.");
            if (current.UserId == user?.Id && current.DepartmentId == dept.Id) throw new ApiException(400, "تحویل‌گیرندهٔ جدید با تخصیص جاری یکسان است.");
            current.EndedAt = date; current.EndReason = "انتقال"; current.EndedBy = c.User.Identity?.Name ?? ""; current.EndReference = Core.Clean(input.Reference);
            await db.SaveChangesAsync();
        }
        var assignment = new Assignment { AssetId = a.Id, AssetCodeAtIssue = a.Code, AssetNameAtIssue = a.Name, SerialAtIssue = a.Serial, QualityAtIssue = a.Quality, IssuerName = c.User.Identity?.Name ?? "", UserId = user?.Id, DepartmentId = dept.Id, StartedAt = date, Notes = Core.Clean(input.Notes, 1000), Reference = Core.Clean(input.Reference), CreatedBy = c.UserId(), RecipientName = user == null ? dept.Name : user.FirstName + " " + user.LastName, DepartmentName = dept.Name };
        db.Assignments.Add(assignment); a.Status = States.Assigned; a.Version++; a.UpdatedAt = DateTime.UtcNow; a.LastOperationDate = date;
        db.Log(c, transfer ? "transfer" : "assign", "Asset", id, (transfer ? "انتقال" : "تخصیص") + " «" + a.Name + "» به " + assignment.RecipientName, before, new { asset = Core.AssetSnapshot(a), assignment = Core.AssignmentView(assignment) }, targetUser: user?.Id);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { message = transfer ? "انتقال اموال با موفقیت ثبت شد." : "تخصیص اموال با موفقیت ثبت شد.", assignment.Id });
    }
}
