'use strict';
/*
 * امین اموال ناواکو — Node.js/SQLite runtime port of the AminAmval ASP.NET Core API.
 *
 * Serves:
 *   - the original SPA in ../src/AminAmval.Api/wwwroot (default files + SPA fallback)
 *   - the full /api/* contract (auth, assets, operations, users, references,
 *     reports, excel export, xlsx import, print, system, backups)
 *
 * DB: SQLite at $DATA_DIR/amin.db (default ../src/AminAmval.Api/runtime-data).
 * The database starts EMPTY; bootstrap creates ONLY the two mandatory accounts
 * admin (Admin) and jamdar (Custodian) with strong passwords from env or random.
 */
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { URL } = require('node:url');

const core = require('./lib/core');
const { openDatabase } = require('./lib/db');
const { hashPassword } = require('./lib/password');
const { SessionManager, makeToken } = require('./lib/session');
const { CsrfService } = require('./lib/csrf');
const http = require('./lib/http-util');
const authmod = require('./lib/auth');

/* ------------------------------------------------------------------ */
/* optional startup log file                                           */
/* When LOG_FILE is set (the Windows launcher sets it to startup-log.txt) */
/* every console line is also appended to that file, so a failure that   */
/* closes the window instantly can still be diagnosed afterwards.        */
/* ------------------------------------------------------------------ */
if (process.env.LOG_FILE) {
  try {
    const logFd = fs.openSync(process.env.LOG_FILE, 'a');
    const writeLog = (s) => { try { fs.writeSync(logFd, s); } catch { /* ignore */ } };
    const wrap = (stream) => {
      const orig = stream.write.bind(stream);
      stream.write = (chunk, enc, cb) => {
        try { writeLog(Buffer.isBuffer(chunk) ? chunk.toString('utf8') : String(chunk)); } catch { /* ignore */ }
        return orig(chunk, enc, cb);
      };
    };
    wrap(process.stdout);
    wrap(process.stderr);
    global.__aminLog = writeLog;
    writeLog(`\n===== ${new Date().toISOString()} AminAmval v1.2.0 starting =====\n`);
  } catch { /* logging must never break the app */ }
}
function logFatal(e) {
  const text = (e && (e.stack || e.message)) || String(e);
  console.error('[fatal] ' + text);
  try { if (global.__aminLog) global.__aminLog('[fatal] ' + text + '\n'); } catch { /* ignore */ }
}
process.on('uncaughtException', (e) => { logFatal(e); process.exitCode = 1; setTimeout(() => process.exit(1), 400); });
process.on('unhandledRejection', (e) => { logFatal(e); });

console.log(`[amin] runtime node=${process.version} platform=${process.platform}/${process.arch}`);
console.log(`[amin] app dir=${__dirname} cwd=${process.cwd()}`);
if (process.env.LOG_FILE) console.log(`[amin] log file=${process.env.LOG_FILE}`);

const REPO_ROOT = path.resolve(__dirname, '..');
const REPO_WEBROOT = path.join(REPO_ROOT, 'src', 'AminAmval.Api', 'wwwroot');
const REPO_DATA_DIR = path.join(REPO_ROOT, 'src', 'AminAmval.Api', 'runtime-data');
// Portable/standalone layout fallback: <app>/wwwroot and <app>/data next to server.js
// (used by the self-contained Windows/zip package; the repo layout still works).
const WEBROOT = process.env.WEBROOT || (fs.existsSync(REPO_WEBROOT) ? REPO_WEBROOT : path.join(__dirname, 'wwwroot'));
const DATA_DIR = process.env.DATA_DIR || (fs.existsSync(REPO_DATA_DIR) ? REPO_DATA_DIR : path.join(__dirname, 'data'));

const PREVIEW = (process.env.PREVIEW_MODE || 'true') !== 'false';
const PORT = parseInt(process.env.PORT || process.env.PREVIEW_PORT || '8080', 10);
const HOST = process.env.HOST || '0.0.0.0';

