# AGENTS.md — FinGuard

Shared instructions for every AI coding agent working in this repo (Claude Code, Cursor).
Keep this file short. Path-specific rules live in `.claude/rules/` and `.cursor/rules/`.
Anything that can be enforced is enforced by tests, analyzers or CI, not by this file.

## What FinGuard is
An entity uploads a CSV with its investment portfolio at a cut-off date. FinGuard validates it,
evaluates it against global maximum-allocation limits per asset class (`cash`, `fixed_income`,
`shares`, `real_estate`; everything in USD), returns `compliant` / `warning` / `breached`, raises
alerts near the limits and produces a rebalancing advisory (deterministic proposal + AI narrative).
This is a portfolio project: cost must stay near zero until Phase 5 (see ADR-0005).

## Stack (verified 2026-11-13; re-verify before upgrading, pin versions in `Directory.Packages.props`)
- .NET 10, ASP.NET Core Minimal APIs, EF Core + Npgsql, PostgreSQL.
- Dapper for read-side queries (ADR-0007).
- Aspire 17.x AppHost orchestrates API, Angular app and PostgreSQL container locally.
- Wolverine 6.x (CQRS handlers, messaging, PostgreSQL transactional outbox).
- Angular 22 (TypeScript 6, Node 24 LTS) with **pnpm** only.
- Microsoft Agent Framework 1.x (`Microsoft.Agents.AI`) over `Microsoft.Extensions.AI`, behind the `IAdvisoryNarrator` port (ADR-0006). Stable packages only.
- Azure (Phase 5 only): Container Apps, PostgreSQL Flexible Server, Key Vault, Application Insights,
  Azure AI Foundry, Microsoft Entra External ID.

## Repository layout
```
src/backend/
  FinGuard.AppHost/            Aspire orchestration
  FinGuard.ServiceDefaults/    OpenTelemetry, health checks, resilience
  FinGuard.Api/                composition root: maps endpoints, wires modules
  BuildingBlocks/              Result, Error, ITenantContext, base abstractions (no business rules)
  Modules/<Module>/            FinGuard.<Module> + FinGuard.<Module>.Contracts
src/frontend/finguard-web/     Angular app
tests/                         unit, integration, architecture tests
docs/adr/                      architecture decision records
docs/architecture/overview.md  diagrams
.github/workflows/             CI (CD arrives in Phase 5)
```
Modules: `Tenancy`, `Portfolio`, `Compliance`, `Advisory` (`Audit` is Phase 5, optional).

## Architecture rules (checked by `tests/FinGuard.ArchitectureTests`)
1. Modular monolith. Each module is organized as vertical slices: `Features/<UseCase>/`.
2. Compliance also has `Domain/` (rich DDD model, Specification pattern) that depends on nothing.
3. A module references another module only through its `*.Contracts` project (events, public queries).
4. One PostgreSQL database; one schema and one DbContext per module; no cross-schema joins or FKs.
5. Modules talk through integration events via Wolverine + transactional outbox.
6. Expected failures return `Result<T>`; exceptions are for unexpected faults only.
7. Every tenant-owned row has `tenant_id`, enforced by EF global query filters. Never take the tenant
   from a request body, route or CSV; it comes from `ITenantContext`.
8. The LLM never computes amounts, never decides compliance, never receives free-text CSV fields.
   Narratives use only `AdvisoryFacts` texts and pass `NarrativeValidator` (ADR-0014).
9. No event sourcing, no microservices, no generic repositories, no MediatR. Commands write with EF Core; queries
   read with Dapper over `NpgsqlDataSource`, own schema only, with a mandatory `tenant_id` filter (ADR-0007).

## Domain rules (source of truth: `docs/decisions.md`)
- Limits (max, % of total portfolio): cash 40, fixed_income 30, shares 25, real_estate 25. Versioned policy;
  each evaluation stores the policy version and a snapshot of limits.
- Utilization = (class value / total) / limit. `>= 0.90` alert. Exactly `1.00` is still within limit (warning).
  `> 1.00` breached. Precedence: breached > warning > compliant. Compare unrounded decimals.
- Rebalance (ADR-0013): Warning and Breached classes go to 0.85 x limit (configurable), floored to cents; the surplus
  goes to classes below their target proportionally to headroom. Order: breached classes first, then alert classes.
  Rounding to cents must preserve the total. Proposal status `NotNeeded`, `Full` or `Partial` (verification; no relax step).
