'use strict';
/*
 * امین اموال — dashboard, audit, excel exports, xlsx import, system, backups
 * (port of ReportEndpoints.cs). Excel output is generated in-memory with
 * SheetJS; backups capture the SQLite database, uploads and session keys
 * into a ZIP manifest with per-file SHA-256 hashes.
 */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { ApiError, clean, digits, required, guid, nowIso, dateOnlyIso, paging, filterSnapshot, Roles } = require('../lib/core');
const http = require('../lib/http-util');
const authmod = require('../lib/auth');
const L = require('./assets-lib');
const XLSX = require('xlsx');

const MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
const AssetHeaders = ['کد اموال', 'کد قبلی', 'نام اموال', 'دسته‌بندی', 'برند', 'مدل', 'سریال', 'کیفیت', 'مالک', 'توضیحات', 'تاریخ خرید (میلادی)', 'ارزش خرید (ریال)', 'برچسب دارد', 'محل نگهداری', 'تصویر'];
const UserHeaders = ['کد پرسنلی', 'نام', 'نام خانوادگی', 'کد ملی', 'واحد سازمانی', 'نام کاربری'];

function stateFaOf(core, s) { return core.stateFa(s); }

/* ---------------- xlsx builders ------------------------------------ */
function workbookBuffer(sheetName, headers, rows, filters) {
  const aoa = [headers, ...rows];
  const ws = XLSX.utils.aoa_to_sheet(aoa);
  ws['!cols'] = headers.map((_, i) => ({ wch: i === 0 ? 21 : 26 }));
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, sheetName);
  const meta = XLSX.utils.aoa_to_sheet([
    ['سامانه', 'امین اموال | ناواکو'],
    ['زمان تولید (UTC)', new Date().toISOString()],
    ['تعداد رکورد', rows.length],
    ['فیلترها', filters || 'بدون فیلتر'],
    ['طبقه‌بندی', 'محرمانه — ویژهٔ استفادهٔ سازمانی. مبالغ به ریال و تاریخ‌های فایل میلادی هستند.']
  ]);
  meta['!cols'] = [{ wch: 28 }, { wch: 95 }];
  XLSX.utils.book_append_sheet(wb, meta, 'اطلاعات گزارش');
  return XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' });
}

function qualityFa(q) { switch (q) { case 'New': return 'نو'; case 'Good': return 'سالم'; case 'Used': return 'کارکرده'; case 'Damaged': return 'معیوب'; default: return q; } }

function auditRowOf(x) {
  return { id: x.id, at: x.at, actorName: x.actorName, actorRole: x.actorRole, ip: x.ip, action: x.action,
    entityType: x.entityType, entityId: x.entityId, description: x.description, before: x.before, after: x.after, correlationId: x.correlationId };
}

function exportAudit(events, filters) {
  return workbookBuffer('سوابق عملیات',
    ['شناسه', 'زمان دقیق (UTC)', 'کاربر', 'نقش', 'IP', 'عملیات', 'نوع موجودیت', 'شناسه موجودیت', 'شرح', 'قبل', 'بعد', 'شناسه پیگیری'],
    events.map(x => [x.id, new Date(x.at).toISOString(), x.actorName, x.actorRole, x.ip, x.action, x.entityType, x.entityId, x.description, x.before, x.after, x.correlationId]),
    filters);
}

function exportAssets(db, rows, filters, core) {
  return workbookBuffer('گزارش اموال',
    ['کد اموال', 'کد قبلی', 'نام اموال', 'دسته‌بندی', 'برند', 'مدل', 'سریال', 'کیفیت', 'مالک', 'وضعیت', 'برچسب دارد', 'تاریخ خرید', 'ارزش خرید (ریال)', 'تحویل‌گیرنده', 'واحد سازمانی', 'تاریخ تخصیص', 'محل نگهداری', 'توضیحات'],
    rows.map(a => {
      const cur = L.currentAssignment(db, a.id);
      const cat = L.hydrateCategory(db, a.categoryId);
      return [
        a.code, a.oldCode, a.name, cat ? cat.name : '', a.brand, a.model, a.serial, qualityFa(a.quality), a.owner,
        core.stateFa(a.status), a.hasLabel ? 'بله' : 'خیر', a.purchaseDate, a.purchaseCost,
        cur ? (cur.userId ? cur.recipientName : 'تخصیص به واحد') : null,
        cur ? cur.departmentName : null, cur ? cur.startedAt : null, a.location, a.description
      ];
    }),
    filters);
}

