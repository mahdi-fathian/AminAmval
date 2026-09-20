'use strict';
/*
 * امین اموال — /api/auth/* endpoints (port of AuthEndpoints.cs).
 */
const { ApiError, strongPassword, digits, clean, guid, nowIso } = require('../lib/core');
const { hashPassword, verifyPassword } = require('../lib/password');
const { makeToken } = require('../lib/session');
const http = require('../lib/http-util');
const authmod = require('../lib/auth');

function publicUser(u, deptName) {
  return {
    id: u.id,
    username: u.username,
    personnelCode: u.personnelCode,
    firstName: u.firstName,
    lastName: u.lastName,
    fullName: u.firstName + ' ' + u.lastName,
    nationalId: u.nationalId,
    departmentId: u.departmentId,
    departmentName: deptName || undefined,
    role: u.role,
    active: !!u.active,
    mustChangePassword: !!u.mustChangePassword,
    version: u.version,
    createdAt: u.createdAt
  };
}

function deptName(db, id) {
  if (!id) return null;
  const r = db.prepare('SELECT name FROM departments WHERE id = ?').get(id);
  return r ? r.name : null;
}

function register(router) {
  /* ---- CSRF minting -------------------------------------------------- */
  router.get('/api/auth/csrf', (ctx) => {
    const pair = ctx.csrf.generate();
    ctx.res.setHeader('Set-Cookie', authmod.buildCsrfCookie(pair.cookie, ctx.settings));
    return { token: pair.token };
  });

  /* ---- login ---------------------------------------------------------- */
  router.post('/api/auth/login', async (ctx) => {
    const input = ctx.body || {};
    const usernameRaw = digits(input.username || '').toLowerCase();
    if (!input.password || String(input.password).length > 128) {
      throw new ApiError(400, 'نام کاربری و گذرواژه را وارد کنید.');
    }
    if (ctx.rateLimit && !ctx.rateLimit(`login:${ctx.req.socket.remoteAddress || '?'}`, 30, 15 * 60 * 1000)) {
      ctx.res.statusCode = 429;
      ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }

    const u = ctx.db.prepare('SELECT * FROM users WHERE username = ?').get(usernameRaw);

    // constant-ish dummy verify to reduce username enumeration timing signal
    if (!u) { hashPassword(input.password); }

    const lockExpired = u && (u.lockoutUntil === null || u.lockoutUntil === undefined || new Date(u.lockoutUntil).getTime() <= Date.now());
    if (!u || !verifyPassword(u.passwordHash, input.password) || !u.active || (u.lockoutUntil && !lockExpired)) {
      if (u && u.active) {
        if (u.lockoutUntil && lockExpired) {
          ctx.db.prepare('UPDATE users SET lockoutUntil = NULL, failedLogins = 0 WHERE id = ?').run(u.id);
        } else if (!u.lockoutUntil) {
          const failed = u.failedLogins + 1;
          const lock = failed >= 5 ? new Date(Date.now() + 15 * 60 * 1000).toISOString() : null;
          ctx.db.prepare('UPDATE users SET failedLogins = ?, lockoutUntil = ?, version = version + 1 WHERE id = ?').run(failed, lock, u.id);
        }
      }
      http.logAudit(ctx.db, ctx, 'login.failed', 'User', u ? u.id : null,
        'ورود ناموفق یا حساب غیرفعال / موقتاً قفل‌شده: ' + clean(usernameRaw, 80));
      throw new ApiError(401, 'اطلاعات ورود نادرست است یا حساب غیرفعال / موقتاً قفل شده است. پس از ۵ تلاش ناموفق، ۱۵ دقیقه صبر کنید.');
    }

    ctx.db.prepare('UPDATE users SET failedLogins = 0, lockoutUntil = NULL WHERE id = ?').run(u.id);

    const sessionId = guid();
    const token = makeToken();
    const now = new Date().toISOString();
    const expiresAt = new Date(Date.now() + 8 * 3600 * 1000).toISOString();
    const userAgent = String(ctx.req.headers['user-agent'] || '').slice(0, 300);
    const ip = ctx.req.socket.remoteAddress || '';
    http.recordSession(ctx.db, {
      id: sessionId, userId: u.id, token, stamp: u.securityStamp,
      ip, userAgent, createdAt: now, lastSeenAt: now, expiresAt
    });

    // hydrate in-memory session registry
    const ref = { id: sessionId, userId: u.id, token, expiresAt, lastSeenAt: now, createdAt: now };
    if (typeof ctx.sessions.attach === 'function') await ctx.sessions.attach(ctx.db, ref);

    ctx.res.setHeader('Set-Cookie', authmod.buildSessionCookie(ctx.req, u.id, sessionId, token, ctx.settings));

    // Build an authenticated context for the subsequent audit write
    ctx.user = { id: u.id, role: u.role, fullName: `${u.firstName} ${u.lastName}`, username: u.username };
    http.logAudit(ctx.db, ctx, 'login', 'User', u.id, 'ورود موفق به سامانه');

    return publicUser(u, deptName(ctx.db, u.departmentId));
  });

  /* ---- sessions list -------------------------------------------------- */
  router.get('/api/auth/sessions', authmod.requireAuth(async (ctx) => {
    const now = Date.now();
    const rows = ctx.db.prepare(`
      SELECT * FROM sessions
      WHERE userId = ? AND revokedAt IS NULL AND expiresAt > ? AND stamp = ?
      ORDER BY lastSeenAt DESC
    `).all(ctx.user.id, new Date(now).toISOString(), ctx.currentStamp);

    const items = rows.map(r => ({
      id: r.id,
      createdAt: r.createdAt,
      lastSeenAt: r.lastSeenAt,
      expiresAt: r.expiresAt,
      ip: r.ip,
      userAgent: r.userAgent,
      current: r.id === ctx.user.sessionId
    }));
    http.logAudit(ctx.db, ctx, 'session.view', 'User', ctx.user.id, 'مشاهدهٔ نشست‌های ورود خود');
    return { items };
  }));

  /* ---- revoke one session -------------------------------------------- */
  router.post('/api/auth/sessions/:id/revoke', authmod.requireAuth(async (ctx) => {
    const sid = ctx.params.id;
    const row = ctx.db.prepare('SELECT * FROM sessions WHERE id = ? AND userId = ?').get(sid, ctx.user.id);
    if (!row) throw new ApiError(404, 'نشست پیدا نشد.');
    if (sid === ctx.user.sessionId) throw new ApiError(400, 'برای پایان نشست فعلی از خروج استفاده کنید.');
    ctx.db.prepare('UPDATE sessions SET revokedAt = ? WHERE id = ?').run(new Date().toISOString(), sid);
    if (typeof ctx.sessions.revokeOne === 'function') await ctx.sessions.revokeOne(ctx.db, ctx.user.id, sid);
    http.logAudit(ctx.db, ctx, 'session.revoke', 'User', ctx.user.id, 'ابطال یکی از نشست‌های دیگر حساب',
      undefined, { ip: row.ip, userAgent: row.userAgent });
    return { message: 'نشست انتخاب‌شده باطل شد.' };
  }));

  /* ---- me ------------------------------------------------------------- */
  router.get('/api/auth/me', authmod.requireAuth(async (ctx) => {
    const u = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(ctx.user.id);
    if (!u) { throw new ApiError(401, 'برای ادامه وارد سامانه شوید.'); }
    return publicUser(u, deptName(ctx.db, u.departmentId));
  }));

  /* ---- logout --------------------------------------------------------- */
  router.post('/api/auth/logout', authmod.requireAuth(async (ctx) => {
    const sid = ctx.user.sessionId;
    ctx.db.prepare('UPDATE sessions SET revokedAt = ? WHERE id = ? AND userId = ?')
      .run(new Date().toISOString(), sid, ctx.user.id);
    if (typeof ctx.sessions.revokeOne === 'function') await ctx.sessions.revokeOne(ctx.db, ctx.user.id, sid);
    ctx.res.setHeader('Set-Cookie', authmod.serializeCookie(authmod.SESSION_COOKIE, '', {
      httpOnly: true, sameSite: ctx.settings.preview ? 'None' : 'Lax',
      secure: ctx.settings.preview ? true : (ctx.settings.secureCookies || false), maxAge: 0
    }));
    http.logAudit(ctx.db, ctx, 'logout', 'User', ctx.user.id, 'خروج از سامانه');
    return { message: 'با موفقیت خارج شدید.' };
  }));

  /* ---- change own password ------------------------------------------- */
  router.post('/api/auth/password', authmod.requireAuth(async (ctx) => {
    const input = ctx.body || {};
    strongPassword(input.newPassword);
    if (ctx.rateLimit && !ctx.rateLimit(`password:${ctx.user.id}`, 10, 60 * 1000)) {
      ctx.res.statusCode = 429; ctx.res.setHeader('Retry-After', '60');
      throw new ApiError(429, 'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.');
    }
    const u = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(ctx.user.id);
    if (!u) throw new ApiError(401, 'برای ادامه وارد سامانه شوید.');
    if (!input.currentPassword || String(input.currentPassword).length > 128 ||
        !verifyPassword(u.passwordHash, input.currentPassword)) {
      http.logAudit(ctx.db, ctx, 'password.failed', 'User', u.id, 'تلاش ناموفق برای تغییر گذرواژه');
      throw new ApiError(400, 'گذرواژهٔ فعلی صحیح نیست.');
    }
    if (input.newPassword === input.currentPassword || input.newPassword === u.nationalId) {
      throw new ApiError(400, 'گذرواژهٔ جدید باید با گذرواژهٔ فعلی و کد ملی متفاوت باشد.');
    }
    const newStamp = guid();
    ctx.db.prepare(`UPDATE users SET passwordHash = ?, mustChangePassword = 0, securityStamp = ?, version = version + 1 WHERE id = ?`)
      .run(hashPassword(input.newPassword), newStamp, u.id);
    // revoke all existing sessions
    ctx.db.prepare('UPDATE sessions SET revokedAt = ? WHERE userId = ? AND revokedAt IS NULL').run(new Date().toISOString(), u.id);
    if (typeof ctx.sessions.revoke === 'function') await ctx.sessions.revoke(ctx.db, u.id);

    // create replacement session
    const sessionId = guid();
    const token = makeToken();
    const now = new Date().toISOString();
    const expiresAt = new Date(Date.now() + 8 * 3600 * 1000).toISOString();
    http.recordSession(ctx.db, {
      id: sessionId, userId: u.id, token, stamp: newStamp,
      ip: ctx.req.socket.remoteAddress || '', userAgent: String(ctx.req.headers['user-agent'] || '').slice(0, 300),
      createdAt: now, lastSeenAt: now, expiresAt
    });
    await ctx.sessions.attach(ctx.db, { id: sessionId, userId: u.id, token, expiresAt, lastSeenAt: now, createdAt: now });
    ctx.res.setHeader('Set-Cookie', authmod.buildSessionCookie(ctx.req, u.id, sessionId, token, ctx.settings));
    ctx.user = { ...ctx.user, sessionId, mustChangePassword: false };
    http.logAudit(ctx.db, ctx, 'password.change', 'User', u.id, 'تغییر گذرواژه و ابطال نشست‌های قبلی');
    const fresh = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(u.id);
    return publicUser(fresh, deptName(ctx.db, fresh.departmentId));
  }));
}

module.exports = { register, publicUser, deptName };
