# Brief #12 — Publish accepted submissions to Compliance through the outbox (closes Phase 2)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-12-submission-accepted-event.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-12-submission-accepted-event.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/submission-accepted-event` |
| PR title (exact, becomes the squash commit) | `feat: publish accepted submissions to Compliance through the outbox` |
| Phase | Phase 2, last brief. Exit criteria already met by #10-#11; this brief adds the first real cross-module event so Phase 3 starts from data Compliance owns |

---

## Goal
When Portfolio accepts a submission, it publishes `SubmissionAccepted` (with the book-value total per asset class) in
the same transaction, through Wolverine's outbox. Compliance consumes it idempotently and stores its own
`portfolio_snapshots` row, the input that Phase 3 will evaluate. Rejected and duplicate uploads publish nothing.

## Context
- Read first and treat as binding: `AGENTS.md` (including "Closing a work session"), `CLAUDE.md`,
  `.claude/rules/backend-vsa.md`, `.claude/rules/compliance-domain.md`, `.claude/rules/testing.md`,
  `docs/decisions.md`, ADR-0003, ADR-0004, ADR-0007, ADR-0009, ADR-0010, `docs/architecture/overview.md` section 4.
- Messaging is proven by Brief #6 probes (`tests/FinGuard.Api.IntegrationTests/Messaging/`): atomic outbox, retries,
  idempotent consumer by `EventId`, envelope tenant propagation, fail-closed without tenant, payload tenant must equal
  envelope tenant. The probe command handler cascades its event by **returning** it.
- `ReceiveSubmissionHandler` returns `Result<SubmissionReceivedResponse>` to `InvokeForCurrentTenantAsync<T>`. It does
  not save: Wolverine's EF transaction middleware saves and commits after `Handle`.
- Architecture tests already enforce: events are sealed records in `FinGuard.<Module>.Contracts.Events` with
  `EventId`, `TenantId`, `OccurredAt`; one DbContext per handler; message types override `ToString()` (D-77); modules
  reference only BuildingBlocks and other modules' Contracts.
- `ComplianceDbContext` exists (schema `compliance`, `InitialSchema` migration only). `Domain/` and `Features/` folders
  exist and are empty or nearly so. Compliance's rich domain is Phase 3: do not design aggregates here.