function exportUsers(rows, filters) {
  return workbookBuffer('کارکنان',
    ['کد پرسنلی', 'نام', 'نام خانوادگی', 'کد ملی', 'واحد سازمانی', 'نام کاربری', 'نقش', 'فعال', 'زمان ثبت (UTC)'],
    rows.map(x => {
      const roleFa = x.role === 'Admin' ? 'مدیر سامانه' : x.role === 'Custodian' ? 'جمعدار اموال' : 'کاربر عادی';
      return [x.personnelCode, x.firstName, x.lastName, x.nationalId, x.deptName, x.username, roleFa, x.active ? 'فعال' : 'غیرفعال', x.createdAt];
    }),
    filters);
}

function excelResponse(res, buffer, filename) {
  const encoded = encodeURIComponent(filename);
  res.statusCode = 200;
  res.setHeader('Content-Type', MIME);
  res.setHeader('Content-Disposition', `attachment; filename="amin-export.xlsx"; filename*=UTF-8''${encoded}`);
  res.setHeader('Cache-Control', 'no-cache, no-store');
  res.setHeader('Content-Length', buffer.length);
  res.end(buffer);
}

/* ---------------- audit query -------------------------------------- */
function auditQuery(db, query) {
  const q = { ...(query || {}) };
  let where = '1=1'; const params = [];
  const term = clean(q.q || '');
  if (term) { where += ' AND (lower(description) LIKE ? ESCAPE \'\\\' OR lower(actorName) LIKE ? ESCAPE \'\\\' OR lower(ip) LIKE ? ESCAPE \'\\\' OR lower(correlationId) LIKE ? ESCAPE \'\\\')'; const like = `%${term.toLowerCase()}%`; params.push(like, like, like, like); }
  if (q.action) { where += ' AND action = ?'; params.push(q.action); }
  if (q.entityType) { where += ' AND entityType = ?'; params.push(q.entityType); }
  if (q.actorId) { where += ' AND actorId = ?'; params.push(q.actorId); }
  const ids = (q.entityIds || '').split(',').filter(Boolean);
  if (ids.length > 200 || ids.some(x => !/^[a-f0-9]{32}$/.test(x))) throw new ApiError(400, 'شناسه‌های انتخاب‌شده معتبر نیستند؛ حداکثر ۲۰۰ مورد انتخاب کنید.');
  if (ids.length) {
    const placeholders = ids.map(() => '?').join(',');
    if (q.scope === 'assets') { where += ` AND assetId IS NOT NULL AND assetId IN (${placeholders})`; params.push(...ids); }
    else if (q.scope === 'users') { where += ` AND ((targetUserId IS NOT NULL AND targetUserId IN (${placeholders})) OR (actorId IS NOT NULL AND actorId IN (${placeholders})))`; params.push(...ids, ...ids); }
    else { where += ` AND entityId IS NOT NULL AND entityId IN (${placeholders})`; params.push(...ids); }
  }
  if (q.from) {
    const d = new Date(q.from); if (isNaN(d)) throw new ApiError(400, 'تاریخ شروع معتبر نیست.');
    where += ' AND at >= ?'; params.push(tehranFromStart(q.from).toISOString());
  }
  if (q.to) {
    const d = new Date(q.to); if (isNaN(d)) throw new ApiError(400, 'تاریخ پایان معتبر نیست.');
    where += ' AND at < ?'; params.push(tehranEndPlusDay(q.to).toISOString());
  }
  return { where, params };
}

function tehranFromStart(v) {
  // Tehran (UTC+03:30) start of day expressed in UTC
  const base = v.includes('T') ? v.slice(0, 10) : v.slice(0, 10);
  return new Date(base + 'T00:00:00+03:30');
}
function tehranEndPlusDay(v) {
  // Tehran start of the NEXT day expressed in UTC
  const base = v.includes('T') ? v.slice(0, 10) : v.slice(0, 10);
  const start = new Date(base + 'T00:00:00+03:30');
  return new Date(start.getTime() + 24 * 3600 * 1000);
}

function limitCount(count) { if (count > 10000) throw new ApiError(400, 'هر خروجی حداکثر ۱۰٬۰۰۰ رکورد دارد. بازه یا فیلتر را محدودتر کنید؛ هیچ ردیفی بی‌اطلاع حذف نمی‌شود.'); }

