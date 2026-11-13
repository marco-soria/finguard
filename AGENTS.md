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
- Aspire 13.x AppHost orchestrates API, Angular app and PostgreSQL container locally.
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
9. No event sourcing, no microservices, no generic repositories, no MediatR.

## Domain rules (source of truth: `docs/decisions.md`)
- Limits (max, % of total portfolio): cash 40, fixed_income 30, shares 25, real_estate 25. Versioned policy;
  each evaluation stores the policy version and a snapshot of limits.
- Utilization = (class value / total) / limit. `>= 0.90` alert. Exactly `1.00` is still within limit (warning).
  `> 1.00` breached. Precedence: breached > warning > compliant. Compare unrounded decimals.
- Rebalance target = 0.85 x limit (configurable); relax to 0.90 if infeasible and mark the proposal `Partial`.
  Order: breached classes first, then alert classes. Surplus goes to other classes proportionally to headroom.
  Rounding to cents must preserve the total.
- Portfolio validation: complete data, no negatives, total > 0, currency `USD`, known asset class.
- CSV columns: `position_id, asset_class, market_value_usd, currency, description`. Limits: 5 MB, 10,000 rows,
  UTF-8. Cut-off date comes from the upload form, not the CSV.
- Several submissions per cut-off date are allowed. Submissions are immutable; latest accepted = current.
  Rejected submissions are stored with their errors. Same SHA-256 + same cut-off date returns the existing submission.

## Commands (update after the Phase 0 scaffold exists)
- Run everything locally: `aspire run` (Docker must be running).
- Backend tests: `dotnet test`.
- Frontend: `pnpm install --frozen-lockfile`, `pnpm test`, `pnpm lint`, `pnpm build` (from `src/frontend/finguard-web`).
- Never use `npm`, `yarn` or `npx`. Use `pnpm`, `pnpm exec`, `pnpm dlx`.

## Conventions
- Everything in the repo is written in English: code, comments, docs, commit messages, UI text, errors.
- Conventional Commits. Small PRs. One slice or one concern per PR.
- Nullable reference types on, warnings as errors, `TimeProvider` instead of `DateTime.UtcNow`.
- Structured logging with message templates. Never log monetary values, CSV rows or descriptions.
- No secrets in the repo. Locally use user-secrets or environment variables; the app must also read
  configuration from Azure Key Vault when `KeyVault:Uri` is set.

## Definition of done
Slice implemented, tests written (unit + integration through HTTP incl. a tenant-isolation case), architecture
tests green, no new analyzer warnings, ADR added or updated if a decision changed, `docs/decisions.md` delta noted.

## When unsure
Read `docs/adr/` first. If a task contradicts an ADR or a rule here, stop and ask instead of working around it.
