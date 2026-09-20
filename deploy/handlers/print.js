'use strict';
/*
 * امین اموال — /api/assets/:id/label and /api/assignments/:id/receipt
 * printable HTML documents (port of PrintEndpoints.cs).
 */
const fs = require('node:fs');
const path = require('node:path');
const QRCode = require('qrcode');
const { ApiError, faDate } = require('../lib/core');
const http = require('../lib/http-util');
const authmod = require('../lib/auth');

function H(text) {
  return String(text === undefined || text === null ? '—' : text)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

async function renderPage(ctx, title, body) {
  const webroot = ctx.base.webroot;
  const logo = 'data:image/png;base64,' + fs.readFileSync(path.join(webroot, 'assets', 'navaco.png')).toString('base64');
  const font = fs.readFileSync(path.join(webroot, 'assets', 'Vazirmatn.woff2')).toString('base64');
  const css = `@font-face{font-family:V;src:url(data:font/woff2;base64,${font})}*{box-sizing:border-box}body{font:14px V,sans-serif;direction:rtl;background:#eef2f8;color:#182443;margin:0;padding:25px}main{background:white;max-width:900px;margin:auto;padding:35px;border:1px solid #dae1f0}header{display:flex;align-items:center;gap:18px;border-bottom:3px solid #4263ef;padding-bottom:20px}header img{width:72px}h1{font-size:21px;margin:0}h2{font-size:16px}small,.note{color:#66738c}.note{font-size:11px;line-height:2}.details{margin:25px 0;display:grid;grid-template-columns:1fr 1fr;gap:0 28px}.details>div{padding:14px 0;border-bottom:1px solid #e3e8f1;display:flex;justify-content:space-between;gap:14px;overflow-wrap:anywhere}.details span{color:#6a7690}.details strong{max-width:65%}.signatures{display:flex;justify-content:space-between;text-align:center;margin-top:55px}footer{margin-top:40px;border-top:1px solid #eee;padding-top:14px;font-size:10px;color:#6a7690}.label{display:flex;gap:20px;border:2px solid #253e8a;border-radius:10px;max-width:680px;padding:18px;margin:30px auto;align-items:center;break-inside:avoid}.qr{width:180px;height:180px}.code{direction:ltr;font:700 20px monospace;overflow-wrap:anywhere}.controls{text-align:center;margin:0 auto 20px}.controls button{font:inherit;background:#4263ef;color:white;border:0;border-radius:7px;padding:12px 25px;cursor:pointer}@page{size:A4;margin:12mm}@media print{body{background:white;padding:0}main{padding:0;border:none}.controls{display:none}.note{color:#444}}`;
  const html = `<!doctype html><html lang='fa' dir='rtl'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>${H(title)} | ناواکو</title><style>${css}</style><div class='controls'><button id='print-document'>چاپ / ذخیره به PDF</button><p>در نسخهٔ ذخیره‌شده نیز می‌توانید از Ctrl+P استفاده کنید.</p></div><main><header><img src='${logo}' alt='ناواکو'><div><h1>${H(title)}</h1><small>امین اموال — شرکت فناوری اطلاعات ناواکو</small></div></header>${body}<footer>تاریخ تهیه: ${faDate()} — سند دارای اطلاعات سازمانی است؛ فقط در اختیار افراد مجاز قرار دهید.</footer></main><script src='/print.js' defer></script></html>`;
  ctx.res.statusCode = 200;
  ctx.res.setHeader('Content-Type', 'text/html; charset=utf-8');
  ctx.res.setHeader('Cache-Control', 'no-cache, no-store');
  ctx.res.setHeader('Content-Security-Policy', "default-src 'none'; style-src 'unsafe-inline'; img-src data:; font-src data:; script-src 'self'; base-uri 'none'; frame-ancestors 'self'; object-src 'none'");
  ctx.res.end(html);
  ctx.handled = true;
}

function register(router) {
  router.get('/api/assets/:id/label', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const a = (require('./assets-lib')).loadAsset(ctx.db, id, authmod.isAdmin(ctx));
    if (ctx.rateLimit && !ctx.rateLimit(`export:${ctx.user.id}`, 30, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    let svg;
    try { svg = await QRCode.toString(a.code, { type: 'svg', errorCorrectionLevel: 'Q', margin: 4 }); }
    catch { svg = ''; }
    const image = 'data:image/svg+xml;base64,' + Buffer.from(svg, 'utf8').toString('base64');
    const body = `<article class='label'><img class='qr' src='${image}' alt='QR کد اموال'><div><h2>${H(a.name)}</h2><p class='code'>${H(a.code)}</p><p>سریال: <bdi>${H(a.serial)}</bdi></p><p>مالک: ${H(a.owner)}</p></div></article><p class='note'>QR فقط کد اموال را در بر دارد و به آدرس موقت یا اطلاعات پرسنلی وابسته نیست. چاپ برچسب، به معنی تأیید نصب فیزیکی آن نیست.</p>`;
    http.logAudit(ctx.db, ctx, 'label.print', 'Asset', id, 'تهیهٔ برچسب QR برای «' + a.code + '»');
    await renderPage(ctx, 'برچسب شناسایی اموال', body);
  }));

  router.get('/api/assignments/:id/receipt', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    if (ctx.rateLimit && !ctx.rateLimit(`export:${ctx.user.id}`, 30, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    const x = ctx.db.prepare('SELECT x.*, a.name AS aname, a.code AS acode, a.serial AS aserial, a.deleted AS adel FROM assignments x LEFT JOIN assets a ON a.id = x.assetId WHERE x.id = ?').get(id);
    if (!x) throw new ApiError(404, 'تخصیص پیدا نشد.');
    if (x.adel && !authmod.isAdmin(ctx)) throw new ApiError(404, 'تخصیص پیدا نشد.');
    const returned = ctx.query.kind === 'return';
    if (returned && !x.endedAt) throw new ApiError(409, 'برای این تخصیص هنوز عودت یا انتقال ثبت نشده است.');
    const title = returned ? 'رسید عودت / خاتمهٔ تحویل' : 'رسید تحویل اموال';
    const name = x.assetNameAtIssue === '' ? x.aname : x.assetNameAtIssue;
    const code = x.assetCodeAtIssue === '' ? x.acode : x.assetCodeAtIssue;
    const items = [
      ['شماره پیگیری', x.id],
      ['نام مال در زمان تحویل', name],
      ['کد اموال در زمان تحویل', code],
      ['شماره سریال در زمان تحویل', x.serialAtIssue],
      ['تحویل‌گیرنده / واحد', x.recipientName],
      ['واحد سازمانی', x.departmentName],
      ['تاریخ تحویل', faDate(x.startedAt)],
      ['تاریخ خاتمه', x.endedAt ? faDate(x.endedAt) : '—'],
      ['شماره رسید / مجوز', returned ? x.endReference : x.reference],
      ['ثبت‌کننده', returned ? x.endedBy : x.issuerName],
      ['شرح', returned ? x.endReason : x.notes]
    ];
    let body = `<div class='details'>${items.map(p => `<div><span>${H(p[0])}</span><strong>${H(p[1])}</strong></div>`).join('')}</div><div class='signatures'><section>نام و امضای تحویل‌دهنده<br><br><br>............................</section><section>نام و امضای تحویل‌گیرنده<br><br><br>............................</section><section>تأیید جمعدار اموال<br><br><br>............................</section></div><p class='note'>محل امضا برای تأیید فیزیکی است؛ این خروجی امضای الکترونیکی یا تأیید حقوقی خودکار ایجاد نمی‌کند.</p>`;
    if (x.assetNameAtIssue === '') body += `<p class='note'>سابقهٔ قدیمی: نام و کدِ snapshot برای این تخصیص در نسخهٔ قبلی ذخیره نشده؛ مقادیر فعلی مال در بازچاپ نمایش داده شده‌اند.</p>`;
    http.logAudit(ctx.db, ctx, 'receipt.print', 'Asset', x.assetId, 'تهیهٔ ' + title, undefined, { assignmentId: x.id }, x.assetId, x.userId);
    await renderPage(ctx, title, body);
  }));
}

module.exports = { register };