- Advisory (ADR-0014): one plain-text English narrative per evaluation from `EvaluationCompleted`, written by the
  `TemplateNarrator` now (a model in #19 may replace it after validation). Evaluations stored before Advisory have none.
- Limits are evaluated on book value (ADR-0010).
- Portfolio validation: complete data, `book_value >= 0` per row and total > 0, currency `USD` only
  (case-insensitive), known asset class, description optional ≤ 150 characters and not stored. `position_id` and
  `description` are rejected when they start with a formula trigger (`=`, `+`, `-`, `@`, full-width forms, tab, CR, LF).
- CSV columns: `position_id, asset_class, book_value, currency, description`. Limits: 5 MB, 10,000 rows,
  UTF-8. Cut-off date comes from the upload form, not the CSV: from 2000-01-01 (first limit policy) up to today (UTC).
- CSV error codes are `Portfolio.Csv.*` (fixed messages, never cell values). Sample files with their expected
  outcomes: `samples/portfolios/README.md`.
- Several submissions per cut-off date are allowed. Submissions are immutable; latest accepted = current.
  Rejected submissions are stored with their errors. Same tenant + same cut-off date + same SHA-256 of the raw bytes
  returns the earliest existing submission, whatever its status (`200 OK`, `duplicate: true`, nothing stored); the
  file name does not matter. Current = latest accepted by `received_at` (tie: `id`) per cut-off date; rejected
  submissions are never current. Both are computed by the API, never by the SPA.

## Commands
- Run everything locally: `aspire run` (Docker must be running). Starts `postgres`, `api` and `web`; the dashboard URL is printed on start.
- Sign in locally: open the `web` URL from the dashboard; `/login` offers Alice (Acme Pension Fund) and Bob (Globex
  Insurance). Restarting the `api` resource invalidates every token, so the app returns to `/login` (R-18).
- Development token (Development only; users `alice`, `bob`; send as `Authorization: Bearer <accessToken>`):
  `Invoke-RestMethod -Method Post -Uri <api>/dev/token -Body '{"user":"alice"}' -ContentType 'application/json'`
- Backend: `dotnet build`, `dotnet test`, `dotnet format` (CI runs `dotnet format --verify-no-changes`).
  `dotnet test` runs on Microsoft.Testing.Platform (`global.json`) and needs Docker running (integration tests start
  PostgreSQL with Testcontainers). Filter example: `dotnet test --project tests/FinGuard.UnitTests --filter-method "*Result*"`.
- New migration (per module, design-time factory as startup):
  `dotnet tool run dotnet-ef migrations add <Name> --project src/backend/Modules/<M>/FinGuard.<M> --startup-project src/backend/Modules/<M>/FinGuard.<M> --context <M>DbContext --output-dir Persistence/Migrations`
- Frontend (from `src/frontend/finguard-web`): `pnpm install --frozen-lockfile`, `pnpm test`, `pnpm lint`, `pnpm build`. `pnpm start` only works under Aspire (it needs `PORT` and the Api URL).
- E2E (Playwright, local only): start `aspire run`, take the `web` URL from the dashboard, then from
  `src/frontend/finguard-web`: `$env:E2E_BASE_URL = '<web url>'; pnpm e2e`. First time: `pnpm exec playwright install chromium`.
- Never use `npm`, `yarn` or `npx`. Use `pnpm`, `pnpm exec`, `pnpm dlx`.
- Machine setup, shells, closing a session, resetting the dev database: `docs/dev-environment.md`.

## Closing a work session (Claude Code and Cursor)
Tool: `pwsh scripts/dev-session.ps1` (`-Snapshot`, `-StartAspire`, `-Report`, `-Stop`; `-WhatIf` works).
1. **Baseline first.** Run `-Snapshot` before any other command. Never stop a process or container from the baseline.
   If it warns about an "unknown owner" AppHost of this repo, ask the owner once whether it is theirs; do not assume.
2. **Start Aspire only with `-StartAspire`** (records PID, start time and branch). Never plain `aspire run`/`start`.
3. **Close what you open.** Before the final message run `-Stop`: `aspire stop` for recorded AppHosts, then the
   remaining new processes and containers, then `dotnet build-server shutdown`. Never remove Docker volumes.
4. **Close-out block** at the end of the final message, per branch: what was stopped and "Still running: none" (or
   what and why); `git log --oneline main..HEAD`; `git diff --stat main...HEAD`; the PR body path
   (`.git/finguard-pr-body*.md`, from `.github/pull_request_template.md`); the owner's commands (`git push -u origin
   <branch>`, `gh pr create --base main --head <branch> --title "<title>" --body-file <path>`, after merge
   `git switch main; git pull --ff-only; git branch -d <branch>`).

## Conventions
- Everything in the repo is written in English: code, comments, docs, commit messages, UI text, errors.
- Conventional Commits. Small PRs. One slice or one concern per PR.
- Git workflow: see docs/git-workflow.md (branch per brief, PR to main, squash merge, never commit to main).
- AI rules: see docs/ai-rules.md (path-scoped, Claude and Cursor files kept in sync).
- `docs/decisions.md` is append-only (CI-enforced).
- Nullable reference types on, warnings as errors, `TimeProvider` instead of `DateTime.UtcNow`.
- Structured logging with message templates. Never log monetary values, CSV rows or descriptions.
- No secrets in the repo. Locally use user-secrets or environment variables; the app must also read
  configuration from Azure Key Vault when `KeyVault:Uri` is set.

## Definition of done
Slice implemented, tests written (unit + integration through HTTP incl. a tenant-isolation case), architecture
tests green, no new analyzer warnings, ADR added or updated if a decision changed, `docs/decisions.md` delta noted,
README status updated when a phase or a visible feature changes.

## When unsure
Read `docs/adr/` first. If a task contradicts an ADR or a rule here, stop and ask instead of working around it.
