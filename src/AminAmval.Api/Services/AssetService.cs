using System.Text.RegularExpressions;
using AminAmval.Data;
using AminAmval.Models;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Services;
public static class AssetService
{
    public static IQueryable<Asset> Query(AppDb db, IQueryCollection q, HttpContext c)
    {
        var archived = q["archived"] == "true";
        if (archived && !c.IsAdmin()) throw new ApiException(403, "بایگانی فقط برای مدیر مجاز است.");
        var query = db.Assets.AsNoTracking().AsSplitQuery().Where(x => x.Deleted == archived).Include(x => x.Category).Include(x => x.DispositionRequests.Where(r => r.State == "Pending")).Include(x => x.Assignments.Where(a => a.EndedAt == null)).AsQueryable();
        if (!c.IsStaff()) query = query.Where(x => x.Assignments.Any(a => a.UserId == c.UserId() && a.EndedAt == null));
        var search = Core.Digits(q["q"].ToString()).ToLowerInvariant();
        if (search.Length > 0) query = query.Where(x => x.Code.ToLower().Contains(search) || x.OldCode.ToLower().Contains(search) || x.Name.ToLower().Contains(search) || x.Serial.ToLower().Contains(search) || x.Brand.ToLower().Contains(search) || x.Model.ToLower().Contains(search) || x.Owner.ToLower().Contains(search) || x.Assignments.Any(a => a.EndedAt == null && (a.RecipientName.ToLower().Contains(search) || a.DepartmentName.ToLower().Contains(search) || (a.User != null && (a.User.PersonnelCode.ToLower().Contains(search) || (a.User.FirstName + " " + a.User.LastName).ToLower().Contains(search)) || a.Department.Name.ToLower().Contains(search)))));
        string category = q["categoryId"].ToString(), status = q["status"].ToString(), user = q["userId"].ToString(), dept = q["departmentId"].ToString(), quality = q["quality"].ToString(), owner = Core.Clean(q["owner"].ToString());
        if (category != "") query = query.Where(x => x.CategoryId == category);
        if (status != "") { if (!States.All.Contains(status)) throw new ApiException(400, "وضعیت انتخابی معتبر نیست."); query = query.Where(x => x.Status == status); }
        if (quality != "") query = query.Where(x => x.Quality == quality);
        if (owner != "") query = query.Where(x => x.Owner.Contains(owner));
        if (user != "") query = query.Where(x => x.Assignments.Any(a => a.UserId == user && a.EndedAt == null));
        if (dept != "") query = query.Where(x => x.Assignments.Any(a => a.DepartmentId == dept && a.EndedAt == null));
        if (q["report"] == "current") query = query.Where(x => x.Status == States.Assigned);
        if (q["report"] == "unlabeled" || q["hasLabel"] == "false") query = query.Where(x => !x.HasLabel);
        else if (q["hasLabel"] == "true") query = query.Where(x => x.HasLabel);
        if (!string.IsNullOrEmpty(q["purchaseFrom"])) { if (!DateTime.TryParse(q["purchaseFrom"], out var from)) throw new ApiException(400, "تاریخ شروع فیلتر معتبر نیست."); query = query.Where(x => x.PurchaseDate >= from.Date); }
        if (!string.IsNullOrEmpty(q["purchaseTo"])) { if (!DateTime.TryParse(q["purchaseTo"], out var to)) throw new ApiException(400, "تاریخ پایان فیلتر معتبر نیست."); to = to.Date.AddDays(1); query = query.Where(x => x.PurchaseDate < to); }
        if (DateTime.TryParse(q["purchaseFrom"], out var f) && DateTime.TryParse(q["purchaseTo"], out var t) && f > t) throw new ApiException(400, "تاریخ شروع نباید بعد از تاریخ پایان باشد.");
        return q["sort"].ToString() switch { "code" => query.OrderBy(x => x.Code).ThenBy(x => x.Id), "name" => query.OrderBy(x => x.Name).ThenBy(x => x.Id), "oldest" => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id), _ => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) };
    }
    public static async Task Apply(AppDb db, Asset a, AssetInput i, bool importedImage = false)
    {
        var code = Core.Digits(i.Code).ToUpperInvariant();
        a.Code = code == "" ? "NV-" + DateTime.UtcNow.ToString("yyMMdd") + "-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant() : Core.Required(code, "کد اموال", 60);
        if (await db.Assets.AnyAsync(x => x.Code == a.Code && x.Id != a.Id)) throw new ApiException(409, "کد اموال تکراری است؛ کدهای بایگانی‌شده نیز قابل استفادهٔ مجدد نیستند.");
        a.OldCode = Core.Clean(Core.Digits(i.OldCode), 60); a.Name = Core.Required(i.Name, "نام اموال");
        if (!await db.Categories.AnyAsync(x => x.Id == i.CategoryId) && !db.Categories.Local.Any(x => x.Id == i.CategoryId)) throw new ApiException(400, "دسته‌بندی معتبر را انتخاب کنید.");
        a.CategoryId = i.CategoryId; a.Brand = Core.Clean(i.Brand, 100); a.Model = Core.Clean(i.Model, 100);
        if (a.Brand == "" && a.Model == "") throw new ApiException(400, "حداقل یکی از برند یا مدل الزامی است.");
        a.Serial = Core.Clean(Core.Digits(i.Serial), 100);
        if (!new[] { "New", "Good", "Used", "Damaged" }.Contains(i.Quality)) throw new ApiException(400, "کیفیت اموال معتبر نیست.");
        a.Quality = i.Quality; a.Owner = Core.Required(i.Owner, "مالک اموال", 150); a.Description = Core.Required(i.Description, "توضیحات", 2000);
        if (!importedImage && !await db.Images.AnyAsync(x => x.Id == i.ImageId)) throw new ApiException(400, "بارگذاری تصویر اموال الزامی است.");
        a.ImageId = Core.Required(i.ImageId, "تصویر", 100);
        a.PurchaseDate = i.PurchaseDate == null ? null : Core.ValidDate(i.PurchaseDate.Value, "تاریخ خرید").Date;
        if (a.PurchaseDate.HasValue && a.LastOperationDate.HasValue && a.PurchaseDate.Value > a.LastOperationDate.Value) throw new ApiException(400, "تاریخ خرید نمی‌تواند بعد از آخرین عملیات اموال باشد.");
        if (a.PurchaseDate.HasValue && a.Assignments.Any(x => x.StartedAt.Date < a.PurchaseDate.Value)) throw new ApiException(400, "تاریخ خرید نمی‌تواند بعد از اولین تخصیص باشد.");
        if (i.PurchaseCost is < 0 or > 1000000000000000L) throw new ApiException(400, "ارزش خرید باید بین صفر و ۱٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰ ریال باشد.");
        a.PurchaseCost = i.PurchaseCost; a.HasLabel = i.HasLabel; a.Location = Core.Clean(i.Location); a.UpdatedAt = DateTime.UtcNow;
    }
    public static void CheckEditable(Asset a)
    {
        if (a.Deleted) throw new ApiException(409, "ابتدا مال را از بایگانی بازگردانید.");
        if (a.DispositionRequests.Any(x => x.State == "Pending")) throw new ApiException(409, "این مال درخواست مجوز در انتظار دارد؛ ابتدا درخواست را تعیین تکلیف کنید.");
    }
    public static (DateTime Date, string Reason, string Reference) ValidateTransition(Asset a, StatusInput input)
    {
        if (a.Status == States.Assigned) throw new ApiException(409, "پیش از تغییر وضعیت، اموال را عودت دهید.");
        if (States.Terminal.Contains(a.Status)) throw new ApiException(409, "وضعیت خروج، اسقاط و فروش نهایی است.");
        if (!States.All.Contains(input.Status) || input.Status == States.Assigned || input.Status == a.Status || (input.Status == States.Available && a.Status is not (States.Maintenance or States.Lost))) throw new ApiException(400, "تغییر وضعیت مجاز نیست.");
        var date = Core.ValidDate(input.Date, "تاریخ عملیات"); Core.CheckChronology(a, date);
        var reason = Core.Required(input.Reason, "علت / شرح عملیات", 1000); var reference = Core.Required(input.Reference, "شمارهٔ مجوز یا صورت‌جلسه", 200);
        if (input.Status == States.Sold && (input.Amount is null or <= 0 or > 1000000000000000L)) throw new ApiException(400, "مبلغ مثبت فروش به ریال الزامی است.");
        return (date, reason, reference);
    }
    public static (string Extension, string ContentType) ImageType(byte[] b)
    {
        if (b.Length < 24 || b.Length > 5 * 1024 * 1024) throw new ApiException(400, "تصویر باید معتبر و حداکثر ۵ مگابایت باشد.");
        if (b.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) {
            var w = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16, 4)); var h = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20, 4));
            if (w < 1 || h < 1 || (long)w * h > 40000000) throw new ApiException(400, "ابعاد تصویر PNG بیش از حد مجاز است.");
            return (".png", "image/png");
        }
        if (b[0] == 0xff && b[1] == 0xd8 && b[2] == 0xff && b[^2] == 0xff && b[^1] == 0xd9) return (".jpg", "image/jpeg");
        if (System.Text.Encoding.ASCII.GetString(b, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(b, 8, 4) == "WEBP") return (".webp", "image/webp");
        throw new ApiException(400, "فقط تصویر واقعی PNG، JPEG یا WebP پذیرفته می‌شود؛ SVG و فایل اجرایی مجاز نیست.");
    }
    public static async Task<UploadedImage> StoreImage(Storage storage, AppDb db, byte[] bytes, string userId)
    {
        var (ext, contentType) = ImageType(bytes); var id = Guid.NewGuid().ToString("N") + ext;
        await File.WriteAllBytesAsync(Path.Combine(storage.Uploads, id), bytes);
        var image = new UploadedImage { Id = id, ContentType = contentType, OwnerUserId = userId, Size = bytes.Length }; db.Images.Add(image); return image;
    }
    public static async Task<Asset> Load(AppDb db, string id, bool includeArchived = false) => await db.Assets.AsSplitQuery().Include(x => x.DispositionRequests).Include(x => x.Category).Include(x => x.Assignments).ThenInclude(x => x.User).Include(x => x.Assignments).ThenInclude(x => x.Department).FirstOrDefaultAsync(x => x.Id == id && (includeArchived || !x.Deleted)) ?? throw new ApiException(404, "اموال مورد نظر پیدا نشد.");
}
