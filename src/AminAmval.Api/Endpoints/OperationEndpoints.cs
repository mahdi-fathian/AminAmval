using AminAmval.Data;
using AminAmval.Models;
using AminAmval.Services;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Endpoints;
public static class OperationEndpoints
{
    public static void MapOperations(this WebApplication app)
    {
        app.MapGet("/api/operations", async (AppDb db, HttpContext c) => {
            var q = db.DispositionRequests.AsNoTracking().Include(x => x.Asset).AsQueryable();
            string state = c.Request.Query["state"].ToString(), text = Core.Clean(c.Request.Query["q"].ToString());
            if (state != "") { if (!new[] { "Pending", "Approved", "Rejected", "Cancelled" }.Contains(state)) throw new ApiException(400, "وضعیت درخواست معتبر نیست."); q = q.Where(x => x.State == state); }
            if (text != "") q = q.Where(x => x.Asset.Code.Contains(text) || x.Asset.Name.Contains(text) || x.Reference.Contains(text) || x.RequesterName.Contains(text));
            var (page, size) = Core.Paging(c.Request.Query); var total = await q.CountAsync(); var items = await q.OrderByDescending(x => x.RequestedAt).Skip((page - 1) * size).Take(size).ToListAsync();
            db.Log(c, "view.operations", "System", null, "مشاهدهٔ درخواست‌های اداری خروج اموال", after: new { state, total }); await db.SaveChangesAsync();
            return Results.Ok(new { items = items.Select(x => View(x, c)), total, page, pageSize = size });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/assets/{id}/disposition-requests", async (string id, StatusInput input, AppDb db, HttpContext c) => {
            await using var tx = await db.Database.BeginTransactionAsync();
            var a = await AssetService.Load(db, id); Core.CheckVersion(a.Version, input.Version); AssetService.CheckEditable(a);
            if (!States.Terminal.Contains(input.Status)) throw new ApiException(400, "درخواست مجوز فقط برای فروش، اسقاط یا خروج است.");
            var (date, reason, reference) = AssetService.ValidateTransition(a, input);
            a.Version++; a.UpdatedAt = DateTime.UtcNow;
            var r = new DispositionRequest { AssetId = id, TargetStatus = input.Status, OriginalStatus = a.Status, EffectiveDate = date, Reason = reason, Reference = reference, Amount = input.Status == States.Sold ? input.Amount : null, RequestedBy = c.UserId(), RequesterName = c.User.Identity?.Name ?? "", AssetVersion = a.Version };
            db.DispositionRequests.Add(r);
            db.Log(c, "disposition.request", "Asset", id, "درخواست مجوز «" + States.Fa(input.Status) + "» برای «" + a.Name + "»", after: new { requestId = r.Id, r.TargetStatus, r.Reason, r.Reference, r.Amount, r.EffectiveDate });
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Created("/api/operations", new { r.Id, r.Version, message = "درخواست ثبت شد؛ تا تصمیم مدیر، این مال برای تغییر یا تخصیص قفل است." });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/operations/{id}/{decision}", async (string id, string decision, DecisionInput input, AppDb db, HttpContext c) => {
            if (!new[] { "approve", "reject", "cancel" }.Contains(decision)) throw new ApiException(404, "عملیات پیدا نشد.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var r = await db.DispositionRequests.FirstOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "درخواست پیدا نشد.");
            if (decision != "cancel" && !c.IsAdmin()) throw new ApiException(403, "تأیید یا رد فقط توسط مدیر مجاز است.");
            if (decision == "cancel" && !c.IsAdmin() && r.RequestedBy != c.UserId()) throw new ApiException(403, "فقط درخواست خودتان را می‌توانید لغو کنید.");
            if (decision == "approve" && r.RequestedBy == c.UserId() && !c.IsAdmin()) throw new ApiException(403, "تأیید درخواست توسط ثبت‌کننده مجاز نیست؛ یک مدیر دیگر باید آن را بررسی کند.");
            Core.CheckVersion(r.Version, input.Version);
            if (r.State != "Pending") throw new ApiException(409, "این درخواست قبلاً تعیین تکلیف شده است.");
            var a = await AssetService.Load(db, r.AssetId); Core.CheckVersion(a.Version, r.AssetVersion);
            var note = Core.Required(input.Note, "توضیح تصمیم", 1000); var before = Core.AssetSnapshot(a); a.Version++; a.UpdatedAt = DateTime.UtcNow;
            if (decision == "approve") {
                var data = new StatusInput(r.TargetStatus, r.EffectiveDate, r.Reason, r.Reference, r.Amount, a.Version);
                var validated = AssetService.ValidateTransition(a, data);
                a.Status = r.TargetStatus; a.LastOperationDate = validated.Date;
                db.Log(c, "status", "Asset", a.Id, "اجرای مجوز تأییدشده: «" + States.Fa(a.Status) + "» برای «" + a.Name + "»", before, new { requestId = r.Id, asset = Core.AssetSnapshot(a), r.Reference, r.Amount, r.EffectiveDate, approvedBy = c.UserId() });
            }
            r.State = decision switch { "approve" => "Approved", "reject" => "Rejected", _ => "Cancelled" };
            r.DecidedAt = DateTime.UtcNow; r.DecidedBy = c.UserId(); r.DeciderName = c.User.Identity?.Name; r.DecisionNote = note; r.Version++;
            db.Log(c, "disposition." + decision, "Asset", a.Id, "تعیین تکلیف درخواست مجوز: " + (decision == "approve" ? "تأیید" : decision == "reject" ? "رد" : "لغو"), after: new { requestId = r.Id, r.State, r.DecisionNote, r.RequesterName, r.DeciderName, asset = Core.AssetSnapshot(a) });
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Ok(new { message = decision == "approve" ? "مجوز تأیید و وضعیت اموال در همان تراکنش تغییر کرد." : "درخواست بسته شد و قفل اموال برداشته شد." });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/assets/{id}/restore", async (string id, DeleteInput input, AppDb db, HttpContext c) => {
            var a = await AssetService.Load(db, id, true); Core.CheckVersion(a.Version, input.Version);
            if (!a.Deleted) throw new ApiException(409, "این مال بایگانی نشده است.");
            var reason = Core.Required(input.Reason, "علت بازگردانی", 1000); var before = Core.AssetSnapshot(a);
            a.Deleted = false; a.Version++; a.UpdatedAt = DateTime.UtcNow;
            db.Log(c, "restore", "Asset", id, "بازگردانی از بایگانی: " + reason, before, Core.AssetSnapshot(a)); await db.SaveChangesAsync();
            return Results.Ok(new { message = "مال با حفظ کد، وضعیت قبلی و تمام سوابق از بایگانی بازگردانده شد." });
        }).RequireAuthorization("Admin");
    }
    private static object View(DispositionRequest r, HttpContext c) => new { r.Id, r.AssetId, assetCode = r.Asset.Code, assetName = r.Asset.Name, r.TargetStatus, r.State, r.Reason, r.Reference, r.Amount, r.EffectiveDate, r.RequestedBy, r.RequesterName, r.RequestedAt, r.DeciderName, r.DecidedAt, r.DecisionNote, r.Version, canApprove = c.IsAdmin() && r.State == "Pending", canReject = c.IsAdmin() && r.State == "Pending", canCancel = r.State == "Pending" && (c.IsAdmin() || r.RequestedBy == c.UserId()) };
}
