'use strict';
/*
 * امین اموال — /api/lookups, /api/users/**, /api/references/**, /api/audit/cancel
 * (port of UserEndpoints.cs).
 */
const { ApiError, Roles, RolesAll, checkVersion, clean, digits, required, username, validNationalId, guid, nowIso, paging, maskNationalId } = require('../lib/core');
const http = require('../lib/http-util');
const authmod = require('../lib/auth');
const authh = require('./auth');
const L = require('./assets-lib');

function register(router) {
  /* ---- lookups (staff) --------------------------------------------- */
  router.get('/api/lookups', authmod.requireStaff(async (ctx) => {
    const categories = [];
    for (const c of ctx.db.prepare('SELECT * FROM categories ORDER BY name').all()) {
      categories.push({
        id: c.id, name: c.name, description: c.description, version: c.version,
        count: ctx.db.prepare('SELECT COUNT(*) AS c FROM assets WHERE categoryId = ? AND deleted = 0').get(c.id).c
      });
    }
    const departments = [];
    for (const d of ctx.db.prepare('SELECT * FROM departments ORDER BY name').all()) {
      departments.push({
        id: d.id, name: d.name, version: d.version,
        count: ctx.db.prepare('SELECT COUNT(*) AS c FROM users WHERE departmentId = ? AND active = 1').get(d.id).c,
        assetCount: ctx.db.prepare('SELECT COUNT(*) AS c FROM assignments WHERE departmentId = ? AND endedAt IS NULL').get(d.id).c
      });
    }
    const users = ctx.db.prepare('SELECT * FROM users WHERE active = 1 ORDER BY lastName').all()
      .map(u => ({ id: u.id, name: u.firstName + ' ' + u.lastName, personnelCode: u.personnelCode, departmentId: u.departmentId, role: u.role }));
    const owners = ctx.db.prepare('SELECT DISTINCT owner FROM assets WHERE deleted = 0 ORDER BY owner').all().map(x => x.owner);
    return { categories, departments, users, owners };
  }));

  /* ---- list users --------------------------------------------------- */
  router.get('/api/users', authmod.requireStaff(async (ctx) => {
    const { where, params } = userQuery(ctx.db, ctx.query);
    const { page, size } = paging(ctx.query);
    const total = ctx.db.prepare(`SELECT COUNT(*) AS c FROM users WHERE ${where}`).get(...params).c;
    const offset = (page - 1) * size;
    const rows = ctx.db.prepare(`SELECT * FROM users WHERE ${where} ORDER BY lastName, firstName LIMIT ? OFFSET ?`).all(...params, size, offset);
    const ids = rows.map(x => x.id);
    const items = rows.map(u => {
      let assetCount = 0;
      if (ids.length) assetCount = ctx.db.prepare(`SELECT COUNT(*) AS c FROM assignments WHERE endedAt IS NULL AND userId = ?`).get(u.id).c;
      return { user: authh.publicUser(u, authh.deptName(ctx.db, u.departmentId)), assetCount };
    });
    http.logAudit(ctx.db, ctx, 'view.list', 'User', null, 'مشاهدهٔ فهرست کاربران',
      undefined, { total, filters: require('../lib/core').filterSnapshot(ctx.query) });
    return { items, total, page, pageSize: size };
  }));

  /* ---- user detail -------------------------------------------------- */
  router.get('/api/users/:id', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const u = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(id);
    if (!u) throw new ApiError(404, 'کاربر پیدا نشد.');
    const assetRows = ctx.db.prepare(`
      SELECT a.* FROM assets a
      WHERE a.deleted = 0 AND EXISTS (
        SELECT 1 FROM assignments x WHERE x.assetId = a.id AND x.userId = ? AND x.endedAt IS NULL
      )
    `).all(id);
    const assets = assetRows.map(a => L.assetView(ctx.db, a, true));
    http.logAudit(ctx.db, ctx, 'view.detail', 'User', id, 'مشاهدهٔ مشخصات و اموال تحویلی «' + u.firstName + ' ' + u.lastName + '»');
    return { user: authh.publicUser(u, authh.deptName(ctx.db, u.departmentId)), assets };
  }));

  /* ---- create user -------------------------------------------------- */
  router.post('/api/users', authmod.requireStaff(async (ctx) => {
    const i = ctx.body || {};
    const values = applyUser(ctx.db, ctx, i, authmod.isAdmin(ctx), true, null);
    const password = i.password ? String(i.password) : values.nationalId;
    if (i.password) require('../lib/core').strongPassword(i.password);
    const stamp = guid();
    ctx.db.prepare(`
      INSERT INTO users (id, username, personnelCode, firstName, lastName, nationalId, departmentId, role, active,
        passwordHash, securityStamp, mustChangePassword, failedLogins, lockoutUntil, createdAt, version)
      VALUES (?,?,?,?,?,?,?,?,?,?,?,1,0,NULL,?,1)
    `).run(
      values.id, values.username, values.personnelCode, values.firstName, values.lastName,
      values.nationalId, values.departmentId, values.role, values.active ? 1 : 0,
      require('../lib/password').hashPassword(password), stamp, nowIso()
    );
    http.logAudit(ctx.db, ctx, 'create', 'User', values.id, 'ایجاد کاربر «' + values.firstName + ' ' + values.lastName + '»؛ تغییر رمز در اولین ورود الزامی است.',
      undefined, { id: values.id, username: values.username, personnelCode: values.personnelCode, role: values.role });
    const created = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(values.id);
    return { id: created.id, username: created.username, message: 'کاربر ثبت شد. گذرواژهٔ پیش‌فرض، کد ملی است؛ تغییر آن در اولین ورود الزامی است.' }; // 201
  }));

  /* ---- update user -------------------------------------------------- */
  router.put('/api/users/:id', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const u = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(id);
    if (!u) throw new ApiError(404, 'کاربر پیدا نشد.');
    checkVersion(u.version, Number(i.version));
    if (!authmod.isAdmin(ctx) && u.role !== Roles.Employee) throw new ApiError(403, 'جمعدار فقط مجاز به ویرایش حساب کارکنان عادی است.');
    if (!i.active && u.id === ctx.user.id) throw new ApiError(400, 'غیرفعال کردن حساب خود مجاز نیست.');
    if (u.role === Roles.Admin && (!i.active || i.role !== Roles.Admin)) {
      const cnt = ctx.db.prepare('SELECT COUNT(*) AS c FROM users WHERE role = ? AND active = 1').get(Roles.Admin).c;
      if (cnt <= 1) throw new ApiError(409, 'سامانه باید حداقل یک مدیر فعال داشته باشد.');
    }
    if (!i.active) {
      const cnt = ctx.db.prepare('SELECT COUNT(*) AS c FROM assignments WHERE userId = ? AND endedAt IS NULL').get(id).c;
      if (cnt) throw new ApiError(409, 'پیش از غیرفعال‌سازی، اموال تحویلی کاربر را عودت یا انتقال دهید.');
    }
    const before = { id: u.id, username: u.username, personnelCode: u.personnelCode, firstName: u.firstName, lastName: u.lastName, nationalId: maskNationalId(u.nationalId), departmentId: u.departmentId, role: u.role, active: !!u.active, version: u.version };
    const values = applyUser(ctx.db, ctx, i, authmod.isAdmin(ctx), false, u);
    const newStamp = guid();
    ctx.db.prepare(`
      UPDATE users SET username = ?, personnelCode = ?, firstName = ?, lastName = ?, nationalId = ?, departmentId = ?,
        role = ?, active = ?, securityStamp = ?, version = version + 1 WHERE id = ?
    `).run(values.username, values.personnelCode, values.firstName, values.lastName, values.nationalId,
      values.departmentId, values.role, values.active ? 1 : 0, newStamp, id);
    ctx.db.prepare('UPDATE sessions SET revokedAt = ? WHERE userId = ? AND revokedAt IS NULL').run(nowIso(), id);
    if (typeof ctx.sessions.revoke === 'function') await ctx.sessions.revoke(ctx.db, id);
    const updated = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(id);
    http.logAudit(ctx.db, ctx, 'update', 'User', id, 'ویرایش مشخصات / دسترسی «' + updated.firstName + ' ' + updated.lastName + '»',
      before, { id: updated.id, username: updated.username, personnelCode: updated.personnelCode, firstName: updated.firstName, lastName: updated.lastName, nationalId: maskNationalId(updated.nationalId), departmentId: updated.departmentId, role: updated.role, active: !!updated.active, version: updated.version });
    return { message: 'اطلاعات کاربر ذخیره شد.', version: updated.version };
  }));

  /* ---- reset password ---------------------------------------------- */
  router.post('/api/users/:id/reset-password', authmod.requireStaff(async (ctx) => {
    const id = ctx.params.id;
    const i = ctx.body || {};
    const u = ctx.db.prepare('SELECT * FROM users WHERE id = ?').get(id);
    if (!u) throw new ApiError(404, 'کاربر پیدا نشد.');
    if (!authmod.isAdmin(ctx) && u.role !== Roles.Employee) throw new ApiError(403, 'تنظیم گذرواژهٔ حساب‌های مدیریتی فقط توسط مدیر مجاز است.');
    require('../lib/core').strongPassword(i.newPassword);
    if (i.newPassword === u.nationalId) throw new ApiError(400, 'از کد ملی به‌عنوان گذرواژهٔ جدید استفاده نکنید.');
    const newStamp = guid();
    ctx.db.prepare('UPDATE users SET passwordHash = ?, securityStamp = ?, mustChangePassword = 1, failedLogins = 0, lockoutUntil = NULL, version = version + 1 WHERE id = ?')
      .run(require('../lib/password').hashPassword(i.newPassword), newStamp, id);
    ctx.db.prepare('UPDATE sessions SET revokedAt = ? WHERE userId = ? AND revokedAt IS NULL').run(nowIso(), id);
    if (typeof ctx.sessions.revoke === 'function') await ctx.sessions.revoke(ctx.db, id);
    http.logAudit(ctx.db, ctx, 'password.reset', 'User', id, 'بازنشانی گذرواژه و ابطال نشست‌ها؛ تغییر رمز در ورود بعدی الزامی است.');
    return { message: 'گذرواژه بازنشانی شد. رمز موقت را به‌صورت امن به کاربر تحویل دهید.' };
  }));

  /* ---- references --------------------------------------------------- */
  router.post('/api/references/:kind', authmod.requireStaff(async (ctx) => {
    const kind = ctx.params.kind;
    const i = ctx.body || {};
    const name = required(i.name || '', 'عنوان', 100);
    if (kind === 'categories') {
      const id = guid();
      ctx.db.prepare('INSERT INTO categories (id, name, description, version) VALUES (?,?,?,1)').run(id, name, clean(i.description || '', 1000));
      http.logAudit(ctx.db, ctx, 'create', 'Category', id, 'ایجاد «' + name + '»', undefined, { name, description: i.description });
      return { id, message: 'اطلاعات پایه ثبت شد.' };
    } else if (kind === 'departments') {
      const id = guid();
      ctx.db.prepare('INSERT INTO departments (id, name, version) VALUES (?,?,1)').run(id, name);
      http.logAudit(ctx.db, ctx, 'create', 'Department', id, 'ایجاد «' + name + '»', undefined, { name, description: i.description });
      return { id, message: 'اطلاعات پایه ثبت شد.' };
    }
    throw new ApiError(404, 'نوع اطلاعات پایه معتبر نیست.');
  }));

  router.put('/api/references/:kind/:id', authmod.requireStaff(async (ctx) => {
    const kind = ctx.params.kind; const id = ctx.params.id; const i = ctx.body || {};
    const name = required(i.name || '', 'عنوان', 100);
    if (kind === 'categories') {
      const x = ctx.db.prepare('SELECT * FROM categories WHERE id = ?').get(id);
      if (!x) throw new ApiError(404, 'دسته‌بندی پیدا نشد.');
      checkVersion(x.version, Number(i.version));
      const before = { name: x.name, description: x.description };
      ctx.db.prepare('UPDATE categories SET name = ?, description = ?, version = version + 1 WHERE id = ?').run(name, clean(i.description || '', 1000), id);
      http.logAudit(ctx.db, ctx, 'update', 'Category', id, 'ویرایش «' + name + '»', before, { name, description: i.description });
      return { message: 'تغییرات ذخیره شد.' };
    } else if (kind === 'departments') {
      const x = ctx.db.prepare('SELECT * FROM departments WHERE id = ?').get(id);
      if (!x) throw new ApiError(404, 'واحد سازمانی پیدا نشد.');
      checkVersion(x.version, Number(i.version));
      const before = { name: x.name };
      ctx.db.prepare('UPDATE departments SET name = ?, version = version + 1 WHERE id = ?').run(name, id);
      http.logAudit(ctx.db, ctx, 'update', 'Department', id, 'ویرایش «' + name + '»', before, { name, description: i.description });
      return { message: 'تغییرات ذخیره شد.' };
    }
    throw new ApiError(404, 'نوع اطلاعات پایه معتبر نیست.');
  }));

  router.delete('/api/references/:kind/:id', authmod.requireAdmin(async (ctx) => {
    const kind = ctx.params.kind; const id = ctx.params.id; const i = ctx.body || {};
    const reason = required(i.reason || '', 'علت حذف', 1000);
    if (kind === 'categories') {
      const x = ctx.db.prepare('SELECT * FROM categories WHERE id = ?').get(id);
      if (!x) throw new ApiError(404, 'دسته‌بندی پیدا نشد.');
      checkVersion(x.version, Number(i.version));
      if (ctx.db.prepare('SELECT COUNT(*) AS c FROM assets WHERE categoryId = ?').get(id).c) {
        throw new ApiError(409, 'دسته‌بندی دارای اموال است و قابل حذف نیست.');
      }
      ctx.db.prepare('DELETE FROM categories WHERE id = ?').run(id);
      http.logAudit(ctx.db, ctx, 'delete', 'Category', id, 'حذف «' + x.name + '»؛ ' + reason, { name: x.name });
      return { message: 'اطلاعات پایه حذف شد.' };
    } else if (kind === 'departments') {
      const x = ctx.db.prepare('SELECT * FROM departments WHERE id = ?').get(id);
      if (!x) throw new ApiError(404, 'واحد پیدا نشد.');
      checkVersion(x.version, Number(i.version));
      const used = ctx.db.prepare('SELECT COUNT(*) AS c FROM users WHERE departmentId = ?').get(id).c +
        ctx.db.prepare('SELECT COUNT(*) AS c FROM assignments WHERE departmentId = ?').get(id).c;
      if (used) throw new ApiError(409, 'واحد دارای کاربر یا سابقهٔ تخصیص است و قابل حذف نیست.');
      ctx.db.prepare('DELETE FROM departments WHERE id = ?').run(id);
      http.logAudit(ctx.db, ctx, 'delete', 'Department', id, 'حذف «' + x.name + '»؛ ' + reason, { name: x.name });
      return { message: 'اطلاعات پایه حذف شد.' };
    }
    throw new ApiError(404, 'نوع اطلاعات پایه معتبر نیست.');
  }));

  /* ---- cancel audit ------------------------------------------------- */
  router.post('/api/audit/cancel', authmod.requireAuth(async (ctx) => {
    const i = ctx.body || {};
    const allowed = ['asset.create', 'asset.edit', 'asset.delete', 'assign', 'transfer', 'return', 'status', 'user.create', 'user.edit', 'user.reset', 'reference', 'import', 'backup', 'asset.restore', 'disposition', 'password'];
    if (!allowed.includes(i.operation)) throw new ApiError(400, 'عملیات معتبر نیست.');
    if (!authmod.isStaff(ctx) && i.operation !== 'password') throw new ApiError(403, 'فقط انصراف از تغییر رمز خودتان مجاز است.');
    const op = i.operation || '';
    let entityId = null;
    if (op === 'password') entityId = ctx.user.id;
    else if (i.entityId !== undefined && i.entityId !== null) entityId = clean(i.entityId, 32);
    const type = (op.startsWith('user') || op === 'password') ? 'User' : (op === 'reference' || op === 'import' || op === 'backup') ? 'System' : 'Asset';
    http.logAudit(ctx.db, ctx, 'cancel', type, entityId, 'انصراف کاربر از عملیات «' + op + '»');
    return { message: 'انصراف ثبت شد.' };
  }));
}

