# Render deploy shape: .NET + Vue on one service (research for #6)

Date: 2026-09-10. Sources: Render docs (web-services, docker, websockets,
free tier, postgres connect, env/secrets, blueprint-spec), Microsoft Learn
(SignalR hosting/scaling), Npgsql docs (security, pooling, EF Core provider).
Uncertain items are marked [UNVERIFIED].

## 1. Recommended image shape: one Dockerfile, one Render web service

- Render has **no native .NET runtime** (native: Node, Python, Ruby, Go, Rust,
  Elixir). .NET must deploy as **Language = Docker**, building from a
  `Dockerfile` at repo root via BuildKit. Image **must be linux/amd64**.
- Shape: **multi-stage Dockerfile, .NET serves the Vue `dist`** — no second
  service, no static site:
  1. `webbuild`: `node:22-bookworm-slim` (or 20 LTS), `npm ci && npm run build`
     → `dist/`.
  2. `dotnetbuild`: `mcr.microsoft.com/dotnet/sdk:8.0`, `dotnet publish -c Release`.
  3. runtime: `mcr.microsoft.com/dotnet/aspnet:8.0`,
     `COPY --from=webbuild …/dist → wwwroot`, `COPY --from=dotnetbuild …/publish`.
- Why .NET 8: LTS with the widest doc/sample coverage (8.0 supported through
  Nov 2026; 10.0 is the newer LTS, 9.0 is STS). Pin `8.0` patch-floating tags;
  switch to `10.0` only if the build track already standardises on it.
- ASP.NET serves the SPA: `app.UseStaticFiles()` +
  `app.MapFallbackToFile("index.html")`; API under `/api`, SignalR hub under
  e.g. `/hub` — same origin, so no CORS in prod.
- Port binding is the #1 deploy-failure cause: bind **`0.0.0.0:$PORT`**
  (Render default `PORT=10000`). .NET 8 official images listen on **8080** by
  default, so override — e.g. `ENV ASPNETCORE_URLS=http://+:10000` or read
  `PORT` at startup. Reserved ports to avoid: 18012, 18013, 19099. Only one
  public HTTP port per service is forwarded.
- Declare it as code later with `render.yaml`: one `type: web` (docker,
  `dockerfilePath: ./Dockerfile`) + one `type: pserv`/`db` Postgres, wiring the
  DB via `fromDatabase: { name, property: connectionString }`.

## 2. SignalR / WebSocket on Render

- Supported with **zero config**: deploy as a normal web service, connect via
  **`wss://<service>.onrender.com/hub`**. No fixed idle timeout on WS.
- **Single instance ⇒ no sticky sessions needed** (per Microsoft: one server /
  one process is exempt). Keep `numInstances: 1` / manual scaling off.
- [UNVERIFIED — no Render doc found] Render documents **no session-affinity /
  sticky-session toggle**. So horizontal scale-out is NOT a safe path: SignalR
  negotiate + transport requests must hit the same server. If scale-out is ever
  needed, add a backplane (**Render Key Value**, Valkey/Redis-compatible) AND
  confirm affinity support with Render support first. Until then: 1 instance.
- Design for drops: deploys/maintenance close all connections (SIGTERM, ~30s
  grace, configurable to 300s). Use `.withAutomaticReconnect()` on the client
  plus server-side graceful shutdown; game state must live in Postgres, not in
  hub memory.
- Free-tier interaction: a free web service **spins down after 15 min without
  inbound HTTP *or* WS messages** (Feb-2026 changelog: WS traffic keeps it
  alive); next request/new WS connection takes **~1 min** to wake. 750 free
  instance-hours per workspace/month; paid tiers don't spin down.

## 3. Hosted Postgres from EF Core (Npgsql)

- Use the **Internal Database URL** (`postgres://user:pass@host/dbname`) for
  the Render service (same region/workspace, private network, lower latency,
  no egress, **no `sslmode` needed**). Use the **External URL**
  (`…?sslmode=require`, TLS 1.2+) only for local dev / CI / tools.
