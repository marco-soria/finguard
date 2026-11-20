# ADR-0012: Compliance domain model

- Status: Accepted
- Date: 2026-11-20

## Context
Phase 3 evaluates a portfolio against global maximum-allocation limits. AGENTS.md makes Compliance the only module
with a rich domain (DDD, Specifications, repositories) and says `Domain/` depends on nothing. Since session 5 it was
open whether that domain may use `Result`/`Error` from BuildingBlocks. The limits are versioned, and an evaluation must
stay reproducible when the policy changes.

## Decision
- **Dependencies (allow-list).** Types in `FinGuard.Compliance.Domain(.*)` may depend only on `System(.*)`, the domain
  itself and `FinGuard.BuildingBlocks.Results` (`Result`, `Error`, which depend only on `System`). An architecture test
  enforces it with positive results. It loads Compliance's direct references, because the shared test architecture
  loads only `FinGuard.*` assemblies and cannot see a dependency on EF Core, Wolverine or ASP.NET.
- **One project.** Compliance stays one assembly with `Domain/` (model), `Features/` (application) and `Persistence/`
  (infrastructure). The dependency direction is enforced by tests, not by project references.
- **Expected vs unexpected failures.** `LimitPolicy.Create` returns `Result<LimitPolicy>`: an invalid configuration is
  expected (seed, future administration). `Evaluation.Create` takes already-valid inputs and throws
  `ArgumentException` on a missing class or a zero total: upstream guarantees them, so they are bugs.
- **Policy versions.** Immutable, global (not tenant-owned) rows in `compliance.limit_policies`: four limit columns, the
  alert threshold and the rebalance target ratio (`numeric(5,4)`), a unique `effective_from`. A change is a new version.
  CHECK constraints mirror the invariants; the repository maps rows through `LimitPolicy.Create` and throws on a
  failure as a second line of defence. Seed v1 is effective from 2000-01-01.
- **Which version applies.** The version in force at the cut-off date: the highest `effective_from` on or before it.
  Before the first version the repository returns null, and the caller decides (Portfolio accepts such dates).
- **Statuses and alerts.** Utilization = (class value / total) / limit, unrounded `decimal`. Class status: `Breached`
  when u > 1, `Warning` when threshold <= u <= 1, else `Compliant`. Overall status by precedence breached > warning >
  compliant. One alert per `Warning` or `Breached` class, carrying class, status and utilization. Statuses are derived
  with in-memory Specifications (`IsInAlert`, `IsBreached`, `And`, `Not`).
- **Reproducibility.** An evaluation keeps a snapshot of the policy it used: version, four limits, threshold, target.

## Consequences
- The domain can return the same `Result` the slices map to HTTP, without a shared-kernel assembly.
- A new dependency in `Domain/` fails the build's tests, including framework packages.
- Re-evaluating an old cut-off date uses the limits that were in force then, not today's.
- **R-37 Decimal division.** `decimal` keeps 28-29 significant digits, so repeating fractions are truncated. Inputs are
  cents and thresholds have at most 4 decimals, so the boundary comparisons stay exact; the boundary tests assert the
  exact utilization values.

## Alternatives
- A separate shared-kernel assembly for `Result`: rejected, namespace churn across every module for no runtime gain.
- Exceptions for expected failures: rejected, locked decision (AGENTS.md rule 6).
- `Result` everywhere, including `Evaluation.Create`: rejected, it would hide bugs as business outcomes.
- Domain, Application and Infrastructure projects: rejected, more projects for the same guarantees.
- Mutable policy or a per-class child table: rejected, four fixed classes and reproducibility (D-157 style).
- "Latest version at evaluation time": rejected, re-evaluating an old date would use today's limits.
- Per-tenant policies: out of scope (limits are global).
