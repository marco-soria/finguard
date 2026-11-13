# FinGuard

An entity uploads a CSV with its investment portfolio at a cut-off date. FinGuard validates it, evaluates it
against global maximum-allocation limits per asset class (`cash`, `fixed_income`, `shares`, `real_estate`; all in
USD), returns `compliant` / `warning` / `breached`, raises alerts near the limits and produces a rebalancing
advisory (deterministic proposal + AI narrative).

Stack: .NET 10 modular monolith (Minimal APIs, EF Core + PostgreSQL, Wolverine), Aspire, Angular 22,
Microsoft Agent Framework. Azure only in Phase 5.

## Status
**Phase 0: repository foundation.** No application code yet.

## Prerequisites (Windows)
- Git
- .NET 10 SDK (version pinned in `global.json`)
- Node.js 24 LTS (see `.node-version`)
- pnpm via Corepack: `corepack enable` (never npm or yarn)
- Docker Desktop
- PowerShell 7 (`pwsh`)

## Quick start
Arrives in PR #2.

## AI coding agents
- `AGENTS.md`: shared rules for every agent (Claude Code, Cursor).
- `CLAUDE.md`: Claude Code specifics. Path-scoped rules live in `.claude/rules/`.
- `.cursor/rules/`: the same path-scoped rules for Cursor.
- `docs/briefs/`: one brief per PR, executed by an agent.

## Documentation
- [Architecture decision records](docs/adr/README.md)
- [Architecture overview](docs/architecture/overview.md)
- [Decisions log](docs/decisions.md)
- [CI/CD](docs/ci-cd.md)
- [Git workflow](docs/git-workflow.md)