- Facts checked on 2026-11-20 (re-check against the 6.64 XML docs at execution):
  - Wolverine routes each message type to its own local queue by default, named after the message type.
    Source: https://wolverine.netlify.app/guide/messaging/transports/local
  - Handlers may return tuples; each item is treated as a separate return value, and `OutgoingMessages` is a collection
    treated as cascading messages. Source: https://wolverine.netlify.app/guide/handlers/return-values
  - Likely, not confirmed: with `InvokeAsync<T>`, the tuple item of type `T` is the response and the other items
    cascade. Prove it with a test before relying on it (fallback below).

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Event name | `SubmissionAccepted` (Portfolio's language: what happened) | `PortfolioSubmitted` (a rejected upload was also "submitted"); `overview.md` is updated |
| Rejected uploads | No event | `PortfolioRejected` (no consumer needs it; add one when a consumer exists) |
| Duplicates | No event (nothing new happened) | Re-publishing |
| Payload | Event-carried state: `SubmissionAccepted(Guid EventId, Guid TenantId, DateTimeOffset OccurredAt, Guid SubmissionId, DateOnly CutOffDate, DateTimeOffset ReceivedAt, IReadOnlyList<AssetClassTotal> Totals)` with `AssetClassTotal(AssetClassCode AssetClass, decimal BookValue)`. Always four totals, one per class, zeros included, in enum order | A Contracts query called back by Compliance (temporal coupling; the event is the snapshot); positions in the event (Compliance needs class totals only; position ids stay in Portfolio) |
| Asset classes in Contracts | `FinGuard.Portfolio.Contracts.AssetClassCode { Cash, FixedIncome, Shares, RealEstate }`. Compliance maps it to its own domain type in Phase 3 | Strings (typos compile) |
| Current version | Every accepted submission is the current one for its date at the moment it is accepted (latest `received_at`), so the event needs no `isCurrent` flag. Compliance applies the same rule when it reads (D-141) | A separate `CurrentSubmissionChanged` event |
| Publishing | Cascade from `ReceiveSubmissionHandler`: return `(Result<SubmissionReceivedResponse>, OutgoingMessages)`; add the event only for a new accepted submission. If the tuple breaks `InvokeAsync<T>`, fall back to injecting `IMessageContext` and `PublishAsync` inside the handler (same transaction), and record it | Publishing after the HTTP response (not atomic) |
| Local queues | Keep Wolverine's default: one durable local queue per message type. No named per-module queues yet | Per-module queues (no ordering or parallelism need yet). Closes open question 1 of session 13 |
| Compliance storage | `compliance.portfolio_snapshots`: `submission_id` (PK), `tenant_id`, `cut_off_date`, `received_at`, `cash`, `fixed_income`, `shares`, `real_estate` (`numeric(18,2)`), `source_event_id` (unique), `stored_at`. Persistence entity `PortfolioSnapshot` (`ITenantOwned`) in `Persistence/`, not a domain aggregate | A child table per class (four fixed classes, YAGNI); building the `Evaluation` aggregate now (Phase 3) |
| Consumer location | `Features/StorePortfolioSnapshot/SubmissionAcceptedHandler` in Compliance, writing through `ComplianceDbContext` only; `Domain/` untouched | Putting EF code in `Domain/` |
| Idempotency | Skip when a snapshot with the same `source_event_id` exists (same pattern as the probe consumer); the unique index is the backstop | Upserts |
| Validation in the consumer | Defensive check: four totals, each ≥ 0, sum > 0; otherwise throw (goes to the error queue after retries) and log ids only. Portfolio guarantees this, so it should never fire | Silently storing bad data |
| Backfill | None. Submissions accepted before this brief have no snapshot (development data only) | A backfill job |
| ADR | New ADR-0011 "Portfolio to Compliance integration contract" (naming, event-carried state, no rejected event, versioning rule: additive changes only; a breaking change means a new event type) | Only a decisions entry (the contract is a long-lived boundary) |

## Environment rules
- AGENTS.md "Closing a work session" from the first command: `scripts/dev-session.ps1 -Snapshot`, Aspire only through
  `-StartAspire`, `-Stop` before the final message, Close-out block with "Still running: none".
- pnpm only, from inside `src/frontend/finguard-web` (no frontend change is expected in this brief). In Git Bash, use
  `aspire.cmd` (D-135).
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Never log amounts, totals, file names, hashes or descriptions. Event and snapshot logs carry ids and the cut-off date
  only. `SubmissionAccepted.ToString()` prints type, `EventId`, `SubmissionId`, `CutOffDate`; never totals.
- `docs/decisions.md` is append-only (CI-enforced, D-137).
- Ask before deviating from this brief; record every approved deviation in the delta.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/submission-accepted-event
scripts/dev-session.ps1 -Snapshot
dotnet build -c Release; dotnet test --no-build -c Release
```
Read the Wolverine 6.64 XML docs for tuple returns and `OutgoingMessages` before writing the handler change.

## Tasks

### 1. Contract (Portfolio.Contracts)
- `AssetClassCode` enum and `Events/SubmissionAccepted.cs` (+ `AssetClassTotal`) as in the decisions, with XML docs
  stating: published once per new accepted submission; never for rejected or duplicate uploads; totals are book value in
  USD (ADR-0010), four entries, zeros included; additive changes only.
- `ToString()` overrides on both records (ids and date only; `AssetClassTotal` prints the class only).

### 2. Publish from Portfolio
- `ReceiveSubmissionHandler` builds the totals from `csv.Positions` (decimal sums per class; classes without positions
  get 0) and cascades `SubmissionAccepted` only on the new-and-accepted path. `OccurredAt = receivedAt`,
  `EventId = Guid.CreateVersion7(receivedAt)`, `TenantId` from `ITenantContext`.
- First prove the tuple return with `InvokeForCurrentTenantAsync<Result<SubmissionReceivedResponse>>` (the endpoint
  must still receive the response). Fallback as decided.
- Map the Portfolio `AssetClass` to `AssetClassCode` in one place (exhaustive switch; unit test covers every value).

### 3. Consume in Compliance
- Entity `PortfolioSnapshot` + mapping in `ComplianceDbContext` (snake_case, `numeric(18,2)`, unique index on
  `source_event_id`, index on `(tenant_id, cut_off_date, received_at)`); migration `AddPortfolioSnapshots`;
  `has-pending-model-changes` clean.
- `SubmissionAcceptedHandler.Handle(SubmissionAccepted @event, ComplianceDbContext db, TimeProvider time, ...)`:
  idempotency check, defensive validation, add snapshot (tenant stamped by the interceptor; the payload tenant is
  already checked against the envelope by `TenantPropagationMiddleware`). Log
  `Stored portfolio snapshot for submission {SubmissionId}` (Information).
- Compliance references `FinGuard.Portfolio.Contracts` only (never `FinGuard.Portfolio`).

### 4. Docs
- `docs/adr/0011-portfolio-compliance-integration-contract.md` (context, decision, consequences, alternatives as in the
  decisions table); add it to `docs/adr/README.md`.
- `docs/architecture/overview.md` section 4: `SubmissionAccepted` replaces `PortfolioSubmitted`; remove
  `PortfolioRejected`; show Compliance storing the snapshot; Phase 3 evaluation stays as the planned next arrow.
- `README.md`: Phase 2 done, Phase 3 next; one line on the event flow (D-143).
- `.claude/rules/backend-vsa.md` + `.cursor/rules/backend-vsa.mdc`: "Integration events cascade from the command
  handler inside its transaction; never publish after the response." and "Event payloads carry the data consumers need
  (event-carried state); consumers never call back into the publisher."
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta on top.

## Tests that prove it
Unit:
- Totals: one class only → other three are 0; several positions per class sum exactly (`0.10 + 0.20 = 0.30` as decimal);
  order of `Totals` follows `AssetClassCode`; every Portfolio `AssetClass` maps to an `AssetClassCode`.
- `SubmissionAccepted.ToString()` and `AssetClassTotal.ToString()` contain no amount (assert a marker amount like
  `987654.32` is absent).
- Consumer validation helper: wrong count, negative total, zero sum → rejected; valid → accepted.
Integration (real PostgreSQL, through HTTP on the real Api host, tracked Wolverine sessions):
- `Upload_ValidCsv_CascadesOneSubmissionAcceptedWithClassTotals` (exact totals from a known CSV)
- `Upload_ValidCsv_ComplianceStoresSnapshotForUploadersTenant` (end to end through the outbox and the durable local
  queue; this also exercises the real module registration, closing R-24)
- `Upload_InvalidCsv_PublishesNothing`
- `Upload_Duplicate_PublishesNothing`
- `SubmissionAccepted_DeliveredTwice_StoresOneSnapshot`
- `SubmissionAccepted_PayloadTenantDiffersFromEnvelope_IsRejected` (real handler)
- `SubmissionAccepted_ForBob_IsInvisibleToAlicesComplianceContext` (EF filter on the new table)
- `SubmissionAccepted_InvalidTotals_GoesToErrorQueueAndStoresNothing`
- `UploadAndSnapshot_Always_LogNoAmounts` (fake log collector; marker amount absent from every record)
- The upload response is unchanged for all existing upload tests (no regression from the tuple return).
Architecture: existing rules cover the new event (shape, `ToString()`), the consumer (one DbContext) and module
references; add one rule if missing: "Compliance does not reference `FinGuard.Portfolio`" (only its Contracts).
No frontend or E2E change; run `pnpm e2e` once anyway (R-25) against `-StartAspire` and stop it.

## Acceptance criteria
- `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- With `-StartAspire`: uploading `samples/portfolios/valid-balanced.csv` as Alice creates exactly one row in
  `compliance.portfolio_snapshots` with Acme's `tenant_id` and the class totals matching the sample README (compare in
  SQL; do not log them); `invalid-mixed.csv` and a duplicate upload create none; `wolverine_dead_letters` is empty.