const BACKUP_HOUR = parseInt(process.env.BACKUP_HOUR_UTC || '22', 10);
const BACKUP_MINUTE = parseInt(process.env.BACKUP_MINUTE_UTC || '30', 10);
const BACKUP_RETENTION_DAYS = Math.min(Math.max(parseInt(process.env.BACKUP_RETENTION_DAYS || '14', 10), 2), 365);

/* ------------------------------------------------------------------ */
/* storage + database                                                  */
/* ------------------------------------------------------------------ */
function fatal(msg, detail) {
  console.error('');
  console.error('===================================================================');
  console.error(' FATAL: ' + msg);
  if (detail) console.error(' ' + detail);
  console.error('===================================================================');
  console.error(' نکته: پوشهٔ برنامه باید روی دیسک استخراج‌شده و قابل‌نوشتن باشد.');
  console.error(' Note: extract the whole ZIP to a writable folder (not inside the ZIP viewer),');
  console.error('       e.g. C:\\AminAmval, then run start-windows.bat again.');
  console.error('');
  process.exitCode = 1;
  setTimeout(() => process.exit(1), 150);
}

try {
  fs.mkdirSync(DATA_DIR, { recursive: true });
  const probe = path.join(DATA_DIR, '.write-test');
  fs.writeFileSync(probe, 'ok');
  fs.unlinkSync(probe);
} catch (e) {
  fatal('cannot create/write the data folder: ' + DATA_DIR, e && e.message);
}
const UPLOADS = path.join(DATA_DIR, 'uploads');
const BACKUPS = path.join(DATA_DIR, 'backups');
const KEYS = path.join(DATA_DIR, 'keys');
try {
  for (const d of [UPLOADS, BACKUPS, KEYS]) fs.mkdirSync(d, { recursive: true });
} catch (e) {
  fatal('cannot create working folders inside: ' + DATA_DIR, e && e.message);
}

if (!fs.existsSync(path.join(WEBROOT, 'index.html'))) {
  fatal('web files not found in: ' + WEBROOT, 'the package is incomplete — please re-extract the ZIP.');
}

const db = openDatabase(path.join(DATA_DIR, 'amin.db'));

/* ------------------------------------------------------------------ */
/* tiny router                                                         */
/* ------------------------------------------------------------------ */
class Router {
  constructor() { this.routes = []; }
  add(method, pattern, handler) { this.routes.push({ method, parts: pattern.split('/').filter(Boolean), handler }); return this; }
  get(p, h) { return this.add('GET', p, h); }
  post(p, h) { return this.add('POST', p, h); }
  put(p, h) { return this.add('PUT', p, h); }
  delete(p, h) { return this.add('DELETE', p, h); }
  match(method, pathname) {
    const segs = pathname.split('/').filter(Boolean);
    for (const r of this.routes) {
      if (r.method !== method || r.parts.length !== segs.length) continue;
      const params = {};
      let ok = true;
      for (let i = 0; i < r.parts.length; i++) {
        if (r.parts[i].startsWith(':')) params[r.parts[i].slice(1)] = decodeURIComponent(segs[i]);
        else if (r.parts[i] !== segs[i]) { ok = false; break; }
      }
      if (ok) return { handler: r.handler, params };
    }
    return null;
  }
}

const csrf = new CsrfService(crypto.randomBytes(32).toString('hex'));

/* ------------------------------------------------------------------ */
/* app context / rate limiter                                          */
/* ------------------------------------------------------------------ */
const rateLimits = http.createRateLimiter();
const rateLimit = (key, limit, windowMs) => rateLimits(key, limit, windowMs);

function makeCtx(req, res, method, pathname, query) {
  return {
    req, res, method, pathname, query,
    params: {},
    body: undefined,
    handled: false,
    traceId: crypto.randomBytes(8).toString('hex'),
    db, csrf, storage: { uploads: UPLOADS, backups: BACKUPS, keys: KEYS },
    settings: { preview: PREVIEW, secureCookies: PREVIEW },
    rateLimit,
    logger: console,
    sessions,
    user: null,
    base
  };
}

/* ------------------------------------------------------------------ */
/* contents                                                             */
/* ------------------------------------------------------------------ */
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2',
  '.ico': 'image/x-icon',
  '.txt': 'text/plain; charset=utf-8',
  '.xml': 'application/xml'
};

