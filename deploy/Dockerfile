# AminAmval (امین اموال ناواکو) — production container image
# Runs standalone: Node.js API + SQLite + the original wwwroot UI in one process.
FROM node:22-bookworm-slim

ENV NODE_ENV=production \
    HOST=0.0.0.0 \
    PORT=8080 \
    PREVIEW_MODE=true \
    DATA_DIR=/data \
    WEBROOT=/app/wwwroot \
    BACKUP_HOUR_UTC=22 \
    BACKUP_MINUTE_UTC=30 \
    BACKUP_RETENTION_DAYS=14

WORKDIR /app

# 1) dependencies first (layer cache)
COPY package.json package-lock.json ./
RUN npm ci --omit=dev

# 2) application code
COPY lib ./lib
COPY handlers ./handlers
COPY server.js ./

# 3) the UI, served from /app/wwwroot (WEBROOT)
COPY wwwroot/ /app/wwwroot/

# persistent state volume (database, uploads, session keys, backups, credentials)
VOLUME ["/data"]

EXPOSE 8080

# run as a non-root user for defense-in-depth
RUN useradd --create-home --uid 10001 app \
    && mkdir -p /data \
    && chown -R app:app /app /data
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD node -e "require('http').get('http://127.0.0.1:'+(process.env.PORT||8080)+'/api/health',r=>process.exit(r.statusCode===200?0:1)).on('error',()=>process.exit(1))"

CMD ["node", "server.js"]