- `pnpm e2e` green once; Aspire stopped; Close-out block with "Still running: none".

## Rules to respect
- ADR-0009: atomic outbox, envelope tenant, idempotent consumer by event id, one DbContext per handler.
- ADR-0007: EF for the consumer's write; no Dapper read is added in this brief.
- ADR-0004: `portfolio_snapshots` is tenant-owned with `tenant_id`, stamped, filtered.
- No cross-schema reference; Compliance never queries `portfolio.*`.
- D-77: identifier-only `ToString()` on every new message type.

## Out of scope
Compliance domain (`LimitPolicy`, `Evaluation`, specifications), evaluation, alerts, `EvaluationCompleted`, any API or UI
for Compliance (Phase 3, Briefs #13-#15); backfill; per-module queues; dependency upgrades (the Phase 3 majors review
happens in Brief #13's preflight).

## Commit plan
1. `feat: add the SubmissionAccepted integration contract` (task 1 with unit tests)
2. `feat: publish SubmissionAccepted from accepted uploads` (task 2 with tests)
3. `feat: store portfolio snapshots in Compliance from accepted submissions` (task 3 with migration and tests)
4. `docs: add ADR-0011 and close Phase 2 in the overview and README` (task 4)

## Pull request
- **Title (exact):** `feat: publish accepted submissions to Compliance through the outbox`
- **Body:** `.git/finguard-pr-body.md` from `.github/pull_request_template.md` with real numbers; Summary states that
  Phase 2 is complete and names ADR-0011.
- Close-out block as AGENTS.md requires, including:
  `git push -u origin feat/submission-accepted-event` and
  `gh pr create --base main --head feat/submission-accepted-event --title "feat: publish accepted submissions to Compliance through the outbox" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first; wait for approval. At the end: how the tuple return behaved (or why the fallback was used), test counts by
project, the SQL check of the snapshot row (counts and tenant only, no amounts in the report), the execution delta, and
the Close-out block.

---

## Appendix A — Decisions delta (session 21: Brief #12 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-152 | Integration event `SubmissionAccepted` (Portfolio → Compliance), published once per new accepted submission; no event for rejected or duplicate uploads | Closes open question 2 of session 18 (naming). Supersedes `PortfolioSubmitted`/`PortfolioRejected` in `overview.md` |
| D-153 | Event-carried state: four `AssetClassTotal(AssetClassCode, decimal BookValue)` entries, zeros included, plus submission id, cut-off date and `ReceivedAt`; Compliance never calls back into Portfolio | Closes open question 2 of session 20 |
| D-154 | `AssetClassCode` enum lives in `FinGuard.Portfolio.Contracts`; Compliance maps it to its own domain type | |
| D-155 | Published by cascading from `ReceiveSubmissionHandler` inside the EF transaction (tuple with `OutgoingMessages`; fallback `IMessageContext.PublishAsync`) | Atomic with the submission rows |
| D-156 | Local queues: Wolverine default (one durable local queue per message type); no named per-module queues | Closes open question 1 of session 13 |
| D-157 | Compliance stores `portfolio_snapshots` (one row per accepted submission, four class columns, unique `source_event_id`), a persistence entity, not a domain aggregate | Phase 3 builds `Evaluation` from it |
| D-158 | No backfill for submissions accepted before this brief | Development data only |
| D-159 | ADR-0011: Portfolio → Compliance integration contract; additive changes only, breaking changes need a new event type | |
| D-160 | Phase 2 closes with Brief #12; Brief #13 opens Phase 3 with the dependency-majors review (R-13, R-31 Aspire 17) in its preflight | |

### Verified on 2026-11-20
| Area | Finding | Source |
| --- | --- | --- |
| Wolverine local queues | Default: an individual local queue per message type, named after the type | https://wolverine.netlify.app/guide/messaging/transports/local |
| Wolverine return values | Tuple returns: each item handled independently; `OutgoingMessages` cascades its items | https://wolverine.netlify.app/guide/handlers/return-values |

### Risks and flags
- **R-24** expected to close: the end-to-end test runs through the real module registration.
- **R-34 Tuple return with `InvokeAsync<T>`.** Likely supported, not yet proven in this codebase; the first commit with
  the change proves it or switches to the fallback.
- **R-35 Amounts in envelope storage.** Wolverine persists event bodies (with totals) in `wolverine` tables. That is
  storage, not logging, and stays inside the database; Phase 5 must keep database access and backups as protected as
  the module tables.

### Next step
Brief #13 (Phase 3 start): majors review, Compliance domain (`LimitPolicy` versioned with seed, `Evaluation`,
`Money`/`Ratio`/`AssetClass`, specifications), boundary tests, and the decision on `Result` in the Compliance domain
(open question 1 of session 5).
