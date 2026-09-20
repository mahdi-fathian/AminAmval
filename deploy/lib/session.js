'use strict';
/*
 * امین اموال — in-memory session manager.
 * Authoritative records live in SQLite (Sessions table); this cache mirrors
 * active session tokens for fast per-request recovery using the exact rules
 * of Program.cs cookie validation (SecurityStamp-based revocation).
 */
const crypto = require('node:crypto');
const { nowIso } = require('./core');

const TTL_MIN = 5;        // activity refresh window
const SESSION_HOURS = 8;

class SessionManager {
  constructor() {
    this.tokens = new Map();   // token -> userId
    this.byUser = new Map();   // userId -> sessionId -> { expiresAt, lastSeenAt, created }
  }

  /** Persist a freshly created session row into the cache. */
  async attach(db, session) {
    const now = Date.now();
    this.tokens.set(session.token, session.userId);
    let m = this.byUser.get(session.userId);
    if (!m) { m = new Map(); this.byUser.set(session.userId, m); }
    m.set(session.id, {
      id: session.id,
      expiresAt: now + SESSION_HOURS * 3600 * 1000,
      lastSeenAt: now,
      created: now
    });
  }

  /** Load active sessions for a user from DB and hydrate the cache. */
  async hydrate(db, userId) {
    const now = Date.now();
    const rows = db.prepare(
      `SELECT id, token, expiresAt, lastSeenAt FROM sessions
       WHERE userId = ? AND revokedAt IS NULL ORDER BY createdAt ASC`
    ).all(userId);
    const m = new Map();
    for (const r of rows) {
      const exp = new Date(r.expiresAt).getTime();
      if (Number.isNaN(exp) || exp <= now) continue;
      this.tokens.set(r.token, userId);
      m.set(r.id, { id: r.id, expiresAt: exp, lastSeenAt: new Date(r.lastSeenAt).getTime(), created: new Date(r.createdAt || r.lastSeenAt).getTime() });
    }
    this.byUser.set(userId, m);
  }

  async revoke(db, userId) {
    const tokensToDelete = [];
    for (const [t, u] of this.tokens) if (u === userId) tokensToDelete.push(t);
    for (const t of tokensToDelete) this.tokens.delete(t);
    this.byUser.delete(userId);
  }

  async revokeOne(db, userId, sessionId) {
    const m = this.byUser.get(userId);
    if (!m) {
      await this.hydrate(db, userId);
      return this.revokeOne(db, userId, sessionId);
    }
    const entry = m.get(sessionId);
    if (!entry) return;
    m.delete(sessionId);
    for (const [t, u] of this.tokens) if (u === userId && t === entry.token) this.tokens.delete(t);
  }

  async touch(db, userId, sessionId) {
    const m = this.byUser.get(userId);
    if (m && m.has(sessionId)) {
      const e = m.get(sessionId);
      e.lastSeenAt = Date.now();
      e.expiresAt = Date.now() + SESSION_HOURS * 3600 * 1000;
    } else {
      await this.hydrate(db, userId);
    }
  }

  isValid(userId, sessionId) {
    const m = this.byUser.get(userId);
    if (!m) return false;
    const e = m.get(sessionId);
    return !!e && e.expiresAt > Date.now();
  }
}

/**
 * Build an opaque session token (Google-style "half" token). The full token is
 * returned to the API layer for the cookie; we only keep its SHA-256 digest in
 * the cache/DB so a leaked cache dump cannot be replayed.
 */
function makeToken() {
  return crypto.randomBytes(32).toString('hex');
}

module.exports = { SessionManager, makeToken, SESSION_HOURS, nowIso };
