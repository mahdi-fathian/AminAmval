'use strict';
/*
 * امین اموال — SQLite persistence layer (node:sqlite, built-in since Node 22.5).
 * Schema mirrors AminAmval.Api Models/Entities.cs; all revisions via PRAGMA user_version.
 */
const fs = require('node:fs');
const path = require('node:path');
const { DatabaseSync } = require('node:sqlite');

const SCHEMA_VERSION = 1;

function openDatabase(file) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const db = new DatabaseSync(file);
  db.exec('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;');
  ensureSchema(db);
  return db;
}

function ensureSchema(db) {
  db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id            TEXT PRIMARY KEY,
      username      TEXT NOT NULL UNIQUE COLLATE NOCASE,
      personnelCode TEXT NOT NULL UNIQUE COLLATE NOCASE,
      firstName     TEXT NOT NULL,
      lastName      TEXT NOT NULL,
      nationalId    TEXT,
      departmentId  TEXT,
      role          TEXT NOT NULL DEFAULT 'Employee',
      active        INTEGER NOT NULL DEFAULT 1,
      passwordHash  TEXT NOT NULL DEFAULT '',
      securityStamp TEXT NOT NULL,
      mustChangePassword INTEGER NOT NULL DEFAULT 1,
      failedLogins  INTEGER NOT NULL DEFAULT 0,
      lockoutUntil  TEXT,
      createdAt     TEXT NOT NULL,
      version       INTEGER NOT NULL DEFAULT 1,
      FOREIGN KEY (departmentId) REFERENCES departments(id)
    );
    CREATE UNIQUE INDEX IF NOT EXISTS ux_users_nationalId ON users(nationalId) WHERE nationalId IS NOT NULL;

    CREATE TABLE IF NOT EXISTS departments (
      id      TEXT PRIMARY KEY,
      name    TEXT NOT NULL UNIQUE,
      version INTEGER NOT NULL DEFAULT 1
    );

    CREATE TABLE IF NOT EXISTS categories (
      id          TEXT PRIMARY KEY,
      name        TEXT NOT NULL UNIQUE,
      description TEXT NOT NULL DEFAULT '',
      version     INTEGER NOT NULL DEFAULT 1
    );

    CREATE TABLE IF NOT EXISTS assets (
      id             TEXT PRIMARY KEY,
      code           TEXT NOT NULL UNIQUE COLLATE NOCASE,
      oldCode        TEXT NOT NULL DEFAULT '',
      name           TEXT NOT NULL,
      categoryId     TEXT NOT NULL,
      brand          TEXT NOT NULL DEFAULT '',
      model          TEXT NOT NULL DEFAULT '',
      serial         TEXT NOT NULL DEFAULT '',
      quality        TEXT NOT NULL DEFAULT 'Good',
      owner          TEXT NOT NULL DEFAULT '',
      description    TEXT NOT NULL DEFAULT '',
      imageId        TEXT NOT NULL DEFAULT '',
      purchaseDate   TEXT,
      purchaseCost   INTEGER,
      hasLabel       INTEGER NOT NULL DEFAULT 1,
      status         TEXT NOT NULL DEFAULT 'Available',
      location       TEXT NOT NULL DEFAULT '',
      createdAt      TEXT NOT NULL,
      updatedAt      TEXT NOT NULL,
      lastOperationDate TEXT,
      deleted        INTEGER NOT NULL DEFAULT 0,
      version        INTEGER NOT NULL DEFAULT 1,
      FOREIGN KEY (categoryId) REFERENCES categories(id)
    );
    CREATE INDEX IF NOT EXISTS ix_assets_deleted_status ON assets(deleted, status);
    CREATE INDEX IF NOT EXISTS ix_assets_serial ON assets(serial);
    CREATE INDEX IF NOT EXISTS ix_assets_purchaseDate ON assets(purchaseDate);

    CREATE TABLE IF NOT EXISTS assignments (
      id            TEXT PRIMARY KEY,
      assetId       TEXT NOT NULL,
      userId        TEXT,
      departmentId  TEXT NOT NULL,
      startedAt     TEXT NOT NULL,
      endedAt       TEXT,
      notes         TEXT NOT NULL DEFAULT '',
      reference     TEXT NOT NULL DEFAULT '',
      endReason     TEXT NOT NULL DEFAULT '',
      createdBy     TEXT NOT NULL DEFAULT '',
      recipientName TEXT NOT NULL DEFAULT '',
      departmentName TEXT NOT NULL DEFAULT '',
      assetCodeAtIssue  TEXT NOT NULL DEFAULT '',
      assetNameAtIssue  TEXT NOT NULL DEFAULT '',
      serialAtIssue     TEXT NOT NULL DEFAULT '',
      qualityAtIssue    TEXT NOT NULL DEFAULT '',
      issuerName    TEXT NOT NULL DEFAULT '',
      endedBy       TEXT NOT NULL DEFAULT '',
      endReference  TEXT NOT NULL DEFAULT '',
      FOREIGN KEY (assetId) REFERENCES assets(id),
      FOREIGN KEY (userId) REFERENCES users(id),
      FOREIGN KEY (departmentId) REFERENCES departments(id)
    );
    CREATE UNIQUE INDEX IF NOT EXISTS ux_assignments_active ON assignments(assetId) WHERE endedAt IS NULL;
    CREATE INDEX IF NOT EXISTS ix_assignments_user ON assignments(userId, endedAt);
    CREATE INDEX IF NOT EXISTS ix_assignments_dept ON assignments(departmentId, endedAt);

    CREATE TABLE IF NOT EXISTS audit (
      id            INTEGER PRIMARY KEY AUTOINCREMENT,
      at            TEXT NOT NULL,
      actorId       TEXT,
      actorName     TEXT NOT NULL DEFAULT '',
      actorRole     TEXT NOT NULL DEFAULT '',
      ip            TEXT NOT NULL DEFAULT '',
      action        TEXT NOT NULL DEFAULT '',
      entityType    TEXT NOT NULL DEFAULT '',
      entityId      TEXT,
      assetId       TEXT,
      targetUserId  TEXT,
      description   TEXT NOT NULL DEFAULT '',
      before        TEXT,
      after         TEXT,
      correlationId TEXT NOT NULL DEFAULT ''
    );
    CREATE INDEX IF NOT EXISTS ix_audit_at ON audit(at);
    CREATE INDEX IF NOT EXISTS ix_audit_asset ON audit(assetId);
    CREATE INDEX IF NOT EXISTS ix_audit_target ON audit(targetUserId);
    CREATE INDEX IF NOT EXISTS ix_audit_entity ON audit(entityType, entityId);

    CREATE TABLE IF NOT EXISTS images (
      id          TEXT PRIMARY KEY,
      contentType TEXT NOT NULL,
      ownerUserId TEXT NOT NULL,
      createdAt   TEXT NOT NULL,
      size        INTEGER NOT NULL
    );

    CREATE TABLE IF NOT EXISTS sessions (
      id         TEXT PRIMARY KEY,
      userId     TEXT NOT NULL,
      token      TEXT NOT NULL,
      stamp      TEXT NOT NULL,
      createdAt  TEXT NOT NULL,
      lastSeenAt TEXT NOT NULL,
      expiresAt  TEXT NOT NULL,
      revokedAt  TEXT,
      ip         TEXT NOT NULL DEFAULT '',
      userAgent  TEXT NOT NULL DEFAULT '',
      FOREIGN KEY (userId) REFERENCES users(id)
    );
    CREATE INDEX IF NOT EXISTS ix_sessions_user ON sessions(userId, revokedAt);
    CREATE INDEX IF NOT EXISTS ix_sessions_expiresAt ON sessions(expiresAt);

    CREATE TABLE IF NOT EXISTS dispositionRequests (
      id            TEXT PRIMARY KEY,
      assetId       TEXT NOT NULL,
      targetStatus  TEXT NOT NULL,
      originalStatus TEXT NOT NULL,
      state         TEXT NOT NULL DEFAULT 'Pending',
      reason        TEXT NOT NULL DEFAULT '',
      reference     TEXT NOT NULL DEFAULT '',
      amount        INTEGER,
      effectiveDate TEXT NOT NULL,
      requestedBy   TEXT NOT NULL DEFAULT '',
      requesterName TEXT NOT NULL DEFAULT '',
      requestedAt   TEXT NOT NULL,
      decidedBy     TEXT,
      deciderName   TEXT,
      decidedAt     TEXT,
      decisionNote  TEXT NOT NULL DEFAULT '',
      assetVersion  INTEGER NOT NULL,
      version       INTEGER NOT NULL DEFAULT 1,
      FOREIGN KEY (assetId) REFERENCES assets(id)
    );
    CREATE UNIQUE INDEX IF NOT EXISTS ux_disposition_pending ON dispositionRequests(assetId) WHERE state = 'Pending';
    CREATE INDEX IF NOT EXISTS ix_disposition_state ON dispositionRequests(state, requestedAt);
  `);

  const ver = db.prepare('PRAGMA user_version').get().user_version;
  if (ver < SCHEMA_VERSION) db.exec(`PRAGMA user_version = ${SCHEMA_VERSION}`);
}

module.exports = { openDatabase, SCHEMA_VERSION };
