# AGENTS.md

Stack: .NET 10 minimal API (`Api`), Vue 3 + TypeScript (`web`), Postgres via EF Core (Npgsql in prod, SQLite file locally).

- API entrypoint is `Api/Program.cs`. Without `DATABASE_URL` it uses a local SQLite `app.db`; with `DATABASE_URL` (Render Postgres URL) it uses Npgsql with `Migrate()` at startup.
- Web dev: `npm run dev --prefix web` (proxies `/api` to localhost:5000). Deploy: root `Dockerfile` + `render.yaml` (single service serving `wwwroot` with fallback).
- Update this file when entrypoints or workflows change.

## Frontend Architecture

The frontend (web) groups code semantically by feature/theme, not by kind/technology. Do not create top-level services/, models/, or components/ folders.

Each theme owns a folder split by role: View/components/, View/models/, View/services/.
Shared code lives under _shared/ (_shared/components/, _shared/models/, _shared/services/).
Promote on second use: a component or model starts in its owning theme folder and moves to _shared/ only the first time a second theme needs it. Nothing goes in _shared/ speculatively.
Tests live next to the tested file.

Extract Components as soon as possible to reach maximum reusability. 

## Backend Architecture

Clean Onion Architecture

Top level folders are grouped by feature and support locality of change.
No seperate folder for services, models, etc. 
Prefer deep modules.

## Agent skills

### Issue tracker

Issues live as GitHub issues (`gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical labels, used as-is. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout (`CONTEXT.md` + `docs/adr/` at root). See `docs/agents/domain.md`.

### Commits

Use the /atomic-commit skill to stage and commit changes.

## Test harness

The factory test phase runs every test-harness.<name> command declared here. Any failure fails the run.

test-harness.api: dotnet test Api.Tests
test-harness.web: npm test --prefix web

## Testing Rules

- Every new component or class ships with tests. Tests need not be 1:1 per function, but every code path (branch, loop, error case) must be covered.

## Code Design

- Keep every function's cyclomatic complexity at 10 or below. Split or extract when a function exceeds it.

## Comment Rules

- only comment your code, if it's absolutely necessery for understanding the code
- never describe in a comment what can be inferred from the function name or by reading the source
- never leave historical data in the comments
- never state an issue on which this change is based on
- do not include content in a comment that stems from the issue or the message history
- if you find a comment that is against this rules