function userQuery(db, query) {
  const q = { ...(query || {}) };
  let where = '1=1'; const params = [];
  const search = digits(q.q || '').toLowerCase();
  if (search) {
    where += ` AND (lower(firstName || ' ' || lastName) LIKE ? OR lower(personnelCode) LIKE ? OR lower(username) LIKE ? OR (nationalId IS NOT NULL AND lower(nationalId) LIKE ?))`;
    const like = `%${search.replace(/[\\%_]/g, (m) => '\\' + m)}%`;
    params.push(like, like, like, like);
  }
  if (q.departmentId) { where += ' AND departmentId = ?'; params.push(q.departmentId); }
  if (q.role) { where += ' AND role = ?'; params.push(q.role); }
  if (q.active === 'true' || q.active === 'false') { where += ' AND active = ?'; params.push(q.active === 'true' ? 1 : 0); }
  return { where, params };
}

function applyUser(db, ctx, i, isAdmin, create, existing) {
  if (!RolesAll.includes(i.role) || (!isAdmin && i.role !== Roles.Employee) || (isAdmin && create && i.role !== Roles.Employee)) {
    throw new ApiError(403, 'مدیر سامانه و جمعدار اموال فقط از مسیر راه‌اندازی اولیه قابل ایجاد هستند.');
  }
  const personnelCode = required(digits(i.personnelCode || '').toUpperCase(), 'کد پرسنلی', 60);
  const uname = username((i.username === undefined || i.username === null || String(i.username).trim() === '') ? personnelCode : i.username);
  const dup = db.prepare('SELECT id FROM users WHERE (username = ? OR personnelCode = ?) AND id <> ?')
    .get(uname, personnelCode, existing ? existing.id : '');
  if (dup) throw new ApiError(409, 'کد پرسنلی یا نام کاربری تکراری است.');
  const firstName = required(i.firstName || '', 'نام', 100);
  const lastName = required(i.lastName || '', 'نام خانوادگی', 100);
  const national = digits(i.nationalId || '');
  const bootstrap = !create && existing && existing.nationalId === null && (existing.username === 'admin' || existing.username === 'jamdar') && existing.personnelCode.startsWith('SYS-');
  let nationalFinal = null;
  if (!(bootstrap && national === '')) {
    if (!require('../lib/core').validNationalId(national)) throw new ApiError(400, 'کد ملی ۱۰ رقمی معتبر وارد کنید.');
    const ndup = db.prepare('SELECT id FROM users WHERE nationalId = ? AND id <> ?').get(national, existing ? existing.id : '');
    if (ndup) throw new ApiError(409, 'کد ملی تکراری است.');
    nationalFinal = national;
  }
  let departmentId = null;
  if (i.departmentId === undefined || i.departmentId === null || i.departmentId === '') {
    if (bootstrap) departmentId = null;
    else throw new ApiError(400, 'واحد سازمانی معتبر را انتخاب کنید.');
  } else {
    if (!db.prepare('SELECT id FROM departments WHERE id = ?').get(i.departmentId)) {
      throw new ApiError(400, 'واحد سازمانی معتبر را انتخاب کنید.');
    }
    departmentId = i.departmentId;
  }
  return {
    id: existing ? existing.id : guid(),
    username: uname, personnelCode, firstName, lastName,
    nationalId: nationalFinal, departmentId, role: i.role, active: !!i.active
  };
}

module.exports = { register, userQuery, applyUser };
