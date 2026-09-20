'use strict';
/*
 * امین اموال ناواکو — helpers, validators and domain constants.
 * Faithful behavioural port of AminAmval.Api/Services/Core.cs + Models/Entities.cs.
 */
const crypto = require('node:crypto');

const Roles = Object.freeze({ Admin: 'Admin', Custodian: 'Custodian', Employee: 'Employee' });
const RolesAll = Object.freeze(['Admin', 'Custodian', 'Employee']);

const States = Object.freeze({
  Available: 'Available', Assigned: 'Assigned', Maintenance: 'Maintenance',
  Scrapped: 'Scrapped', Sold: 'Sold', Lost: 'Lost', Exited: 'Exited'
});
const StatesAll = Object.freeze(['Available', 'Assigned', 'Maintenance', 'Scrapped', 'Sold', 'Lost', 'Exited']);
const Terminal = Object.freeze(['Scrapped', 'Sold', 'Exited']);
const Qualities = Object.freeze(['New', 'Good', 'Used', 'Damaged']);

function stateFa(s) {
  switch (s) {
    case 'Available': return 'موجود در انبار';
    case 'Assigned': return 'در حال بهره‌برداری';
    case 'Maintenance': return 'در تعمیر';
    case 'Scrapped': return 'اسقاط‌شده';
    case 'Sold': return 'فروخته‌شده';
    case 'Lost': return 'مفقودشده';
    case 'Exited': return 'خارج‌شده';
    default: return s;
  }
}

class ApiError extends Error {
  constructor(status, message, errors = null) {
    super(message);
    this.status = status;
    this.errors = errors;
    this.isApi = true;
  }
}

/* ------------------------------------------------------------------ */
/* ids / timestamps                                                    */
/* ------------------------------------------------------------------ */
function guid() { return crypto.randomBytes(16).toString('hex'); } // matches ^[a-f0-9]{32}$
function nowIso() { return new Date().toISOString(); }
function dateOnlyIso(d) { // Date -> 'YYYY-MM-DD'
  return d.toISOString().slice(0, 10);
}
function isGuid(s) { return typeof s === 'string' && /^[a-f0-9]{32}$/.test(s); }

/* ------------------------------------------------------------------ */
/* text helpers (mirror of Core.Clean / Digits)                        */
/* ------------------------------------------------------------------ */
const NUM_MAP = {
  '۰': '0', '۱': '1', '۲': '2', '۳': '3', '۴': '4', '۵': '5', '۶': '6', '۷': '7', '۸': '8', '۹': '9',
  '٠': '0', '١': '1', '٢': '2', '٣': '3', '٤': '4', '٥': '5', '٦': '6', '٧': '7', '٨': '8', '٩': '9'
};
function clean(value, max = 200) {
  if (value === null || value === undefined) value = '';
  let s = String(value).trim().replace(/ي/g, 'ی').replace(/ك/g, 'ک');
  if (s.length > max) throw new ApiError(400, `طول متن نباید بیشتر از ${max} نویسه باشد.`);
  return s;
}
function digits(value) {
  const s = clean(value);
  let out = '';
  for (const ch of s) out += (ch in NUM_MAP) ? NUM_MAP[ch] : ch;
  return out;
}
function required(value, label, max = 200) {
  const s = clean(value, max);
  if (s.length === 0) throw new ApiError(400, `${label} الزامی است.`);
  return s;
}
function checkVersion(current, expected) {
  if (current !== expected) throw new ApiError(409, 'اطلاعات توسط کاربر دیگری تغییر کرده است. صفحه را تازه‌سازی و دوباره تلاش کنید.');
}