function fileStamp() {
  const d = new Date();
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}-${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}`;
}

/* ---------------- register ------------------------------------------ */
function register(router, ctxBase) {
  /* ---- dashboard ---------------------------------------------------- */
  router.get('/api/dashboard', authmod.requireAuth(async (ctx) => {
    const db = ctx.db;
    const staff = authmod.isStaff(ctx);
    const assetWhere = staff ? 'deleted = 0' : `deleted = 0 AND EXISTS (SELECT 1 FROM assignments x WHERE x.assetId = assets.id AND x.userId = ? AND x.endedAt IS NULL)`;
    const assetParams = staff ? [] : [ctx.user.id];

    const counts = db.prepare(`SELECT status, COUNT(*) AS count FROM assets WHERE ${assetWhere} GROUP BY status`).all(...assetParams);
    const categories = db.prepare(`
      SELECT c.id AS id, c.name AS name, COUNT(a.id) AS count
      FROM categories c LEFT JOIN assets a ON a.categoryId = c.id AND a.deleted = 0
      GROUP BY c.id ORDER BY count DESC
    `).all();
    const total = staff
      ? db.prepare('SELECT COUNT(*) AS c FROM assets WHERE deleted = 0').get().c
      : db.prepare('SELECT COUNT(*) AS c FROM assets WHERE deleted = 0 AND EXISTS (SELECT 1 FROM assignments x WHERE x.assetId = assets.id AND x.userId = ? AND x.endedAt IS NULL)').get(ctx.user.id).c;
    const unlabeled = db.prepare(`SELECT COUNT(*) AS c FROM assets WHERE deleted = 0 AND hasLabel = 0 AND (${staff ? '1=1' : `EXISTS (SELECT 1 FROM assignments x WHERE x.assetId = assets.id AND x.userId = ? AND x.endedAt IS NULL)`})`).get(...(staff ? [] : [ctx.user.id])).c;
    const cost = staff ? (db.prepare('SELECT COALESCE(SUM(purchaseCost),0) AS s FROM assets WHERE deleted = 0').get().s || 0) : 0;

    const recentWhere = staff ? 'endedAt IS NULL AND a.deleted = 0' : 'endedAt IS NULL AND a.deleted = 0 AND x.userId = ?';
    const recentParams = staff ? [] : [ctx.user.id];
    const recent = db.prepare(`
      SELECT x.id AS id, x.assetId AS assetId, a.name AS assetName, a.code AS assetCode, a.imageId AS imageId,
        x.recipientName AS recipient, x.departmentName AS department, x.startedAt AS startedAt
      FROM assignments x LEFT JOIN assets a ON a.id = x.assetId
      WHERE ${recentWhere} ORDER BY x.startedAt DESC LIMIT 5
    `).all(...recentParams).map(r => ({ id: r.id, assetId: r.assetId, assetName: r.assetName, assetCode: r.assetCode, imageId: r.imageId, recipient: r.recipient, department: r.department, startedAt: r.startedAt }));

    const activities = staff
      ? db.prepare(`SELECT id, at, action, description, actorName, assetId, entityType FROM audit WHERE action NOT LIKE 'view%' AND (${authmod.isAdmin(ctx) ? '1=1' : `entityType = 'Asset'`}) ORDER BY at DESC LIMIT 6`).all()
      : [];
    const setup = staff ? {
      categories: db.prepare('SELECT COUNT(*) AS c FROM categories').get().c,
      departments: db.prepare('SELECT COUNT(*) AS c FROM departments').get().c,
      employees: db.prepare(`SELECT COUNT(*) AS c FROM users WHERE role = 'Employee'`).get().c,
      assets: total
    } : null;
    const activeUsers = staff ? db.prepare('SELECT COUNT(*) AS c FROM users WHERE active = 1').get().c : 0;
    const pendingOperations = staff ? db.prepare(`SELECT COUNT(*) AS c FROM dispositionRequests WHERE state = 'Pending'`).get().c : 0;

    http.logAudit(db, ctx, 'view.dashboard', 'System', null, 'مشاهدهٔ داشبورد اختصاصی نقش');
    return {
      pendingOperations, total, counts, categories: categories.filter(c => c.count > 0 || c.count > 0),
      unlabeled, purchaseValue: cost, recent, activities, setup, activeUsers,
      backup: authmod.isAdmin(ctx) ? ctx.base.backupStatus() : null
    };
  }));

  /* ---- audit (admin) ------------------------------------------------ */
  router.get('/api/audit', authmod.requireAdmin(async (ctx) => {
    const { where, params } = auditQuery(ctx.db, ctx.query);
    const { page, size } = paging(ctx.query);
    const total = ctx.db.prepare(`SELECT COUNT(*) AS c FROM audit WHERE ${where}`).get(...params).c;
    const offset = (page - 1) * size;
    const items = ctx.db.prepare(`SELECT * FROM audit WHERE ${where} ORDER BY at DESC, id DESC LIMIT ? OFFSET ?`).all(...params, size, offset);
    http.logAudit(ctx.db, ctx, 'view.audit', 'System', null, 'مشاهدهٔ گزارش ممیزی', undefined, { filters: filterSnapshot(ctx.query), total });
    return { items, total, page, pageSize: size };
  }));

  /* ---- excel exports ------------------------------------------------- */
  router.get('/api/reports/:kind/export', authmod.requireStaff(async (ctx) => {
    const kind = ctx.params.kind;
    if (ctx.rateLimit && !ctx.rateLimit(`export:${ctx.user.id}`, 30, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    let buffer; let count;
    const ts = fileStamp();
    if (kind === 'audit') {
      if (!authmod.isAdmin(ctx)) throw new ApiError(403, 'خروجی کل سوابق فقط برای مدیر مجاز است.');
      const { where, params } = auditQuery(ctx.db, ctx.query);
      count = ctx.db.prepare(`SELECT COUNT(*) AS c FROM audit WHERE ${where}`).get(...params).c;
      limitCount(count);
      const rows = ctx.db.prepare(`SELECT * FROM audit WHERE ${where} ORDER BY at DESC`).all(...params);
      buffer = exportAudit(rows, 'q=' + (ctx.query.q || ''));
    } else if (kind === 'users') {
      const uq = require('./users').userQuery(ctx.db, ctx.query);
      count = ctx.db.prepare(`SELECT COUNT(*) AS c FROM users WHERE ${uq.where}`).get(...uq.params).c;
      limitCount(count);
      const rows = ctx.db.prepare(`SELECT * FROM users WHERE ${uq.where} ORDER BY lastName, firstName`).all(...uq.params)
        .map(x => ({ ...x, deptName: x.departmentId ? (ctx.db.prepare('SELECT name FROM departments WHERE id = ?').get(x.departmentId) || {}).name : null }));
      buffer = exportUsers(rows, 'q=' + (ctx.query.q || ''));
    } else if (kind === 'assets' || kind === 'current' || kind === 'unlabeled') {
      const dict = { ...ctx.query };
      if (kind !== 'assets') dict.report = kind;
      const { where, params } = require('./assets').assetQuery(ctx.db, dict, ctx);
      count = ctx.db.prepare(`SELECT COUNT(*) AS c FROM assets a WHERE ${where}`).get(...params).c;
      limitCount(count);
      const rows = ctx.db.prepare(`SELECT a.* FROM assets a WHERE ${where}`).all(...params);
      buffer = exportAssets(ctx.db, rows, 'report=' + kind, ctx.base.coreIo);
      count = rows.length;
    } else throw new ApiError(404, 'گزارش پیدا نشد.');
    // audit the export, then flush the file (audit is also written in same response flow)
    http.logAudit(ctx.db, ctx, 'export', kind === 'users' ? 'User' : kind === 'audit' ? 'System' : 'Asset', null,
      'دریافت خروجی Excel «' + kind + '»', undefined, { filters: filterSnapshot(ctx.query), count });
    excelResponse(ctx.res, buffer, 'amin-' + kind + '-' + ts + '.xlsx');
    ctx.handled = true;
  }));

  /* ---- per-asset history export ------------------------------------- */
  router.get('/api/assets/:id/history/export', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    if (ctx.rateLimit && !ctx.rateLimit(`export:${ctx.user.id}`, 30, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    const asset = ctx.db.prepare(`SELECT * FROM assets WHERE id = ? AND (deleted = 0 OR ? = 1)`).get(id, authmod.isAdmin(ctx) ? 1 : 0);
    if (!asset) throw new ApiError(404, 'اموال پیدا نشد.');
    const rows = ctx.db.prepare('SELECT * FROM audit WHERE assetId = ? ORDER BY at DESC').all(id);
    limitCount(rows.length);
    http.logAudit(ctx.db, ctx, 'export.history', 'Asset', id, 'خروجی کامل تاریخچهٔ اموال «' + asset.code + '»', undefined, { count: rows.length });
    excelResponse(ctx.res, exportAudit(rows, 'asset=' + asset.code), 'amin-history-' + fileStamp() + '.xlsx');
    ctx.handled = true;
  }));

  /* ---- import templates --------------------------------------------- */
  router.get('/api/import/:kind/template', authmod.requireStaff(async (ctx) => {
    const kind = ctx.params.kind;
    if (!['assets', 'users'].includes(kind)) throw new ApiError(404, 'نوع قالب معتبر نیست.');
    const headers = kind === 'assets' ? AssetHeaders : UserHeaders;
    const ws = XLSX.utils.aoa_to_sheet([headers]);
    ws['!cols'] = headers.map(() => ({ wch: 25 }));
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, kind === 'assets' ? 'اموال' : 'کارکنان');
    const guide = [
      kind === 'assets'
        ? ['قالب ورود اموال — داده‌ها را از ردیف دوم وارد کنید. نام و ترتیب ستون‌ها را تغییر ندهید.']
        : ['قالب ورود کارکنان — داده‌ها را از ردیف دوم وارد کنید. نام و ترتیب ستون‌ها را تغییر ندهید.'],
      [kind === 'assets'
        ? 'الزامی: نام اموال، دسته‌بندی، حداقل یکی از برند/مدل، کیفیت، مالک، توضیحات و تصویر.'
        : 'الزامی: کد پرسنلی، نام، نام خانوادگی، کد ملی و واحد سازمانی.'],
      kind === 'assets'
        ? ['کد اموال یکتا است؛ اگر خالی بماند کد خودکار تولید می‌شود.']
        : ['کد ملی باید ۱۰ رقم معتبر و به صورت متن باشد؛ صفر ابتدایی را حذف نکنید.'],
      ['حداکثر ۱۰۰۰ رکورد و ۲۰ مگابایت در هر فایل. فرمول و ماکرو پذیرفته نمی‌شود.'],
      ['ابتدا پیش‌نمایش را بررسی کنید؛ وجود حتی یک خطا مانع ثبت کل فایل می‌شود.']
    ];
    const gws = XLSX.utils.aoa_to_sheet(guide);
    gws['!cols'] = [{ wch: 125 }];
    gws['!rows'] = guide.map(() => ({ hpt: 36 }));
    XLSX.utils.book_append_sheet(wb, gws, 'راهنما');
    const buffer = XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' });
    http.logAudit(ctx.db, ctx, 'template', 'System', null, 'دریافت قالب Excel «' + kind + '»');
    excelResponse(ctx.res, buffer, 'amin-template-' + kind + '.xlsx');
    ctx.handled = true;
  }));

  /* ---- import (preview + commit) ------------------------------------ */
  router.post('/api/import/:kind', authmod.requireStaff(async (ctx) => {
    const kind = ctx.params.kind;
    if (ctx.rateLimit && !ctx.rateLimit(`bulk:${ctx.user.id}`, 12, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    if (!['assets', 'users'].includes(kind)) throw new ApiError(404, 'نوع ورود معتبر نیست.');
    const file = await http.receiveFile(ctx, 21 * 1024 * 1024);
    if (!file) throw new ApiError(400, 'فایل انتخاب نشده است.');
    if (!file.filename.toLowerCase().endsWith('.xlsx') || file.buffer.length > 20 * 1024 * 1024) {
      throw new ApiError(400, 'فقط فایل XLSX تا ۲۰ مگابایت پذیرفته می‌شود.');
    }
    const commit = ctx.query.commit === 'true';
    const result = await runImport(ctx, kind, file.buffer, commit);
    return result;
  }));

  /* ---- system status (admin) ---------------------------------------- */
  router.get('/api/system', authmod.requireAdmin(async (ctx) => {
    const db = ctx.db;
    http.logAudit(db, ctx, 'view.settings', 'System', null, 'مشاهدهٔ تنظیمات و وضعیت سامانه');
    const dbfile = path.join(ctx.base.dataRoot, 'amin.db');
    let databaseBytes = 0;
    try { databaseBytes = fs.statSync(dbfile).size; } catch { }
    let uploadBytes = 0;
    try { uploadBytes = fs.readdirSync(ctx.base.uploads).reduce((s, f) => s + fs.statSync(path.join(ctx.base.uploads, f)).size, 0); } catch { }
    const backups = ctx.base.backupsList();
    return {
      version: '1.1.0',
      runtime: 'AminAmval 1.1 (SQLite)',
      database: 'SQLite amin.db',
      databaseBytes,
      uploadBytes,
      auditCount: db.prepare('SELECT COUNT(*) AS c FROM audit').get().c,
      assets: db.prepare('SELECT COUNT(*) AS c FROM assets WHERE deleted = 0').get().c,
      users: db.prepare('SELECT COUNT(*) AS c FROM users').get().c,
      backup: ctx.base.backupStatus(),
      backups: backups.map(b => ({ name: b.name, size: b.size, createdAt: b.createdAt }))
    };
  }));

  /* ---- manual backup (admin) ---------------------------------------- */
  router.post('/api/backups', authmod.requireAdmin(async (ctx) => {
    if (ctx.rateLimit && !ctx.rateLimit(`backup:${ctx.user.id}`, 6, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    const name = await ctx.base.createBackup();
    http.logAudit(ctx.db, ctx, 'backup.create', 'System', null, 'پشتیبان‌گیری دستی پایگاه داده، تصاویر و کلیدهای نشست', undefined, { filename: name, size: fs.statSync(path.join(ctx.base.backups, name)).size });
    return { name, message: 'نسخهٔ پشتیبان با موفقیت تهیه شد.' };
  }));

  /* ---- download backup (admin) -------------------------------------- */
  router.get('/api/backups/:name', authmod.requireAdmin(async (ctx) => {
    const name = ctx.params.name;
    if (ctx.rateLimit && !ctx.rateLimit(`backup:${ctx.user.id}`, 6, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    if (!/^amin-[0-9]{8}-[0-9]{6}-[a-f0-9]{6}\.zip$/.test(name)) throw new ApiError(404, 'نسخهٔ پشتیبان پیدا نشد.');
    const p = path.join(ctx.base.backups, name);
    if (!fs.existsSync(p)) throw new ApiError(404, 'نسخهٔ پشتیبان پیدا نشد.');
    http.logAudit(ctx.db, ctx, 'backup.download', 'System', null, 'دریافت نسخهٔ پشتیبان حساس', undefined, { filename: name });
    http.sendFileAsync(ctx.res, p, 'application/zip', name);
    ctx.handled = true;
  }));
}

/* ---------------- import engine ------------------------------------ */
async function runImport(ctx, kind, bytes, commit) {
  const db = ctx.db;
  let wb;
  try { wb = XLSX.read(bytes, { type: 'buffer', cellDates: true }); }
  catch { throw new ApiError(400, 'فایل Excel قابل خواندن نیست. فقط XLSX بدون رمز، ماکرو و خرابی را بارگذاری کنید.'); }
  const sheetName = wb.SheetNames[0];
  const sheet = wb.Sheets[sheetName];
  if (!sheet) throw new ApiError(400, 'فایل فاقد کاربرگ است.');
  const headers = kind === 'assets' ? AssetHeaders : UserHeaders;
  const matrix = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '' });
  if (matrix.length < 1) throw new ApiError(400, 'فایل فاقد کاربرگ است.');
  const first = matrix[0] || [];
  for (let i = 0; i < headers.length; i++) {
    if (clean(first[i] === undefined ? '' : first[i]) !== headers[i]) {
      throw new ApiError(400, `عنوان ستون ${i + 1} باید «${headers[i]}» باشد. قالب سامانه را دانلود کنید.`);
    }
  }
  if (matrix.length - 1 > 1000) throw new ApiError(400, 'حداکثر ۱۰۰۰ ردیف مجاز است؛ ستون اضافی را حذف کنید.');

  const existingCodes = new Set(db.prepare('SELECT code FROM assets').all().map(x => x.code.toLowerCase()));
  const people = db.prepare('SELECT username, personnelCode, nationalId FROM users').all();
  const usernames = new Set(people.map(x => x.username.toLowerCase()));
  const personnel = new Set(people.map(x => x.personnelCode.toLowerCase()));
  const nationals = new Set(people.filter(x => x.nationalId).map(x => x.nationalId));
  const images = new Set(db.prepare('SELECT id FROM images').all().map(x => x.id));

  const rows = [];
  const errors = [];
  let total = 0;
  const cellOf = (r, col) => (matrix[r] ? matrix[r][col] : undefined);

  // helper to detect formulas in the raw sheet
  const hasFormula = (r, col) => {
    const addr = XLSX.utils.encode_cell({ r, c: col });
    const cell = sheet[addr];
    return !!(cell && cell.f);
  };

  const S = (r, col) => {
    if (hasFormula(r, col)) throw new ApiError(400, 'فرمول مجاز نیست؛ فقط مقدار ثابت وارد کنید.');
    const v = cellOf(r, col);
    if (v instanceof Date) return v;
    return clean(v, 2000);
  };

  for (let r = 1; r < matrix.length; r++) {
    if (matrix[r] && matrix[r].every(v => v === '' || v === undefined || v === null)) continue;
    total++;
    try {
      if (kind === 'users') {
        const personnelCode = required(digits(String(S(r, 0) || '')).toUpperCase(), 'کد پرسنلی', 60);
        const firstName = required(String(S(r, 1) || ''), 'نام', 100);
        const lastName = required(String(S(r, 2) || ''), 'نام خانوادگی', 100);
        const nationalId = digits(String(S(r, 3) || ''));
        const refName = required(String(S(r, 4) || ''), 'واحد سازمانی', 100);
        const uname = require('../lib/core').username(String(S(r, 5) || '') === '' ? personnelCode : String(S(r, 5)));
        if (!require('../lib/core').validNationalId(nationalId)) throw new ApiError(400, 'کد ملی ۱۰ رقمی معتبر نیست؛ صفر ابتدایی باید حفظ شود.');
        if (usernames.has(uname) || personnel.has(personnelCode.toLowerCase()) || nationals.has(nationalId)) {
          throw new ApiError(400, 'نام کاربری، کد پرسنلی یا کد ملی در فایل یا سامانه تکراری است.');
        }
        usernames.add(uname); personnel.add(personnelCode.toLowerCase()); nationals.add(nationalId);
        rows.push({ row: r + 1, kind: 'user', personnelCode, firstName, lastName, nationalId, refName, username: uname });
      } else {
        const codeRaw = digits(String(S(r, 0) || '')).toUpperCase();
        const oldCode = clean(digits(String(S(r, 1) || '')), 60);
        const name = required(String(S(r, 2) || ''), 'نام اموال');
        const refName = required(String(S(r, 3) || ''), 'دسته‌بندی', 100);
        const brand = clean(String(S(r, 4) || ''), 100);
        const model = clean(String(S(r, 5) || ''), 100);
        const serial = clean(digits(String(S(r, 6) || '')), 100);
        const qualityRaw = String(S(r, 7) || '');
        const owner = required(String(S(r, 8) || ''), 'مالک', 150);
        const description = required(String(S(r, 9) || ''), 'توضیحات', 2000);
        if (codeRaw && existingCodes.has(codeRaw.toLowerCase())) throw new ApiError(400, 'کد اموال در فایل یا سامانه تکراری است.');
        if (codeRaw) existingCodes.add(codeRaw.toLowerCase());
        if (brand === '' && model === '') throw new ApiError(400, 'برند یا مدل الزامی است.');
        const qmap = { 'نو': 'New', 'سالم': 'Good', 'کارکرده': 'Used', 'معیوب': 'Damaged', 'New': 'New', 'Good': 'Good', 'Used': 'Used', 'Damaged': 'Damaged' };
        const quality = qmap[qualityRaw];
        if (!quality) throw new ApiError(400, 'کیفیت باید نو، سالم، کارکرده یا معیوب باشد.');
        let purchaseDate = null;
        const dateRaw = S(r, 10);
        if (dateRaw instanceof Date) purchaseDate = dateOnlyIso(dateRaw);
        else if (dateRaw !== '' && dateRaw !== undefined && dateRaw !== null) {
          const s = String(dateRaw).trim();
          if (!/^\d{4}-\d{2}-\d{2}$/.test(digits(s))) throw new ApiError(400, 'تاریخ خرید باید میلادی و به صورت yyyy-MM-dd باشد.');
          purchaseDate = digits(s);
        }
        let cost = null;
        const costRaw = String(S(r, 11) || '');
        if (costRaw !== '') {
          const c = Number(digits(costRaw).replace(/[,٬]/g, ''));
          if (!Number.isFinite(c) || c < 0 || c > 1000000000000000) throw new ApiError(400, 'ارزش خرید باید عدد صحیح غیرمنفی به ریال باشد.');
          cost = c;
        }
        const labelRaw = String(S(r, 12) || '');
        let hasLabel = true;
        if (labelRaw === 'خیر' || labelRaw === 'false' || labelRaw === '0') hasLabel = false;
        else if (labelRaw === 'بله' || labelRaw === 'true' || labelRaw === '1' || labelRaw === '') hasLabel = true;
        else throw new ApiError(400, 'ستون برچسب باید بله یا خیر باشد.');
        const location = clean(String(S(r, 13) || ''));
        const imgRef = String(S(r, 14) || '').trim();
        let imageId = null;
        if (images.has(imgRef)) imageId = imgRef;
        // NOTE: floating pictures inside XLSX are not extracted; the embedded
        // image column is accepted as a pre-uploaded image id, consistent with
        // the guide's alternative ("شناسهٔ تصویر از قبل بارگذاری‌شده").
        if (!imageId) {
          throw new ApiError(400, 'تصویر الزامی است؛ شناسهٔ تصویرِ از قبل بارگذاری‌شده در این سامانه را در ستون تصویر بنویسید یا ابتدا تصویر را بارگذاری کنید.');
        }
        rows.push({ row: r + 1, kind: 'asset', codeRaw, oldCode, name, refName, brand, model, serial, quality, owner, description, purchaseDate, cost, hasLabel, location, imageId });
      }
    } catch (e) {
      if (process.env.DEBUG_IMPORT) console.log('[import-debug] row', r + 1, '->', e && e.constructor && e.constructor.name, e && e.message);
      errors.push({ row: r + 1, message: e instanceof ApiError ? e.message : 'مقدار سلول یا تصویر قابل خواندن نیست؛ قالب و راهنمای فایل را بررسی کنید.' });
    }
  }

  if (total === 0) throw new ApiError(400, 'فایل فاقد داده است؛ ردیف‌ها را پس از عنوان‌ها وارد کنید.');

  if (!commit || errors.length > 0) {
    http.logAudit(db, ctx, 'import.preview', kind === 'assets' ? 'Asset' : 'User', null, 'اعتبارسنجی فایل Excel بدون ثبت اطلاعات', undefined, { total, valid: rows.length, errors: errors.length });
    return {
      canCommit: errors.length === 0, total, valid: rows.length, errors, committed: false,
      preview: rows.slice(0, 10).map(x => ({
        row: x.row,
        name: x.kind === 'user' ? x.firstName + ' ' + x.lastName : x.name,
        code: x.kind === 'user' ? x.personnelCode : (x.codeRaw || ''),
        reference: x.refName
      }))
    };
  }

  db.exec('BEGIN');
  try {
    const categoryByName = new Map(db.prepare('SELECT id, name FROM categories').all().map(x => [x.name, x.id]));
    const deptByName = new Map(db.prepare('SELECT id, name FROM departments').all().map(x => [x.name, x.id]));
    let counter = 0;
    for (const x of rows) {
      if (x.kind === 'user') {
        let deptId = deptByName.get(x.refName);
        if (!deptId) {
          deptId = guid();
          db.prepare('INSERT INTO departments (id, name, version) VALUES (?,?,1)').run(deptId, x.refName);
          deptByName.set(x.refName, deptId);
          http.logAudit(db, ctx, 'create', 'Department', deptId, 'ایجاد واحد سازمانی از Excel', undefined, { name: x.refName });
        }
        const uid = guid();
        db.prepare(`
          INSERT INTO users (id, username, personnelCode, firstName, lastName, nationalId, departmentId, role, active,
            passwordHash, securityStamp, mustChangePassword, failedLogins, lockoutUntil, createdAt, version)
          VALUES (?,?,?,?,?,?,?,?,1,?,?,1,0,NULL,?,1)
        `).run(uid, x.username, x.personnelCode, x.firstName, x.lastName, x.nationalId, deptId,
          Roles.Employee, require('../lib/password').hashPassword(x.nationalId), guid(), nowIso());
        http.logAudit(db, ctx, 'import.create', 'User', uid, 'ورود کاربر «' + x.firstName + ' ' + x.lastName + '» از Excel، ردیف ' + x.row);
        counter++;
      } else {
        let catId = categoryByName.get(x.refName);
        if (!catId) {
          catId = guid();
          db.prepare('INSERT INTO categories (id, name, description, version) VALUES (?,?,?,1)').run(catId, x.refName, '');
          categoryByName.set(x.refName, catId);
          http.logAudit(db, ctx, 'create', 'Category', catId, 'ایجاد دسته‌بندی از Excel', undefined, { name: x.refName });
        }
        const code = x.codeRaw || 'NV-' + new Date().toISOString().slice(2, 8).replace(/-/g, '') + '-' + guid().slice(0, 16).toUpperCase();
        const aid = guid();
        const now = nowIso();
        db.prepare(`
          INSERT INTO assets (id, code, oldCode, name, categoryId, brand, model, serial, quality, owner, description,
            imageId, purchaseDate, purchaseCost, hasLabel, status, location, createdAt, updatedAt, lastOperationDate, deleted, version)
          VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,NULL,0,1)
        `).run(aid, code, x.oldCode, x.name, catId, x.brand, x.model, x.serial, x.quality, x.owner, x.description,
          x.imageId, x.purchaseDate, x.cost, x.hasLabel ? 1 : 0, 'Available', x.location, now, now);
        const created = db.prepare('SELECT * FROM assets WHERE id = ?').get(aid);
        http.logAudit(db, ctx, 'import.create', 'Asset', aid, 'ورود اموال «' + created.name + '» از Excel، ردیف ' + x.row, undefined, L.assetSnapshot(created));
        counter++;
      }
    }
    http.logAudit(db, ctx, 'import.commit', kind === 'assets' ? 'Asset' : 'User', null, 'ثبت نهایی ورود گروهی Excel', undefined, { count: counter });
    db.exec('COMMIT');
    return { committed: true, total, valid: rows.length, canCommit: true, errors: [], message: `${rows.length} رکورد با موفقیت و به‌صورت یکپارچه ثبت شد.` };
  } catch (e) {
    db.exec('ROLLBACK');
    throw e;
  }
}

module.exports = { register };
