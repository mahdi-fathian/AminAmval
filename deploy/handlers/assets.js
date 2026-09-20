'use strict';
/*
 * امین اموال — /api/assets/**, /api/files/** endpoints (port of AssetEndpoints.cs).
 */
const { ApiError, States, checkVersion, clean, digits, required, validDate, guid, nowIso, dateOnlyIso, filterSnapshot, paging } = require('../lib/core');
const http = require('../lib/http-util');
const authmod = require('../lib/auth');
const L = require('./assets-lib');

function escapeLike(v) { return String(v).replace(/[\\%_]/g, (m) => '\\' + m); }

/* ---------------- query builder (port of AssetService.Query) ------ */
function assetQuery(db, query, ctx) {
  const q = { ...(query || {}) };
  const archived = q.archived === 'true';
  if (archived && !authmod.isAdmin(ctx)) throw new ApiError(403, 'بایگانی فقط برای مدیر مجاز است.');
  const staff = authmod.isStaff(ctx);

  let where = `a.deleted = ${archived ? 1 : 0}`;
  const params = [];

  const search = digits(q.q || '').toLowerCase();
  if (search) {
    where += ` AND (lower(a.code) LIKE ?
      OR lower(a.oldCode) LIKE ?
      OR lower(a.name) LIKE ?
      OR lower(a.serial) LIKE ?
      OR lower(a.brand) LIKE ?
      OR lower(a.model) LIKE ?
      OR lower(a.owner) LIKE ?
      OR EXISTS (SELECT 1 FROM assignments cur
                 WHERE cur.assetId = a.id AND cur.endedAt IS NULL
                 AND (lower(cur.recipientName) LIKE ?
                      OR lower(cur.departmentName) LIKE ?
                      OR EXISTS (SELECT 1 FROM users usr WHERE usr.id = cur.userId
                                 AND (lower(usr.personnelCode) LIKE ?
                                      OR lower(usr.firstName || ' ' || usr.lastName) LIKE ?))
                      OR EXISTS (SELECT 1 FROM departments dp WHERE dp.id = cur.departmentId AND lower(dp.name) LIKE ?))))`;
    const like = `%${escapeLike(search)}%`;
    for (let i = 0; i < 12; i++) params.push(like);
  }
  if (q.categoryId) { where += ` AND a.categoryId = ?`; params.push(q.categoryId); }
  if (q.status) {
    if (!require('../lib/core').StatesAll.includes(q.status)) throw new ApiError(400, 'وضعیت انتخابی معتبر نیست.');
    where += ` AND a.status = ?`; params.push(q.status);
  }
  if (q.quality) { where += ` AND a.quality = ?`; params.push(q.quality); }
  if (q.owner) { const o = clean(q.owner); where += ` AND lower(a.owner) LIKE ?`; params.push(`%${escapeLike(o.toLowerCase())}%`); }
  if (q.userId) { where += ` AND EXISTS (SELECT 1 FROM assignments x WHERE x.assetId = a.id AND x.userId = ? AND x.endedAt IS NULL)`; params.push(q.userId); }
  if (q.departmentId) { where += ` AND EXISTS (SELECT 1 FROM assignments x WHERE x.assetId = a.id AND x.departmentId = ? AND x.endedAt IS NULL)`; params.push(q.departmentId); }
  if (q.report === 'current') { where += ` AND a.status = 'Assigned'`; }
  if (q.report === 'unlabeled' || q.hasLabel === 'false') { where += ` AND a.hasLabel = 0`; }
  else if (q.hasLabel === 'true') { where += ` AND a.hasLabel = 1`; }
  if (q.purchaseFrom) {
    const d = new Date(q.purchaseFrom + 'T00:00:00.000Z');
    if (isNaN(d)) throw new ApiError(400, 'تاریخ شروع فیلتر معتبر نیست.');
    where += ` AND a.purchaseDate >= ?`; params.push(q.purchaseFrom);
  }
  if (q.purchaseTo) {
    const d = new Date(q.purchaseTo);
    if (isNaN(d)) throw new ApiError(400, 'تاریخ پایان فیلتر معتبر نیست.');
    const next = new Date(q.purchaseTo + 'T00:00:00.000Z');
    next.setUTCDate(next.getUTCDate() + 1);
    where += ` AND a.purchaseDate < ?`; params.push(dateOnlyIso(next));
  }
  if (q.purchaseFrom && q.purchaseTo) {
    const f = new Date(digits(q.purchaseFrom)); const t = new Date(digits(q.purchaseTo));
    if (!isNaN(f) && !isNaN(t) && f > t) throw new ApiError(400, 'تاریخ شروع نباید بعد از تاریخ پایان باشد.');
  }

  if (!staff) {
    where += ` AND EXISTS (SELECT 1 FROM assignments x WHERE x.assetId = a.id AND x.userId = ? AND x.endedAt IS NULL)`;
    params.push(ctx.user.id);
  }

  let order;
  switch (q.sort) {
    case 'code': order = 'a.code ASC, a.id ASC'; break;
    case 'name': order = 'a.name ASC, a.id ASC'; break;
    case 'oldest': order = 'a.createdAt ASC, a.id ASC'; break;
    default: order = 'a.createdAt DESC, a.id DESC';
  }
  return { where, params, order };
}

