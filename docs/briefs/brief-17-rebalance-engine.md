# Brief #17 — Deterministic rebalance proposal (opens Phase 4)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-17-rebalance-engine.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-17-rebalance-engine.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/rebalance-proposal` |
| PR title (exact, becomes the squash commit) | `feat: add the deterministic rebalance proposal to compliance evaluations` |
| Phase | Phase 4, first of three (#17 rebalance engine, #18 Advisory with template narrator, #19 Agent Framework narrator on a local model). Phase 4 exit: proposal always present; narrative asynchronous and degrades gracefully |

---

## Goal
Every evaluation carries a deterministic rebalance proposal computed by a pure domain service in Compliance: which
classes move, from which book value to which, with the total preserved to the cent. The proposal is stored with the
evaluation, added to `EvaluationCompleted` (additively), returned by the evaluation API and shown on the submission
detail page.

## Context
- Read first and treat as binding: `AGENTS.md` (rebalance rules), `CLAUDE.md`, `.claude/rules/compliance-domain.md`
  (lines on the rebalance engine), `.claude/rules/frontend.md`, `.claude/rules/testing.md`, `docs/decisions.md`
  (newest delta: session 25 executed), ADR-0002, ADR-0011, ADR-0012.
- Domain today: `Evaluation.Create(submissionId, cutOffDate, classValues, policy, evaluatedAt)` builds four
  `ClassResult`s (utilization, status), overall status, alerts and a `PolicySnapshot` (limits, `AlertThreshold`,
  `RebalanceTargetRatio`). Persisted through `EvaluationRepository` into `compliance.evaluations` and
  `compliance.evaluation_classes` (row models, D-194). `EvaluationCompleted` carries four `ClassEvaluation`s.
  `GET /api/compliance/submissions/{id}/evaluation` returns the evaluation; the detail page shows it (#15).
- Rules as written today (D-03, D-04, compliance rules): target = `RebalanceTargetRatio` × limit; "if infeasible, relax
  to `AlertThreshold` and mark `Partial`"; breached classes first, then alert classes; surplus to the other classes in
  proportion to headroom up to their target; cent rounding preserves the total, remainder cent to the class with the
  largest headroom. Policy invariant: `RebalanceTargetRatio × sum(limits) ≥ 1` (D-03, enforced by D-180).
- **Planning finding (changes D-03, owner to confirm before running this brief):** with that invariant the plan at the
  target ratio is always feasible in exact arithmetic, so the "relax to the alert threshold" step can never run.
  Proof: `Σ_c (target_c − value_c) = (R × ΣL − 1) × T ≥ 0`. Split the classes into *reducers* (status Warning or
  Breached, reduced to target), *middle* classes (at or above target but below the alert threshold, left alone) and
  *receivers* (below target). Then `headroom(receivers) − surplus(reducers) − excess(middle) ≥ 0`, so receivers can
  always absorb the surplus. The only thing that can still go wrong is cent rounding (targets are floored to cents).
  This brief therefore replaces the relax step with a **verification step**: re-evaluate the proposed allocation with
  the same policy; `Full` if every class ends Compliant, `Partial` otherwise (only reachable through rounding on very
  small totals).

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Feasibility | No relax step. Proposal status: `NotNeeded` (overall Compliant: no moves), `Full` (after the moves every class is Compliant), `Partial` (verification found a non-Compliant class, rounding only) | Keeping the relax-to-0.90 step (unreachable code under the invariant; it would also leave classes at Warning on purpose) |
| Reducers | Classes with status Warning or Breached, reduced to `floor_cents(R × L_c × T)` | Reducing middle classes too (moves money that does not need to move) |
| Receivers | Classes with value < their floored target; each receives `floor_cents(S × h_c / H)`; leftover cents (at most three) go to the receiver with the largest headroom, ties by `AssetClass` order (D-04) | Equal split; largest-remainder method across all receivers (D-04 already fixes the rule) |
| Middle classes | Unchanged | |
| Order of moves | Reducers first: Breached before Warning, then utilization descending, then `AssetClass` order; then receivers by amount received descending, then `AssetClass` order. Order is for presentation and determinism only; amounts do not depend on it | |
| Where it lives | `Domain/Rebalancing/`: `RebalanceEngine.Propose(...)` (static, pure), `RebalanceProposal` (status + one `ClassMove` per class: current, proposed, change) and `ProposalStatus`. `Evaluation.Create` calls it, so every new evaluation has a proposal. No I/O, no clock, no AI (compliance rules) | A separate aggregate or handler step (the proposal is part of the evaluation result and shares its policy snapshot) |
| Persistence | `compliance.evaluations.proposal_status varchar(16) NULL`; `compliance.evaluation_classes.proposed_book_value numeric(18,2) NULL`. Null only for evaluations stored before this brief (dev data; no backfill) | Recomputing old proposals in a migration (domain code in migrations) |
| Event | `EvaluationCompleted` gains `ProposalStatus` (`ProposalStatusCode`, contract enum) and `ClassEvaluation` gains `ProposedBookValue`; additive (ADR-0011). Still no route until Advisory subscribes (D-199) | A separate `RebalanceProposed` event (one fact, one event) |
| API | `EvaluationResponse` gains `proposal: { status, moves: [{ assetClass, currentBookValue, proposedBookValue, change }] }` in presentation order; `null` for evaluations without a stored proposal; `moves` empty for `NotNeeded`. Exact decimals (D-197) | |
| UI | Detail page section "Rebalancing proposal" under the evaluation: status sentence (`No rebalancing needed.` / `Moving the amounts below brings every class within its limit.` / `Partial: rounding leaves {Class} outside its target; review the amounts.`), table Asset class / Current / Proposed / Change (signed, `UsdPipe`), and a totals row (Current total = Proposed total). Legacy `null` → `No proposal is available for this evaluation.` | Charts (later, if wanted) |
| ADR | ADR-0013 "Rebalance engine": the algorithm, the feasibility proof, the verification step, rounding; supersedes the relax step of D-03 | Only a decisions entry (domain rule change) |

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot` first, Aspire only through `-StartAspire`,
  `-Stop` before the final message, Close-out block with "Still running: none".
- pnpm only, from inside `src/frontend/finguard-web`; Git Bash uses `aspire.cmd` (D-135).
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Logs and `ToString()`: identifiers only (D-162). No amounts in logs, also not in the engine's exceptions.
- `docs/decisions.md` is append-only (D-137). Ask before deviating; record approved deviations.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/rebalance-proposal
scripts/dev-session.ps1 -Snapshot
dotnet build -c Release; dotnet test --no-build -c Release      # baseline 442
```

## Tasks

### 1. Domain (`Domain/Rebalancing/`)
- `ProposalStatus { NotNeeded, Full, Partial }`, `ClassMove(AssetClass Class, Money Current, Money Proposed)` with
  `Change` (signed decimal), `RebalanceProposal(ProposalStatus Status, IReadOnlyList<ClassMove> Moves)` (always four
  moves internally, one per class; "moves" with zero change are filtered only in presentation).
- `RebalanceEngine.Propose(IReadOnlyList<ClassResult> classes, Money total, PolicySnapshot policy)` implementing the
  decisions above with `decimal` cents arithmetic (`Math.Floor(x * 100) / 100`), then the verification step through
  the same utilization/specification code `Evaluation` uses (no second implementation of the status rule).
- `Evaluation` gains `Proposal`; `Evaluation.Create` computes it. Existing constructor guards unchanged.
- No new dependency in `Domain/` (allow-list D-169).

### 2. Persistence and contracts
- Row models and mapping for the two new columns; migration `AddRebalanceProposal`; `has-pending-model-changes` clean.
- `FinGuard.Compliance.Contracts`: `ProposalStatusCode`; `EvaluationCompleted.ProposalStatus`;
  `ClassEvaluation.ProposedBookValue`; mapping with exhaustive switches; `ToString()` unchanged (ids only).

### 3. Read slice
- Dapper query reads the new columns; response mapping builds `proposal` in presentation order (same ordering rules,
  implemented once in the read slice; a unit test pins the order); `null` when `proposal_status` is null.

### 4. Frontend
- `ComplianceApi` types; "Rebalancing proposal" section as decided; `data-proposal-status` for tests; accessible table
  with caption; signed change shown with an explicit `+`/`−` and text, never colour only.

### 5. Docs
- `docs/adr/0013-rebalance-engine.md` (+ `docs/adr/README.md`).
- `.claude/rules/compliance-domain.md` + `.cursor/rules/compliance-domain.mdc` (same commit) and `AGENTS.md`: replace
  the relax step with the verification step and the three statuses.
- `docs/architecture/overview.md` (evaluation includes the proposal), `README.md` (Phase 4 in progress; what the detail
  page now shows), `samples/portfolios/README.md` (expected proposal for `valid-shares-heavy.csv`).
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta on top.

## Tests that prove it
Unit (pure domain):
- `NotNeeded` for every Compliant portfolio (boundary rows 0.8999 and 0: no moves).
- One breached class (shares-heavy sample values): shares reduced to `floor(0.85 × 0.25 × T)`; receivers get amounts in
  proportion to headroom; every class Compliant afterwards; `Full`; Σ proposed = Σ current to the cent.
- Exactly at the alert threshold (u = 0.90, Warning) → reduced to target.
- Breached + Warning together: both reduced; order Breached first.
- Middle class (target ≤ value < alert) is not moved.
- Rounding: totals that produce fractional targets; leftover cents go to the largest headroom, ties by enum order;
  total preserved.
- `Partial` reachable: a tiny total (for example 1.00 USD) where rounding leaves a class non-Compliant; assert the
  status and that the total is still preserved. If no such input exists with the seed policy, construct a policy that
  satisfies the invariant with equality and say which inputs hit it.
- Generated table: a deterministic pseudo-random generator (fixed seed, no new library) for 10,000 portfolios with
  totals from 1.00 to 10^12 and the seed policy: total preserved, no class increases if it was a reducer, no class
  decreases if it was a receiver, middle classes unchanged, every receiver ends ≤ its target + 3 cents, and status
  `Full` whenever every class ends Compliant (and `Partial` otherwise). Report how many `Partial` cases occurred.
- Determinism: the same input twice gives the same proposal; changing the input order of classes does not change it.
- Contract and row mappings for the new fields; `ToString()` of the event still has no amounts.
Integration (real PostgreSQL, through HTTP):
- `Upload_SharesHeavy_StoresFullProposalPreservingTotal` (DB and API agree; total equal; shares reduced).
- `Upload_ValidBalanced_StoresNotNeeded`.
- `GetEvaluation_LegacyWithoutProposal_ReturnsNullProposal` (row inserted without the new columns).
- `Upload_Valid_EvaluationCompletedCarriesProposal` (tracked session, matched by submission id, D-165).
- Tenant isolation unchanged (existing tests cover the tables).
Vitest: section states (NotNeeded, Full with rows and totals row, Partial sentence, null legacy), signed change text.
E2E: extend `evaluation.e2e.ts`: shares-heavy shows a Full proposal with Shares decreasing and a totals row where
current equals proposed; balanced shows "No rebalancing needed.". Run twice.

## Acceptance criteria
- `dotnet build -c Release` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm lint`, `pnpm test --watch=false`, `pnpm build`, `pnpm exec prettier --check src e2e` green.
- With `-StartAspire`: the three samples show the expected proposal (shares-heavy Full, balanced NotNeeded, rejected
  none); `pnpm e2e` green twice; Aspire stopped; Close-out with "Still running: none".

## Rules to respect
- ADR-0002: the engine is deterministic; no AI; the LLM will only explain (Phase 4 #18-#19).
- ADR-0012: domain allow-list; `decimal` only; unrounded comparisons for statuses.
- ADR-0011: additive contract change only.
- No business rule in the SPA: the proposal and its status come from the API.

## Out of scope
Advisory module and narratives (#18, #19); re-evaluating or backfilling old evaluations; charts; trade instructions per
position (the proposal is per asset class); minimum-allocation limits (out of scope for the product).

## Commit plan
1. `feat: add the rebalance engine to the Compliance domain` (task 1 with unit tests)
2. `feat: store the rebalance proposal and publish it with EvaluationCompleted` (task 2 with tests)
3. `feat: return and show the rebalance proposal` (tasks 3-4 with tests)
4. `docs: add ADR-0013 and update the rebalance rules` (task 5)

## Pull request
- **Title (exact):** `feat: add the deterministic rebalance proposal to compliance evaluations`
- **Body:** `.git/finguard-pr-body.md` from the template; Summary names ADR-0013 and says the relax step of D-03 is
  superseded (with the one-line proof).
- Close-out block with:
  `git push -u origin feat/rebalance-proposal` and
  `gh pr create --base main --head feat/rebalance-proposal --title "feat: add the deterministic rebalance proposal to compliance evaluations" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first; wait for approval. At the end: the shares-heavy proposal as computed (classes, current, proposed, change;
these are sample numbers, so they may appear in the report), the generated-table summary (cases, Partial count), test
counts, two E2E runs, a screenshot of the proposal section (light), the execution delta and the Close-out block.

---

## Appendix A — Decisions delta (session 26: Phase 4 plan and Brief #17 planning)

### Session 25 closing decisions (agreed in the planning chat, recorded here)
| ID | Decision | Notes |
| --- | --- | --- |
| D-229 | Phase 4 runs locally at zero cost and needs no account: `TemplateNarrator` first, then a local model through Ollama in a container (Aspire Community Toolkit), verified at Brief #19. GitHub Models is not relied on (a November 2026 source reports it retired; unverified) | Open question 1 of the kickoff (local model) narrowed; final choice in Brief #19 |
| D-230 | A Phase 6 (VPS: Dokploy, Tailscale, Hetzner; personal domain) is planned after Phase 5, not designed yet. Phase 5 is designed to be portable: one standard image configured by environment variables, migrations as a job, OTLP exporter chosen by configuration, Entra External ID kept, SPA as a static build, AI provider behind `IAdvisoryNarrator` | |
| D-231 | The owner buys the personal domain before Phase 5; SWA Free supports custom domains with free certificates, so Phase 5 already uses it | |
| D-232 | The repository stays a monorepo for every phase | |

### Phase 4 plan
| Brief | Scope |
| --- | --- |
| #17 | Rebalance engine in Compliance, stored with the evaluation, in `EvaluationCompleted`, API and UI |
| #18 | Advisory module: consumes `EvaluationCompleted`, `TemplateNarrator`, stores the narrative, API, "Advisory" section with an asynchronous state; R-40 backfill decision |
| #19 | `AgentFrameworkNarrator` over an `IChatClient` on a local model (Ollama via Aspire), output validation (numbers must match), timeout and fallback to the template, prompt-injection safeguards; closes Phase 4 |
Phase 5 then starts at #20.

### Brief #17 decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-233 | The relax-to-alert-threshold step of D-03 is removed: under `R × ΣL ≥ 1` the plan at the target ratio is always feasible in exact arithmetic. A verification step re-evaluates the proposed allocation: `Full` if every class ends Compliant, `Partial` otherwise (rounding only); `NotNeeded` for Compliant evaluations | **Supersedes part of D-03; owner confirmation required.** ADR-0013 |
| D-234 | Reducers = Warning and Breached classes, reduced to `floor_cents(R × L × T)`; receivers = classes below their target, proportional to headroom, leftover cents to the largest headroom (D-04); middle classes untouched | |
| D-235 | Move order: Breached, Warning (utilization desc), then receivers (amount desc), ties by `AssetClass` order; presentation only | |
| D-236 | The proposal is part of `Evaluation` (computed in `Evaluation.Create`), stored in two nullable columns; no backfill | |
| D-237 | `EvaluationCompleted` gains `ProposalStatus` and `ClassEvaluation.ProposedBookValue` (additive) | |
| D-238 | API `proposal` block (null for legacy evaluations) and a "Rebalancing proposal" section with a totals row | |

### Risks and flags
- **R-46 Rounding `Partial` on tiny totals.** Only reachable through cent rounding; the generated table measures it.
  If it shows up for realistic totals, revisit the rounding rule.

### Next step
Brief #18: Advisory module with `TemplateNarrator` (consumes `EvaluationCompleted` with the proposal) and its UI.
