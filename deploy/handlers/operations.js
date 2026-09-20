'use strict';
/*
 * امین اموال — /api/operations/* and disposition request flows
 * (port of OperationEndpoints.cs).
 */
const { ApiError, States, Terminal, checkVersion, clean, required, validDate, guid, nowIso, dateOnlyIso, paging } = require('../lib/core');
const http = require('../lib/http-util');
const authmod = require('../lib/auth');
const L = require('./assets-lib');

function requestView(ctx, r, asset) {
  return {
    id: r.id, assetId: r.assetId,
    assetCode: asset ? asset.code : '',
    assetName: asset ? asset.name : '',
    targetStatus: r.targetStatus, state: r.state, reason: r.reason, reference: r.reference,
    amount: r.amount, effectiveDate: r.effectiveDate, requestedBy: r.requestedBy, requesterName: r.requesterName,
    requestedAt: r.requestedAt, deciderName: r.deciderName, decidedAt: r.decidedAt, decisionNote: r.decisionNote,
    version: r.version,
    canApprove: authmod.isAdmin(ctx) && r.state === 'Pending',
    canReject: authmod.isAdmin(ctx) && r.state === 'Pending',
    canCancel: r.state === 'Pending' && (authmod.isAdmin(ctx) || r.requestedBy === ctx.user.id)
  };
}

