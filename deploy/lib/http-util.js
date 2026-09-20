'use strict';
/*
 * امین اموال — shared router + request/response plumbing (JSON reads, responses,
 * error translation identical to Program.cs middleware). Also the /api/health
 * endpoint. Secure by default: audit trail for rejected authenticated calls.
 */
const { URL } = require('node:url');
const { ApiError, filterSnapshot } = require('./core');
const auth = require('./auth');

/* ---------------------------------------------------------------- */
/* response & body helpers                                           */
/* ---------------------------------------------------------------- */
function json(res, status, obj) {
  const body = JSON.stringify(obj);
  res.statusCode = status;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.setHeader('Cache-Control', 'no-cache, no-store');
  res.setHeader('Content-Length', Buffer.byteLength(body));
  res.end(body);
}

function sendFile(res, status, path, contentType, extraHeaders = {}) {
  const fs = require('node:fs');
  try {
    const stat = fs.statSync(path);
    if (!stat.isFile()) return json(res, 404, { message: 'فایل مورد نظر پیدا نشد.' });
    res.statusCode = status;
    res.setHeader('Content-Type', contentType);
    res.setHeader('Content-Length', stat.size);
    for (const [k, v] of Object.entries(extraHeaders)) res.setHeader(k, v);
    fs.createReadStream(path).pipe(res);
    return true;
  } catch (e) {
    json(res, 404, { message: 'فایل مورد نظر پیدا نشد.' });
    return true;
  }
}

async function readJson(req) {
  return new Promise((resolve, reject) => {
    let size = 0;
    const chunks = [];
    req.on('data', (c) => {
      size += c.length;
      if (size > 25 * 1024 * 1024) { reject(new ApiError(400, 'ساختار یا اندازهٔ درخواست معتبر نیست.')); req.destroy(); return; }
      chunks.push(c);
    });
    req.on('end', () => {
      if (chunks.length === 0) return resolve({});
      let text = Buffer.concat(chunks).toString('utf8');
      try { resolve(JSON.parse(text)); }
      catch { reject(new ApiError(400, 'ساختار داده‌های ارسالی معتبر نیست.')); }
    });
    req.on('error', (e) => reject(e instanceof ApiError ? e : new ApiError(400, 'ساختار یا اندازهٔ درخواست معتبر نیست.')));
  });
}

/* ---------------------------------------------------------------- */
/* simple fixed-window rate limiter (login + action policies)        */
/* ---------------------------------------------------------------- */
function createRateLimiter() {
  const buckets = new Map(); // key -> { count, windowStart }
  function allow(key, limit, windowMs) {
    const now = Date.now();
    const b = buckets.get(key);
    if (!b || now - b.windowStart >= windowMs) {
      buckets.set(key, { count: 1, windowStart: now });
      return true;
    }
    b.count += 1;
    if (b.count > limit) return false;
    return true;
  }
  // opportunistic cleanup
  setInterval(() => {
    const now = Date.now();
    for (const [k, b] of buckets) if (now - b.windowStart >= 15 * 60 * 1000) buckets.delete(k);
  }, 5 * 60 * 1000).unref();
  return allow;
}

/* ---------------------------------------------------------------- */
/* audit logger                                                      */
/* ---------------------------------------------------------------- */
function logAudit(db, ctx, action, type, id, description, before, after, assetId, targetUser) {
  const actor = ctx && ctx.user ? ctx.user : null;
  const ip = ctx && ctx.req ? (ctx.req.socket.remoteAddress || '') : 'system';
  const correlation = (ctx && ctx.traceId) || require('node:crypto').randomBytes(8).toString('hex');
  db.prepare(`
    INSERT INTO audit (at, actorId, actorName, actorRole, ip, action, entityType, entityId, assetId, targetUserId, description, before, after, correlationId)
    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)
  `).run(
    new Date().toISOString(),
    actor ? actor.id : null,
    actor ? actor.fullName : 'سامانه',
    actor ? actor.role : 'System',
    ip,
    action,
    type,
    id || null,
    assetId || (type === 'Asset' ? id || null : null),
    targetUser || (type === 'User' ? id || null : null),
    description,
    before === undefined || before === null ? null : JSON.stringify(before),
    after === undefined || after === null ? null : JSON.stringify(after),
    correlation
  );
  return correlation;
}

/* ---------------------------------------------------------------- */
/* tackle the "Sessions.token" full-raw storage                      */
/* ---------------------------------------------------------------- */
function recordSession(db, { id, userId, token, stamp, ip, userAgent, createdAt, lastSeenAt, expiresAt }) {
  db.prepare(`
    INSERT INTO sessions (id, userId, token, stamp, createdAt, lastSeenAt, expiresAt, revokedAt, ip, userAgent)
    VALUES (?,?,?,?,?,?,?,NULL,?,?)
  `).run(id, userId, token, stamp, createdAt, lastSeenAt, expiresAt, ip || '', userAgent || '');
}

