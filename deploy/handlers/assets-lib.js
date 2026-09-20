'use strict';
/*
 * امین اموال — /api/assets/* endpoints (port of AssetEndpoints.cs).
 */
const { ApiError, States, StatesAll, Terminal, Qualities, checkVersion, clean, digits, required, validDate, guid, nowIso, dateOnlyIso } = require('../lib/core');
const http = require('../lib/http-util');
const authmod = require('../lib/auth');

/* ---------------- row hydration ---------------------------------- */
function hydrateDept(db, id) { return id ? (db.prepare('SELECT * FROM departments WHERE id = ?').get(id) || null) : null; }
function hydrateCategory(db, id) { return db.prepare('SELECT * FROM categories WHERE id = ?').get(id) || null; }

function currentAssignment(db, assetId) {
  return db.prepare(`
    SELECT a.*, u.departmentId AS userDeptId
    FROM assignments a
    LEFT JOIN users u ON u.id = a.userId
    WHERE a.assetId = ? AND a.endedAt IS NULL
  `).get(assetId) || null;
}

function pendingRequest(db, assetId) {
  return db.prepare(`SELECT * FROM dispositionRequests WHERE assetId = ? AND state = 'Pending'`).get(assetId) || null;
}

function assignmentView(x) {
  return {
    id: x.id, assetId: x.assetId, userId: x.userId, departmentId: x.departmentId,
    userName: x.userId ? x.recipientName : null,
    departmentName: x.departmentName,
    startedAt: x.startedAt, endedAt: x.endedAt, notes: x.notes,
    reference: x.reference, endReason: x.endReason
  };
}

function assetSnapshot(a) {
  return {
    id: a.id, code: a.code, oldCode: a.oldCode, name: a.name, categoryId: a.categoryId,
    brand: a.brand, model: a.model, serial: a.serial, quality: a.quality, owner: a.owner,
    description: a.description, imageId: a.imageId, purchaseDate: a.purchaseDate,
    purchaseCost: a.purchaseCost, hasLabel: !!a.hasLabel, status: a.status, location: a.location,
    deleted: !!a.deleted, version: a.version
  };
}

function assetView(db, a, staff) {
  const current = currentAssignment(db, a.id);
  const pending = pendingRequest(db, a.id);
  const cat = hydrateCategory(db, a.categoryId);
  return {
    id: a.id, code: a.code, oldCode: a.oldCode, name: a.name, categoryId: a.categoryId,
    categoryName: cat ? cat.name : null,
    brand: a.brand, model: a.model, serial: a.serial, quality: a.quality, owner: a.owner,
    description: a.description, imageId: a.imageId, purchaseDate: a.purchaseDate,
    purchaseCost: staff ? a.purchaseCost : null, hasLabel: !!a.hasLabel, status: a.status,
    location: a.location, deleted: !!a.deleted, pendingRequestId: pending ? pending.id : null,
    version: a.version, createdAt: a.createdAt, updatedAt: a.updatedAt,
    currentAssignment: current ? assignmentView(current) : null
  };
}

function loadAsset(db, id, includeArchived = false) {
  const row = db.prepare(`SELECT * FROM assets WHERE id = ? AND (deleted = 0 OR ? = 1)`)
    .get(id, includeArchived ? 1 : 0);
  if (!row) throw new ApiError(404, 'اموال مورد نظر پیدا نشد.');
  return row;
}

function checkEditable(db, a) {
  if (a.deleted) throw new ApiError(409, 'ابتدا مال را از بایگانی بازگردانید.');
  if (pendingRequest(db, a.id)) {
    throw new ApiError(409, 'این مال درخواست مجوز در انتظار دارد؛ ابتدا درخواست را تعیین تکلیف کنید.');
  }
}

function verifyCsrf(ctx) {
  const token = ctx.req.headers['x-csrf-token'];
  if (!token || !ctx.csrf.isValid(token)) {
    throw new ApiError(400, 'نشست امنیتی معتبر نیست. صفحه را تازه‌سازی کنید.');
  }
}

