# Brief #13 — Compliance domain model and versioned limit policy (opens Phase 3)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-13-compliance-domain-policy.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-13-compliance-domain-policy.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/compliance-domain-and-policy` |
| PR title (exact, becomes the squash commit) | `feat: add the Compliance domain model and versioned limit policy` |
| Phase | Phase 3, first of three (#13 domain + policy, #14 evaluation flow + `EvaluationCompleted` + read API, #15 result UI). Phase 3 exit: boundary tables green (0.8999 to 1.0001); result visible in the UI |

---

## Goal
Build the pure Compliance domain (value objects, `LimitPolicy` aggregate with invariants, `Evaluation` aggregate,
specifications, statuses and alerts) with the mandatory boundary tests, and persist the versioned global limit policy
with its seed and repository. No evaluation is stored or published yet (Brief #14).

## Context
- Read first and treat as binding: `AGENTS.md` (domain rules, "Closing a work session"), `CLAUDE.md`,
  `.claude/rules/compliance-domain.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`,
  `docs/decisions.md` (newest delta: session 21 executed), ADR-0001, ADR-0002, ADR-0007, ADR-0010, ADR-0011.
- Compliance today: `ComplianceDbContext` (public sealed, D-161), `PortfolioSnapshot` (one row per accepted
  submission, four class columns in book value), `SubmissionAcceptedHandler`. `Domain/` is empty;
  `ComplianceDomainTests` guards it with `WithoutRequiringPositiveResults()` until the first domain type exists.
- Domain rules (AGENTS.md, compliance-domain rules): limits global, maximums only, % of total book value; utilization =
  (class value / total) / limit; `>= 0.90` alert; exactly `1.00` is warning; `> 1.00` breached; precedence breached >
  warning > compliant; compare unrounded decimals; policy invariants `RebalanceTargetRatio x sum(limits) >= 1` and
  `RebalanceTargetRatio < AlertThreshold`; evaluations store the policy version and a snapshot of limits and thresholds;
  total zero never reaches Compliance.
- Seed limits (AGENTS.md): cash 0.40, fixed_income 0.30, shares 0.25, real_estate 0.25; alert threshold 0.90;
  rebalance target ratio 0.85 (0.85 x 1.20 = 1.02 >= 1).
- Open question since session 5: may the Compliance domain use `Result`/`Error`? `FinGuard.BuildingBlocks.Results`
  types depend only on `System.*` (checked: `Result.cs`, `Error.cs` import only `System.Diagnostics.CodeAnalysis`);
  the HTTP mapping lives in `FinGuard.BuildingBlocks.Http`.
- The rebalance engine is Phase 4: do not build it here, but keep `RebalanceTargetRatio` in the policy because its
  invariant is a policy invariant.

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| `Result` in the domain | Allowed: `Domain/` may depend on `System.*`, `FinGuard.Compliance.Domain.*` and `FinGuard.BuildingBlocks.Results` only. The architecture rule becomes an allow-list and requires positive results | Moving `Result` to a new shared-kernel assembly (namespace churn across every module for no runtime gain); exceptions for expected failures (locked decision) |
| Expected vs unexpected | `LimitPolicy.Create(...)` returns `Result<LimitPolicy>` (invalid configuration is an expected failure: seed, future admin). `Evaluation.Create(...)` takes already-valid inputs and throws `ArgumentException` on impossible ones (missing class, total 0): upstream guarantees them, so they are bugs | Result everywhere (hides bugs as business outcomes) |
| Project layout | Stay one project `FinGuard.Compliance` with `Domain/`, `Features/` (application), `Persistence/` (infrastructure); dependency direction enforced by architecture tests | Splitting into Domain/Application/Infrastructure projects (more projects, same guarantees) |
| Value objects | `readonly record struct`s: `Money` (decimal, ≥ 0, USD implied by ADR-0010), `Ratio` (decimal), `AssetClass` enum (Cash, FixedIncome, Shares, RealEstate) owned by the domain | Reusing `AssetClassCode` from Portfolio.Contracts in the domain (domain must not depend on another module's contract; mapping happens in `Features/`, Brief #14) |
| Statuses | Class status: `Compliant` (u < threshold), `Warning` (threshold ≤ u ≤ 1), `Breached` (u > 1). Overall status by precedence. An **alert** is raised for every class with status `Warning` or `Breached`, carrying the class, status and utilization | Separate "alert" and "warning" concepts (one rule, one word set) |
| Specifications | Minimal `Specification<T>` base (`IsSatisfiedBy`, `And`, `Not`), and `IsInAlert(threshold)`, `IsBreached` over a `ClassUtilization` value. Used by `Evaluation` to derive statuses. Domain-only, never EF expressions | A generic expression-tree specification library (over-engineering) |
| Policy versions | Immutable versions: `version` (int, PK), `effective_from` (date, unique), four limit columns (`numeric(5,4)`), `alert_threshold`, `rebalance_target_ratio`, `created_at`. A change is a new version; rows are never updated | Mutable policy; per-class child table (four fixed classes, as D-157) |
| Which version applies | The version **in force at the cut-off date**: highest `effective_from` ≤ cut-off date. Seed v1 is effective from `2000-01-01`, so every realistic date has a policy | "Latest version at evaluation time" (re-evaluating an old date would use today's limits) |
| Global, not tenant-owned | `limit_policies` has no `tenant_id` and is not `ITenantOwned` (limits are identical for every entity). It is read-only for the application | Per-tenant copies (out of scope: per-entity limits) |
| Seed | EF migration `AddLimitPolicies` creates the table and inserts v1 through `HasData` or migration SQL; same rows in every environment | Runtime seeding (environment-dependent) |
| Repository | `ILimitPolicyRepository.GetEffectiveAsync(DateOnly cutOffDate, CancellationToken)` in `Domain/` (interface), EF implementation in `Persistence/` using `AsNoTracking`, mapping rows through `LimitPolicy.Create` (a stored row that fails the invariants throws: corrupted data is unexpected). Only Compliance has repositories (locked) | Dapper for this read (it feeds a command in Brief #14, so it belongs to the write side) |
| Majors review | Report only: re-check the three "plan a brief" rows of the session 18 review (Aspire 17, pnpm 12, Node 26) and any new major since 2026-11-19. Recommend when to schedule each (before Phase 5 at the latest) | Upgrading inside a domain brief |

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot` first, Aspire only through `-StartAspire`
  (only needed for the migration check), `-Stop` before the final message, Close-out block with "Still running: none".
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Logs and `ToString()` carry identifiers only (D-162): a policy is identified by its version.
- `docs/decisions.md` is append-only (D-137).
- Ask before deviating; record every approved deviation in the delta.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/compliance-domain-and-policy
scripts/dev-session.ps1 -Snapshot
dotnet build -c Release; dotnet test --no-build -c Release
```
Then the majors review (report only), with sources, into the execution delta.

## Tasks

### 1. Architecture rule first
Rewrite `ComplianceDomain_AnyType_DependsOnNoInfrastructure` as an allow-list
(`ComplianceDomain_AnyType_DependsOnlyOnSystemItselfAndResults`): types in `FinGuard.Compliance.Domain(.*)` may depend
only on `System(.*)`, `FinGuard.Compliance.Domain(.*)` and `FinGuard.BuildingBlocks.Results`. Remove
`WithoutRequiringPositiveResults()` once the first domain type exists (same commit as the domain). Prove it with a
temporary probe (a domain type using an EF or Portfolio.Contracts type fails), then revert the probe.

### 2. Domain (`Domain/`)
- Value objects: `Money`, `Ratio`, `AssetClass`, `ClassUtilization` (class, value, share, limit, utilization; all
  unrounded `decimal`).
- `LimitPolicy` aggregate: `Version`, `EffectiveFrom`, `Limits` (exactly one per `AssetClass`, each in (0, 1]),
  `AlertThreshold` in (0, 1], `RebalanceTargetRatio` in (0, 1). `Create(...)` returns `Result<LimitPolicy>` with codes
  `Compliance.Policy.LimitMissing`, `LimitOutOfRange`, `AlertThresholdOutOfRange`, `TargetRatioOutOfRange`,
  `TargetNotBelowAlert`, `LimitsTooTightForTarget` (`ratio x sum(limits) < 1`). Messages are fixed sentences.
- `Evaluation` aggregate: `Evaluation.Create(Guid submissionId, DateOnly cutOffDate, IReadOnlyDictionary<AssetClass,
  Money> classValues, LimitPolicy policy, DateTimeOffset evaluatedAt)`. Guards: all four classes present, total > 0.
  Holds per-class results (`ClassUtilization` + `ClassStatus`), `OverallStatus`, `Alerts`, and a **policy snapshot**
  (version, four limits, alert threshold, target ratio) so it stays reproducible. Immutable after creation; no setters.
  `Id = Guid.CreateVersion7(evaluatedAt)`.
- Specifications: `Specification<T>`, `IsInAlertSpecification(Ratio threshold)`, `IsBreachedSpecification`.
- `ILimitPolicyRepository` interface.
- No EF attributes, no Wolverine, no logging, no clock (`evaluatedAt` is passed in).

### 3. Persistence (`Persistence/`)
- `LimitPolicyRow` (persistence model, `internal`) mapped to `compliance.limit_policies` as decided; `numeric(5,4)` for
  ratios; unique index on `effective_from`. Migration `AddLimitPolicies` with v1 seed. `has-pending-model-changes`
  clean.
- `LimitPolicyRepository` (EF, `AsNoTracking`): highest `effective_from` ≤ cut-off date; maps through
  `LimitPolicy.Create`, throwing on failure. Registered in `ComplianceModule` (type-based registration, D-74 style).
- The `Evaluation` aggregate is **not** mapped yet (Brief #14).

### 4. Docs and rules
- `.claude/rules/compliance-domain.md` + `.cursor/rules/compliance-domain.mdc` (same commit): the allow-list rule;
  "`Result` for expected domain failures (policy creation), guards that throw for impossible inputs"; "policy in force
  at the cut-off date"; alert definition.
- `docs/adr/0012-compliance-domain-model.md` (short): `Result` in the domain via the allow-list, single-project Clean
  Architecture enforced by tests, versioned immutable global policy selected by cut-off date, statuses and alerts.
  Add to `docs/adr/README.md`.
- `README.md` status: Phase 3 in progress (D-143).
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta (with the majors report) on top.

## Tests that prove it
Unit (pure domain, no I/O, table-driven):
- **Boundary table, per class** (all four classes, mandatory): utilization `0.8999`, `0.90`, `0.9001`, `0.9999`,
  `1.00`, `1.0001`, plus a class with value 0. Expected class status: Compliant, Warning, Warning, Warning, Warning,
  Breached, Compliant. Build inputs from totals that make the utilization exact in `decimal` (e.g. total 1,000,000.00;
  shares 225,000.00 → u = 0.90; shares 250,025.00 → u = 1.0001) and assert the exact utilization value too.
- Overall status precedence: one breached + one warning → Breached; only warnings → Warning; none → Compliant.
- Alerts: one per Warning/Breached class, none for Compliant; carries class, status, utilization.
- Unrounded comparison: a case where rounding to 2 or 4 decimals would flip the status (e.g. u = 0.899999…) stays
  Compliant; a case just above 1.00 stays Breached.
- Policy invariants: each error code with a minimal failing input; the seed values pass; `0.85 x 1.20 = 1.02` passes
  and a set summing to 1.17 with target 0.85 fails `LimitsTooTightForTarget`; target 0.90 with threshold 0.90 fails
  `TargetNotBelowAlert`.
- `Evaluation.Create` guards: missing class, total 0 → `ArgumentException`; policy snapshot equals the policy used.
- Specifications: `IsInAlert`, `IsBreached`, `And`, `Not` truth tables.
- `ToString()` of domain types used in messages later prints no amounts (marker test) — at least `Evaluation` and
  `ClassUtilization`.
Integration (real PostgreSQL):
- `Migrations_Apply_SeedPolicyVersionOneExists` (values exactly as seeded).
- `GetEffective_CutOffBeforeFirstPolicy_...`: not reachable with the 2000-01-01 seed; instead insert a v2 effective
  2026-01-01 in a test transaction and assert: 2025-12-31 → v1, 2026-01-01 → v2, 2026-06-30 → v2.
- `GetEffective_CorruptedRow_Throws` (a row violating an invariant, inserted by SQL in the test).
- `LimitPolicies_AreGlobal_SameForAliceAndBob` (no tenant filter on this table).
Architecture: the allow-list rule passes with positive results; the probe fails as expected (temporary).

## Acceptance criteria
- `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- `compliance.limit_policies` exists with v1 after `-StartAspire` (check by SQL), then `-Stop`.
- Domain folder has no reference outside the allow-list; no EF mapping of `Evaluation` yet.
- Close-out block with "Still running: none".

## Rules to respect
- Locked: Compliance uses DDD, Clean Architecture and Specifications; only Compliance has repositories; `decimal` only.
- Compare unrounded ratios; round only for display (no display in this brief).
- ADR-0007: the repository is the write side's read; no Dapper here.
- D-162: no amounts, dates or free text in logs or `ToString()`.

## Out of scope
Storing evaluations, `EvaluationCompleted`, any API or UI (Briefs #14-#15); the rebalance engine (Phase 4); policy
administration (post-MVP, D-12); dependency upgrades; the theme toggle (Brief #15).

## Commit plan
1. `test: make the Compliance domain rule an allow-list` (task 1 rule text; positive results still optional until commit 2)
2. `feat: add the Compliance domain model with specifications and boundary tests` (task 2, removes `WithoutRequiringPositiveResults`)
3. `feat: persist the versioned limit policy with its seed` (task 3 with integration tests)
4. `docs: add ADR-0012 and update Compliance rules` (task 4 and the decisions deltas)

## Pull request
- **Title (exact):** `feat: add the Compliance domain model and versioned limit policy`
- **Body:** `.git/finguard-pr-body.md` from the template, real numbers; Summary names ADR-0012 and says Phase 3 started.
- Close-out block with:
  `git push -u origin feat/compliance-domain-and-policy` and
  `gh pr create --base main --head feat/compliance-domain-and-policy --title "feat: add the Compliance domain model and versioned limit policy" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first; wait for approval. At the end: majors report, the boundary table as run (inputs and exact utilizations),
test counts by project, the seed SQL check, the execution delta and the Close-out block.

---

## Appendix A — Decisions delta (session 22: Brief #13 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-169 | Compliance `Domain/` may depend only on `System.*`, itself and `FinGuard.BuildingBlocks.Results` (architecture allow-list) | Closes open question 1 of session 5 |
| D-170 | `LimitPolicy.Create` returns `Result`; `Evaluation.Create` guards throw on impossible inputs | Expected vs unexpected failures (locked decision) |
| D-171 | Compliance stays one project with `Domain/`, `Features/`, `Persistence/`; direction enforced by tests | |
| D-172 | Domain owns `AssetClass`; `AssetClassCode` (Portfolio.Contracts) is mapped in `Features/` | |
| D-173 | Class status Compliant / Warning (threshold ≤ u ≤ 1) / Breached (u > 1); an alert per Warning or Breached class | |
| D-174 | Minimal in-memory `Specification<T>` (`IsSatisfiedBy`, `And`, `Not`) with `IsInAlert` and `IsBreached` | |
| D-175 | Global immutable policy versions in `compliance.limit_policies` (four limit columns, threshold, target ratio, `effective_from` unique); seed v1 effective 2000-01-01 | Not tenant-owned |
| D-176 | An evaluation uses the policy in force at the cut-off date (highest `effective_from` ≤ cut-off) | |
| D-177 | `ILimitPolicyRepository` (EF, write side) maps rows through `LimitPolicy.Create` and throws on corrupted rows | |
| D-178 | ADR-0012: Compliance domain model | |

### Risks and flags
- **R-37 Decimal division.** `decimal` keeps 28-29 significant digits; utilizations are exact for the boundary inputs
  but repeating fractions are truncated. Comparisons stay correct at the boundaries because the inputs are cents and
  the thresholds have ≤ 4 decimals; document it in ADR-0012 and keep the boundary tests exact.

### Next step
Brief #14: evaluate on `SubmissionAccepted` (from the stored snapshot), persist `Evaluation` (repository), publish
`EvaluationCompleted` through the outbox, and a read API for the latest evaluation per submission.
