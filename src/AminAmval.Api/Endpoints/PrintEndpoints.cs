using System.Globalization;
using System.Net;
using System.Text;
using AminAmval.Data;
using AminAmval.Models;
using AminAmval.Services;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace AminAmval.Endpoints;
public static class PrintEndpoints
{
    public static void MapPrint(this WebApplication app)
    {
        app.MapGet("/api/assets/{id}/label", async (string id, AppDb db, HttpContext c, IWebHostEnvironment env) => {
            var a = await AssetService.Load(db, id, c.IsAdmin());
            using var generator = new QRCodeGenerator(); using var qr = generator.CreateQrCode(a.Code, QRCodeGenerator.ECCLevel.Q); using var svg = new SvgQRCode(qr);
            var image = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.GetGraphic(4)));
            var body = $"<article class='label'><img class='qr' src='{image}' alt='QR کد اموال'><div><h2>{H(a.Name)}</h2><p class='code'>{H(a.Code)}</p><p>سریال: <bdi>{H(a.Serial)}</bdi></p><p>مالک: {H(a.Owner)}</p></div></article><p class='note'>QR فقط کد اموال را در بر دارد و به آدرس موقت یا اطلاعات پرسنلی وابسته نیست. چاپ برچسب، به معنی تأیید نصب فیزیکی آن نیست.</p>";
            db.Log(c, "label.print", "Asset", id, "تهیهٔ برچسب QR برای «" + a.Code + "»"); await db.SaveChangesAsync();
            return Page(c, env, "برچسب شناسایی اموال", body);
        }).RequireAuthorization("Staff").RequireRateLimiting("export");
        app.MapGet("/api/assignments/{id}/receipt", async (string id, AppDb db, HttpContext c, IWebHostEnvironment env) => {
            var x = await db.Assignments.Include(x => x.Asset).FirstOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "تخصیص پیدا نشد.");
            if (x.Asset.Deleted && !c.IsAdmin()) throw new ApiException(404, "تخصیص پیدا نشد.");
            bool returned = c.Request.Query["kind"] == "return";
            if (returned && x.EndedAt == null) throw new ApiException(409, "برای این تخصیص هنوز عودت یا انتقال ثبت نشده است.");
            string title = returned ? "رسید عودت / خاتمهٔ تحویل" : "رسید تحویل اموال";
            var name = x.AssetNameAtIssue == "" ? x.Asset.Name : x.AssetNameAtIssue; var code = x.AssetCodeAtIssue == "" ? x.Asset.Code : x.AssetCodeAtIssue;
            var items = new (string, string?)[] { ("شماره پیگیری", x.Id), ("نام مال در زمان تحویل", name), ("کد اموال در زمان تحویل", code), ("شماره سریال در زمان تحویل", x.SerialAtIssue), ("تحویل‌گیرنده / واحد", x.RecipientName), ("واحد سازمانی", x.DepartmentName), ("تاریخ تحویل", FaDate(x.StartedAt)), ("تاریخ خاتمه", x.EndedAt == null ? "—" : FaDate(x.EndedAt.Value)), ("شماره رسید / مجوز", returned ? x.EndReference : x.Reference), ("ثبت‌کننده", returned ? x.EndedBy : x.IssuerName), ("شرح", returned ? x.EndReason : x.Notes) };
            var body = "<div class='details'>" + string.Concat(items.Select(p => "<div><span>" + H(p.Item1) + "</span><strong>" + H(p.Item2) + "</strong></div>")) + "</div><div class='signatures'><section>نام و امضای تحویل‌دهنده<br><br><br>............................</section><section>نام و امضای تحویل‌گیرنده<br><br><br>............................</section><section>تأیید جمعدار اموال<br><br><br>............................</section></div><p class='note'>محل امضا برای تأیید فیزیکی است؛ این خروجی امضای الکترونیکی یا تأیید حقوقی خودکار ایجاد نمی‌کند.</p>";
            if (x.AssetNameAtIssue == "") body += "<p class='note'>سابقهٔ قدیمی: نام و کدِ snapshot برای این تخصیص در نسخهٔ قبلی ذخیره نشده؛ مقادیر فعلی مال در بازچاپ نمایش داده شده‌اند.</p>";
            db.Log(c, "receipt.print", "Asset", x.AssetId, "تهیهٔ " + title, after: new { assignmentId = x.Id }, targetUser: x.UserId); await db.SaveChangesAsync();
            return Page(c, env, title, body);
        }).RequireAuthorization("Staff").RequireRateLimiting("export");
    }
    private static string H(string? text) => WebUtility.HtmlEncode(text ?? "—");
    private static string FaDate(DateTime date) => date.ToString("yyyy/MM/dd", CultureInfo.GetCultureInfo("fa-IR"));
    private static IResult Page(HttpContext c, IWebHostEnvironment env, string title, string body)
    {
        var logo = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(env.WebRootPath, "assets", "navaco.png")));
        var font = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(env.WebRootPath, "assets", "Vazirmatn.woff2")));
        c.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:; font-src data:; script-src 'self'; base-uri 'none'; frame-ancestors 'self'; object-src 'none'";
        var css = "@font-face{font-family:V;src:url(data:font/woff2;base64," + font + ")}*{box-sizing:border-box}body{font:14px V,sans-serif;direction:rtl;background:#eef2f8;color:#182443;margin:0;padding:25px}main{background:white;max-width:900px;margin:auto;padding:35px;border:1px solid #dae1f0}header{display:flex;align-items:center;gap:18px;border-bottom:3px solid #4263ef;padding-bottom:20px}header img{width:72px}h1{font-size:21px;margin:0}h2{font-size:16px}small,.note{color:#66738c}.note{font-size:11px;line-height:2}.details{margin:25px 0;display:grid;grid-template-columns:1fr 1fr;gap:0 28px}.details>div{padding:14px 0;border-bottom:1px solid #e3e8f1;display:flex;justify-content:space-between;gap:14px;overflow-wrap:anywhere}.details span{color:#6a7690}.details strong{max-width:65%}.signatures{display:flex;justify-content:space-between;text-align:center;margin-top:55px}footer{margin-top:40px;border-top:1px solid #eee;padding-top:14px;font-size:10px;color:#6a7690}.label{display:flex;gap:20px;border:2px solid #253e8a;border-radius:10px;max-width:680px;padding:18px;margin:30px auto;align-items:center;break-inside:avoid}.qr{width:180px;height:180px}.code{direction:ltr;font:700 20px monospace;overflow-wrap:anywhere}.controls{text-align:center;margin:0 auto 20px}.controls button{font:inherit;background:#4263ef;color:white;border:0;border-radius:7px;padding:12px 25px;cursor:pointer}@page{size:A4;margin:12mm}@media print{body{background:white;padding:0}main{padding:0;border:none}.controls{display:none}.note{color:#444}}";
        return Results.Content("<!doctype html><html lang='fa' dir='rtl'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>" + H(title) + " | ناواکو</title><style>" + css + "</style><div class='controls'><button id='print-document'>چاپ / ذخیره به PDF</button><p>در نسخهٔ ذخیره‌شده نیز می‌توانید از Ctrl+P استفاده کنید.</p></div><main><header><img src='" + logo + "' alt='ناواکو'><div><h1>" + H(title) + "</h1><small>امین اموال — شرکت فناوری اطلاعات ناواکو</small></div></header>" + body + "<footer>تاریخ تهیه: " + FaDate(DateTime.UtcNow.AddHours(3.5)) + " — سند دارای اطلاعات سازمانی است؛ فقط در اختیار افراد مجاز قرار دهید.</footer></main><script src='/print.js' defer></script></html>", "text/html; charset=utf-8");
    }
}
