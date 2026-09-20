using AminAmval.Data;
using AminAmval.Models;
using AminAmval.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Endpoints;
public static class UserEndpoints
{
    public static void MapUsersAndReferences(this WebApplication app)
    {
        app.MapGet("/api/lookups", async (AppDb db, HttpContext c) => Results.Ok(new {
            categories = await db.Categories.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Description, x.Version, count = db.Assets.Count(a => a.CategoryId == x.Id && !a.Deleted) }).ToListAsync(),
            departments = await db.Departments.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Version, count = db.Users.Count(u => u.DepartmentId == x.Id && u.Active), assetCount = db.Assignments.Count(a => a.DepartmentId == x.Id && a.EndedAt == null) }).ToListAsync(),
            users = await db.Users.AsNoTracking().Where(x => x.Active).OrderBy(x => x.LastName).Select(x => new { x.Id, name = x.FirstName + " " + x.LastName, x.PersonnelCode, x.DepartmentId, x.Role }).ToListAsync(),
            owners = await db.Assets.Where(x => !x.Deleted).Select(x => x.Owner).Distinct().OrderBy(x => x).ToListAsync()
        })).RequireAuthorization("Staff");
        app.MapGet("/api/users", async (AppDb db, HttpContext c) => {
            var q = QueryUsers(db, c.Request.Query); var (page, size) = Core.Paging(c.Request.Query);
            var total = await q.CountAsync(); var users = await q.Skip((page - 1) * size).Take(size).ToListAsync();
            var ids = users.Select(x => x.Id).ToList(); var counts = await db.Assignments.Where(x => x.EndedAt == null && x.UserId != null && ids.Contains(x.UserId)).GroupBy(x => x.UserId!).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
            db.Log(c, "view.list", "User", null, "مشاهدهٔ فهرست کاربران", after: new { total, filters = Core.FilterSnapshot(c.Request.Query) }); await db.SaveChangesAsync();
            return Results.Ok(new { items = users.Select(u => new { user = Core.PublicUser(u), assetCount = counts.GetValueOrDefault(u.Id) }), total, page, pageSize = size });
        }).RequireAuthorization("Staff");
        app.MapGet("/api/users/{id}", async (string id, AppDb db, HttpContext c) => {
            var u = await db.Users.Include(x => x.Department).FirstOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "کاربر پیدا نشد.");
            var assets = await db.Assets.AsNoTracking().Where(x => !x.Deleted && x.Assignments.Any(a => a.UserId == id && a.EndedAt == null)).Include(x => x.Category).Include(x => x.Assignments.Where(a => a.EndedAt == null)).ToListAsync();
            db.Log(c, "view.detail", "User", id, "مشاهدهٔ مشخصات و اموال تحویلی «" + u.FirstName + " " + u.LastName + "»"); await db.SaveChangesAsync();
            return Results.Ok(new { user = Core.PublicUser(u), assets = assets.Select(a => Core.AssetView(a)) });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/users", async (UserInput input, AppDb db, HttpContext c) => {
            var u = new AppUser(); await ApplyUser(db, u, input, c.IsAdmin(), true);
            var password = string.IsNullOrEmpty(input.Password) ? u.NationalId! : input.Password;
            if (!string.IsNullOrEmpty(input.Password)) Core.StrongPassword(password);
            u.PasswordHash = new PasswordHasher<AppUser>().HashPassword(u, password); db.Users.Add(u);
            db.Log(c, "create", "User", u.Id, "ایجاد کاربر «" + u.FirstName + " " + u.LastName + "»؛ تغییر رمز در اولین ورود الزامی است.", after: Core.AuditUser(u)); await db.SaveChangesAsync();
            return Results.Created("/api/users/" + u.Id, new { u.Id, u.Username, message = "کاربر ثبت شد. گذرواژهٔ پیش‌فرض، کد ملی است؛ تغییر آن در اولین ورود الزامی است." });
        }).RequireAuthorization("Staff");
        app.MapPut("/api/users/{id}", async (string id, UserInput input, AppDb db, HttpContext c) => {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "کاربر پیدا نشد."); Core.CheckVersion(u.Version, input.Version);
            if (!c.IsAdmin() && u.Role != Roles.Employee) throw new ApiException(403, "جمعدار فقط مجاز به ویرایش حساب کارکنان عادی است.");
            if (!input.Active && u.Id == c.UserId()) throw new ApiException(400, "غیرفعال کردن حساب خود مجاز نیست.");
            if (u.Role == Roles.Admin && (!input.Active || input.Role != Roles.Admin) && await db.Users.CountAsync(x => x.Role == Roles.Admin && x.Active) <= 1) throw new ApiException(409, "سامانه باید حداقل یک مدیر فعال داشته باشد.");
            if (!input.Active && await db.Assignments.AnyAsync(x => x.UserId == id && x.EndedAt == null)) throw new ApiException(409, "پیش از غیرفعال‌سازی، اموال تحویلی کاربر را عودت یا انتقال دهید.");
            var before = Core.AuditUser(u);
            await ApplyUser(db, u, input, c.IsAdmin(), false); u.Version++;
            u.SecurityStamp = Guid.NewGuid().ToString("N"); await SessionService.RevokeAll(db, u.Id);
            db.Log(c, "update", "User", id, "ویرایش مشخصات / دسترسی «" + u.FirstName + " " + u.LastName + "»", before, Core.AuditUser(u)); await db.SaveChangesAsync();
            return Results.Ok(new { message = "اطلاعات کاربر ذخیره شد.", u.Version });
        }).RequireAuthorization("Staff");
        app.MapPost("/api/users/{id}/reset-password", async (string id, ResetPasswordInput input, AppDb db, HttpContext c) => {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "کاربر پیدا نشد.");
            if (!c.IsAdmin() && u.Role != Roles.Employee) throw new ApiException(403, "تنظیم گذرواژهٔ حساب‌های مدیریتی فقط توسط مدیر مجاز است.");
            Core.StrongPassword(input.NewPassword);
            if (input.NewPassword == u.NationalId) throw new ApiException(400, "از کد ملی به‌عنوان گذرواژهٔ جدید استفاده نکنید.");
            u.PasswordHash = new PasswordHasher<AppUser>().HashPassword(u, input.NewPassword); u.SecurityStamp = Guid.NewGuid().ToString("N"); await SessionService.RevokeAll(db, u.Id); u.MustChangePassword = true; u.FailedLogins = 0; u.LockoutUntil = null; u.Version++;
            db.Log(c, "password.reset", "User", id, "بازنشانی گذرواژه و ابطال نشست‌ها؛ تغییر رمز در ورود بعدی الزامی است."); await db.SaveChangesAsync();
            return Results.Ok(new { message = "گذرواژه بازنشانی شد. رمز موقت را به‌صورت امن به کاربر تحویل دهید." });
        }).RequireAuthorization("Staff").RequireRateLimiting("password");
        app.MapPost("/api/references/{kind}", async (string kind, ReferenceInput input, AppDb db, HttpContext c) => {
            var name = Core.Required(input.Name, "عنوان", 100); string id;
            if (kind == "categories") { var item = new Category { Name = name, Description = Core.Clean(input.Description, 1000) }; db.Categories.Add(item); id = item.Id; }
            else if (kind == "departments") { var item = new Department { Name = name }; db.Departments.Add(item); id = item.Id; }
            else throw new ApiException(404, "نوع اطلاعات پایه معتبر نیست.");
            db.Log(c, "create", kind == "categories" ? "Category" : "Department", id, "ایجاد «" + name + "»", after: new { name, input.Description }); await db.SaveChangesAsync();
            return Results.Ok(new { id, message = "اطلاعات پایه ثبت شد." });
        }).RequireAuthorization("Staff");
        app.MapPut("/api/references/{kind}/{id}", async (string kind, string id, ReferenceInput input, AppDb db, HttpContext c) => {
            var name = Core.Required(input.Name, "عنوان", 100); object before;
            if (kind == "categories") { var x = await db.Categories.FindAsync(id) ?? throw new ApiException(404, "دسته‌بندی پیدا نشد."); Core.CheckVersion(x.Version, input.Version); before = new { x.Name, x.Description }; x.Name = name; x.Description = Core.Clean(input.Description, 1000); x.Version++; }
            else if (kind == "departments") { var x = await db.Departments.FindAsync(id) ?? throw new ApiException(404, "واحد سازمانی پیدا نشد."); Core.CheckVersion(x.Version, input.Version); before = new { x.Name }; x.Name = name; x.Version++; }
            else throw new ApiException(404, "نوع اطلاعات پایه معتبر نیست.");
            db.Log(c, "update", kind == "categories" ? "Category" : "Department", id, "ویرایش «" + name + "»", before, new { name, input.Description }); await db.SaveChangesAsync();
            return Results.Ok(new { message = "تغییرات ذخیره شد." });
        }).RequireAuthorization("Staff");
        app.MapDelete("/api/references/{kind}/{id}", async (string kind, string id, AppDb db, HttpContext c) => {
            var input = await c.Request.ReadFromJsonAsync<DeleteInput>() ?? throw new ApiException(400, "علت حذف الزامی است."); var reason = Core.Required(input.Reason, "علت حذف", 1000); string name;
            if (kind == "categories") { var x = await db.Categories.FindAsync(id) ?? throw new ApiException(404, "دسته‌بندی پیدا نشد."); Core.CheckVersion(x.Version, input.Version); if (await db.Assets.AnyAsync(a => a.CategoryId == id)) throw new ApiException(409, "دسته‌بندی دارای اموال است و قابل حذف نیست."); name = x.Name; db.Categories.Remove(x); }
            else if (kind == "departments") { var x = await db.Departments.FindAsync(id) ?? throw new ApiException(404, "واحد پیدا نشد."); Core.CheckVersion(x.Version, input.Version); if (await db.Users.AnyAsync(u => u.DepartmentId == id) || await db.Assignments.AnyAsync(a => a.DepartmentId == id)) throw new ApiException(409, "واحد دارای کاربر یا سابقهٔ تخصیص است و قابل حذف نیست."); name = x.Name; db.Departments.Remove(x); }
            else throw new ApiException(404, "نوع اطلاعات پایه معتبر نیست.");
            db.Log(c, "delete", kind == "categories" ? "Category" : "Department", id, "حذف «" + name + "»؛ " + reason, before: new { name }); await db.SaveChangesAsync();
            return Results.Ok(new { message = "اطلاعات پایه حذف شد." });
        }).RequireAuthorization("Admin");
        app.MapPost("/api/audit/cancel", async (CancelInput input, AppDb db, HttpContext c) => {
            if (!new[] { "asset.create", "asset.edit", "asset.delete", "assign", "transfer", "return", "status", "user.create", "user.edit", "user.reset", "reference", "import", "backup", "asset.restore", "disposition", "password" }.Contains(input.Operation)) throw new ApiException(400, "عملیات معتبر نیست.");
            if (!c.IsStaff() && input.Operation != "password") throw new ApiException(403, "فقط انصراف از تغییر رمز خودتان مجاز است.");
            string? id = input.Operation == "password" ? c.UserId() : input.EntityId == null ? null : Core.Clean(input.EntityId, 32);
            db.Log(c, "cancel", (input.Operation.StartsWith("user") || input.Operation == "password") ? "User" : input.Operation is "reference" or "import" or "backup" ? "System" : "Asset", id, "انصراف کاربر از عملیات «" + input.Operation + "»"); await db.SaveChangesAsync(); return Results.Ok(new { message = "انصراف ثبت شد." });
        }).RequireAuthorization().RequireRateLimiting("audit");
    }
    public static IQueryable<AppUser> QueryUsers(AppDb db, IQueryCollection query)
    {
        var q = db.Users.AsNoTracking().Include(x => x.Department).AsQueryable(); string search = Core.Digits(query["q"].ToString()).ToLowerInvariant(), dept = query["departmentId"].ToString(), role = query["role"].ToString(), active = query["active"].ToString();
        if (search != "") q = q.Where(x => (x.FirstName + " " + x.LastName).Contains(search) || x.PersonnelCode.ToLower().Contains(search) || x.Username.Contains(search) || (x.NationalId != null && x.NationalId.Contains(search)));
        if (dept != "") q = q.Where(x => x.DepartmentId == dept); if (role != "") q = q.Where(x => x.Role == role); if (active is "true" or "false") q = q.Where(x => x.Active == (active == "true"));
        return q.OrderBy(x => x.LastName).ThenBy(x => x.FirstName);
    }
    public static async Task ApplyUser(AppDb db, AppUser u, UserInput i, bool admin, bool create)
    {
        if (!Roles.All.Contains(i.Role) || (!admin && i.Role != Roles.Employee) || (admin && create && i.Role != Roles.Employee)) throw new ApiException(403, "مدیر سامانه و جمعدار اموال فقط از مسیر راه‌اندازی اولیه قابل ایجاد هستند.");
        u.PersonnelCode = Core.Required(Core.Digits(i.PersonnelCode).ToUpperInvariant(), "کد پرسنلی", 60); u.Username = Core.Username(string.IsNullOrWhiteSpace(i.Username) ? u.PersonnelCode : i.Username);
        if (await db.Users.AnyAsync(x => x.Id != u.Id && (x.Username == u.Username || x.PersonnelCode == u.PersonnelCode))) throw new ApiException(409, "کد پرسنلی یا نام کاربری تکراری است.");
        u.FirstName = Core.Required(i.FirstName, "نام", 100); u.LastName = Core.Required(i.LastName, "نام خانوادگی", 100);
        var national = Core.Digits(i.NationalId);
        // Only the two original bootstrap accounts may remain without personnel PII.
        var bootstrap = !create && u.NationalId == null && (u.Username is "admin" or "jamdar") && u.PersonnelCode.StartsWith("SYS-");
        if (!(bootstrap && national == "")) {
            if (!Core.ValidNationalId(national)) throw new ApiException(400, "کد ملی ۱۰ رقمی معتبر وارد کنید.");
            if (await db.Users.AnyAsync(x => x.Id != u.Id && x.NationalId == national)) throw new ApiException(409, "کد ملی تکراری است.");
            u.NationalId = national;
        }
        if (string.IsNullOrEmpty(i.DepartmentId) && bootstrap) u.DepartmentId = null;
        else { if (!await db.Departments.AnyAsync(x => x.Id == i.DepartmentId) && !db.Departments.Local.Any(x => x.Id == i.DepartmentId)) throw new ApiException(400, "واحد سازمانی معتبر را انتخاب کنید."); u.DepartmentId = i.DepartmentId; }
        u.Role = i.Role; u.Active = i.Active;
    }
}
