#!/usr/bin/env python3
# AminAmval end-to-end API smoke test (cookies carried manually to emulate the
# HTTPS preview, since Python's urllib won't send Secure cookies over http).
import json, subprocess, sys, urllib.request, urllib.parse, http.cookiejar, struct, zlib

B = 'http://localhost:8097'
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
manual = {}   # name -> value (Secure cookies over http)

pass_count = 0
fail_count = 0

def req(method, path, body=None, headers=None, form=None):
    url = B + path
    h = {**(headers or {})}
    if manual: h['Cookie'] = '; '.join(f'{k}={v}' for k, v in manual.items())
    data = None
    if form is not None:
        data = form
    elif body is not None:
        data = json.dumps(body).encode()
        h['Content-Type'] = 'application/json'
    r = urllib.request.Request(url, data=data, method=method, headers=h)
    try:
        with opener.open(r) as resp:
            raw = resp.read()
            ct = resp.headers.get('Content-Type', '')
            # capture Set-Cookie into manual dict
            for sc in resp.headers.get_all('Set-Cookie') or []:
                name = sc.split('=', 1)[0]
                val = sc.split('=', 1)[1].split(';', 1)[0]
                if val == '' or 'Max-Age=0' in sc or 'Expires=Thu, 01 Jan 1970' in sc:
                    manual.pop(name, None)
                else:
                    manual[name] = val
            return resp.status, (json.loads(raw) if 'json' in ct and raw else raw), resp.headers
    except urllib.error.HTTPError as e:
        raw = e.read()
        try: return e.code, json.loads(raw), e.headers
        except Exception: return e.code, raw, e.headers

def csrf():
    _, d, _ = req('GET', '/api/auth/csrf')
    csrf_token = d['token']
    manual['Amin.Csrf'] = csrf_token
    return csrf_token

def check(name, cond, extra=''):
    global pass_count, fail_count
    if cond:
        pass_count += 1
        print(f'  ✅ {name}')
    else:
        fail_count += 1
        print(f'  ❌ {name} {extra}')

# 0. health
s, d, _ = req('GET', '/api/health')
check('health endpoint', s == 200 and d.get('status') == 'ok', d)

# 1. unauthenticated access rejected
s, d, _ = req('GET', '/api/dashboard')
check('unauthenticated dashboard -> 401', s == 401, (s, d))

# 2. login
t = csrf()
s, d, _ = req('POST', '/api/auth/login', {'username': 'admin', 'password': 'Admin1289@@@'}, {'X-CSRF-TOKEN': t})
check('admin login', s == 200 and d.get('role') == 'Admin', (s, d))
check('admin mustChangePassword initially true', d.get('mustChangePassword') is True)

# 3. force password change
t = csrf()
s, d, _ = req('POST', '/api/auth/password', {'currentPassword': 'Admin1289@@@', 'newPassword': 'AdminNew1289@@@'}, {'X-CSRF-TOKEN': t})
check('admin password change', s == 200 and d.get('mustChangePassword') is False, (s, d))

# 4. dashboard now empty
s, d, _ = req('GET', '/api/dashboard')
check('dashboard empty (total=0, activeUsers=2)', s == 200 and d.get('total') == 0 and d.get('activeUsers') == 2, (s, d.get('total'), d.get('activeUsers')))

# 5. references
t = csrf()
s, d, _ = req('POST', '/api/references/departments', {'name': 'IT'}, {'X-CSRF-TOKEN': t})
check('create department', s == 200 and 'id' in d, d)
t = csrf()
s, d, _ = req('POST', '/api/references/categories', {'name': 'Computers', 'description': 'Laptops'}, {'X-CSRF-TOKEN': t})
check('create category', s == 200 and 'id' in d, d)

s, d, _ = req('GET', '/api/lookups')
cat = d['categories'][0]['id']; dept = d['departments'][0]['id']
check('lookups populated', len(d['departments']) == 1 and len(d['categories']) == 1)

# 6. create employee
t = csrf()
s, d, _ = req('POST', '/api/users', {'personnelCode': 'E1001', 'username': 'e1001', 'firstName': 'Ahmad', 'lastName': 'Mohammadi',
                                     'nationalId': '0012345679', 'departmentId': dept, 'role': 'Employee', 'active': True}, {'X-CSRF-TOKEN': t})