/* ---------------------------------------------------------------- */
/* wrapping helpers for endpoint definition                          */
/* ---------------------------------------------------------------- */

/** Wrap an async endpoint handler with error translation. */
function route(fn) {
  return async (req, res, ctx) => {
    try {
      const result = await fn(ctx);
      if (result !== undefined && !res.writableEnded) {
        if (Array.isArray(result)) json(res, 200, result);
        else json(res, 200, result);
      }
    } catch (e) {
      throw e;
    }
  };
}

/* ---------------------------------------------------------------- */
/* multipart/file helpers (for /api/files and /api/import)           */
/* ---------------------------------------------------------------- */
function parseBoundary(ct) {
  const m = /boundary=(?:"([^"]+)"|([^;]+))/i.exec(ct || '');
  return m ? (m[1] || m[2]).trim() : null;
}

async function receiveMultipart(req, maxBytes = 21 * 1024 * 1024) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    let done = false;
    req.on('data', (c) => {
      size += c.length;
      if (size > maxBytes) { done = true; reject(new ApiError(400, 'ساختار یا اندازهٔ درخواست معتبر نیست.')); req.destroy(); return; }
      chunks.push(c);
    });
    req.on('end', () => { if (done) return; resolve(Buffer.concat(chunks)); });
    req.on('error', (e) => reject(e instanceof ApiError ? e : new ApiError(400, 'ساختار یا اندازهٔ درخواست معتبر نیست.')));
  });
}

function parseMultipart(buffer, boundary) {
  const fields = Object.create(null);
  const files = [];
  if (!boundary || buffer.length === 0) return { fields, files };
  const delim = Buffer.from('--' + boundary);
  const parts = [];
  let start = 0;
  while (true) {
    const idx = buffer.indexOf(delim, start);
    if (idx === -1) break;
    const end = buffer.indexOf(delim, idx + delim.length);
    if (end === -1) break;
    parts.push(buffer.subarray(idx + delim.length, end));
    start = end + delim.length;
    // stop at closing delimiter
    const after = buffer.subarray(start, start + 2).toString();
    if (after === '--') break;
  }
  for (const part of parts) {
    const headerEnd = part.indexOf('\r\n\r\n');
    if (headerEnd === -1) continue;
    const head = part.subarray(0, headerEnd).toString('utf8');
    let body = part.subarray(headerEnd + 4);
    if (body.length >= 2 && body[body.length - 2] === 0x0d && body[body.length - 1] === 0x0a) {
      body = body.subarray(0, body.length - 2);
    }
    const nameMatch = /name="([^"]*)"/.exec(head);
    if (!nameMatch) continue;
    const key = nameMatch[1];
    const filenameMatch = /filename="([^"]*)"/.exec(head);
    if (filenameMatch) {
      const ctMatch = /content-type:\s*([^\r\n]+)/i.exec(head);
      const item = { field: key, filename: filenameMatch[1], contentType: ctMatch ? ctMatch[1].trim() : 'application/octet-stream', buffer: body };
      files.push(item);
      if (fields[key] === undefined) fields[key] = item;
    } else {
      let val = body.toString('utf8');
      if (fields[key] === undefined) fields[key] = val;
      else if (Array.isArray(fields[key])) fields[key].push(val);
      else fields[key] = [fields[key], val];
    }
  }
  return { fields, files };
}

async function receiveFile(ctx, maxBytes) {
  const ct = ctx.req.headers['content-type'] || '';
  if (!/multipart\/form-data/i.test(ct)) {
    const body = await readJson(ctx.req);
    return null;
  }
  const boundary = parseBoundary(ct);
  if (!boundary) throw new ApiError(400, 'فایل تصویر را انتخاب کنید.');
  const buf = await receiveMultipart(ctx.req, maxBytes);
  const parsed = parseMultipart(buf, boundary);
  const file = parsed.files.find(f => f.field === 'file');
  return file ? { buffer: file.buffer, filename: file.filename, contentType: file.contentType } : null;
}

function sendFileAsync(res, filePath, contentType) {
  const fs = require('node:fs');
  fs.stat(filePath, (err, stat) => {
    if (err || !stat.isFile()) { json(res, 404, { message: 'فایل مورد نظر پیدا نشد.' }); return; }
    res.statusCode = 200;
    res.setHeader('Content-Type', contentType);
    res.setHeader('Content-Length', stat.size);
    fs.createReadStream(filePath).pipe(res);
  });
}

module.exports = {
  json, sendFile, readJson, createRateLimiter, logAudit, recordSession, route,
  receiveMultipart, parseMultipart, receiveFile, sendFileAsync,
  auth
};