function register(router) {
  /* ---- list (staff) ------------------------------------------------ */
  router.get('/api/operations', authmod.requireStaff(async (ctx) => {
    const q = { ...(ctx.query || {}) };
    let where = '1=1'; const params = [];
    if (q.state) {
      if (!['Pending', 'Approved', 'Rejected', 'Cancelled'].includes(q.state)) throw new ApiError(400, 'وضعیت درخواست معتبر نیست.');
      where += ' AND dr.state = ?'; params.push(q.state);
    }
    const text = clean(q.q || '');
    if (text) {
      where += ` AND (lower(a.code) LIKE ? OR lower(a.name) LIKE ? OR lower(dr.reference) LIKE ? OR lower(dr.requesterName) LIKE ?)`;
      const like = `%${text.toLowerCase()}%`;
      params.push(like, like, like, like);
    }
    const { page, size } = paging(ctx.query);
    const total = ctx.db.prepare(`SELECT COUNT(*) AS c FROM dispositionRequests dr LEFT JOIN assets a ON a.id = dr.assetId WHERE ${where}`).get(...params).c;
    const offset = (page - 1) * size;
    const rows = ctx.db.prepare(`
      SELECT dr.*, a.code AS assetCode, a.name AS assetName
      FROM dispositionRequests dr LEFT JOIN assets a ON a.id = dr.assetId
      WHERE ${where} ORDER BY dr.requestedAt DESC LIMIT ? OFFSET ?
    `).all(...params, size, offset);
    const items = rows.map(r => {
      const asset = { code: r.assetCode, name: r.assetName };
      delete r.assetCode; delete r.assetName;
      return requestView(ctx, r, asset);
    });
    http.logAudit(ctx.db, ctx, 'view.operations', 'System', null, 'مشاهدهٔ درخواست‌های اداری خروج اموال',
      undefined, { state: q.state || '', total });
    return { items, total, page, pageSize: size };
  }));

  /* ---- create disposition request (staff) -------------------------- */
  router.post('/api/assets/:id/disposition-requests', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const a = L.loadAsset(ctx.db, id);
    checkVersion(a.version, Number(i.version));
    L.checkEditable(ctx.db, a);
    if (!Terminal.includes(i.status)) throw new ApiError(400, 'درخواست مجوز فقط برای فروش، اسقاط یا خروج است.');
    const { date, reason, reference } = L.validateTransition(ctx.db, a, i);

    ctx.db.prepare('UPDATE assets SET version = version + 1, updatedAt = ? WHERE id = ?').run(nowIso(), id);
    const reqId = guid();
    const newAssetVersion = a.version + 1;
    ctx.db.prepare(`
      INSERT INTO dispositionRequests
        (id, assetId, targetStatus, originalStatus, state, reason, reference, amount,
         effectiveDate, requestedBy, requesterName, requestedAt, decidedBy, deciderName,
         decidedAt, decisionNote, assetVersion, version)
      VALUES
        (?,?,?,?,?,?,?,?,?,?,?,?,NULL,NULL,NULL,'',?,1)
    `).run(
      reqId, id, i.status, a.status, 'Pending', reason, reference,
      i.status === States.Sold ? Number(i.amount) : null,
      dateOnlyIso(date), ctx.user.id, ctx.user.fullName, nowIso(), newAssetVersion
    );
    http.logAudit(ctx.db, ctx, 'disposition.request', 'Asset', id,
      'درخواست مجوز «' + require('../lib/core').stateFa(i.status) + '» برای «' + a.name + '»',
      undefined, { requestId: reqId, targetStatus: i.status, reason, reference, amount: i.status === States.Sold ? Number(i.amount) : null, effectiveDate: dateOnlyIso(date) });
    return { id: reqId, version: 1, message: 'درخواست ثبت شد؛ تا تصمیم مدیر، این مال برای تغییر یا تخصیص قفل است.' }; // 201
  }));

  /* ---- decide (approve/reject/cancel) ------------------------------ */
  router.post('/api/operations/:id/:decision', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const decision = ctx.params.decision;
    const i = ctx.body || {};
    if (!['approve', 'reject', 'cancel'].includes(decision)) throw new ApiError(404, 'عملیات پیدا نشد.');
    const r = ctx.db.prepare('SELECT * FROM dispositionRequests WHERE id = ?').get(id);
    if (!r) throw new ApiError(404, 'درخواست پیدا نشد.');
    if (decision !== 'cancel' && !authmod.isAdmin(ctx)) throw new ApiError(403, 'تأیید یا رد فقط توسط مدیر مجاز است.');
    if (decision === 'cancel' && !authmod.isAdmin(ctx) && r.requestedBy !== ctx.user.id) throw new ApiError(403, 'فقط درخواست خودتان را می‌توانید لغو کنید.');
    if (decision === 'approve' && r.requestedBy === ctx.user.id && !authmod.isAdmin(ctx)) throw new ApiError(403, 'تأیید درخواست توسط ثبت‌کننده مجاز نیست؛ یک مدیر دیگر باید آن را بررسی کند.');
    checkVersion(r.version, Number(i.version));
    if (r.state !== 'Pending') throw new ApiError(409, 'این درخواست قبلاً تعیین تکلیف شده است.');

    const a = L.loadAsset(ctx.db, r.assetId);
    checkVersion(a.version, r.assetVersion);
    const note = required(i.note || '', 'توضیح تصمیم', 1000);
    const before = L.assetSnapshot(a);
    const newState = decision === 'approve' ? 'Approved' : decision === 'reject' ? 'Rejected' : 'Cancelled';

    ctx.db.exec('BEGIN');
    try {
      if (decision === 'approve') {
        const data = { status: r.targetStatus, date: r.effectiveDate, reason: r.reason, reference: r.reference, amount: r.amount, version: a.version + 1 };
        const v = L.validateTransition(ctx.db, a, data);
        ctx.db.prepare('UPDATE assets SET status = ?, lastOperationDate = ?, version = version + 1, updatedAt = ? WHERE id = ?')
          .run(r.targetStatus, dateOnlyIso(v.date), nowIso(), r.assetId);
        http.logAudit(ctx.db, ctx, 'status', 'Asset', a.id,
          'اجرای مجوز تأییدشده: «' + require('../lib/core').stateFa(a.status) + '» برای «' + a.name + '»',
          before, { requestId: id, asset: L.assetSnapshot(ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(a.id)), reference: r.reference, amount: r.amount, effectiveDate: r.effectiveDate, approvedBy: ctx.user.id });
      }
      ctx.db.prepare(`UPDATE dispositionRequests SET state = ?, decidedAt = ?, decidedBy = ?, deciderName = ?, decisionNote = ?, version = version + 1 WHERE id = ?`)
        .run(newState, nowIso(), ctx.user.id, ctx.user.fullName, note, id);
      http.logAudit(ctx.db, ctx, 'disposition.' + decision, 'Asset', a.id,
        'تعیین تکلیف درخواست مجوز: ' + (decision === 'approve' ? 'تأیید' : decision === 'reject' ? 'رد' : 'لغو'),
        undefined, { requestId: id, state: newState, decisionNote: note, requesterName: r.requesterName, deciderName: ctx.user.fullName });
      ctx.db.exec('COMMIT');
      return { message: decision === 'approve' ? 'مجوز تأیید و وضعیت اموال در همان تراکنش تغییر کرد.' : 'درخواست بسته شد و قفل اموال برداشته شد.' };
    } catch (e) {
      ctx.db.exec('ROLLBACK');
      throw e;
    }
  }));

  /* ---- restore archived asset (admin) ------------------------------ */
  router.post('/api/assets/:id/restore', authmod.requireAdmin(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const a = L.loadAsset(ctx.db, id, true);
    checkVersion(a.version, Number(i.version));
    if (!a.deleted) throw new ApiError(409, 'این مال بایگانی نشده است.');
    const reason = required(i.reason || '', 'علت بازگردانی', 1000);
    const before = L.assetSnapshot(a);
    ctx.db.prepare('UPDATE assets SET deleted = 0, version = version + 1, updatedAt = ? WHERE id = ?').run(nowIso(), id);
    const updated = ctx.db.prepare('SELECT * FROM assets WHERE id = ?').get(id);
    http.logAudit(ctx.db, ctx, 'restore', 'Asset', id, 'بازگردانی از بایگانی: ' + reason, before, L.assetSnapshot(updated));
    return { message: 'مال با حفظ کد، وضعیت قبلی و تمام سوابق از بایگانی بازگردانده شد.' };
  }));
}

module.exports = { register };
