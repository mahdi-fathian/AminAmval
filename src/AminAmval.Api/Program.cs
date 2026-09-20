using System.Net;
using System.Threading.RateLimiting;
using AminAmval.Data;
using AminAmval.Endpoints;
using AminAmval.Models;
using AminAmval.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => { o.AddServerHeader = false; o.Limits.MaxRequestBodySize = 25 * 1024 * 1024; });
var storage = new Storage(builder.Configuration, builder.Environment);
var preview = builder.Configuration.GetValue<bool>("PREVIEW_MODE");
builder.Services.AddSingleton(storage);
builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(storage.Keys)).SetApplicationName("Navaco.AminAmval.v1");
builder.Services.Configure<ForwardedHeadersOptions>(o => {
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (builder.Configuration.GetValue<bool>("TRUST_PROXY")) { o.KnownNetworks.Clear(); o.KnownProxies.Clear(); }
    foreach (var proxy in (builder.Configuration["KNOWN_PROXIES"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)) if (IPAddress.TryParse(proxy, out var ip)) o.KnownProxies.Add(ip);
    o.ForwardLimit = 1;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
    o.Cookie.Name = "Amin.Session"; o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = preview ? SameSiteMode.None : SameSiteMode.Lax;
    o.Cookie.SecurePolicy = preview ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return c.Response.WriteAsJsonAsync(new { message = "برای ادامه وارد سامانه شوید." }); };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return c.Response.WriteAsJsonAsync(new { message = "شما اجازهٔ انجام این عملیات را ندارید." }); };
    o.Events.OnValidatePrincipal = async c => {
        try {
            var db = c.HttpContext.RequestServices.GetRequiredService<AppDb>();
            var idClaim = c.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (idClaim == null) { c.RejectPrincipal(); return; }
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == idClaim.Value);
            var sid = c.Principal?.FindFirst("sid")?.Value;
            var session = string.IsNullOrEmpty(sid) ? null : await db.Sessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid);
            var stampClaim = c.Principal?.FindFirst("stamp")?.Value;
            var now = DateTime.UtcNow;
            if (u == null || !u.Active || string.IsNullOrEmpty(stampClaim) || u.SecurityStamp != stampClaim || session == null || session.UserId != u.Id || session.Stamp != u.SecurityStamp || session.RevokedAt != null || session.ExpiresAt <= now) { c.RejectPrincipal(); return; }
            if (session.LastSeenAt < now.AddMinutes(-5))
                await db.Sessions.Where(x => x.Id == sid && x.RevokedAt == null).ExecuteUpdateAsync(set => set.SetProperty(x => x.LastSeenAt, now).SetProperty(x => x.ExpiresAt, now.AddHours(8)));
        } catch {
            c.RejectPrincipal();
        }
    };
});
builder.Services.AddAuthorization(o => {
    o.AddPolicy("Staff", p => p.RequireRole(Roles.Admin, Roles.Custodian));
    o.AddPolicy("Admin", p => p.RequireRole(Roles.Admin));
});
builder.Services.AddAntiforgery(o => {
    o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "Amin.Csrf"; o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = preview ? SameSiteMode.None : SameSiteMode.Lax;
    o.Cookie.SecurePolicy = preview ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.OnRejected = async (c, ct) => { c.HttpContext.Response.Headers.RetryAfter = "60"; await c.HttpContext.Response.WriteAsJsonAsync(new { message = "تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید." }, ct); };
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
    foreach (var (policy, limit) in new[] { ("bulk", 12), ("upload", 30), ("export", 30), ("password", 10), ("backup", 6), ("audit", 60) })
        o.AddPolicy(policy, c => RateLimitPartition.GetFixedWindowLimiter(c.UserId(), _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService(p => p.GetRequiredService<BackupService>());
builder.Services.AddScoped<ExcelService>();
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase; });
var app = builder.Build();
await Core.BootstrapAsync(app.Services);
app.UseForwardedHeaders();
app.Use(async (c, next) => {
    c.Response.Headers.XContentTypeOptions = "nosniff";
    c.Response.Headers["Referrer-Policy"] = "same-origin";
    c.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    c.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors " + (preview ? "*" : "'self'") + ";";
    if (c.Request.IsHttps) c.Response.Headers.StrictTransportSecurity = "max-age=31536000";
    if (c.Request.Path.StartsWithSegments("/api")) c.Response.Headers.CacheControl = "no-cache, no-store";
    try { await next(); }
    catch (ApiException ex) { c.Response.StatusCode = ex.Status; await c.Response.WriteAsJsonAsync(new { message = ex.Message, errors = ex.Errors }); }
    catch (DbUpdateConcurrencyException) { c.Response.StatusCode = 409; await c.Response.WriteAsJsonAsync(new { message = "اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید." }); }
    catch (DbUpdateException ex) { app.Logger.LogWarning("Database constraint rejected. Trace {Trace}: {Error}", c.TraceIdentifier, ex.InnerException?.GetType().Name); c.Response.StatusCode = 409; await c.Response.WriteAsJsonAsync(new { message = "کد تکراری یا وابستگی اطلاعات اجازهٔ ثبت این تغییر را نمی‌دهد." }); }
    catch (BadHttpRequestException) { c.Response.StatusCode = 400; await c.Response.WriteAsJsonAsync(new { message = "ساختار یا اندازهٔ درخواست معتبر نیست." }); }
    catch (System.Text.Json.JsonException) { c.Response.StatusCode = 400; await c.Response.WriteAsJsonAsync(new { message = "ساختار داده‌های ارسالی معتبر نیست." }); }
    catch (Exception ex) { app.Logger.LogError(ex, "Unhandled error {Trace}", c.TraceIdentifier); if (!c.Response.HasStarted) { c.Response.StatusCode = 500; await c.Response.WriteAsJsonAsync(new { message = "خطایی در پردازش رخ داد. دوباره تلاش کنید یا شناسهٔ پیگیری را به مدیر بدهید.", traceId = c.TraceIdentifier }); } }
    // Rejected authenticated operations use a fresh context: never save partially
    // validated changes that might still be tracked in the request's DbContext.
    if (c.Request.Path.StartsWithSegments("/api") && c.User.Identity?.IsAuthenticated == true && c.Response.StatusCode >= 400 && !c.Request.Path.StartsWithSegments("/api/auth")) {
        try {
            using var scope = app.Services.CreateScope(); var auditDb = scope.ServiceProvider.GetRequiredService<AppDb>();
            var match = System.Text.RegularExpressions.Regex.Match(c.Request.Path.Value ?? "", @"^/api/(assets|users)/([a-f0-9]{32})(?:/|$)");
            var type = match.Success ? (match.Groups[1].Value == "assets" ? "Asset" : "User") : "System";
            auditDb.Log(c, "request.rejected", type, match.Success ? match.Groups[2].Value : null, "درخواست ردشده یا ناموفق: " + c.Request.Method + " " + (c.Request.Path.Value ?? "").Substring(0, Math.Min(200, c.Request.Path.Value?.Length ?? 0)), after: new { statusCode = c.Response.StatusCode });
            await auditDb.SaveChangesAsync();
        } catch (Exception ex) { app.Logger.LogWarning(ex, "Could not persist rejection audit {Trace}", c.TraceIdentifier); }
    }
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = c.File.Name.EndsWith("woff2") ? "public,max-age=31536000,immutable" : "no-cache" });
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (c, next) => {
    if (c.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(c.Request.Method) && !HttpMethods.IsHead(c.Request.Method) && !HttpMethods.IsOptions(c.Request.Method)) {
        try { await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c); }
        catch (AntiforgeryValidationException) { c.Response.StatusCode = 400; await c.Response.WriteAsJsonAsync(new { message = "نشست امنیتی معتبر نیست. صفحه را تازه‌سازی کنید." }); return; }
    }
    if (c.User.Identity?.IsAuthenticated == true && c.User.FindFirst("change")?.Value == "1" && c.Request.Path.StartsWithSegments("/api") && !new[] { "/api/auth/me", "/api/auth/password", "/api/auth/logout", "/api/auth/csrf", "/api/health" }.Contains(c.Request.Path.Value)) {
        c.Response.StatusCode = 403; await c.Response.WriteAsJsonAsync(new { message = "پیش از ادامه، گذرواژهٔ اولیه را تغییر دهید.", mustChangePassword = true }); return;
    }
    await next();
});
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "1.1.0", utc = DateTime.UtcNow }));
app.MapAuth();
app.MapAssets();
app.MapOperations();
app.MapPrint();
app.MapUsersAndReferences();
app.MapReportsAndSystem();
app.Map("/api/{**path}", () => Results.NotFound(new { message = "مسیر مورد نظر وجود ندارد." }));
app.MapFallbackToFile("index.html");
app.Run();
