# Brief #14 — Evaluate accepted portfolios, store the evaluation and publish EvaluationCompleted (Phase 3)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-14-compliance-evaluation.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-14-compliance-evaluation.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/compliance-evaluation` |
| PR title (exact, becomes the squash commit) | `feat: evaluate accepted portfolios and publish EvaluationCompleted` |
| Phase | Phase 3, second of three. Phase 3 exit: boundary tables green (done in #13); result visible in the UI (#15) |

---

## Goal
Every accepted submission is evaluated against the limit policy in force at its cut-off date. The `Evaluation` is
stored with its policy snapshot, `EvaluationCompleted` is published through the outbox, and the Api exposes the
evaluation of a submission. Portfolio stops accepting cut-off dates before the first policy (2000-01-01).

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/compliance-domain.md`,
  `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`, `docs/decisions.md` (newest delta: session 22 executed),
  ADR-0007, ADR-0009, ADR-0011, ADR-0012.
- Compliance today: `SubmissionAcceptedHandler` stores `PortfolioSnapshot` (four class columns, idempotent by
  `EventId`); `Domain/` has `LimitPolicy`, `Evaluation` (immutable, policy snapshot, `ComplianceStatus` ordered by
  precedence, alerts), specifications, `ILimitPolicyRepository.GetEffectiveAsync` returning `LimitPolicy?` (D-179);
  `compliance.limit_policies` with CHECK constraints and seed v1 effective 2000-01-01; `/api/compliance` group exists
  with `TenantMember`, no endpoints. `FinGuard.Compliance.Contracts` exists and is empty.
- R-38 / D-179: null policy is reachable today because Portfolio accepts any past date.
- **E2E conflict found while planning:** `e2e/duplicate.e2e.ts` `randomPastDate()` picks dates from 1990-01-01 onward,
  so the new lower bound would make that flow fail about a third of the time. Task 1 fixes it.
- Session 22 closing decisions were agreed in the planning chat but are not in the repo yet; they go into this brief's
  planning delta with new IDs (Appendix A), because the chat numbers D-182 to D-187 collided with the execution delta.

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Earliest cut-off date | Portfolio rejects cut-off dates before 2000-01-01 as a transport failure: 400, `Portfolio.Submission.CutOffDateTooEarly`, nothing stored. The date is a named constant in Portfolio with a comment pointing to D-175 (Portfolio cannot ask Compliance) | Storing a "not evaluated" evaluation for dates without a policy (a state no real user needs) |
| Null policy in Compliance | After this change a null policy can only come from old data or a bug: the evaluation handler throws `InvalidOperationException` (ids only); the message goes to the error queue after retries | A business outcome for an impossible case |
| Where evaluation runs | The snapshot handler cascades an internal command `EvaluatePortfolio(Guid SubmissionId)` (Compliance `Features/`, not a contract). A separate handler evaluates. The received fact (snapshot) and the derived result (evaluation) commit separately, and a failed evaluation can be replayed from the error queue without losing the snapshot | Evaluating inside `SubmissionAcceptedHandler` (one bug would also roll back the received data); a scheduled sweep |
| One evaluation per submission | Unique `submission_id` on evaluations; `EvaluatePortfolio` skips when one exists (idempotent). Re-evaluation under a new policy version is post-MVP | Re-evaluating on every delivery |
| Persistence model | `IEvaluationRepository` (`Domain/`): `ExistsForSubmissionAsync`, `Add`. EF implementation maps the aggregate to **row models** (`EvaluationRow` + four `EvaluationClassRow`s), the same style as `LimitPolicyRow`; the domain gets no EF constructor or attribute. Nothing loads an `Evaluation` back through EF in this brief (reads are Dapper) | Mapping the aggregate directly with owned types (needs EF-friendly constructors in `Domain/`) |
| Tables | `compliance.evaluations`: `id` PK, `tenant_id`, `submission_id` (unique), `cut_off_date`, `submission_received_at`, `evaluated_at`, `total_book_value numeric(18,2)`, `overall_status varchar(16)`, policy snapshot (`policy_version`, four limits `numeric(5,4)`, `alert_threshold`, `rebalance_target_ratio`). `compliance.evaluation_classes`: PK (`evaluation_id`, `asset_class`), `tenant_id`, `book_value numeric(18,2)`, `share numeric`, `utilization numeric`, `status`. `share` and `utilization` use **unconstrained `numeric`** so the stored value equals the domain's unrounded decimal. Alerts are not stored: they are the classes with status Warning or Breached | A separate alerts table (derived data); `numeric(p,s)` for ratios (would round) |
| Event | `FinGuard.Compliance.Contracts.Events.EvaluationCompleted(Guid EventId, Guid TenantId, DateTimeOffset OccurredAt, Guid EvaluationId, Guid SubmissionId, DateOnly CutOffDate, ComplianceStatusCode OverallStatus, int PolicyVersion, IReadOnlyList<ClassEvaluation> Classes)`; `ClassEvaluation(AssetClassCode AssetClass, decimal BookValue, decimal Limit, decimal Utilization, ComplianceStatusCode Status)` in `FinGuard.Compliance.Contracts` (not `.Events`, D-163). Compliance.Contracts defines its **own** `AssetClassCode` and `ComplianceStatusCode` | Referencing Portfolio.Contracts from Compliance.Contracts (contract-to-contract coupling); a consumer now (Advisory is Phase 4; the event is proven by tracked sessions) |
| Read API | `GET /api/compliance/submissions/{submissionId:guid}/evaluation` → overall status, policy version, evaluated at, total, classes (asset class, book value, share, limit, utilization, status), alerts. Dapper, own schema, `tenant_id` filter. Not found (unknown, other tenant, or not evaluated yet) → 404 `Compliance.Evaluation.NotFound` | A list endpoint now (#15 decides what the UI needs); polling semantics beyond 404 |
| Numbers in the response | Exact decimals as JSON numbers; the API never rounds. The UI (#15) formats and never re-derives a status | Rounding in the API |
| Logging | Ids only (D-162): `Evaluated submission {SubmissionId} as {EvaluationId}`. No status, amounts or dates in logs or `ToString()` | |

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot` first, Aspire only through `-StartAspire`,
  `-Stop` before the final message, Close-out block with "Still running: none".
- pnpm from inside `src/frontend/finguard-web`; Git Bash uses `aspire.cmd` (D-135).
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- `docs/decisions.md` is append-only (D-137). Ask before deviating; record approved deviations.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/compliance-evaluation
scripts/dev-session.ps1 -Snapshot
dotnet build -c Release; dotnet test --no-build -c Release
```

## Tasks

### 1. Portfolio: earliest cut-off date (D-187 of the chat, new ID in Appendix A)
- `ReceiveSubmissionHandler.Validate`: cut-off date before `MinimumCutOffDate` (2000-01-01) → `Error.Validation(
  "Portfolio.Submission.CutOffDateTooEarly", "The cut-off date must be on or after 2000-01-01.")`. Same ordering rules
  as the other transport checks; nothing stored.
- `e2e/duplicate.e2e.ts`: `randomPastDate()` draws from 2000-01-01 to 30 days before today.
- `AGENTS.md` domain rules and `samples/portfolios/README.md` (if it states date rules): add the lower bound.

### 2. Compliance contracts
- `FinGuard.Compliance.Contracts`: `AssetClassCode`, `ComplianceStatusCode`, `ClassEvaluation`,
  `Events/EvaluationCompleted` with XML docs (published once per evaluation; four classes in enum order; amounts in USD
  book value; additive changes only, ADR-0011 rule). `ToString()` overrides: ids only (`ClassEvaluation` prints the
  class only).

### 3. Evaluation flow
- `SubmissionAcceptedHandler`: after adding the snapshot, cascade `EvaluatePortfolio(SubmissionId)` (tuple return,
  D-164). Not cascaded when the event was a duplicate.
- `Features/EvaluatePortfolio/`: `EvaluatePortfolio` command (`ToString()`: id only) and handler:
  1. skip if an evaluation exists for the submission;
  2. load the snapshot (EF, own context; tenant filter applies); missing snapshot → throw (bug);
  3. `GetEffectiveAsync(cutOffDate)`; null → throw (D-179 after task 1);
  4. `Evaluation.Create(...)` with `TimeProvider`;
  5. `IEvaluationRepository.Add`; cascade `EvaluationCompleted` (map domain enums to contract enums in one place,
     exhaustive switches with unit tests).
- `Persistence/`: `EvaluationRow`, `EvaluationClassRow`, mapping, `EvaluationRepository`, migration `AddEvaluations`
  (FK `evaluation_classes → evaluations` cascade; index `(tenant_id, cut_off_date, submission_received_at)`; no FK to
  `portfolio_snapshots` needed, but allowed within the schema). Both row types are `ITenantOwned`.
  `has-pending-model-changes` clean.

### 4. Read slice
- `Features/GetEvaluation/`: query `GetEvaluation(Guid SubmissionId)`, response records, Dapper handler (two queries or
  one join; `tenant_id` in every `WHERE`), endpoint mapped in `MapComplianceEndpoints` with
  `InvokeForCurrentTenantAsync`. Class order: enum order. Alerts: the Warning/Breached classes, same order.

### 5. Docs
- `docs/architecture/overview.md` section 4: snapshot → `EvaluatePortfolio` → evaluation + `EvaluationCompleted`;
  Advisory still planned.
- `.claude/rules/compliance-domain.md` + `.cursor/rules/compliance-domain.mdc`: "Received facts and derived results
  commit separately (snapshot, then `EvaluatePortfolio`)"; "ratios are stored as unconstrained `numeric`".
- `README.md`: status line for Phase 3 (evaluation done, UI next).
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta on top.

## Tests that prove it
Unit:
- Enum mappings (domain ↔ contracts) cover every value.
- Row mapping: an `Evaluation` maps to one `EvaluationRow` and four class rows with exact values and the policy snapshot.
- `ToString()` of `EvaluatePortfolio`, `EvaluationCompleted`, `ClassEvaluation`: marker amount and date absent.
- Portfolio: 1999-12-31 → `CutOffDateTooEarly`; 2000-01-01 → passes the transport checks.
Integration (real PostgreSQL, real Api host, tracked sessions matched by submission id, D-165):
- `Upload_ValidBalanced_IsEvaluatedCompliantWithPolicyVersionOne` (sample file; status and policy snapshot checked in the DB)
- `Upload_SharesHeavy_IsEvaluatedBreachedWithSharesAlert`
- `Upload_ExactlyAtLimit_StoresUtilizationOneAndWarning` (a CSV where one class has u = 1.00 exactly; the stored
  `utilization` equals `1` exactly and the API returns `Warning`: proves `numeric` keeps the unrounded value)
- `Upload_JustAboveLimit_IsBreached` (u = 1.0001 through the whole flow)
- `Upload_Valid_PublishesOneEvaluationCompleted` (payload ids, overall status, four classes)
- `EvaluatePortfolio_DeliveredTwice_StoresOneEvaluation`
- `SubmissionAccepted_DeliveredTwice_EvaluatesOnce` (no second `EvaluatePortfolio` cascade)
- `Upload_CutOffBefore2000_Returns400TooEarlyAndStoresNothing`; `Upload_CutOffOn2000_01_01_IsEvaluated`
- `GetEvaluation_Own_Returns200WithClassesAndAlerts`; `GetEvaluation_OtherTenants_Returns404`;
  `GetEvaluation_UnknownSubmission_Returns404`
- `EvaluationFlow_Always_LogsIdsOnly` (marker amount, date and status text absent from log records)
- Tenant isolation on the new tables through EF (Alice's context cannot see Bob's evaluation rows).
Architecture: existing rules cover the event shape, `ToString()`, one DbContext per handler, Domain allow-list
(`IEvaluationRepository` lives in `Domain/` and uses only domain types).
E2E: no new flow; run `pnpm e2e` twice (the date fix must hold) against `-StartAspire`, then stop it.

## Acceptance criteria
- `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm lint`, `pnpm test --watch=false`, `pnpm exec prettier --check src e2e` green (only the E2E file changes).
- With `-StartAspire`, as Alice: `valid-balanced.csv` → one evaluation, `Compliant`; `valid-shares-heavy.csv` → one
  evaluation, `Breached` with a shares alert; `GET .../evaluation` returns them; `wolverine_dead_letters` empty. Report
  statuses and counts only, no amounts.
- `pnpm e2e` green twice; Aspire stopped; Close-out with "Still running: none".

## Rules to respect
- ADR-0012: domain allow-list; `Result` for expected failures, throws for impossible inputs.
- ADR-0009: atomic cascades, idempotent handlers, envelope tenant, one DbContext per handler.
- ADR-0007: EF for writes (through the repository), Dapper for the read slice with `tenant_id`.
- D-162: identifiers only in logs and `ToString()`.
- The UI is untouched in this brief except the E2E date helper.

## Out of scope
Result UI and theme toggle (#15); rebalance proposal and Advisory (Phase 4); re-evaluation on new policy versions;
evaluation list endpoints; Aspire 17 (its own chore brief after #15).

## Commit plan
1. `feat: reject cut-off dates before the first limit policy` (task 1, Portfolio tests, E2E date helper)
2. `feat: add the EvaluationCompleted contract` (task 2)
3. `feat: evaluate accepted portfolios and store evaluations` (task 3 with migration and tests)
4. `feat: expose the evaluation of a submission` (task 4 with tests)
5. `docs: update overview, Compliance rules and decisions log` (task 5)

## Pull request
- **Title (exact):** `feat: evaluate accepted portfolios and publish EvaluationCompleted`
- **Body:** `.git/finguard-pr-body.md` from the template with real numbers.
- Close-out block with:
  `git push -u origin feat/compliance-evaluation` and
  `gh pr create --base main --head feat/compliance-evaluation --title "feat: evaluate accepted portfolios and publish EvaluationCompleted" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first; wait for approval. At the end: test counts by project, the acceptance check (statuses and counts only),
the execution delta and the Close-out block.

---

## Appendix A — Decisions delta (session 23: Brief #14 planning)

### Session 22 closing decisions (agreed in the planning chat, recorded here with repo IDs)
| ID | Decision | Notes |
| --- | --- | --- |
| D-185 | Aspire 17 upgrade in its own chore brief after Brief #15 and before Phase 4 | Closes R-08 and R-31 when done; `dev-session.ps1` and `docs/dev-environment.md` in scope |
| D-186 | pnpm stays on 11 until GitHub's dependency graph reads two-document lockfiles (dependabot-core #15904); re-check at Phase 5 start | Confirms D-42 |
| D-187 | Node 26 arrives with the Phase 5 container images | |
| D-188 | EFCore.NamingConventions stays on 10.x with .NET 10 LTS | |
| D-189 | Minor and patch updates come only through Dependabot's monthly grouped PRs; no manual chore PR | D-48 |
| D-190 | Portfolio rejects cut-off dates before 2000-01-01 (`Portfolio.Submission.CutOffDateTooEarly`, 400, not stored); the constant points to D-175 | Resolves R-38 (option b) |

### Brief #14 decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-191 | A null effective policy in Compliance is a bug after D-190: the handler throws and the message goes to the error queue | |
| D-192 | The snapshot handler cascades an internal `EvaluatePortfolio` command; snapshot and evaluation commit separately | Replayable evaluation without losing the received fact |
| D-193 | One evaluation per submission (unique `submission_id`); re-evaluation post-MVP | |
| D-194 | `IEvaluationRepository` maps the aggregate to row models; no EF constructors in `Domain/` | Same style as `LimitPolicyRow` |
| D-195 | `share` and `utilization` stored as unconstrained `numeric` (exact unrounded values); alerts derived, not stored | |
| D-196 | `EvaluationCompleted` in `FinGuard.Compliance.Contracts.Events`; Compliance.Contracts defines its own `AssetClassCode` and `ComplianceStatusCode`; no contract-to-contract references | |
| D-197 | `GET /api/compliance/submissions/{submissionId}/evaluation`; 404 `Compliance.Evaluation.NotFound` for unknown, other tenant or not yet evaluated; exact decimals, no rounding in the API | |

### Risks and flags
- **R-38** resolved by D-190.
- **R-39 Display rounding vs status.** u = 0.89999996 is Compliant but rounds to 90.00 %. #15 must format so the
  shown number never contradicts the status (for example truncate to 2 decimals, or show the status next to a
  percentage with more precision near thresholds) and must never recompute a status.

### Next step
Brief #15 (closes Phase 3): evaluation result in the submission detail page (status, classes, alerts, policy version),
"evaluating…" state while the 404 lasts, three-state theme toggle (D-106), E2E for an accepted and a breached upload.