check('create employee', s == 200 and 'id' in d, d)
uid = d['id']

# 7. invalid national id rejected
t = csrf()
s, d, _ = req('POST', '/api/users', {'personnelCode': 'E1002', 'username': 'e1002', 'firstName': 'X', 'lastName': 'Y',
                                     'nationalId': '123', 'departmentId': dept, 'role': 'Employee', 'active': True}, {'X-CSRF-TOKEN': t})
check('invalid national id rejected', s == 400)

# 8. upload image
def chunk(t, data):
    c = struct.pack('>I', len(data)) + t + data
    return c + struct.pack('>I', zlib.crc32(t + data) & 0xffffffff)
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b'\x00\xff\x00\x00')) + chunk(b'IEND', b'')
boundary = '----aminboundary'
form = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="t.png"\r\nContent-Type: image/png\r\n\r\n').encode() + png + f'\r\n--{boundary}--\r\n'.encode()
t = csrf()
s, d, _ = req('POST', '/api/files', form=form, headers={'X-CSRF-TOKEN': t, 'Content-Type': f'multipart/form-data; boundary={boundary}'})
check('upload image', s == 200 and 'id' in d, (s, d))
img = d['id']

# 9. create asset
t = csrf()
s, d, _ = req('POST', '/api/assets', {'code': 'AST-0001', 'name': 'Lenovo ThinkPad T14', 'categoryId': cat, 'brand': 'Lenovo',
                                      'model': 'ThinkPad', 'serial': 'SN111', 'quality': 'Good', 'owner': 'Navaco', 'description': 'desc',
                                      'imageId': img, 'purchaseDate': '2024-01-01', 'purchaseCost': 50000000, 'hasLabel': True, 'location': 'Rack 1'}, {'X-CSRF-TOKEN': t})
check('create asset', s == 200 and 'id' in d, d)
aid = d['id']

# 10. asset search
s, d, _ = req('GET', '/api/assets?q=AST-0001')
check('asset search q=AST-0001', s == 200 and d.get('total') == 1, (s, d.get('total')))

# 11. duplicate code rejected
t = csrf()
s, d, _ = req('POST', '/api/assets', {'code': 'AST-0001', 'name': 'X', 'categoryId': cat, 'brand': 'B', 'owner': 'O', 'description': 'd', 'imageId': img}, {'X-CSRF-TOKEN': t})
check('duplicate asset code rejected', s == 409, (s, d))

# 12. image required
t = csrf()
s, d, _ = req('POST', '/api/assets', {'code': 'AST-0002', 'name': 'X', 'categoryId': cat, 'brand': 'B', 'owner': 'O', 'description': 'd', 'imageId': 'nope'}, {'X-CSRF-TOKEN': t})
check('asset image required', s == 400)

# 13. assign
t = csrf()
s, d, _ = req('POST', f'/api/assets/{aid}/assign', {'userId': uid, 'departmentId': dept, 'startedAt': '2024-02-01', 'reference': 'REF-1', 'notes': '', 'version': 1}, {'X-CSRF-TOKEN': t})
check('assign asset', s == 200, (s, d))

# 14. detail has assignment
s, d, _ = req('GET', f'/api/assets/{aid}')
check('asset detail currentAssignment', d['asset']['status'] == 'Assigned' and d['asset']['currentAssignment']['userName'] == 'Ahmad Mohammadi', d['asset'])

# 15. employee asset count
s, d, _ = req('GET', '/api/users?q=e1001')
check('user assetCount = 1', d['items'][0]['assetCount'] == 1)

# 16. return
s, d, _ = req('GET', f'/api/assets/{aid}')
ver = d['asset']['version']
t = csrf()
s, d, _ = req('POST', f'/api/assets/{aid}/return', {'date': '2024-03-01', 'reason': 'test return', 'reference': '', 'location': 'Rack 1', 'version': ver}, {'X-CSRF-TOKEN': t})
check('return asset', s == 200, (s, d))

