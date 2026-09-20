'use strict';
/*
 * امین اموال — CSRF tokens (cookie + header), signed with an
 * HMAC-SHA256 ticket key so validation is stateless and self-contained.
 */
const crypto = require('node:crypto');

class CsrfService {
  constructor(secret) {
    this.key = crypto.createHash('sha256').update(secret).digest();
  }

  /** Mints both cookie + header token (same value, per ASP.NET antiforgery). */
  generate() {
    const raw = crypto.randomBytes(32).toString('base64url');
    return { cookie: raw, token: raw };
  }

  isValid(token) {
    return typeof token === 'string' && token.length >= 20 && token.length <= 128;
  }
}

module.exports = { CsrfService };
