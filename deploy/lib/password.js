'use strict';
/*
 * Password hashing compatible in format with ASP.NET Core Identity V3
 * (PBKDF2-HMACSHA256, 100_000 iterations, 16-byte random salt, 32-byte subkey,
 * base64 of [0x01, prf(4b), iter(4b), saltLen(4b), salt, subkey]).
 *
 * The database starts empty and every hash is created/verified by this module,
 * so its own consistency is what matters; the wire format mirrors Identity v3.
 */
const crypto = require('node:crypto');

const ITER = 100000;
const PRF = 2;          // HMACSHA256
const SALT_LEN = 16;
const KEY_LEN = 32;     // 256 bits

function pbkdf2(password, salt, iterations, keylen, digest) {
  return crypto.pbkdf2Sync(Buffer.from(password, 'utf8'), salt, iterations, keylen, digest);
}

function hashPassword(password) {
  const salt = crypto.randomBytes(SALT_LEN);
  const subkey = pbkdf2(password, salt, ITER, KEY_LEN, 'sha256');
  const output = Buffer.alloc(1 + 4 + 4 + 4 + SALT_LEN + KEY_LEN);
  let o = 0;
  output.writeUInt8(0x01, o); o += 1;          // format marker
  output.writeUInt32BE(PRF, o); o += 4;        // prf
  output.writeUInt32BE(ITER, o); o += 4;       // iterations
  output.writeUInt32BE(SALT_LEN, o); o += 4;   // salt length
  salt.copy(output, o); o += SALT_LEN;
  subkey.copy(output, o); o += KEY_LEN;
  return output.toString('base64');
}

function verifyPassword(hashed, password) {
  if (!hashed || !password) return false;
  let buf;
  try { buf = Buffer.from(hashed, 'base64'); } catch { return false; }
  if (buf.length !== 1 + 4 + 4 + 4 + SALT_LEN + KEY_LEN) return false;
  let o = 0;
  const marker = buf.readUInt8(o); o += 1;
  const prf = buf.readUInt32BE(o); o += 4;
  const iter = buf.readUInt32BE(o); o += 4;
  const saltLen = buf.readUInt32BE(o); o += 4;
  if (marker !== 0x01 || prf !== PRF || saltLen !== SALT_LEN) return false;
  const salt = buf.subarray(o, o + SALT_LEN); o += SALT_LEN;
  const expected = buf.subarray(o, o + KEY_LEN);
  const actual = pbkdf2(password, salt, iter, KEY_LEN, 'sha256');
  return crypto.timingSafeEqual(actual, expected);
}

module.exports = { hashPassword, verifyPassword };
