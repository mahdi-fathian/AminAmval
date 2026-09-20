'use strict';
/*
 * امین اموال — HTTP helpers: cookies, auth/authorization middleware,
 * CspDefaults and common API plumbing. Ports the security posture of Program.cs:
 * Secure + SameSite=None cookies (HTTPS preview), HttpOnly session cookie,
 * cookie-name anti-tamper audit, and role/attribute-gated endpoints.
 */
const crypto = require('node:crypto');
const { ApiError } = require('./core');

const SESSION_COOKIE = 'Amin.Session';
const CSRF_COOKIE = 'Amin.Csrf';

function parseCookies(req) {
  const out = {};
  const raw = req.headers.cookie;
  if (!raw) return out;
  for (const part of raw.split(';')) {
    const idx = part.indexOf('=');
    if (idx === -1) continue;
    const k = part.slice(0, idx).trim();
    let v = part.slice(idx + 1).trim();
    try { v = decodeURIComponent(v); } catch { /* keep raw */ }
    out[k] = v;
  }
  return out;
}

function serializeCookie(name, value, { httpOnly = true, sameSite = 'None', secure = true, maxAge = 0, path = '/' } = {}) {
  let c = `${name}=${encodeURIComponent(value)}; Path=${path}; SameSite=${sameSite}`;
  if (maxAge > 0) c += `; Max-Age=${maxAge}`;
  if (secure) c += '; Secure';
  if (httpOnly) c += '; HttpOnly';
  return c;
}

/**
 * Parse request, authoritatively validate the session against SQLite using the
 * exact Program.cs OnValidatePrincipal rules, and attach ctx.user / ctx.session.
 */
function parseAuth(req, db, sessions, logger) {
  const cookies = parseCookies(req);
  const raw = cookies[SESSION_COOKIE];
  if (!raw) return null;
  const parts = raw.split('.'); // userId.sessionId.token
  if (parts.length !== 3) return null;
  const [userId, sessionId, token] = parts;
  if (!/^[a-f0-9]{32}$/.test(userId) || !/^[a-f0-9]{32}$/.test(sessionId) || !/^[a-f0-9]{64}$/.test(token)) return null;

  if (typeof sessions.isValid === 'function') {
    let ok = sessions.isValid(userId, sessionId);
    if (ok) {
      const user = getUser(db, userId);
      if (user) { sessions.touch(db, userId, sessionId); return buildPrincipal(user, sessionId); }
    }
    // Try to write-through from DB (e.g. cold cache after restart).
    sessions.hydrate(db, userId);
  }

  const user = getUser(db, userId);
  if (!user) return null;
  const session = db.prepare(
    `SELECT * FROM sessions WHERE id = ? AND userId = ? AND revokedAt IS NULL`
  ).get(sessionId, userId);
  if (!session) return null;

  const now = Date.now();
  const expiresAt = new Date(session.expiresAt).getTime();
  if (session.token !== token) return null;
  if (session.stamp !== user.securityStamp) return null;
  if (Number.isNaN(expiresAt) || expiresAt <= now) return null;

  const lastSeen = new Date(session.lastSeenAt).getTime();
  if (!Number.isNaN(lastSeen) && now - lastSeen > 5 * 60 * 1000) {
    const nextLast = new Date().toISOString();
    const nextExp = new Date(now + 8 * 3600 * 1000).toISOString();
    db.prepare(`UPDATE sessions SET lastSeenAt = ?, expiresAt = ? WHERE id = ?`).run(nextLast, nextExp, sessionId);
    session.expiresAt = nextExp;
  }
  if (typeof sessions.attachRefreshed === 'function') sessions.attachRefreshed(userId, session, token);
  return buildPrincipal(user, sessionId);
}

function getUser(db, id) {
  return db.prepare(`SELECT * FROM users WHERE id = ?`).get(id) || null;
}

function buildPrincipal(user, sessionId) {
  return {
    id: user.id,
    username: user.username,
    role: user.role,
    fullName: `${user.firstName} ${user.lastName}`,
    mustChangePassword: !!user.mustChangePassword,
    active: !!user.active,
    departmentId: user.departmentId,
    securityStamp: user.securityStamp,
    sessionId
  };
}

function isAdmin(ctx) { return !!ctx.user && ctx.user.role === 'Admin'; }
function isStaff(ctx) { return !!ctx.user && (ctx.user.role === 'Admin' || ctx.user.role === 'Custodian'); }

/** Express-style guard creators used by the router definitions. */
function requireAuth(handler) {
  return async function (ctx, next) {
    const principal = parseAuth(ctx.req, ctx.db, ctx.sessions, ctx.logger);
    if (!principal) throw new ApiError(401, 'برای ادامه وارد سامانه شوید.');
    ctx.user = principal;
    ctx.currentStamp = principal.securityStamp;
    return handler(ctx, next);
  };
}

function requireStaff(handler) {
  return requireAuth(async (ctx, next) => {
    if (!isStaff(ctx)) throw new ApiError(403, 'شما اجازهٔ انجام این عملیات را ندارید.');
    return handler(ctx, next);
  });
}

function requireAdmin(handler) {
  return requireAuth(async (ctx, next) => {
    if (!isAdmin(ctx)) throw new ApiError(403, 'شما اجازهٔ انجام این عملیات را ندارید.');
    return handler(ctx, next);
  });
}

/* ------------------------------------------------------------------ */
/* Security headers                                                    */
/* ------------------------------------------------------------------ */
function applyHeaders(res, { preview, isApi }) {
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Referrer-Policy', 'same-origin');
  res.setHeader('Permissions-Policy', 'camera=(), microphone=(), geolocation=()');
  const ancestors = preview ? '*' : "'self'";
  res.setHeader('Content-Security-Policy',
    `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors ${ancestors};`);
  if (isApi) {
    res.setHeader('Cache-Control', 'no-cache, no-store');
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
  }
  // Note: HSTS is omitted because the preview terminates TLS at the proxy.
  res.removeHeader('X-Powered-By');
  res.removeHeader('Server');
}

function buildSessionCookie(req, userId, sessionId, token, settings) {
  return serializeCookie(
    SESSION_COOKIE,
    `${userId}.${sessionId}.${token}`,
    {
      httpOnly: true,
      sameSite: settings.preview ? 'None' : 'Lax',
      secure: settings.preview ? true : (settings.secureCookies ?? false),
      maxAge: 8 * 3600,
      path: '/'
    }
  );
}

function buildCsrfCookie(value, settings) {
  return serializeCookie(
    CSRF_COOKIE,
    value,
    {
      httpOnly: true,
      sameSite: settings.preview ? 'None' : 'Lax',
      secure: settings.preview ? true : (settings.secureCookies ?? false),
      path: '/'
    }
  );
}

module.exports = {
  SESSION_COOKIE, CSRF_COOKIE,
  parseCookies, serializeCookie, parseAuth, getUser, buildPrincipal,
  isAdmin, isStaff, requireAuth, requireStaff, requireAdmin,
  applyHeaders, buildSessionCookie, buildCsrfCookie
};