function serveStatic(res, urlPath) {
  let p = decodeURIComponent(urlPath);
  if (p.includes('\0')) { res.statusCode = 400; res.end(); return; }
  let file = path.normalize(path.join(WEBROOT, p));
  if (!file.startsWith(WEBROOT)) { res.statusCode = 403; res.end('Forbidden'); return; }
  try {
    let stat = fs.statSync(file);
    if (stat.isDirectory()) {
      file = path.join(file, 'index.html');
      stat = fs.statSync(file);
    }
    const ext = path.extname(file).toLowerCase();
    res.statusCode = 200;
    res.setHeader('Content-Type', MIME[ext] || 'application/octet-stream');
    res.setHeader('Cache-Control', ext === '.woff2' ? 'public,max-age=31536000,immutable' : 'no-cache');
    res.setHeader('Content-Length', stat.size);
    fs.createReadStream(file).pipe(res);
  } catch {
    // SPA fallback to index.html (hash routes never hit the server, but be safe)
    try {
      const index = path.join(WEBROOT, 'index.html');
      const stat = fs.statSync(index);
      res.statusCode = 200;
      res.setHeader('Content-Type', 'text/html; charset=utf-8');
      res.setHeader('Cache-Control', 'no-cache');
      res.setHeader('Content-Length', stat.size);
      fs.createReadStream(index).pipe(res);
    } catch {
      res.statusCode = 404; res.end('Not found');
    }
  }
}

/* ------------------------------------------------------------------ */
/* backup service                                                      */
/* ------------------------------------------------------------------ */
let lastBackupError = null;
let backupRunning = false;
let backupLastTime = null;

function backupFiles() {
  try {
    return fs.readdirSync(BACKUPS)
      .filter(n => /^amin-[0-9]{8}-[0-9]{6}-[a-f0-9]{6}\.zip$/.test(n))
      .map(n => {
        const full = path.join(BACKUPS, n);
        const st = fs.statSync(full);
        return { name: n, size: st.size, createdAt: new Date(st.mtime.getTime() + 3.5 * 3600 * 1000).toISOString() };
      })
      .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
  } catch { return []; }
}

function backupStatus() {
  const now = new Date();
  let next = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), BACKUP_HOUR, BACKUP_MINUTE));
  if (next.getTime() <= now.getTime()) next = new Date(next.getTime() + 24 * 3600 * 1000);
  const list = backupFiles();
  return {
    enabled: true,
    running: backupRunning,
    lastError: lastBackupError,
    lastBackup: list.length ? list[0].createdAt : null,
    nextRun: next.toISOString(),
    retentionDays: BACKUP_RETENTION_DAYS,
    hourUtc: BACKUP_HOUR,
    minuteUtc: BACKUP_MINUTE
  };
}

