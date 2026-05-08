/**
 * node-app — sample service wired into the AspireC4 TestAppHost.
 *
 * Aspire injects connection strings as environment variables:
 *   ConnectionStrings__redis   → StackExchange.Redis format  (e.g. "localhost:6379")
 *   ConnectionStrings__posgres → Npgsql ADO.NET format       (e.g. "Host=localhost;Port=5432;...")
 *
 * Endpoints:
 *   GET /health          — liveness probe
 *   GET /ping/redis      — PING the Redis server
 *   GET /ping/postgres   — run SELECT 1 against PostgreSQL
 */

import express from 'express'
import Redis from 'ioredis'
import pg from 'pg'

const PORT = parseInt(process.env.PORT ?? '3000', 10)

// ---------------------------------------------------------------------------
// Connection-string parsers
// ---------------------------------------------------------------------------

/**
 * Converts an Aspire/StackExchange.Redis connection string to ioredis options.
 * Handles:
 *   - bare "host:port"
 *   - "host:port,password=xxx,ssl=true"
 *   - "redis[s]://…" URLs (pass-through)
 */
function redisOptions(cs) {
  if (!cs) return { host: 'localhost', port: 6379 }
  if (cs.startsWith('redis://') || cs.startsWith('rediss://')) {
    const url = new URL(cs)
    return {
      host: url.hostname,
      port: Number(url.port) || 6379,
      ...(url.password ? { password: decodeURIComponent(url.password) } : {}),
    }
  }

  // StackExchange.Redis format:  "host:port[,key=value,...]"
  const [endpoint, ...pairs] = cs.split(',')
  const [host, port = '6379'] = endpoint.split(':')
  const extras = Object.fromEntries(pairs.map(p => p.split('=')))
  return {
    host,
    port: Number(port),
    ...(extras.password ? { password: extras.password } : {}),
    ...(extras.ssl === 'true' ? { tls: {} } : {}),
  }
}

/**
 * Converts an Aspire/Npgsql ADO.NET connection string to a node-postgres Pool config.
 * Handles:
 *   - "Host=…;Port=…;Username=…;Password=…;Database=…"
 *   - "postgresql://…" / "postgres://…" URLs (pass-through)
 */
function postgresConfig(cs) {
  if (!cs) return { host: 'localhost', port: 5432 }
  if (cs.startsWith('postgresql://') || cs.startsWith('postgres://'))
    return { connectionString: cs }

  const map = Object.fromEntries(
    cs
      .split(';')
      .filter(Boolean)
      .map(pair => {
        const idx = pair.indexOf('=')
        return [pair.slice(0, idx).trim().toLowerCase(), pair.slice(idx + 1).trim()]
      })
  )
  return {
    host: map['host'] ?? 'localhost',
    port: Number(map['port'] ?? 5432),
    user: map['username'] ?? map['user id'] ?? 'postgres',
    password: map['password'],
    database: map['database'] ?? 'postgres',
  }
}

// ---------------------------------------------------------------------------
// Clients  (lazy-connect so startup never blocks if a dependency is slow)
// ---------------------------------------------------------------------------

const redis = new Redis({
  ...redisOptions(process.env['ConnectionStrings__redis']),
  lazyConnect: true,
  enableOfflineQueue: false,
  connectTimeout: 5_000,
  maxRetriesPerRequest: 1,
})

redis.on('error', err => console.error('[redis] connection error:', err.message))

const pgPool = new pg.Pool({
  ...postgresConfig(process.env['ConnectionStrings__posgres']),
  connectionTimeoutMillis: 5_000,
  max: 3,
})

// ---------------------------------------------------------------------------
// HTTP server
// ---------------------------------------------------------------------------

const app = express()
app.use(express.json())

/** GET /health — liveness probe; always 200 while the process is running */
app.get('/health', (_req, res) => {
  res.json({ status: 'ok', service: 'node-app' })
})

/** GET /ping/redis — verifies Redis connectivity with a PING command */
app.get('/ping/redis', async (_req, res) => {
  try {
    // connect() is idempotent when already connected
    await redis.connect().catch(() => {})
    const response = await redis.ping()
    res.json({ status: 'ok', response })
  } catch (err) {
    console.error('[redis] ping failed:', err.message)
    res.status(503).json({ status: 'error', message: err.message })
  }
})

/** GET /ping/postgres — verifies PostgreSQL connectivity with SELECT 1 */
app.get('/ping/postgres', async (_req, res) => {
  try {
    const { rows } = await pgPool.query('SELECT 1 AS ping, now() AS ts')
    res.json({ status: 'ok', response: rows[0] })
  } catch (err) {
    console.error('[postgres] ping failed:', err.message)
    res.status(503).json({ status: 'error', message: err.message })
  }
})

app.listen(PORT, () => {
  console.log(`node-app listening on port ${PORT}`)
  console.log(`  redis   → ${process.env['ConnectionStrings__redis'] ?? '(default localhost:6379)'}`)
  console.log(`  postgres→ ${process.env['ConnectionStrings__posgres'] ?? '(default localhost:5432)'}`)
})
