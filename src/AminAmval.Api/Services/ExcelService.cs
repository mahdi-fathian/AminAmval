using System.Globalization;
using System.IO.Compression;
using AminAmval.Data;
using AminAmval.Endpoints;
using AminAmval.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Services;
public record ImportError(int Row, string Message);
public class ImportRow
{
    public int Row { get; set; }
    public AssetInput? Asset { get; set; }
    public UserInput? User { get; set; }
    public string ReferenceName { get; set; } = "";
    public byte[]? Image { get; set; }
}
public class ExcelService(AppDb db, Storage storage)
{
    public const string Mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public static readonly string[] AssetHeaders = ["کد اموال", "کد قبلی", "نام اموال", "دسته‌بندی", "برند", "مدل", "سریال", "کیفیت", "مالک", "توضیحات", "تاریخ خرید (میلادی)", "ارزش خرید (ریال)", "برچسب دارد", "محل نگهداری", "تصویر"];
    public static readonly string[] UserHeaders = ["کد پرسنلی", "نام", "نام خانوادگی", "کد ملی", "واحد سازمانی", "نام کاربری"];
    private static readonly Dictionary<string, string> Qualities = new() { ["نو"] = "New", ["سالم"] = "Good", ["کارکرده"] = "Used", ["معیوب"] = "Damaged", ["New"] = "New", ["Good"] = "Good", ["Used"] = "Used", ["Damaged"] = "Damaged" };
    private static string SafeText(string s) => s.Length > 0 && "=+-@\t\r\n".Contains(s[0]) ? "'" + s : s;
    private static void Put(IXLCell cell, object? value)
    {
        if (value == null) { cell.Value = ""; return; }
        switch (value) {
            case int n: cell.Value = n; break;
            case long n: cell.Value = n; cell.Style.NumberFormat.Format = "#,##0"; break;
            case double n: cell.Value = n; cell.Style.NumberFormat.Format = "#,##0"; break;
            case bool b: cell.Value = b ? "بله" : "خیر"; break;
            case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss"; break;
            default: cell.SetValue(SafeText(value.ToString() ?? "")); break;
        }
    }
    public static byte[] Workbook(string sheetName, string[] headers, IEnumerable<object?[]> rows, string? filters = null)
    {
        using var book = new XLWorkbook(); var sheet = book.Worksheets.Add(sheetName); sheet.RightToLeft = true;
        sheet.Style.Font.FontName = "Tahoma"; sheet.Style.Font.FontSize = 10;
        for (int i = 0; i < headers.Length; i++) { Put(sheet.Cell(1, i + 1), headers[i]); sheet.Column(i + 1).Width = i == 0 ? 21 : 26; }
        var row = 2; foreach (var cells in rows) { for (int i = 0; i < cells.Length; i++) Put(sheet.Cell(row, i + 1), cells[i]); row++; }
        var header = sheet.Range(1, 1, 1, headers.Length); header.Style.Fill.BackgroundColor = XLColor.FromHtml("#3f63f5"); header.Style.Font.FontColor = XLColor.White; header.Style.Font.Bold = true; header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center; sheet.Row(1).Height = 30;
        sheet.SheetView.FreezeRows(1); sheet.Range(1, 1, Math.Max(1, row - 1), headers.Length).SetAutoFilter();
        if (row > 2) { sheet.Range(2, 1, row - 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center; for (int r = 2; r < row; r++) { sheet.Row(r).Height = 25; if (r % 2 == 0) sheet.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f6fc"); } }
        var meta = book.Worksheets.Add("اطلاعات گزارش"); meta.RightToLeft = true; meta.Column(1).Width = 28; meta.Column(2).Width = 95;
        Put(meta.Cell(1, 1), "سامانه"); Put(meta.Cell(1, 2), "امین اموال | ناواکو"); Put(meta.Cell(2, 1), "زمان تولید (UTC)"); Put(meta.Cell(2, 2), DateTime.UtcNow.ToString("O")); Put(meta.Cell(3, 1), "تعداد رکورد"); Put(meta.Cell(3, 2), row - 2); Put(meta.Cell(4, 1), "فیلترها"); Put(meta.Cell(4, 2), filters ?? "بدون فیلتر"); Put(meta.Cell(5, 1), "طبقه‌بندی"); Put(meta.Cell(5, 2), "محرمانه — ویژهٔ استفادهٔ سازمانی. مبالغ به ریال و تاریخ‌های فایل میلادی هستند.");
        using var ms = new MemoryStream(); book.SaveAs(ms); return ms.ToArray();
    }
    public static byte[] Template(string kind)
    {
        if (kind is not ("assets" or "users")) throw new ApiException(404, "نوع قالب معتبر نیست.");
        var headers = kind == "assets" ? AssetHeaders : UserHeaders; using var book = new XLWorkbook(); var sheet = book.Worksheets.Add(kind == "assets" ? "اموال" : "کارکنان"); sheet.RightToLeft = true; sheet.Style.Font.FontName = "Tahoma";
        for (int i = 0; i < headers.Length; i++) { sheet.Cell(1, i + 1).Value = headers[i]; sheet.Column(i + 1).Width = 25; sheet.Range(2, i + 1, 1001, i + 1).Style.NumberFormat.Format = "@"; }
        sheet.Row(1).Height = 32; sheet.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#3f63f5"); sheet.Range(1, 1, 1, headers.Length).Style.Font.FontColor = XLColor.White; sheet.SheetView.FreezeRows(1);
        var guide = book.Worksheets.Add("راهنما"); guide.RightToLeft = true; guide.Column(1).Width = 125; guide.Style.Font.FontName = "Tahoma";
        var notes = kind == "assets" ? new[] {
            "قالب ورود اموال — داده‌ها را از ردیف دوم وارد کنید. نام و ترتیب ستون‌ها را تغییر ندهید.",
            "الزامی: نام اموال، دسته‌بندی، حداقل یکی از برند/مدل، کیفیت، مالک، توضیحات و تصویر.",
            "کد اموال یکتا است؛ اگر خالی بماند کد خودکار تولید می‌شود. کد قبلی و سریال اختیاری هستند.",
            "کیفیت: نو، سالم، کارکرده یا معیوب. برچسب دارد: بله / خیر (خالی = بله).",
            "تصویر: با Insert > Pictures تصویر PNG یا JPEG را اضافه و گوشهٔ بالا-چپ آن را دقیقاً در ستون تصویر و ردیف همان مال قرار دهید.",
            "هر تصویر حداکثر ۵ مگابایت. از گزینهٔ Place over Cells استفاده کنید، نه فرمول IMAGE یا Place in Cell.",
            "به‌جای تصویر جاسازی‌شده می‌توانید شناسهٔ یک تصویرِ از قبل بارگذاری‌شده در همین سامانه را بنویسید؛ URL اینترنتی پذیرفته نمی‌شود.",
            "تاریخ خرید اختیاری، میلادی و با قالب yyyy-MM-dd یا سلول واقعی تاریخ؛ ارزش خرید عدد صحیح و به ریال است.",
            "دسته‌بندی جدید هنگام ثبت نهایی ساخته می‌شود؛ همهٔ اموال ابتدا موجود در انبار خواهند بود.",
            "حداکثر ۱۰۰۰ رکورد و ۲۰ مگابایت در هر فایل. فرمول، ماکرو و فایل XLS قدیمی پذیرفته نمی‌شود.",
            "ابتدا پیش‌نمایش را بررسی کنید. وجود حتی یک خطا مانع ثبت کل فایل می‌شود؛ هیچ ورود ناقصی انجام نمی‌شود."
        } : new[] {
            "قالب ورود کارکنان — داده‌ها را از ردیف دوم وارد کنید. نام و ترتیب ستون‌ها را تغییر ندهید.",
            "الزامی: کد پرسنلی، نام، نام خانوادگی، کد ملی و واحد سازمانی.",
            "کد ملی باید ۱۰ رقم معتبر و به صورت متن باشد؛ صفر ابتدایی را حذف نکنید.",
            "نام کاربری اختیاری؛ اگر خالی باشد، همان کد پرسنلی است. ۳ تا ۶۰ نویسهٔ لاتین/عدد و ._- مجاز است.",
            "تمام حساب‌های ورودی نقش کاربر عادی دارند؛ تغییر نقش فقط توسط مدیر و از صفحهٔ ویرایش مجاز است.",
            "گذرواژهٔ اولیه مطابق سند کد ملی است؛ کاربر در اولین ورود ملزم به تغییر آن است. این فایل حاوی اطلاعات حساس است.",
            "واحد سازمانی جدید هنگام ثبت نهایی ساخته می‌شود. کد پرسنلی، نام کاربری و کد ملی باید یکتا باشند.",
            "حداکثر ۱۰۰۰ رکورد و ۲۰ مگابایت. فرمول، ماکرو و XLS قدیمی مجاز نیست. ثبت فقط پس از پیش‌نمایشِ بدون خطا انجام می‌شود."
        };
        for (int r = 0; r < notes.Length; r++) { guide.Cell(r + 1, 1).Value = notes[r]; guide.Row(r + 1).Height = 36; guide.Cell(r + 1, 1).Style.Alignment.WrapText = true; }
        using var ms = new MemoryStream(); book.SaveAs(ms); return ms.ToArray();
    }
    public async Task<object> Import(string kind, byte[] bytes, bool commit, HttpContext c)
    {
        if (kind is not ("assets" or "users")) throw new ApiException(404, "نوع ورود معتبر نیست.");
        if (bytes.Length > 20 * 1024 * 1024) throw new ApiException(400, "فایل Excel نباید بیش از ۲۰ مگابایت باشد.");
        try {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (zip.Entries.Count > 5000 || zip.Entries.Sum(x => x.Length) > 80 * 1024 * 1024 || zip.Entries.Any(x => x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || x.FullName.Contains("externalLinks", StringComparison.OrdinalIgnoreCase))) throw new ApiException(400, "فایل بسیار بزرگ است یا ماکرو / پیوند خارجی دارد.");
        } catch (InvalidDataException) { throw new ApiException(400, "فایل XLSX معتبر نیست."); }
        using XLWorkbook book = Open(bytes);
        var sheet = book.Worksheets.FirstOrDefault() ?? throw new ApiException(400, "فایل فاقد کاربرگ است."); var headers = kind == "assets" ? AssetHeaders : UserHeaders;
        for (int i = 0; i < headers.Length; i++) if (Core.Clean(sheet.Cell(1, i + 1).GetString()) != headers[i]) throw new ApiException(400, $"عنوان ستون {i + 1} باید «{headers[i]}» باشد. قالب سامانه را دانلود کنید.");
        int last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 1;
        if (last > 1001 || (sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0) > headers.Length) throw new ApiException(400, "حداکثر ۱۰۰۰ ردیف مجاز است؛ ستون اضافی را حذف کنید.");
        var existingCodes = (await db.Assets.Select(x => x.Code).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var people = await db.Users.AsNoTracking().Select(x => new { x.Username, x.PersonnelCode, x.NationalId }).ToListAsync();
        var usernames = people.Select(x => x.Username).ToHashSet(StringComparer.OrdinalIgnoreCase); var personnel = people.Select(x => x.PersonnelCode).ToHashSet(StringComparer.OrdinalIgnoreCase); var nationals = people.Where(x => x.NationalId != null).Select(x => x.NationalId!).ToHashSet();
        var images = (await db.Images.Select(x => x.Id).ToListAsync()).ToHashSet();
        var rows = new List<ImportRow>(); var errors = new List<ImportError>(); int total = 0;
        for (int r = 2; r <= last; r++) {
            if (Enumerable.Range(1, headers.Length).All(i => sheet.Cell(r, i).IsEmpty())) continue;
            total++;
            try {
                string S(int col) { var cell = sheet.Cell(r, col); if (cell.HasFormula) throw new ApiException(400, "فرمول مجاز نیست؛ فقط مقدار ثابت وارد کنید."); return Core.Clean(cell.GetString(), 2000); }
                for (int col = 1; col <= headers.Length; col++) _ = S(col);
                var row = new ImportRow { Row = r };
                if (kind == "users") {
                    var u = new UserInput { PersonnelCode = Core.Required(Core.Digits(S(1)).ToUpperInvariant(), "کد پرسنلی", 60), FirstName = Core.Required(S(2), "نام", 100), LastName = Core.Required(S(3), "نام خانوادگی", 100), NationalId = Core.Digits(S(4)), Role = Roles.Employee };
                    row.ReferenceName = Core.Required(S(5), "واحد سازمانی", 100); u.Username = Core.Username(S(6) == "" ? u.PersonnelCode : S(6));
                    if (!Core.ValidNationalId(u.NationalId)) throw new ApiException(400, "کد ملی ۱۰ رقمی معتبر نیست؛ صفر ابتدایی باید حفظ شود.");
                    if (!usernames.Add(u.Username) || !personnel.Add(u.PersonnelCode) || !nationals.Add(u.NationalId)) throw new ApiException(400, "نام کاربری، کد پرسنلی یا کد ملی در فایل یا سامانه تکراری است.");
                    row.User = u;
                } else {
                    var a = new AssetInput { Code = Core.Clean(Core.Digits(S(1)).ToUpperInvariant(), 60), OldCode = Core.Clean(Core.Digits(S(2)), 60), Name = Core.Required(S(3), "نام اموال"), Brand = Core.Clean(S(5), 100), Model = Core.Clean(S(6), 100), Serial = Core.Clean(Core.Digits(S(7)), 100), Owner = Core.Required(S(9), "مالک", 150), Description = Core.Required(S(10), "توضیحات", 2000), Location = Core.Clean(S(14)) };
                    row.ReferenceName = Core.Required(S(4), "دسته‌بندی", 100);
                    if (a.Code != "" && !existingCodes.Add(a.Code)) throw new ApiException(400, "کد اموال در فایل یا سامانه تکراری است.");
                    if (a.Brand == "" && a.Model == "") throw new ApiException(400, "برند یا مدل الزامی است.");
                    if (!Qualities.TryGetValue(S(8), out var quality)) throw new ApiException(400, "کیفیت باید نو، سالم، کارکرده یا معیوب باشد."); a.Quality = quality;
                    if (S(11) != "") {
                        DateTime date;
                        if (sheet.Cell(r, 11).DataType == XLDataType.DateTime) date = sheet.Cell(r, 11).GetDateTime();
                        else if (!DateTime.TryParseExact(Core.Digits(S(11)), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) throw new ApiException(400, "تاریخ خرید باید میلادی و به صورت yyyy-MM-dd باشد.");
                        a.PurchaseDate = Core.ValidDate(date, "تاریخ خرید");
                    }
                    if (S(12) != "") { if (!long.TryParse(Core.Digits(S(12)).Replace(",", "").Replace("٬", ""), out var cost) || cost < 0 || cost > 1000000000000000L) throw new ApiException(400, "ارزش خرید باید عدد صحیح غیرمنفی به ریال باشد."); a.PurchaseCost = cost; }
                    a.HasLabel = S(13) switch { "بله" or "true" or "1" or "" => true, "خیر" or "false" or "0" => false, _ => throw new ApiException(400, "ستون برچسب باید بله یا خیر باشد.") };
                    var picture = sheet.Pictures.FirstOrDefault(p => p.TopLeftCell.Address.RowNumber == r && p.TopLeftCell.Address.ColumnNumber == 15);
                    if (picture != null) { picture.ImageStream.Position = 0; using var ms = new MemoryStream(); await picture.ImageStream.CopyToAsync(ms); row.Image = ms.ToArray(); AssetService.ImageType(row.Image); a.ImageId = "embedded"; }
                    else if (images.Contains(S(15))) a.ImageId = S(15);
                    else throw new ApiException(400, "تصویر الزامی است؛ یک تصویر شناور را در سلول ستون تصویرِ همین ردیف جاسازی کنید یا شناسهٔ تصویر موجود را وارد کنید.");
                    row.Asset = a;
                }
                rows.Add(row);
            } catch (ApiException e) { errors.Add(new ImportError(r, e.Message)); }
            catch (Exception) { errors.Add(new ImportError(r, "مقدار سلول یا تصویر قابل خواندن نیست؛ قالب و راهنمای فایل را بررسی کنید.")); }
        }
        if (total == 0) throw new ApiException(400, "فایل فاقد داده است؛ ردیف‌ها را پس از عنوان‌ها وارد کنید.");
        if (!commit || errors.Count > 0) {
            db.Log(c, "import.preview", kind == "assets" ? "Asset" : "User", null, "اعتبارسنجی فایل Excel بدون ثبت اطلاعات", after: new { total, valid = rows.Count, errors = errors.Count }); await db.SaveChangesAsync();
            return new { canCommit = errors.Count == 0, total, valid = rows.Count, errors, committed = false, preview = rows.Take(10).Select(x => new { x.Row, name = x.Asset?.Name ?? x.User!.FirstName + " " + x.User.LastName, code = x.Asset?.Code ?? x.User!.PersonnelCode, reference = x.ReferenceName }) };
        }
        await using var tx = await db.Database.BeginTransactionAsync(); var writtenImages = new List<string>();
        try {
            var categories = await db.Categories.ToDictionaryAsync(x => x.Name); var departments = await db.Departments.ToDictionaryAsync(x => x.Name);
            foreach (var row in rows) {
                if (row.Asset != null) {
                    if (!categories.TryGetValue(row.ReferenceName, out var cat)) { cat = new Category { Name = row.ReferenceName }; db.Categories.Add(cat); categories[cat.Name] = cat; db.Log(c, "create", "Category", cat.Id, "ایجاد دسته‌بندی از Excel", after: new { cat.Name }); }
                    row.Asset.CategoryId = cat.Id;
                    if (row.Image != null) { var image = await AssetService.StoreImage(storage, db, row.Image, c.UserId()); row.Asset.ImageId = image.Id; writtenImages.Add(image.Id); }
                    var asset = new Asset(); await AssetService.Apply(db, asset, row.Asset, row.Image != null); db.Assets.Add(asset); db.Log(c, "import.create", "Asset", asset.Id, "ورود اموال «" + asset.Name + "» از Excel، ردیف " + row.Row, after: Core.AssetSnapshot(asset));
                } else {
                    if (!departments.TryGetValue(row.ReferenceName, out var dept)) { dept = new Department { Name = row.ReferenceName }; db.Departments.Add(dept); departments[dept.Name] = dept; db.Log(c, "create", "Department", dept.Id, "ایجاد واحد سازمانی از Excel", after: new { dept.Name }); }
                    var i = row.User!; i.DepartmentId = dept.Id; var user = new AppUser(); await UserEndpoints.ApplyUser(db, user, i, c.IsAdmin(), true); user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, user.NationalId!); db.Users.Add(user); db.Log(c, "import.create", "User", user.Id, "ورود کاربر «" + user.FirstName + " " + user.LastName + "» از Excel، ردیف " + row.Row, after: Core.AuditUser(user));
                }
            }
            db.Log(c, "import.commit", kind == "assets" ? "Asset" : "User", null, "ثبت نهایی ورود گروهی Excel", after: new { count = rows.Count });
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return new { committed = true, total, valid = rows.Count, canCommit = true, errors = Array.Empty<ImportError>(), message = $"{rows.Count} رکورد با موفقیت و به‌صورت یکپارچه ثبت شد." };
        } catch { foreach (var id in writtenImages) { var path = Path.Combine(storage.Uploads, id); if (File.Exists(path)) File.Delete(path); } throw; }
    }
    private static XLWorkbook Open(byte[] bytes)
    {
        try { return new XLWorkbook(new MemoryStream(bytes)); } catch { throw new ApiException(400, "فایل Excel قابل خواندن نیست. فقط XLSX بدون رمز، ماکرو و خرابی را بارگذاری کنید."); }
    }
    public static byte[] ExportAssets(IEnumerable<Asset> assets, string? filters)
    {
        string Quality(string q) => q switch { "New" => "نو", "Good" => "سالم", "Used" => "کارکرده", "Damaged" => "معیوب", _ => q };
        return Workbook("گزارش اموال", ["کد اموال", "کد قبلی", "نام اموال", "دسته‌بندی", "برند", "مدل", "سریال", "کیفیت", "مالک", "وضعیت", "برچسب دارد", "تاریخ خرید", "ارزش خرید (ریال)", "تحویل‌گیرنده", "واحد سازمانی", "تاریخ تخصیص", "محل نگهداری", "توضیحات"], assets.Select(a => { var x = a.Assignments.FirstOrDefault(x => x.EndedAt == null); return new object?[] { a.Code, a.OldCode, a.Name, a.Category?.Name, a.Brand, a.Model, a.Serial, Quality(a.Quality), a.Owner, States.Fa(a.Status), a.HasLabel, a.PurchaseDate, a.PurchaseCost, x?.UserId == null ? (x == null ? null : "تخصیص به واحد") : x.RecipientName, x?.DepartmentName, x?.StartedAt, a.Location, a.Description }; }), filters);
    }
    public static byte[] ExportAudit(IEnumerable<AuditEvent> events, string? filters) => Workbook("سوابق عملیات", ["شناسه", "زمان دقیق (UTC)", "کاربر", "نقش", "IP", "عملیات", "نوع موجودیت", "شناسه موجودیت", "شرح", "قبل", "بعد", "شناسه پیگیری"], events.Select(x => new object?[] { x.Id, x.At.ToString("O"), x.ActorName, x.ActorRole, x.Ip, x.Action, x.EntityType, x.EntityId, x.Description, x.Before, x.After, x.CorrelationId }), filters);
}
