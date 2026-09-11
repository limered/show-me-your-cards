# AGENTS.md

Stack: .NET 10 minimal API (`dashboard/src/Api`), Vue 3 + TypeScript (`dashboard/src/web`), Postgres via EF Core (Npgsql in prod, SQLite file locally).

- API entrypoint is `dashboard/src/Api/Program.cs`. Without `DATABASE_URL` it uses a local SQLite `app.db`; with `DATABASE_URL` (Render Postgres URL) it uses Npgsql with `Migrate()` at startup.
- Web dev: `npm run dev --prefix dashboard/src/web` (proxies `/api` to localhost:5000). Deploy: root `Dockerfile` + `render.yaml` (single service serving `wwwroot` with fallback).
- Update this file when entrypoints or workflows change.

## Agent skills

### Issue tracker

Issues live as GitHub issues (`gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical labels, used as-is. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout (`CONTEXT.md` + `docs/adr/` at root). See `docs/agents/domain.md`.

### Commits

Use the /atomic-commit skill to stage and commit changes.

### Test harness

The factory test phase runs every test-harness.<name> command declared here. Any failure fails the run.

test-harness.api: dotnet test dashboard/src/Api.Tests
test-harness.web: npm test --prefix dashboard/src/web

### Comment Rules

- only comment your code, if it's absolutely necessery for understanding the code
- never describe in a comment what can be inferred from the function name or by reading the source
- never leave historical data in the comments
- never state an issue on which this change is based on
- do not include content in a comment that stems from the issue or the message history
- if you find a comment that is against this rules