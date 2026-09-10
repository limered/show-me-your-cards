# AGENTS.md

Greenfield repo: `README.md` ("A simple Scrum Poker Game") + `LICENSE` only. Single commit, no code, no toolchain, no CI.

- No build / test / lint commands exist yet. Don't assume a stack; confirm with user before scaffolding.
- Update this file when a stack, entrypoint, or workflow is established.

## Agent skills

### Issue tracker

Issues live as GitHub issues (`gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical labels, used as-is. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout (`CONTEXT.md` + `docs/adr/` at root). See `docs/agents/domain.md`.

Test harness

The factory test phase runs every test-harness.<name> command declared here. Any failure fails the run.

test-harness.api: dotnet test dashboard/src/Api.Tests
test-harness.web: npm test --prefix dashboard/src/web