const JSZip = require('jszip');
async function createBackup() {
  if (backupRunning) throw new core.ApiError(409, 'پشتیبان‌گیری دیگری در حال اجراست.');
  backupRunning = true;
  try {
    // consistent SQLite snapshot via VACUUM INTO
    const dbSnapshot = path.join(DATA_DIR, '.backup-amin.db');
    try { fs.unlinkSync(dbSnapshot); } catch { }
    db.exec(`VACUUM INTO '${dbSnapshot.replace(/'/g, "''")}'`);

    const zip = new JSZip();
    const hashes = {};
    const addFile = (fullPath, inZipName) => {
      if (!fs.existsSync(fullPath)) return;
      const buf = fs.readFileSync(fullPath);
      zip.file(inZipName, buf);
      hashes[inZipName] = crypto.createHash('sha256').update(buf).digest('hex');
    };
    addFile(dbSnapshot, 'amin.db');
    fs.rmSync(dbSnapshot, { force: true });
    for (const f of fs.readdirSync(UPLOADS)) addFile(path.join(UPLOADS, f), 'uploads/' + f);
    for (const f of fs.readdirSync(KEYS)) addFile(path.join(KEYS, f), 'keys/' + f);

    zip.file('manifest.json', JSON.stringify({ version: 1, createdUtc: new Date().toISOString(), files: hashes }, null, 2));
    const d = new Date();
    const pad = (n) => String(n).padStart(2, '0');
    const stamp = `${d.getUTCFullYear()}${pad(d.getUTCMonth() + 1)}${pad(d.getUTCDate())}-${pad(d.getUTCHours())}${pad(d.getUTCMinutes())}${pad(d.getUTCSeconds())}`;
    const name = 'amin-' + stamp + '-' + crypto.randomBytes(3).toString('hex') + '.zip';
    const outPath = path.join(BACKUPS, name);
    const buf = await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' });
    fs.writeFileSync(outPath, buf);

    for (const old of backupFiles().filter(x => new Date(x.createdAt).getTime() < Date.now() - BACKUP_RETENTION_DAYS * 24 * 3600 * 1000)) {
      try { fs.unlinkSync(path.join(BACKUPS, old.name)); } catch { }
    }
    lastBackupError = null;
    backupLastTime = new Date().toISOString();
    return name;
  } catch (e) {
    lastBackupError = 'پشتیبان‌گیری ناموفق بود؛ فضای دیسک و دسترسی پوشهٔ داده را بررسی کنید.';
    console.error('[backup] failed:', e.message);
    throw e;
  } finally {
    backupRunning = false;
  }
}

/* ------------------------------------------------------------------ */
/* bootstrap (empty DB -> only admin + jamdar)                         */
/* ------------------------------------------------------------------ */
function bootstrap() {
  const count = db.prepare('SELECT COUNT(*) AS c FROM users').get().c;
  if (count > 0) {
    console.log('[bootstrap] existing database detected (users=' + count + ') — skipping seed.');
    return { created: false };
  }
  console.log('[bootstrap] empty database — creating mandatory admin & custodian accounts.');

  const roles = [
    { username: 'admin', role: core.Roles.Admin, first: 'Admin', last: 'System', env: 'ADMIN_PASSWORD' },
    { username: 'jamdar', role: core.Roles.Custodian, first: 'Custodian', last: 'Assets', env: 'CUSTODIAN_PASSWORD' }
  ];
  const lines = ['AminAmval - Initial Credentials', 'Delete this file after secure delivery.', ''];
  const creds = [];
  const now = new Date().toISOString();
  for (const r of roles) {
    let password = process.env[r.env];
    if (!password || password.trim() === '') password = core.newPassword();
    try { core.strongPassword(password); }
    catch (e) { throw new Error(`${r.env} is not a valid strong password: ${e.message}`); }
    const id = core.guid();
    db.prepare(`
      INSERT INTO users (id, username, personnelCode, firstName, lastName, nationalId, departmentId, role, active,
        passwordHash, securityStamp, mustChangePassword, failedLogins, lockoutUntil, createdAt, version)
      VALUES (?,?,?,?,?,NULL,NULL,?,1,?,?,1,0,NULL,?,1)
    `).run(id, r.username, 'SYS-' + r.username.toUpperCase(), r.first, r.last, r.role,
      hashPassword(password), core.guid(), now);
    lines.push(`${r.role}: ${r.username} / ${password}`);
    creds.push({ username: r.username, role: r.role, firstName: r.first, lastName: r.last, password });
  }

  http.logAudit(db, null, 'bootstrap', 'System', null, 'Initial system bootstrap with two admin accounts.');
  const file = path.join(DATA_DIR, 'initial-credentials.txt');
  fs.writeFileSync(file, lines.join('\n') + '\n');
  if (process.platform !== 'win32') fs.chmodSync(file, 0o600);
  console.log('[bootstrap] credentials written to', file);
  for (const c of creds) {
    console.log(`[credentials] ${c.role}\tusername: ${c.username}\tpassword: ${c.password}`);
  }
  return { created: true, creds };
}

/* ------------------------------------------------------------------ */
/* register endpoints                                                  */
/* ------------------------------------------------------------------ */
const sessions = new SessionManager();
const base = {
  dataRoot: DATA_DIR, uploads: UPLOADS, backups: BACKUPS, keys: KEYS, webroot: WEBROOT,
  coreIo: core,
  backupStatus, backupsList: backupFiles, createBackup
};