# 17. disposition request + approve
s, d, _ = req('GET', f'/api/assets/{aid}')
ver = d['asset']['version']
t = csrf()
s, d, _ = req('POST', f'/api/assets/{aid}/disposition-requests', {'status': 'Scrapped', 'date': '2024-04-01', 'reason': 'old', 'reference': 'REF-9', 'amount': None, 'version': ver}, {'X-CSRF-TOKEN': t})
check('disposition request created', s == 200 and 'id' in d, (s, d))
opid = d['id']
t = csrf()
s, d, _ = req('POST', f'/api/operations/{opid}/approve', {'note': 'approved', 'version': 1}, {'X-CSRF-TOKEN': t})
check('admin approve disposition', s == 200, (s, d))
s, d, _ = req('GET', f'/api/assets/{aid}')
check('asset scrapped after approval', d['asset']['status'] == 'Scrapped')

# 18. excel exports
s, raw, hdrs = req('GET', '/api/reports/assets/export')
check('assets excel export', s == 200 and hdrs.get('Content-Type', '').startswith('application/vnd.openxmlformats'), (s, hdrs.get('Content-Type')))
s, raw, hdrs = req('GET', '/api/reports/audit/export')
check('audit excel export', s == 200)
s, raw, hdrs = req('GET', '/api/import/users/template')
check('import template', s == 200)

# 19. system status
s, d, _ = req('GET', '/api/system')
check('system status', s == 200 and d.get('users') == 3 and d.get('assets') == 1, (s, d.get('users'), d.get('assets')))

# 20. backup create + audit limits
t = csrf()
s, d, _ = req('POST', '/api/backups', {}, {'X-CSRF-TOKEN': t})
check('manual backup', s == 200 and 'name' in d, (s, d))

# 21. jamdar login, blocked from audit
manual2 = {}
def req2(method, path, body=None, headers=None):
    url = B + path
    h = {**(headers or {})}
    if manual2: h['Cookie'] = '; '.join(f'{k}={v}' for k, v in manual2.items())
    data = json.dumps(body).encode() if body is not None else None
    if body is not None: h['Content-Type'] = 'application/json'
    r = urllib.request.Request(url, data=data, method=method, headers=h)
    try:
        with opener.open(r) as resp:
            raw = resp.read(); ct = resp.headers.get('Content-Type','')
            for sc in resp.headers.get_all('Set-Cookie') or []:
                nm = sc.split('=',1)[0]; vl = sc.split('=',1)[1].split(';',1)[0]
                if vl=='' or 'Max-Age=0' in sc: manual2.pop(nm, None)
                else: manual2[nm] = vl
            return resp.status, (json.loads(raw) if 'json' in ct and raw else raw)
    except urllib.error.HTTPError as e:
        raw = e.read()
        try: return e.code, json.loads(raw)
        except: return e.code, raw

s, d = req2('GET', '/api/auth/csrf'); manual2['Amin.Csrf'] = d['token']
s, d = req2('POST', '/api/auth/login', {'username': 'jamdar', 'password': 'Jamdar1289@@@'}, {'X-CSRF-TOKEN': manual2['Amin.Csrf']})
check('jamdar login', s == 200 and d.get('role') == 'Custodian' and d.get('mustChangePassword') is True)
s, d = req2('GET', '/api/audit')
check('jamdar blocked from audit (403)', s == 403, (s, d))
s, d = req2('GET', '/api/dashboard')
check('jamdar forced to change initial password (403)', s == 403 and d.get('mustChangePassword') is True, (s, d))
s, d = req2('POST', '/api/auth/password', {'currentPassword': 'Jamdar1289@@@', 'newPassword': 'JamdarNew1289@@@'}, {'X-CSRF-TOKEN': manual2['Amin.Csrf']})
check('jamdar changes initial password', s == 200 and d.get('mustChangePassword') is False)
s, d = req2('GET', '/api/dashboard')
check('jamdar dashboard ok', s == 200 and d.get('activeUsers') == 3 and d.get('pendingOperations') == 0)

# 22. logout admin
t = csrf()
s, d, _ = req('POST', '/api/auth/logout', {}, {'X-CSRF-TOKEN': t})
check('logout', s == 200, (s, d))
manual.pop('Amin.Session', None)
s, d, _ = req('GET', '/api/dashboard')
check('after logout -> 401', s == 401)

print()
print(f'== {pass_count} passed, {fail_count} failed ==')
sys.exit(1 if fail_count else 0)