/* ---------------- asset validation (port of AssetService.Apply) ---- */
function applyAsset(db, a, i, { importedImage = false } = {}) {
  const codeRaw = digits((i.code || '').toUpperCase());
  let code = codeRaw;
  if (code === '') code = 'NV-' + new Date().toISOString().slice(2, 8).replace(/-/g, '') + '-' + guid().slice(0, 16).toUpperCase();
  else code = required(code, 'کد اموال', 60);
  const exists = db.prepare('SELECT id FROM assets WHERE code = ? AND id <> ?').get(code, a.id || '');
  if (exists) throw new ApiError(409, 'کد اموال تکراری است؛ کدهای بایگانی‌شده نیز قابل استفادهٔ مجدد نیستند.');

  const oldCode = clean(digits(i.oldCode || ''), 60);
  const name = required(i.name, 'نام اموال');
  const categoryId = required(i.categoryId, 'دسته‌بندی');
  if (a.id) {
    if (!db.prepare('SELECT id FROM categories WHERE id = ?').get(categoryId)) {
      throw new ApiError(400, 'دسته‌بندی معتبر را انتخاب کنید.');
    }
  }
  const brand = clean(i.brand || '', 100);
  const model = clean(i.model || '', 100);
  if (brand === '' && model === '') throw new ApiError(400, 'حداقل یکی از برند یا مدل الزامی است.');
  const serial = clean(digits(i.serial || ''), 100);
  if (!Qualities.includes(i.quality)) throw new ApiError(400, 'کیفیت اموال معتبر نیست.');
  const owner = required(i.owner || '', 'مالک اموال', 150);
  const description = required(i.description || '', 'توضیحات', 2000);
  if (!importedImage && !db.prepare('SELECT id FROM images WHERE id = ?').get(i.imageId || '')) {
    throw new ApiError(400, 'بارگذاری تصویر اموال الزامی است.');
  }
  const imageId = required(i.imageId || '', 'تصویر', 100);

  let purchaseDate = null;
  if (i.purchaseDate) {
    const d = validDate(i.purchaseDate, 'تاریخ خرید');
    purchaseDate = dateOnlyIso(d);
  }
  if (purchaseDate) {
    const lastOp = a.lastOperationDate;
    if (lastOp && purchaseDate > dateOnlyIso(new Date(lastOp))) {
      throw new ApiError(400, 'تاریخ خرید نمی‌تواند بعد از آخرین عملیات اموال باشد.');
    }
    const firstAssign = a.id ? db.prepare('SELECT startedAt FROM assignments WHERE assetId = ? ORDER BY startedAt ASC LIMIT 1').get(a.id) : null;
    if (firstAssign && dateOnlyIso(new Date(firstAssign.startedAt)) < purchaseDate) {
      throw new ApiError(400, 'تاریخ خرید نمی‌تواند بعد از اولین تخصیص باشد.');
    }
  }
  let cost = i.purchaseCost === undefined || i.purchaseCost === null || i.purchaseCost === '' ? null : Number(i.purchaseCost);
  if (cost !== null && (!Number.isFinite(cost) || cost < 0 || cost > 1000000000000000)) {
    throw new ApiError(400, 'ارزش خرید باید بین صفر و ۱٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰ ریال باشد.');
  }

  return {
    code, oldCode, name, categoryId, brand, model, serial,
    quality: i.quality, owner, description, imageId, purchaseDate, purchaseCost: cost,
    hasLabel: !!i.hasLabel, location: clean(i.location || '')
  };
}

function validateTransition(db, a, input) {
  if (a.status === States.Assigned) throw new ApiError(409, 'پیش از تغییر وضعیت، اموال را عودت دهید.');
  if (Terminal.includes(a.status)) throw new ApiError(409, 'وضعیت خروج، اسقاط و فروش نهایی است.');
  if (!StatesAll.includes(input.status) || input.status === States.Assigned || input.status === a.status ||
      (input.status === States.Available && !(a.status === States.Maintenance || a.status === States.Lost))) {
    throw new ApiError(400, 'تغییر وضعیت مجاز نیست.');
  }
  const date = validDate(input.date, 'تاریخ عملیات');
  checkChronology(db, a, date);
  const reason = required(input.reason || '', 'علت / شرح عملیات', 1000);
  const reference = required(input.reference || '', 'شمارهٔ مجوز یا صورت‌جلسه', 200);
  if (input.status === States.Sold) {
    const amount = Number(input.amount);
    if (!Number.isFinite(amount) || amount <= 0 || amount > 1000000000000000) {
      throw new ApiError(400, 'مبلغ مثبت فروش به ریال الزامی است.');
    }
  }
  return { date, reason, reference };
}

function checkChronology(db, a, date) {
  const day = dateOnlyIso(date);
  if (a.lastOperationDate && day < dateOnlyIso(new Date(a.lastOperationDate))) {
    throw new ApiError(400, 'تاریخ عملیات نمی‌تواند قبل از آخرین رویداد اموال باشد.');
  }
  if (a.purchaseDate && day < dateOnlyIso(new Date(a.purchaseDate))) {
    throw new ApiError(400, 'تاریخ عملیات نمی‌تواند قبل از تاریخ خرید باشد.');
  }
  const maxEnd = a.id ? db.prepare('SELECT MAX(endedAt) AS m FROM assignments WHERE assetId = ?').get(a.id).m : null;
  if (maxEnd && day < dateOnlyIso(new Date(maxEnd))) {
    throw new ApiError(400, 'تاریخ عملیات نمی‌تواند قبل از آخرین عودت یا انتقال باشد.');
  }
}

module.exports = {
  assetView, assetSnapshot, loadAsset, currentAssignment, pendingRequest, checkEditable,
  applyAsset, validateTransition, checkChronology, verifyCsrf,
  hydrateCategory, hydrateDept
};