const router = new Router();

router.get('/api/health', () => ({ status: 'ok', version: '1.1.0', utc: new Date().toISOString() }));

require('./handlers/auth').register(router);
require('./handlers/assets').register(router);
require('./handlers/operations').register(router);
require('./handlers/print').register(router);
require('./handlers/users').register(router);
require('./handlers/reports').register(router, base);

router.get('/api/:path', () => { throw new core.ApiError(404, 'مسیر مورد نظر وجود ندارد.'); });
router.post('/api/:path', () => { throw new core.ApiError(404, 'مسیر مورد نظر وجود ندارد.'); });
router.put('/api/:path', () => { throw new core.ApiError(404, 'مسیر مورد نظر وجود ندارد.'); });
router.delete('/api/:path', () => { throw new core.ApiError(404, 'مسیر مورد نظر وجود ندارد.'); });

/* ------------------------------------------------------------------ */
/* request dispatch                                                    */
/* ------------------------------------------------------------------ */
async function dispatch(req, res) {
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  const method = req.method;
  const pathname = url.pathname;
  const query = Object.fromEntries(url.searchParams.entries());
  const ctx = makeCtx(req, res, method, pathname, query);

  // forward headers for client IP (behind proxy)
  const forwarded = req.headers['x-forwarded-for'];
  if (forwarded) { const first = forwarded.split(',')[0].trim(); ctx.clientIp = first; }
  else ctx.clientIp = req.socket.remoteAddress || '';

  // global security headers
  authmod.applyHeaders(res, { preview: PREVIEW, isApi: pathname.startsWith('/api') });

  try {
    // 1) defensive: tampered cookie-name handling
    const cookies = authmod.parseCookies(req);
    if (cookies['Amin.Session'] !== undefined && (req.headers.cookie || '').length > 8192) {
      http.json(res, 409, { message: 'کوکی نشست نامعتبر است. دوباره وارد شوید.' });
      return;
    }

    const matched = router.match(method, pathname);
    if (matched) {
      ctx.params = matched.params;

      // 2) antiforgery for state-changing API calls (double-submit token)
      if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
        const headerToken = req.headers['x-csrf-token'];
        const cookieToken = cookies['Amin.Csrf'];
        if (!headerToken || !cookieToken || headerToken !== cookieToken || !csrf.isValid(headerToken)) {
          throw new core.ApiError(400, 'نشست امنیتی معتبر نیست. صفحه را تازه‌سازی کنید.');
        }
      }

      // 3) authentication (authoritative, mirroring cookie validation)
      const principal = authmod.parseAuth(req, db, sessions, console);
      ctx.user = principal;
      ctx.currentStamp = principal ? principal.securityStamp : null;

      // 4) mandatory password change gate
      if (principal && principal.mustChangePassword &&
          ['/api/auth/me', '/api/auth/password', '/api/auth/logout', '/api/auth/csrf', '/api/health'].indexOf(pathname) === -1) {
        throw (() => { const e = new core.ApiError(403, 'پیش از ادامه، گذرواژهٔ اولیه را تغییر دهید.'); e.mustChangePassword = true; return e; })();
      }

      // 5) parse JSON body for state-changing routes
      if (!['GET', 'HEAD', 'OPTIONS'].includes(method) && !/multipart\/form-data/i.test(req.headers['content-type'] || '')) {
        ctx.body = await http.readJson(req);
      }

      const result = await matched.handler(ctx);
      if (!ctx.handled && result !== undefined) http.json(res, 200, result);
      return;
    }

    // not an API route -> static
    if (pathname.startsWith('/api')) {
      http.json(res, 404, { message: 'مسیر مورد نظر وجود ندارد.' });
      return;
    }
    serveStatic(res, pathname);
  } catch (e) {
    if (res.headersSent) { try { res.end(); } catch { } return; }
    handleError(ctx, e);
  }
}