function listAssets(db, query, ctx) {
  const { where, params, order } = assetQuery(db, query, ctx);
  const { page, size } = paging(query);
  const total = db.prepare(`SELECT COUNT(*) AS c FROM assets a WHERE ${where}`).get(...params).c;
  const offset = (page - 1) * size;
  const rows = db.prepare(`SELECT a.* FROM assets a WHERE ${where} ORDER BY ${order} LIMIT ? OFFSET ?`)
    .all(...params, size, offset);
  const items = rows.map(a => L.assetView(db, a, authmod.isStaff(ctx)));
  return { items, total, page, pageSize: size };
}

/* ================================================================== */
function register(router) {
  /* ---- list ------------------------------------------------------- */
  router.get('/api/assets', authmod.requireAuth(async (ctx) => {
    const data = listAssets(ctx.db, ctx.query, ctx);
    http.logAudit(ctx.db, ctx, 'view.list', 'Asset', null, 'مشاهدهٔ فهرست اموال',
      undefined, { filters: filterSnapshot(ctx.query), total: data.total });
    return data;
  }));

  /* ---- detail ----------------------------------------------------- */
  router.get('/api/assets/:id', authmod.requireAuth(async (ctx) => {
    const id = ctx.params.id;
    const a = L.loadAsset(ctx.db, id, authmod.isAdmin(ctx));
    const staff = authmod.isStaff(ctx);
    if (!staff) {
      const owns = ctx.db.prepare(`SELECT COUNT(*) AS c FROM assignments WHERE assetId = ? AND userId = ? AND endedAt IS NULL`).get(id, ctx.user.id).c;
      if (!owns) throw new ApiError(404, 'اموال مورد نظر پیدا نشد.');
    }
    const assignments = ctx.db.prepare(`SELECT * FROM assignments WHERE assetId = ? ORDER BY startedAt DESC`).all(id);
    let history = [];
    let historyTotal = 0;
    if (staff) {
      history = ctx.db.prepare(`SELECT * FROM audit WHERE assetId = ? ORDER BY id DESC LIMIT 200`).all(id);
      historyTotal = ctx.db.prepare(`SELECT COUNT(*) AS c FROM audit WHERE assetId = ?`).get(id).c;
    }
    http.logAudit(ctx.db, ctx, 'view.detail', 'Asset', id, 'مشاهدهٔ شناسنامهٔ اموال «' + a.name + '»');
    return {
      asset: L.assetView(ctx.db, a, staff),
      assignments: staff ? assignments.map(assignmentView) : [],
      history, historyTotal
    };
  }));

  /* ---- paginated per-asset history (cursor) ----------------------- */
  router.get('/api/assets/:id/history', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const a = ctx.db.prepare(`SELECT id FROM assets WHERE id = ? AND (deleted = 0 OR ? = 1)`).get(id, authmod.isAdmin(ctx) ? 1 : 0);
    if (!a) throw new ApiError(404, 'اموال پیدا نشد.');
    const { page, size } = paging(ctx.query);
    let where = 'assetId = ?'; const params = [id];
    if (ctx.query.beforeId && /^\d+$/.test(ctx.query.beforeId)) { where += ' AND id < ?'; params.push(+ctx.query.beforeId); }
    const total = ctx.db.prepare(`SELECT COUNT(*) AS c FROM audit WHERE ${where}`).get(...params).c;
    const offset = (page - 1) * size;
    const items = ctx.db.prepare(`SELECT * FROM audit WHERE ${where} ORDER BY id DESC LIMIT ? OFFSET ?`).all(...params, size, offset);
    return { items, total, page, pageSize: size };
  }));

  /* ---- create ----------------------------------------------------- */
  router.post('/api/assets', authmod.requireStaff(async (ctx) => {
    const i = ctx.body || {};
    const a = { id: guid() };
    const values = L.applyAsset(ctx.db, a, i, {});
    const now = nowIso();
    ctx.db.prepare(`
      INSERT INTO assets (id, code, oldCode, name, categoryId, brand, model, serial, quality, owner,
        description, imageId, purchaseDate, purchaseCost, hasLabel, status, location, createdAt, updatedAt,
        lastOperationDate, deleted, version)
      VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,NULL,0,1)
    `).run(
      a.id, values.code, values.oldCode, values.name, values.categoryId, values.brand, values.model,
      values.serial, values.quality, values.owner, values.description, values.imageId,
      values.purchaseDate, values.purchaseCost, values.hasLabel ? 1 : 0, States.Available,
      values.location, now, now
    );
    const created = ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(a.id);
    http.logAudit(ctx.db, ctx, 'create', 'Asset', a.id, 'ثبت اموال «' + created.name + '» با کد ' + created.code,
      undefined, L.assetSnapshot(created));
    return { id: a.id, code: created.code, version: 1, message: 'اموال با موفقیت ثبت شد.' }; // 201
  }));

  /* ---- update ----------------------------------------------------- */
  router.put('/api/assets/:id', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const a = L.loadAsset(ctx.db, id);
    const i = ctx.body || {};
    checkVersion(a.version, Number(i.version));
    L.checkEditable(ctx.db, a);
    const values = L.applyAsset(ctx.db, a, i, {});
    const before = L.assetSnapshot(a);
    ctx.db.prepare(`
      UPDATE assets SET code=?, oldCode=?, name=?, categoryId=?, brand=?, model=?, serial=?, quality=?,
        owner=?, description=?, imageId=?, purchaseDate=?, purchaseCost=?, hasLabel=?, location=?,
        updatedAt=?, version=version+1 WHERE id=?
    `).run(
      values.code, values.oldCode, values.name, values.categoryId, values.brand, values.model,
      values.serial, values.quality, values.owner, values.description, values.imageId,
      values.purchaseDate, values.purchaseCost, values.hasLabel ? 1 : 0, values.location, nowIso(), id
    );
    const updated = ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(id);
    http.logAudit(ctx.db, ctx, 'update', 'Asset', id, 'ویرایش اموال «' + updated.name + '»', before, L.assetSnapshot(updated));
    return { id: updated.id, version: updated.version, message: 'تغییرات ذخیره شد.' };
  }));

  /* ---- archive (delete) ------------------------------------------- */
  router.delete('/api/assets/:id', authmod.requireAdmin(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const a = L.loadAsset(ctx.db, id);
    checkVersion(a.version, Number(i.version));
    L.checkEditable(ctx.db, a);
    if (a.status === States.Assigned) throw new ApiError(409, 'ابتدا اموال را عودت دهید؛ اموال تخصیص‌یافته قابل حذف نیست.');
    const before = L.assetSnapshot(a);
    ctx.db.prepare('UPDATE assets SET deleted = 1, version = version + 1, updatedAt = ? WHERE id = ?').run(nowIso(), id);
    const updated = ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(id);
    http.logAudit(ctx.db, ctx, 'delete', 'Asset', id, 'بایگانی اموال؛ علت: ' + required(i.reason || '', 'علت حذف', 1000), before, L.assetSnapshot(updated));
    return { message: 'اموال بایگانی شد؛ سوابق برای حسابرسی محفوظ است.' };
  }));

  /* ---- assign / transfer ------------------------------------------ */
  const assignHandler = (transfer) => authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const a = L.loadAsset(ctx.db, id);
    checkVersion(a.version, Number(i.version));
    L.checkEditable(ctx.db, a);
    if ((!transfer && a.status !== States.Available) || (transfer && a.status !== States.Assigned)) {
      throw new ApiError(409, transfer ? 'فقط اموال در حال بهره‌برداری قابل انتقال است.' : 'فقط اموال موجود در انبار قابل تخصیص است.');
    }
    const date = validDate(i.startedAt, 'تاریخ شروع تخصیص');
    L.checkChronology(ctx.db, a, date);

    let user = null;
    if (i.userId) {
      user = ctx.db.prepare('SELECT * FROM users WHERE id = ? AND active = 1').get(i.userId);
      if (!user) throw new ApiError(400, 'شخص انتخاب‌شده معتبر یا فعال نیست.');
      if (user.role !== 'Employee') throw new ApiError(400, 'تخصیص اموال فقط به کارکنان سازمان مجاز است.');
    }
    const deptId = user ? user.departmentId : i.departmentId;
    if (!deptId) throw new ApiError(400, 'واحد سازمانی تحویل‌گیرنده مشخص نیست.');
    const dept = ctx.db.prepare('SELECT * FROM departments WHERE id = ?').get(deptId);
    if (!dept) throw new ApiError(400, 'برای تخصیص به شخص، واحد سازمانی او باید مشخص باشد؛ برای تخصیص به واحد، واحد را انتخاب کنید.');

    const current = L.currentAssignment(ctx.db, id);
    if (current && dateOnlyIso(date) < dateOnlyIso(new Date(current.startedAt))) {
      throw new ApiError(400, 'تاریخ انتقال قبل از تخصیص جاری است.');
    }
    if (current && current.userId === (user ? user.id : null) && current.departmentId === deptId) {
      throw new ApiError(400, 'تحویل‌گیرندهٔ جدید با تخصیص جاری یکسان است.');
    }

    // one transaction
    ctx.db.exec('BEGIN');
    try {
      const before = { asset: L.assetSnapshot(a), assignment: current ? assignmentView(current) : null };
      const opDay = dateOnlyIso(date);
      if (current) {
        ctx.db.prepare(`UPDATE assignments SET endedAt = ?, endReason = 'انتقال', endedBy = ?, endReference = ? WHERE id = ?`)
          .run(opDay, ctx.user.fullName, clean(i.reference || ''), current.id);
      }
      const assignmentId = guid();
      ctx.db.prepare(`
        INSERT INTO assignments (id, assetId, userId, departmentId, startedAt, endedAt, notes, reference, createdBy,
          recipientName, departmentName, assetCodeAtIssue, assetNameAtIssue, serialAtIssue, qualityAtIssue, issuerName)
        VALUES (?,?,?,?,?,NULL,?,?,?,?,?,?,?,?,?,?)
      `).run(
        assignmentId, id, user ? user.id : null, deptId, opDay, clean(i.notes || '', 1000),
        clean(i.reference || ''), ctx.user.id, user ? `${user.firstName} ${user.lastName}` : dept.name, dept.name,
        a.code, a.name, a.serial, a.quality, ctx.user.fullName
      );
      ctx.db.prepare(`UPDATE assets SET status = ?, version = version + 1, updatedAt = ?, lastOperationDate = ? WHERE id = ?`)
        .run(States.Assigned, nowIso(), opDay, id);
      const updated = ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(id);
      const newAssignment = ctx.db.prepare('SELECT * FROM assignments WHERE id = ?').get(assignmentId);
      http.logAudit(ctx.db, ctx, transfer ? 'transfer' : 'assign', 'Asset', id,
        (transfer ? 'انتقال' : 'تخصیص') + ' «' + a.name + '» به ' + newAssignment.recipientName,
        before, { asset: L.assetSnapshot(updated), assignment: assignmentView(newAssignment) }, id, user ? user.id : null);
      ctx.db.exec('COMMIT');
      return { message: transfer ? 'انتقال اموال با موفقیت ثبت شد.' : 'تخصیص اموال با موفقیت ثبت شد.', assignmentId };
    } catch (e) {
      ctx.db.exec('ROLLBACK');
      throw e;
    }
  });

  router.post('/api/assets/:id/assign', assignHandler(false));
  router.post('/api/assets/:id/transfer', assignHandler(true));

  /* ---- return ----------------------------------------------------- */
  router.post('/api/assets/:id/return', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const a = L.loadAsset(ctx.db, id);
    checkVersion(a.version, Number(i.version));
    L.checkEditable(ctx.db, a);
    const current = L.currentAssignment(ctx.db, id);
    if (a.status !== States.Assigned || !current) throw new ApiError(409, 'اموال تخصیص جاری ندارد.');
    const date = validDate(i.date, 'تاریخ عودت');
    L.checkChronology(ctx.db, a, date);
    if (dateOnlyIso(date) < dateOnlyIso(new Date(current.startedAt))) {
      throw new ApiError(400, 'تاریخ عودت نمی‌تواند قبل از شروع تخصیص باشد.');
    }
    const before = { asset: L.assetSnapshot(a), assignment: assignmentView(current) };
    const retDay = dateOnlyIso(date);
    const retReason = required(i.reason || '', 'علت عودت', 1000);
    const retReference = clean(i.reference || '');
    const retLocation = clean(i.location || '');
    ctx.db.prepare(`UPDATE assignments SET endedAt = ?, endReason = ?, endedBy = ?, endReference = ? WHERE id = ?`)
      .run(retDay, retReason, ctx.user.fullName, retReference, current.id);
    ctx.db.prepare(`UPDATE assets SET status = ?, lastOperationDate = ?, location = ?, version = version + 1, updatedAt = ? WHERE id = ?`)
      .run(States.Available, retDay, retLocation, nowIso(), id);
    const updated = ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(id);
    const after = ctx.db.prepare('SELECT * FROM assignments WHERE id = ?').get(current.id);
    http.logAudit(ctx.db, ctx, 'return', 'Asset', id,
      'عودت «' + a.name + '» از ' + (current.userId ? current.recipientName : current.departmentName),
      before, { asset: L.assetSnapshot(updated), assignment: assignmentView(after), reference: retReference },
      id, current.userId);
    return { message: 'عودت ثبت شد و اموال به انبار بازگشت.' };
  }));

  /* ---- status change ---------------------------------------------- */
  router.post('/api/assets/:id/status', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const a = L.loadAsset(ctx.db, id);
    checkVersion(a.version, Number(i.version));
    L.checkEditable(ctx.db, a);
    const { date, reason, reference } = L.validateTransition(ctx.db, a, i);
    if (require('../lib/core').Terminal.includes(i.status)) {
      throw new ApiError(409, 'برای فروش، اسقاط یا خروج ابتدا درخواست مجوز ثبت و تأیید مدیر دیگر را دریافت کنید.');
    }
    const before = L.assetSnapshot(a);
    ctx.db.prepare(`UPDATE assets SET status = ?, lastOperationDate = ?, version = version + 1, updatedAt = ? WHERE id = ?`)
      .run(i.status, dateOnlyIso(date), nowIso(), id);
    const updated = ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(id);
    http.logAudit(ctx.db, ctx, 'status', 'Asset', id,
      'تغییر وضعیت «' + a.name + '» به «' + require('../lib/core').stateFa(i.status) + '»؛ ' + reason,
      before, { asset: L.assetSnapshot(updated), effectiveDate: dateOnlyIso(date), reason, reference, amount: i.status === States.Sold ? Number(i.amount) : null });
    return { message: 'تغییر وضعیت با ثبت مجوز و سوابق انجام شد.' };
  }));

  /* ---- image upload ------------------------------------------------ */
  router.post('/api/files', authmod.requireStaff(async (ctx) => {
    if (ctx.rateLimit && !ctx.rateLimit(`upload:${ctx.user.id}`, 30, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    const file = await http.receiveFile(ctx);
    if (!file) throw new ApiError(400, 'فایل تصویر را انتخاب کنید.');
    if (file.buffer.length > 5 * 1024 * 1024) throw new ApiError(400, 'حداکثر اندازهٔ تصویر ۵ مگابایت است.');

    const { ext, contentType } = detectImage(file.buffer);
    const imgId = guid() + ext;
    require('node:fs').writeFileSync(require('node:path').join(ctx.storage.uploads, imgId), file.buffer);
    ctx.db.prepare('INSERT INTO images (id, contentType, ownerUserId, createdAt, size) VALUES (?,?,?,?,?)')
      .run(imgId, contentType, ctx.user.id, nowIso(), file.buffer.length);
    http.logAudit(ctx.db, ctx, 'upload', 'Image', imgId, 'بارگذاری تصویر اموال',
      undefined, { contentType, size: file.buffer.length });
    return { id: imgId, url: '/api/files/' + imgId };
  }));

  /* ---- image retrieval -------------------------------------------- */
  router.get('/api/files/:id', authmod.requireAuth(async (ctx) => {
    const id = ctx.params.id;
    if (!/^[a-f0-9]{32}\.(png|jpg|webp)$/.test(id)) throw new ApiError(404, 'تصویر پیدا نشد.');
    const image = ctx.db.prepare('SELECT * FROM images WHERE id = ?').get(id);
    if (!image) throw new ApiError(404, 'تصویر پیدا نشد.');
    const staff = authmod.isStaff(ctx);
    if (!staff) {
      const owns = ctx.db.prepare(`SELECT COUNT(*) AS c FROM assets WHERE deleted = 0 AND imageId = ? AND EXISTS (SELECT 1 FROM assignments x WHERE x.assetId = assets.id AND x.userId = ? AND x.endedAt IS NULL)`).get(id, ctx.user.id).c;
      if (!owns) throw new ApiError(404, 'تصویر پیدا نشد.');
    }
    const fs = require('node:fs');
    const p = require('node:path').join(ctx.storage.uploads, id);
    if (!fs.existsSync(p)) throw new ApiError(404, 'فایل تصویر در دسترس نیست.');
    http.sendFileAsync(ctx.res, p, image.contentType);
    ctx.handled = true;
  }));
}

function assignmentView(x) {
  return {
    id: x.id, assetId: x.assetId, userId: x.userId, departmentId: x.departmentId,
    userName: x.userId ? x.recipientName : null,
    departmentName: x.departmentName, startedAt: x.startedAt, endedAt: x.endedAt,
    notes: x.notes, reference: x.reference, endReason: x.endReason
  };
}

function detectImage(b) {
  if (b.length < 24) throw new ApiError(400, 'تصویر باید معتبر و حداکثر ۵ مگابایت باشد.');
  const PNG = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
  if (PNG.every((v, i) => b[i] === v)) {
    const w = b.readUInt32BE(16), h = b.readUInt32BE(20);
    if (w < 1 || h < 1 || w * h > 40000000) throw new ApiError(400, 'ابعاد تصویر PNG بیش از حد مجاز است.');
    return { ext: '.png', contentType: 'image/png' };
  }
  if (b[0] === 0xFF && b[1] === 0xD8 && b[2] === 0xFF && b[b.length - 2] === 0xFF && b[b.length - 1] === 0xD9) {
    return { ext: '.jpg', contentType: 'image/jpeg' };
  }
  if (b.toString('ascii', 0, 4) === 'RIFF' && b.toString('ascii', 8, 12) === 'WEBP') {
    return { ext: '.webp', contentType: 'image/webp' };
  }
  throw new ApiError(400, 'فقط تصویر واقعی PNG، JPEG یا WebP پذیرفته می‌شود؛ SVG و فایل اجرایی مجاز نیست.');
}

module.exports = { register, assetQuery, listAssets };
