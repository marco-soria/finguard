# Brief #10 — Portfolio CSV validation: accepted positions or stored rejection errors (opens Phase 2)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-10-csv-validation.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-10-csv-validation.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/portfolio-csv-validation` |
| PR title (exact, becomes the squash commit) | `feat: validate portfolio CSV and store positions or rejection errors` |
| Phase | Phase 2, first brief (#10-#12). Phase 2 exit: all validation cases tested; two submissions for one date coexist |

---

## Goal
Every upload is now parsed and validated in the same command. A valid file becomes an `Accepted` submission with its
positions stored; an invalid file becomes a `Rejected` submission with its errors stored (D-10). Both are created
resources (201). The pull request template gets a richer format, and this brief ships its own filled PR description.
The preflight includes the dependency-majors review (R-13), report only.

## Context
- Read first and treat as binding: `AGENTS.md` (domain rules: CSV columns, limits, validation), `CLAUDE.md`,
  `.claude/rules/backend-vsa.md`, `.claude/rules/frontend.md`, `.claude/rules/testing.md`, `docs/decisions.md` (newest
  delta first: session 17 executed), ADR-0004, ADR-0007, ADR-0009, `docs/git-workflow.md`.
- Domain facts (AGENTS.md, D-06, D-10, D-11): columns `position_id, asset_class, market_value_usd, currency,
  description`; max 5 MB, 10,000 rows, UTF-8; asset classes `cash`, `fixed_income`, `shares`, `real_estate`; complete
  data, no negatives, total > 0, currency `USD`; submissions are immutable; rejected submissions are stored with their
  errors; never log monetary values, CSV rows or descriptions.
- Current state: `ReceiveSubmission` (Brief #8) does transport checks (empty, > 5 MB, not `.csv`, future date) and stores
  `Received` + bytes; read slices (Brief #9) return `status` as a string; the upload form shows `Uploaded {file}.`; the
  E2E test uploads an in-memory CSV. Portfolio is a plain VSA module: no aggregates, no repositories (ADR-0007).
- Note: the squash commit of PR #16 is `feat: submission history and upload ui (#16)`, not the brief's title. From
  this brief on, the PR is created from the filled body below with the exact title.
- Facts verified on 2026-11-19 (re-check at execution):
  - `CsvHelper` 34.0.0 is the latest stable (2026-10-28); the previous stable is 33.1.0 (2025-06-02). 34.0 is a new
    major described as a SIMD-accelerated, allocation-free reader, so older samples may not match its API.
    Source: https://www.nuget.org/packages/CsvHelper
  - `Sep` (nietras) is fast but still 0.x. Source: https://www.nuget.org/packages/Sep
  - Npgsql 10 reads `date` as `DateOnly`; Dapper 2.1.89 maps it natively (session 17).

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Where validation runs | Synchronously inside `ReceiveSubmission` (one transaction: submission + bytes + positions or errors) | Async validation through a message (no consumer benefit at 10,000 rows; more states to show) |
| HTTP outcome | Transport problems (no file, empty, > 5 MB, not `.csv`, bad or future date) stay 400/413 and are **not** stored. Content problems produce a stored `Rejected` submission: **201** with `status: "Rejected"` and the errors. A valid file: **201** with `status: "Accepted"` | 422 for rejected (the submission was created; the client reads `status`); storing transport failures (nothing meaningful to store) |
| CSV reader | `CsvHelper` (latest stable 34.x), used only as an RFC 4180 field reader; FinGuard validates every field itself. If 34.x lacks a needed API, fall back to 33.1.0 and record it | `Sep` (0.x); a hand-written parser (quoting and multi-line fields are easy to get wrong) |
| Encoding | Strict UTF-8 (`throwOnInvalidBytes: true`); a UTF-8 BOM is allowed | Guessing encodings |
| Header | Exactly the five columns, case-insensitive, trimmed, any order; missing, unknown or duplicate columns reject the file | Positional columns without a header |
| Normalisation | Fields trimmed; `asset_class` and `currency` compared case-insensitively and stored lower/upper-case; blank lines ignored | Strict case |
| Amounts | `market_value_usd`: invariant culture, digits with an optional `.` and at most 2 decimals; no sign, thousands separator or exponent; 0 allowed per position; max `9999999999999999.99` (`numeric(18,2)`) | Accepting `1,000.00` or `1e6` |
| Description | Optional, max 500 characters, **not stored** (the raw file keeps it; free text never leaves Portfolio) | Storing free text in a column |
| Errors | Each error: `row` (header = row 1, as in a spreadsheet; null for file errors), `column` (nullable), `code`, `message`. Messages never echo cell content. Store and return the first 100; `errorCount` holds the total | Unbounded error lists; echoing values |
| Positions | `portfolio.positions`: one row per CSV row of an accepted file | Storing class totals now (Compliance input is designed in Brief #12) |
| Phase 1 rows | Migration turns existing `Received` submissions into `Rejected` with one error `Portfolio.Csv.NotValidated` ("Uploaded before validation existed."). The `Received` status disappears | Keeping a legacy status in the enum |
| PR format | Richer `.github/pull_request_template.md`; every brief from now on ends with a PR section (exact title + body skeleton) and Claude Code writes the filled body to `.git/finguard-pr-body.md` (never committed) | Writing the PR body by hand after the fact |

## Environment rules
- Never run `npm`, `yarn` or `npx`; use `pnpm`, `pnpm exec`, `pnpm dlx`. Never push, never open PRs, never change GitHub
  settings. Stop after local commits.
- Never invent APIs or versions. Stable versions only; `.NET 10` line for `Microsoft.*` packages. Read the XML docs of
  the CsvHelper version you install before writing the reader.
- Never log file names, file contents, rows, amounts, descriptions or hashes. Log submission id, size, status, row
  count and error count only.
- Ask before deviating from this brief; record every approved deviation in the delta.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/portfolio-csv-validation
dotnet --version; node --version; pnpm --version
docker info --format '{{.ServerVersion}}'
dotnet build -c Release; dotnet test --no-build -c Release
Push-Location src/frontend/finguard-web; pnpm install --frozen-lockfile; pnpm lint; pnpm test --watch=false; pnpm build; Pop-Location
```
Everything must be green before the first change.

**Dependency-majors review (R-13), report only, no upgrades in this brief.** Produce a table for: .NET SDK/runtime,
`Microsoft.*` 10.x packages, Aspire, WolverineFx, EF Core / Npgsql EF provider, EFCore.NamingConventions, Dapper,
xUnit v3, Testcontainers, ArchUnitNET, Angular (+ CLI, Material/CDK), TypeScript, Vitest, ESLint/angular-eslint,
Playwright, pnpm, Node, GitHub Actions used in CI. Columns: current pinned, latest stable major, release date, support
status (LTS/STS/EOL where it applies), blocker or dependency (e.g. Material follows Angular), recommendation
(stay / plan a brief / upgrade before Phase 5). Use official sources (NuGet, npm `pnpm view`, release pages) and cite
them. The table goes into the execution delta.

## Tasks

### 1. Pull request template
Replace `.github/pull_request_template.md` with:
```markdown
## Summary
<!-- One paragraph: what changed and why. One slice or one concern per PR. -->

## Brief and decisions
- Brief: <!-- docs/briefs/brief-NN-....md -->
- Decisions: <!-- D-xx ... from docs/decisions.md, or "none" -->
- ADR: <!-- docs/adr/....md, or "none" -->

## Changes
<!-- Bullets grouped by area: Backend, Frontend, Database (migrations), Tests, Docs/Rules. -->

## How it was tested
<!-- Commands run and results: backend unit/integration/architecture counts, frontend Vitest count,
     E2E run (required when the frontend or a Portfolio slice changed, R-25), manual check. -->

## Deviations and risks
<!-- Deviations from the brief (approved), new or changed risks (R-xx), follow-ups. "None" if none. -->

## Screenshots
<!-- UI changes only: light and dark. Delete this section otherwise. -->

## Checklist
- [ ] PR title is the exact Conventional Commits title from the brief (it becomes the squash commit)
- [ ] Tests added or updated (unit + integration through HTTP, tenant-isolation case where relevant)
- [ ] Architecture tests green; no new analyzer warnings
- [ ] No secrets, file names, CSV content, amounts or personal data in logs
- [ ] Migrations reviewed (if any): additive, tenant_id on tenant-owned tables, no cross-schema references
- [ ] Rules files (`.claude/rules` and `.cursor/rules`) updated together when a rule changed
- [ ] `docs/decisions.md` execution delta written
```
Update `docs/git-workflow.md` step 4: the brief's PR section gives the exact title; Claude Code leaves the filled body in
`.git/finguard-pr-body.md`; create the PR with
`gh pr create --base main --title "<exact title>" --body-file .git/finguard-pr-body.md` or paste it in the GitHub UI.

### 2. Persistence
- `SubmissionStatus`: `Accepted`, `Rejected` (remove `Received`).
- `Submission`: add `RowCount` (int, data rows read; 0 when the file failed before rows) and `ErrorCount` (int).
  Factory methods `Accept(...)` and `Reject(...)` replace `Receive(...)`; both stay immutable.
- `Position` (`ITenantOwned`): `SubmissionId`, `TenantId`, `RowNumber` (int), `PositionId` (varchar 64), `AssetClass`
  (varchar 20, stored lower-case via an internal `AssetClass` enum with string conversion), `MarketValueUsd`
  (`numeric(18,2)`). PK (`submission_id`, `row_number`); FK to `submissions` (cascade). Table `portfolio.positions`.
- `SubmissionError` (`ITenantOwned`): `SubmissionId`, `TenantId`, `Ordinal` (int), `RowNumber` (int?), `Column`
  (varchar 32?), `Code` (varchar 64), `Message` (varchar 200). PK (`submission_id`, `ordinal`); FK cascade. Table
  `portfolio.submission_errors`.
- Migration `AddCsvValidation`: new tables and columns (`row_count`, `error_count` default 0), then the data step for
  Phase 1 rows (`Received` → `Rejected`, `error_count = 1`, one `submission_errors` row with code
  `Portfolio.Csv.NotValidated`, `row_number`/`column` null). Raw SQL in the migration is fine; keep it idempotent within
  the migration. Check `has-pending-model-changes` is clean afterwards.

### 3. CSV reader and validator (`Features/ReceiveSubmission/Csv/`)
- `PortfolioCsvReader.Read(ReadOnlyMemory<byte> content) -> PortfolioCsvResult` (pure, no I/O, no logging):
  either `Positions` (list of `ParsedPosition(RowNumber, PositionId, AssetClass, MarketValueUsd)`) and `RowCount`, or
  `Errors` (first 100) + `ErrorCount` + `RowCount`. Constants for the limits (`MaxRows = 10_000`, `MaxErrors = 100`,
  `MaxPositionIdLength = 64`, `MaxDescriptionLength = 500`).
- File errors stop reading: `Portfolio.Csv.InvalidEncoding`, `Portfolio.Csv.HeaderMissing` (no header line),
  `Portfolio.Csv.ColumnMissing`, `Portfolio.Csv.ColumnUnknown`, `Portfolio.Csv.ColumnDuplicated` (one error per offending
  column, `column` set), `Portfolio.Csv.MalformedRow` (bad quoting, wrong field count; `row` set), `Portfolio.Csv.NoRows`,
  `Portfolio.Csv.TooManyRows` (stop at row 10,001).
- Row errors are collected for every row (count all, keep the first 100): `PositionIdRequired`, `PositionIdTooLong`,
  `PositionIdDuplicated`, `AssetClassRequired`, `AssetClassUnknown`, `MarketValueRequired`, `MarketValueInvalid`,
  `MarketValueNegative`, `MarketValueTooPrecise`, `MarketValueOutOfRange`, `CurrencyRequired`, `CurrencyNotUsd`,
  `DescriptionTooLong` (all prefixed `Portfolio.Csv.`).
- File-level total: only when there are no other errors, `Portfolio.Csv.TotalNotPositive` if the sum is 0.
- Messages are fixed English sentences per code (e.g. "The market value must not be negative."), never containing the
  cell value.
- Error order: by row, then by column order of the canonical header.

### 4. Command and responses
- `ReceiveSubmissionHandler`: transport checks unchanged (still `Result` failures, nothing stored). Then hash, read,
  and create an `Accepted` submission + positions or a `Rejected` submission + errors, plus `SubmissionContent` in both
  cases. Log: `Stored submission {SubmissionId} ({SizeBytes} bytes): {Status}, {RowCount} rows, {ErrorCount} errors`.
- `SubmissionReceivedResponse`: `submissionId`, `status`, `receivedAt`, `rowCount`, `errorCount`, `errors`
  (`[{ row, column, code, message }]`, at most 100). The endpoint keeps `201 Created` + `Location` for both outcomes.
- `GetSubmission` (details) adds `rowCount`, `errorCount` and `errors` (from `submission_errors`, ordered by `ordinal`);
  `ListSubmissions` adds `rowCount` and `errorCount`. Both stay Dapper, own schema, `tenant_id` filter; neither reads
  `submission_contents` or `positions`.
- Update `docs/architecture/overview.md` section 4: the upload answers 201 with `Accepted` or `Rejected`; no 422/202.
  Leave the event names as they are; Brief #12 reconciles them with D-93.

### 5. Frontend (minimal; the full errors UI is Brief #11)
- Interfaces follow the new JSON (`rowCount`, `errorCount`, `errors`).
- Upload status message: `Accepted {fileName}: {rowCount} positions.` or `Rejected {fileName}: {errorCount} errors.`
  followed by a link "View details" to `/submissions/{id}`.
- History: add an "Errors" column (number; empty for accepted).
- Detail page: show `rowCount`, `errorCount` and, when rejected, a simple list of the first errors
  (`Row {row}, {column}: {message}`; file errors without row/column). Keep it plain; Brief #11 designs it properly.

### 6. Samples and E2E
- `samples/portfolios/` with three small files and a `README.md` (English) describing each:
  `valid-balanced.csv` (all four classes, within limits), `valid-shares-heavy.csv` (shares above 25 % of the total; for
  Phase 3), `invalid-mixed.csv` (one error of each common kind). Fictional position ids and descriptions only.
- Update `e2e/submissions.e2e.ts`: the existing flow uploads a valid in-memory CSV and expects `Accepted`; add a second
  test that uploads an invalid CSV and expects `Rejected` in the status message, the history row, and an error line on
  the detail page. Unique file names per run.

### 7. Rules and docs
- `.claude/rules/backend-vsa.md` + `.cursor/rules/backend-vsa.mdc` (same commit): content validation stores a
  `Rejected` submission (201), transport validation returns 400/413 without storing; validation messages never echo
  cell values; read queries never select `submission_contents` or `positions` unless the slice is about positions.
- `.claude/rules/testing.md` + `.cursor/rules/testing.mdc`: CSV rules are proven by table-driven unit tests on the
  reader (one case per error code and per boundary) plus integration tests for both outcomes.
- `AGENTS.md`: under the domain rules, link `samples/portfolios/README.md` and list the CSV error codes' prefix
  `Portfolio.Csv.*`.
- `docs/decisions.md`: add Appendix A as the session 18 planning delta, then the execution delta on top (including the
  majors table).

## Tests that prove it
Unit (`FinGuard.UnitTests`, table-driven, no I/O):
- Encoding: valid UTF-8, UTF-8 with BOM, invalid byte sequence → `InvalidEncoding`.
- Header: exact, different order, upper-case, extra spaces → accepted; missing column, unknown column, duplicated
  column, empty file → the matching codes.
- Rows: each row error code with the minimal failing input; `row` numbering (header = 1, quoted multi-line description
  still counts as one row); blank lines ignored; `MalformedRow` for an unclosed quote.
- Amounts: `0`, `0.00`, `1`, `1.5`, `1.50`, `9999999999999999.99` accepted; `-1`, `1.505`, `1,000`, `1e3`, `+1`, `abc`,
  `10000000000000000.00` rejected with the right code.
- Limits: exactly 10,000 rows accepted; 10,001 → `TooManyRows`; 150 bad rows → 100 errors returned, `ErrorCount` 150.
- `PositionIdDuplicated` (second occurrence is the error); description of 500 accepted, 501 rejected.
- Total: all zero values → `TotalNotPositive`; not reported when other errors exist.
- No message contains the offending cell value (assert over every code).
- Handler (with `FakeTimeProvider`): valid → `Accepted` with positions; invalid → `Rejected` with errors; transport
  failures unchanged.

Integration (`FinGuard.Api.IntegrationTests`, real PostgreSQL, through HTTP):
- `Upload_ValidCsv_Returns201AcceptedAndStoresPositions`
- `Upload_InvalidCsv_Returns201RejectedAndStoresErrors`
- `Upload_TenThousandRows_IsAccepted`
- `Upload_ManyBadRows_StoresFirst100ErrorsAndTotalCount`
- `Upload_TransportFailure_StoresNothing` (empty file, not `.csv`, future date)
- `Upload_TwoFilesSameCutOffDate_BothStored` (Phase 2 exit criterion, without idempotency yet: different content)
- `Upload_ValidCsv_StampsTenantOnPositionsAndErrors` (Alice's rows carry Acme's tenant id)
- `GetSubmission_Rejected_ReturnsErrors`; `GetSubmission_OtherTenantsRejected_Returns404`
- `ListSubmissions_IncludesRowAndErrorCounts`
- `Upload_Any_DoesNotLogContentNamesAmountsOrDescriptions` (fake log collector; markers in position id, description and
  amount)
- `Migration_PhaseOneReceivedRows_BecomeRejectedWithNotValidated` (seed a `Received` row through SQL on a fresh database
  before applying `AddCsvValidation`, if the fixture allows; otherwise a migration SQL unit check, and say which)

Architecture: existing suite green (new entities are `ITenantOwned` and mapped by `PortfolioDbContext` only).

Frontend (Vitest): upload status message for both outcomes and the details link; history "Errors" column; detail page
lists errors for rejected and hides the list for accepted.

E2E (Playwright, local): accepted flow and rejected flow, twice in a row against the same database.

## Acceptance criteria
- `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm test --watch=false`, `pnpm build`,
  `pnpm exec prettier --check src e2e` green.
- With `aspire run`: `pnpm e2e` passes twice; the three sample files give Accepted / Accepted / Rejected through the UI.
- `portfolio.positions` and `portfolio.submission_errors` rows carry the uploader's `tenant_id`; no new cross-schema
  reference.
- Api log for each upload is one line with id, size, status, row count and error count; nothing else from the file.
- `.git/finguard-pr-body.md` exists, follows the new template, and holds the real numbers of this run.

## Rules to respect
- ADR-0007: EF writes, Dapper reads; mandatory `tenant_id` filter in every Dapper query.
- D-77: new or changed message types override `ToString()` with identifiers and counts only (no file name).
- D-91: never log the file name or hash; never use the file name as a path.
- The tenant never comes from the CSV, form, route or query string.
- Portfolio stays plain VSA: no aggregates, no repositories, no specifications.
- Frontend: Material components, `--mat-sys-*` tokens, `cutOffDate` never through `Date`.

## Out of scope
SHA-256 idempotency and "current version per cut-off date" (Brief #11); the designed errors UI and R-28 (Brief #11);
integration event to Compliance and the local-queue decision (Brief #12); class totals; any dependency upgrade; asset
class or limit changes; download of the original file.

## Commit plan
1. `docs: improve the pull request template` (task 1)
2. `feat: add portfolio CSV reader and validation rules` (task 3 with its unit tests; CsvHelper added to CPM)
3. `feat: store accepted positions and rejection errors for submissions` (tasks 2 and 4 with migration and integration
   tests)
4. `feat: show validation outcome in the upload and history screens` (task 5 with Vitest specs)
5. `test: add sample portfolios and the rejected-upload end-to-end flow` (task 6)
6. `docs: update rules, overview and decisions log` (task 7)

Every commit builds and its tests pass. End every commit message with the attribution lines your harness requires.

## Pull request
- **Title (exact):** `feat: validate portfolio CSV and store positions or rejection errors`
- **Body:** write `.git/finguard-pr-body.md` from the new template at the end of the run, with real values. Skeleton:

```markdown
## Summary
Uploads are now parsed and validated in the same command. A valid CSV becomes an Accepted submission with its
positions; an invalid CSV becomes a Rejected submission with its errors (D-10). Transport problems are still 400/413
and are not stored. The PR template gets a richer format.

## Brief and decisions
- Brief: docs/briefs/brief-10-csv-validation.md
- Decisions: D-110 to D-118 (planning), D-1xx (execution)
- ADR: none

## Changes
- Backend: ...
- Database: migration `AddCsvValidation` (positions, submission_errors, row/error counts, Phase 1 rows → Rejected)
- Frontend: ...
- Tests: ...
- Docs/Rules: PR template, git workflow, backend-vsa, testing, AGENTS.md, overview, decisions

## How it was tested
- Backend: unit N, integration N, architecture N (all green)
- Frontend: Vitest N
- E2E: 2 tests, two runs against the same database
- Manual: the three sample files through the UI (Accepted / Accepted / Rejected)

## Deviations and risks
- ...

## Screenshots
- Upload result and detail page for a rejected file (light and dark)

## Checklist
- [x] ...
```

## Report back
- Plan first; wait for approval.
- At the end: the majors table, resolved CsvHelper version and the API you used, deviations, findings, test counts by
  project, the two E2E runs, screenshots (rejected detail page, light and dark), the execution delta in
  `docs/decisions.md`, and the path of the filled PR body.

---

## Appendix A — Decisions delta (session 18: Brief #10 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-110 | Every brief ends with a "Pull request" section (exact title + body skeleton); Claude Code writes the filled body to `.git/finguard-pr-body.md`; `.github/pull_request_template.md` gains Changes, How it was tested, Deviations and risks, Screenshots and a longer checklist | PR #16 squash title differed from the brief's title |
| D-111 | CSV validation runs synchronously in `ReceiveSubmission`; one transaction stores submission, bytes and positions or errors | |
| D-112 | Transport failures → 400/413, not stored; content failures → stored `Rejected` submission, 201 with `status` and errors; valid → 201 `Accepted` | Supersedes the 422/202 sketch in `overview.md` |
| D-113 | CSV reader: CsvHelper (latest stable 34.x, fallback 33.1.0) as a field reader only; strict UTF-8, BOM allowed; header of exactly the five columns, case-insensitive, any order | Rejected: Sep (0.x), hand-written parser |
| D-114 | Value rules: trimmed fields; asset class and currency case-insensitive; amounts invariant, ≤ 2 decimals, no sign/thousands/exponent, 0 allowed per row, max `numeric(18,2)`; position id required, ≤ 64, unique per file; description optional ≤ 500 and not stored; total > 0 | Assumptions on top of AGENTS.md; reopen if a real file format disagrees |
| D-115 | Errors carry row (header = 1), column, code `Portfolio.Csv.*`, fixed message without cell values; first 100 stored and returned, `error_count` holds the total | |
| D-116 | Accepted rows stored in `portfolio.positions` (PK submission + row); errors in `portfolio.submission_errors`; both tenant-owned | Class totals designed with the Compliance input in Brief #12 |
| D-117 | `Received` status removed; Phase 1 rows migrated to `Rejected` with `Portfolio.Csv.NotValidated` | Dev data only |
| D-118 | Sample portfolios in `samples/portfolios/` (fictional data) for manual checks, demos and Phase 3 | |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| CsvHelper | 34.0.0 latest stable (2026-10-28), new major with a reworked reader; 33.1.0 previous stable | https://www.nuget.org/packages/CsvHelper |
| Sep | Latest is 0.x | https://www.nuget.org/packages/Sep |

### Risks and flags
- **R-29 CsvHelper 34 is a fresh major.** Few samples match its API. Mitigation: read the package XML docs, keep the
  reader behind `PortfolioCsvReader`, fall back to 33.1.0 if needed.
- **R-30 Validation latency.** 10,000 rows plus 10,000 EF inserts run inside the request. Measure in
  `Upload_TenThousandRows_IsAccepted` (report the time); if it exceeds a few seconds, consider batching or `COPY` in
  Phase 5.

### Open questions
1. Per-module local queues: decide with the Compliance event (Brief #12).
2. Event naming: `overview.md` says `PortfolioSubmitted`/`PortfolioRejected`, D-93 says `SubmissionAccepted`; settle in
   Brief #12.

### Next step
Brief #11: SHA-256 idempotency (D-11), current version per cut-off date (latest accepted), errors UI and R-28.