function handleError(ctx, e) {
  const res = ctx.res;
  if (e instanceof core.ApiError) {
    http.json(res, e.status, e.mustChangePassword ? { message: e.message, mustChangePassword: true } : { message: e.message, ...(e.errors ? { errors: e.errors } : {}) });
    if (e.status === 401 && e.logoutHint) { }
    return;
  }
  if (e && e.code && String(e.code).startsWith('SQLITE_CONSTRAINT')) {
    http.json(res, 409, { message: 'کد تکراری یا وابستگی اطلاعات اجازهٔ ثبت این تغییر را نمی‌دهد.' });
    return;
  }
  console.error('[error]', ctx.traceId, e);
  http.json(res, 500, { message: 'خطایی در پردازش رخ داد. دوباره تلاش کنید یا شناسهٔ پیگیری را به مدیر بدهید.', traceId: ctx.traceId });
}

/* ------------------------------------------------------------------ */
/* server                                                              */
/* ------------------------------------------------------------------ */
const server = require('node:http').createServer((req, res) => {
  dispatch(req, res).catch((e) => {
    if (!res.headersSent) handleError(makeCtx(req, res, req.method, new URL(req.url, 'http://localhost').pathname, {}), e);
  });
});

server.on('clientError', (err, socket) => { try { socket.destroy(); } catch { } });

function onListening() {
  const addr = server.address();
  const actualPort = addr && typeof addr === 'object' ? addr.port : PORT;

  const boot = bootstrap();
  console.log('Bootstrap result:', boot);
  console.log(`[amin] امین اموال ناواکو در حال اجرا روی http://127.0.0.1:${actualPort}`);
  console.log(`[amin] preview-cookies=${PREVIEW} data=${DATA_DIR}`);

  if (process.platform === 'win32' && process.env.OPEN_BROWSER === '1') {
    try { require('node:child_process').exec(`start "" "http://127.0.0.1:${actualPort}/"`); } catch { /* opening the browser is optional */ }
  }

  // initial backup shortly after startup (if none exists)
  setTimeout(() => {
    if (backupFiles().length === 0) {
      createBackup().then(n => console.log('[backup] initial backup created:', n)).catch(() => {});
    }
  }, 15000);

  // daily scheduled backup loop
  (async function schedule() {
    while (true) {
      const waitMs = nextBackupDelay();
      await new Promise(r => setTimeout(r, waitMs));
      try { const n = await createBackup(); console.log('[backup] scheduled backup:', n); }
      catch (e) { console.warn('[backup] scheduled backup failed, retrying later'); }
    }
  })();
}

/* If the requested port is busy (very common on Windows: another 8080 app,
   Skype/Hyper-V reserved ranges, a previous instance still running) the server
   moves to the next free port instead of crashing — the used URL is printed. */
server.on('listening', onListening); // registered once — never duplicated on port retries
let listenAttempt = 0;
function startListening(port) {
  server.listen(port, HOST);
}
server.on('error', (err) => {
  if (err && err.code === 'EADDRINUSE' && listenAttempt < 20) {
    listenAttempt += 1;
    const next = PORT + listenAttempt;
    console.warn(`[amin] port ${PORT + listenAttempt - 1} is busy — trying ${next} ...`);
    setTimeout(() => startListening(next), 150);
    return;
  }
  if (err && (err.code === 'EACCES' || err.code === 'EADDRINUSE')) {
    fatal(`cannot listen on port ${PORT} (${err.code})`, 'close the other program using this port, or set PORT=9090 before starting.');
    return;
  }
  console.error('[amin] server error:', err);
  process.exitCode = 1;
});

startListening(PORT);

function nextBackupDelay() {
  const now = new Date();
  let next = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), BACKUP_HOUR, BACKUP_MINUTE));
  if (next.getTime() <= now.getTime()) next = new Date(next.getTime() + 24 * 3600 * 1000);
  return Math.min(Math.max(next.getTime() - now.getTime(), 1000), 24 * 3600 * 1000);
}

for (const sig of ['SIGINT', 'SIGTERM']) {
  process.on(sig, () => {
    console.log(`[amin] ${sig} — shutting down cleanly.`);
    try { db.close(); } catch { }
    server.close(() => process.exit(0));
    setTimeout(() => process.exit(0), 3000).unref();
  });
}