- Npgsql: default `SslMode=Prefer` negotiates TLS, but be explicit —
  production (internal URL): leave default / no sslmode; local dev (external
  URL): `Ssl Mode=Require` (i.e. `sslmode=require`). `VerifyCA`/`VerifyFull`
  need Render's CA bundle — skip unless policy demands it.
- Pooling: Npgsql pools **by default** via `NpgsqlDataSource` (EF manages it).
  Keep defaults; only tune `Maximum Pool Size` down if the free DB's
  connection cap is hit. (`Pooling=false` / `No Reset On Close` only matters
  with an external pooler like PgBouncer — not our shape.)
- Migrations: run `dotnet ef database update` as a Render **pre-deploy
  command** or `db.Database.Migrate()` at startup — pick one in the build
  track; either is fine for v1.
- Free Postgres limits (decisions matter): **1 GB storage, 1 free DB per
  workspace, NO backups/pooling mgmt, EXPIRES 30 days after creation + 14-day
  grace then DATA DELETED**. Fine for prototype, not for anything real —
  budget a paid instance before the 30-day clock runs out.

## 4. Secrets / env on Render

- Secrets live in the **Dashboard → Service → Environment** tab (env vars) or
  `render.yaml` `envVars` — **never hardcode values in yaml/repo**. Pattern:
  non-secret as `value:`, secret as `sync: false` placeholder (filled in
  Dashboard on first deploy), DB as `fromDatabase`. Env groups share vars
  across services; secret files (>env size or key files) land at
  `/etc/secrets/<filename>` (1 MB total cap).
- Render encrypts env at rest (AES-128+) and in transit (TLS 1.2+), but values
  printed to stdout/stderr **do land in logs in plaintext** — don't log the
  connection string. Note: for Docker builds Render maps env to build `ARG`s,
  which can linger in image layers — keep build-time secrets in secret files.
- Minimal prod env: `DATABASE_URL` (fromDatabase, internal URL),
  `ASPNETCORE_ENVIRONMENT=Production`, `PORT` (default 10000, usually unset).

## 5. Local-dev ↔ Render mapping (one path for build work)

| Concern | Local dev | On Render |
|---|---|---|
| Frontend | `npm run dev` (Vite HMR) + proxy `/api`,`/hub` → `http://localhost:5000` (or 8080) | `dist/` served by Kestrel, same origin — no proxy, no CORS |
| Backend | `dotnet run` (reads `PORT` fallback e.g. 5000, `ASPNETCORE_ENVIRONMENT=Development`) | Kestrel binds `0.0.0.0:$PORT` (10000) |
| DB | External URL in local `DATABASE_URL` (e.g. `.env`, gitignored) with `sslmode=require` | Internal URL in `DATABASE_URL` via `fromDatabase` |
| Hub URL | `http://localhost:5000/hub` (or proxied same-origin) | `wss://<service>.onrender.com/hub` (relative path in client: `/hub`) |

Rule for later work: frontend uses **relative URLs** (`/api`, `/hub`) so the
same build runs locally (behind Vite proxy) and on Render (same origin).

## 6. Open risks / decisions for build tracks

1. **Free Postgres 30-day expiry** — calendar a paid upgrade or DB migration
   before expiry; no backups on free.
2. **Cold starts kill SignalR** on free (~1 min wake) — acceptable for v1 demo,
   paid tier removes it.
3. **Scale = 1 instance max** until affinity/backplane story is proven
   ([UNVERIFIED] Render affinity). Fine for poker-room scale; revisit with
   Render Key Value backplane only if needed.
4. **.NET version pin** — recommended `8.0` LTS images; confirm in build track.
5. **Build minutes / bandwidth quotas** on free — Docker multi-stage builds
   consume pipeline minutes; keep `dist`/publish layer caching sane.