/* ------------------------------------------------------------------ */
/* validators                                                          */
/* ------------------------------------------------------------------ */
function validNationalId(s) {
  if (!/^[0-9]{10}$/.test(s) || new Set(s).size === 1) return false;
  let sum = 0;
  for (let i = 0; i < 9; i++) sum += (+s[i]) * (10 - i);
  const r = sum % 11;
  return +s[9] === (r < 2 ? r : 11 - r);
}
function username(s) {
  const v = digits(s).toLowerCase();
  if (!/^[a-z0-9][a-z0-9._-]{2,59}$/.test(v)) {
    throw new ApiError(400, 'نام کاربری باید ۳ تا ۶۰ حرف لاتین، عدد، نقطه، خط تیره یا زیرخط باشد.');
  }
  return v;
}
function strongPassword(s) {
  if (!s || s.length < 12 || s.length > 128 ||
      !/[a-z]/.test(s) || !/[A-Z]/.test(s) || !/[0-9]/.test(s) || !/[^A-Za-z0-9]/.test(s)) {
    throw new ApiError(400, 'گذرواژه باید ۱۲ تا ۱۲۸ نویسه و شامل حروف بزرگ و کوچک لاتین، عدد و نماد باشد.');
  }
}
/** Parse a date input (ISO or yyyy-MM-dd) to a UTC Date, validating range. */
function validDate(value, label) {
  let d;
  if (value === null || value === undefined || value === '') throw new ApiError(400, `${label} معتبر نیست یا در آینده است.`);
  if (typeof value === 'object' && value instanceof Date) d = new Date(value.getTime());
  else {
    const s = String(value);
    if (/^\d{4}-\d{2}-\d{2}$/.test(s)) d = new Date(s + 'T00:00:00.000Z');
    else d = new Date(s);
    if (isNaN(d.getTime())) throw new ApiError(400, `${label} معتبر نیست یا در آینده است.`);
  }
  const tehranToday = new Date(Date.now() + 3.5 * 3600 * 1000);
  if (d.getUTCFullYear() < 1900 || d.getTime() > tehranToday.setHours(23, 59, 59, 999)) {
    throw new ApiError(400, `${label} معتبر نیست یا در آینده است.`);
  }
  return d;
}

function newPassword() {
  return 'Nv!' + crypto.randomBytes(8).toString('hex').toLowerCase() + 'A7';
}

/* ------------------------------------------------------------------ */
/* persian / tehran formatting (used by print + system status)         */
/* ------------------------------------------------------------------ */
function faParts(fmtParts) {
  const o = {};
  for (const p of fmtParts) o[p.type] = p.value;
  return o;
}
function faDate(value) { // 'yyyy/MM/dd' in tehran (persian calendar)
  const d = value ? new Date(value) : new Date();
  if (isNaN(d.getTime())) return '—';
  const p = faParts(new Intl.DateTimeFormat('fa-IR', { timeZone: 'Asia/Tehran', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(d));
  return `${p.year}/${p.month}/${p.day}`;
}

/* ------------------------------------------------------------------ */
/* masking                                                             */
/* ------------------------------------------------------------------ */
function maskNationalId(v) {
  if (!v) return null;
  return '******' + v.slice(-4);
}
function filterSnapshot(query) {
  const out = {};
  for (const [k, v] of Object.entries(query || {})) {
    out[k] = (k === 'q') ? digits(v).replace(/(?<!\d)\d{10}(?!\d)/g, '[کد حساس ماسک‌شده]') : v;
  }
  return out;
}
function paging(query) {
  let page = parseInt(query.page, 10); if (!Number.isFinite(page)) page = 1; page = Math.min(Math.max(page, 1), 100000);
  let size = parseInt(query.pageSize, 10); if (!Number.isFinite(size)) size = 20; size = Math.min(Math.max(size, 1), 100);
  return { page, size };
}

module.exports = {
  Roles, RolesAll, States, StatesAll, Terminal, Qualities, stateFa,
  ApiError, guid, nowIso, dateOnlyIso, isGuid,
  clean, digits, required, checkVersion, validNationalId, username, strongPassword, validDate, newPassword,
  faDate, maskNationalId, filterSnapshot, paging
};
