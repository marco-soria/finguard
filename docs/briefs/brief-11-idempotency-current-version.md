# Brief #11 — Idempotent uploads, current submission per cut-off date and error details (Phase 2)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-11-idempotency-current-version.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-11-idempotency-current-version.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/submission-idempotency-and-current-version` |
| PR title (exact, becomes the squash commit) | `feat: add idempotent uploads, current submission per cut-off date and error details` |
| Phase | Phase 2, second brief (#10-#12). Phase 2 exit: all validation cases tested; two submissions for one date coexist |

---

## Goal
1. Repair the decisions log: PR #18's conflict resolution dropped the Brief #10 deltas, and add a CI guard so it cannot
   happen silently again.
2. Same file + same cut-off date returns the existing submission instead of storing a copy (D-11).
3. Every submission says whether it is the **current** one for its cut-off date (latest accepted).
4. Users see why something failed: the error list of a rejected file, and the API's `detail` for request-level
   failures (R-28).
5. Refresh the README: it still says "Phase 0".

## Context
- Read first and treat as binding: `AGENTS.md` (including "Closing a work session"), `CLAUDE.md`,
  `.claude/rules/backend-vsa.md`, `.claude/rules/frontend.md`, `.claude/rules/testing.md`, `docs/decisions.md`,
  ADR-0004, ADR-0007, ADR-0010, `docs/dev-environment.md`, `docs/git-workflow.md`.
- **Lost deltas.** `57d5ad5` (PR #17) has three deltas at the top of `docs/decisions.md`: "session 19: Brief #10
  amendments" (D-126 to D-131), "session 18: Brief #10 executed" (D-119 to D-125, majors review, R-31), "session 18:
  Brief #10 planning" (D-110 to D-118, R-29, R-30). `46e313b` (PR #18) removed them while resolving a conflict;
  nothing else was lost (`git diff 57d5ad5 46e313b --stat`: only `decisions.md` has deletions). The current newest delta
  is "session 19: session hygiene" (D-132 to D-136).
- Upload today (Briefs #8-#10a): transport checks → SHA-256 → CSV validation → stored `Accepted` (positions) or
  `Rejected` (errors), 201 + `Location`. Read slices: list (newest 100, `rowCount`, `errorCount`), details (`errors`).
  `ResultHttpExtensions` already puts `Error.Description` into ProblemDetails `detail` with a `code` extension; the SPA
  shows only `title` (D-80), so a future cut-off date shows "Validation failed" without the reason.
- One user per tenant (locked decision), so concurrent identical uploads by the same tenant are rare.

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Lost deltas | Restore the three deltas verbatim from `57d5ad5`, placed below "session 19: session hygiene", in the first commit | Rewriting them from memory |
| Guard | `docs/decisions.md` is **append-only**: corrections go into a new delta that supersedes the old entry. CI `hygiene` job fails a pull request that deletes or changes any existing line of it | Manual review only (that is what failed) |
| Duplicate rule | Same tenant + same `cut_off_date` + same SHA-256 of the raw bytes → return the **earliest** matching submission, whatever its status. The file name does not matter | Matching on file name; returning only accepted ones |
| Duplicate response | `200 OK` with the same body shape as a new upload plus `"duplicate": true`, and `Location` of the existing submission; a new upload stays `201` with `"duplicate": false`. Nothing is stored for a duplicate (no new bytes, no new row) | 409 (the client did nothing wrong); a new row pointing to the old one |
| Enforcement | Pre-check query in the handler (EF, own DbContext) plus a **non-unique** index `(tenant_id, cut_off_date, sha256)`. Two simultaneous identical uploads may both be stored; accepted as a known race | Unique constraint: dev and test databases already hold identical uploads (Phase 1/2 E2E), so the migration would fail or need a data cleanup on immutable rows; the race needs two parallel requests from the tenant's single user |
| Current version | Computed at read time: among a tenant's `Accepted` submissions for a cut-off date, the one with the latest `received_at` (tie: highest `id`). Exposed as `isCurrent` on list items and details. Rejected submissions are never current and never change which one is current | A stored flag (needs updates on every upload; derived data) |
| Current lookup for later phases | No public query yet; Brief #12 decides what Compliance receives | Designing the Contracts query now |
| Error details in the SPA | `ApiError` gains `detail` and `code`. For 4xx responses the UI shows `title` and `detail`; for 5xx and non-ProblemDetails it keeps the generic title. Backend messages are fixed English sentences without user data (D-115), so showing them is safe | Showing raw server text for 5xx |
| Rejected file UI | Detail page: `mat-table` of errors (Row, Column, Message; file errors show "File"), header "Showing the first 100 of N errors" when `errorCount > 100`; error codes stay in the DOM as `data-code` for tests, not as visible text | Showing codes to users |
| README | Rewritten as the reviewer's landing page (outline in task 7) and kept current: AGENTS.md "Definition of done" gains "README status updated when a phase or a visible feature changes" | Leaving it for Phase 5 |

## Environment rules
- Follow AGENTS.md "Closing a work session" from the first command: `scripts/dev-session.ps1 -Snapshot` at the start,
  Aspire only through `-StartAspire`, `-Stop` before the final message, Close-out block with "Still running: none".
- Never run `npm`, `yarn` or `npx`; use `pnpm` from inside `src/frontend/finguard-web` (from the repo root pnpm
  fails with `ERR_PNPM_BAD_PM_VERSION`). In Git Bash, call the Aspire CLI as `aspire.cmd` (D-135).
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Never log file names, hashes, contents, amounts or descriptions. The duplicate log line has ids only.
- Ask before deviating from this brief; record every approved deviation in the delta.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/submission-idempotency-and-current-version
scripts/dev-session.ps1 -Snapshot
dotnet build -c Release; dotnet test --no-build -c Release
Push-Location src/frontend/finguard-web; pnpm install --frozen-lockfile; pnpm lint; pnpm test --watch=false; pnpm build; Pop-Location
git diff 57d5ad5 46e313b --stat   # confirm only docs/decisions.md lost lines
```

## Tasks

### 1. Restore the decisions log and add the append-only guard
- Copy lines 6-156 of `git show 57d5ad5:docs/decisions.md` (the three Brief #10 deltas) into the current file right
  after the "session 19: session hygiene" delta, unchanged. Check with
  `git diff 57d5ad5 -- docs/decisions.md`: only the session-hygiene delta should differ.
- `.github/workflows/ci.yml`, `hygiene` job, pull requests only: check out with `fetch-depth: 0`, then fail when
  `git diff --numstat origin/${{ github.base_ref }}...HEAD -- docs/decisions.md` reports deleted lines, with the message
  `docs/decisions.md is append-only: add a new delta that supersedes the entry instead of editing it.`
  Verify locally with a throwaway commit that changes one old line (the check fails), then drop that commit.
- `docs/git-workflow.md`: when resolving a conflict in `docs/decisions.md`, keep both sides (each delta is a block);
  run two branches in parallel only if at most one of them adds a delta, otherwise merge the first before starting the
  second.
- `AGENTS.md` conventions: one line "`docs/decisions.md` is append-only (CI-enforced)".

### 2. Idempotent upload (backend)
- Migration `AddSubmissionHashIndex`: non-unique index on `(tenant_id, cut_off_date, sha256)`.
- `ReceiveSubmissionHandler`: after the transport checks and the hash, before reading the CSV:
  `submissions.Where(cut_off_date, sha256).OrderBy(received_at).ThenBy(id).FirstOrDefault()` through
  `PortfolioDbContext` (tenant filter applies). If found: no writes; return the existing submission's data
  (status, counts, first 100 errors from `submission_errors`) with `Duplicate = true`. Log
  `Duplicate upload of submission {SubmissionId}` (Information).
- Response record gains `Duplicate` (bool). Endpoint: `201 Created` for new, `200 OK` for duplicate, `Location` in
  both. `ToString()` override includes `Duplicate`.
- Test data: every existing test helper that builds CSV content must make it unique per call (e.g. a unique
  `position_id` suffix), so tests do not become accidental duplicates in the shared test database. Only the
  idempotency tests reuse content on purpose. Same for the E2E in-memory CSVs.

### 3. Current version (backend)
- `ListSubmissions` and `GetSubmission` add `isCurrent`. Dapper SQL, own schema, `tenant_id` filter in every
  sub-select, for example a window over accepted rows:
  `ROW_NUMBER() OVER (PARTITION BY cut_off_date ORDER BY received_at DESC, id DESC)` restricted to
  `status = 'Accepted'`; `isCurrent = (status = 'Accepted' AND rn = 1)`. For details, compute it for the one row with
  an `EXISTS`/`NOT EXISTS` on a newer accepted submission of the same tenant and date. Keep the existing indexes
  sufficient (check `EXPLAIN` once on the dev database and note it in the delta).

### 4. Error details in the SPA (frontend)
- `ApiError(status, title, detail?, code?)`; `problemDetailsInterceptor` maps `detail` and `code` when present and
  strings; one helper `errorMessage(error)` returns `title` for 5xx/generic and `"{title}: {detail}"` (or two lines;
  pick one and test it) for 4xx with a detail. Components use the helper only.
- `UploadForm`: shows the helper's text on failure; on a duplicate shows
  `Already uploaded on {receivedAt local}: {status}.` with "View details". New uploads keep the Brief #10 message.
- `SubmissionHistory`: status cell shows `Accepted`, `Rejected`, and a "Current" marker (Material chip or text badge
  using `--mat-sys-*` tokens) when `isCurrent`.
- `SubmissionDetail`: "Current version for {cutOffDate}" / "Not the current version" line for accepted ones; the
  errors `mat-table` described in the decisions; `data-code` attributes.
- Accessibility: the marker has text (not colour only); tables have captions or `aria-label`.

### 5. E2E
- Existing tests generate unique content per run.
- New `duplicate.e2e.ts`: upload a unique valid CSV, upload the same bytes again with the same date → duplicate
  message, history has one row for it; upload a second unique valid CSV for the same date → the newer row is
  "Current", the older is not (Phase 2 exit: two submissions for one date coexist, latest accepted is current).
- A future cut-off date shows the API detail (R-28).

### 6. Rules and docs
- `.claude/rules/backend-vsa.md` + `.cursor/rules/backend-vsa.mdc`: idempotency rule (pre-check, 200 + `duplicate`,
  no writes); derived values like `isCurrent` are computed in read queries, not stored.
- `.claude/rules/frontend.md` + `.cursor/rules/frontend.mdc`: "Show `title` and `detail` for 4xx through
  `errorMessage`; never show server text for 5xx." (replaces the D-80 "only title" line).
- `AGENTS.md` domain rules: duplicate semantics and "current = latest accepted by `received_at`".
- `docs/architecture/overview.md`: upload flow with the duplicate branch.

### 7. README refresh
Rewrite `README.md` (English, concise, no badges that need external services). Outline:
1. One-paragraph pitch (what FinGuard does; limits evaluated on book value, ADR-0010).
2. **Status**: phase table (0 Foundations ✓, 1 Walking skeleton ✓, 2 Portfolio ingestion in progress, 3-5 planned) with
   one line each; link to `docs/decisions.md`.
3. **Architecture at a glance**: a small Mermaid diagram (modules Tenancy / Portfolio / Compliance / Advisory, Api,
   PostgreSQL schemas, Wolverine outbox, Angular SPA) and 6-8 bullets of the key choices, each linking its ADR
   (modular monolith + VSA, Wolverine outbox, tenant isolation, EF commands + Dapper queries, deterministic engine + AI
   narrator, local-first / Azure in Phase 5, book value).
4. **Try it in two minutes**: prerequisites link to `docs/dev-environment.md`, `aspire run` (Git Bash: `aspire.cmd`),
   open `web`, sign in as Alice, upload the files from `samples/portfolios/` and what to expect.
5. **Quality**: how to run backend tests, frontend tests, E2E; test counts at the time of writing; architecture tests
   and banned APIs in one sentence each.
6. **Repository map** (short tree of top-level folders).
7. **Working with AI agents**: the existing section, plus briefs and the session close-out rule.
8. **Documentation** links (existing list).
Remove the stale "Phase 0" text and the duplicated prerequisites. Keep it under about 150 lines.

## Tests that prove it
Unit:
- `errorMessage`-equivalent on the backend is not needed; handler tests with `FakeTimeProvider`: duplicate of an
  accepted file returns it with `Duplicate = true` and stores nothing; duplicate of a rejected file returns the
  rejected one; same bytes with another date is new; same bytes for another tenant is new.
Integration (through HTTP, real PostgreSQL):
- `Upload_SameFileSameDateTwice_Returns200DuplicateWithSameIdAndStoresOneRow`
- `Upload_SameFileDifferentDate_Returns201New`
- `Upload_SameFileAsOtherTenant_Returns201New` (tenant isolation of the duplicate check)
- `Upload_DuplicateOfRejected_ReturnsRejectedWithErrors`
- `Upload_DuplicateWithDifferentFileName_IsDuplicate`
- `ListSubmissions_TwoAcceptedSameDate_NewestIsCurrent`
- `ListSubmissions_NewerRejectedSameDate_DoesNotChangeCurrent`
- `ListSubmissions_IsCurrent_IgnoresOtherTenantsSubmissions` (Bob's newer accepted file for the same date does not
  change Alice's current one)
- `GetSubmission_IsCurrent_MatchesList`
- `Upload_Duplicate_LogsIdsOnly` (no file name or hash in logs)
- ProblemDetails for a future cut-off date carries `detail` and `code` (existing test extended if needed).
CI guard: proven locally as described in task 1; mention the run in the delta.
Vitest: interceptor maps `detail`/`code`; `errorMessage` for 400 with detail, 400 without, 404, 500, non-ProblemDetails;
upload form duplicate message; history "Current" marker; detail page errors table, the "first 100 of N" header and
the current-version line.
E2E: as task 5, two runs in a row against the same database.

## Acceptance criteria
- `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm test --watch=false`, `pnpm build`,
  `pnpm exec prettier --check src e2e` green.
- `pnpm e2e` passes twice against a fresh `-StartAspire` instance, which is then stopped.
- `docs/decisions.md` contains every delta from `57d5ad5` plus the session-hygiene delta and this brief's deltas;
  `git diff 57d5ad5 -- docs/decisions.md` shows only additions.
- README renders on GitHub (Mermaid included) and its quick start works as written.
- Close-out block with "Still running: none" and the PR commands.

## Rules to respect
- ADR-0007: EF writes and the duplicate pre-check through `PortfolioDbContext`; reads through Dapper with `tenant_id`.
- Submissions stay immutable; a duplicate never updates the existing row.
- D-77: changed message/response types keep identifier-only `ToString()`.
- D-103: `cutOffDate` never through `Date`; `receivedAt` in local time.
- No business rule in the frontend: `isCurrent` and `duplicate` come from the API.

## Out of scope
The integration event to Compliance, class totals, local-queue decision and event naming (Brief #12); paging or
filters; download of the original file; dependency upgrades (Aspire 17, Node 26, pnpm 12 stay planned); deployment
design (recorded below as a Phase 5 decision only).

## Commit plan
1. `docs: restore Brief #10 decision deltas and make the log append-only` (task 1, CI guard included)
2. `feat: return the existing submission for duplicate uploads` (task 2 with tests)
3. `feat: mark the current submission per cut-off date` (task 3 with tests)
4. `feat: show error details and the current version in the web app` (task 4 with Vitest)
5. `test: add duplicate and current-version end-to-end flows` (task 5)
6. `docs: refresh README and update rules for idempotency` (tasks 6-7, decisions delta)

## Pull request
- **Title (exact):** `feat: add idempotent uploads, current submission per cut-off date and error details`
- **Body:** `.git/finguard-pr-body.md` from `.github/pull_request_template.md` with the real numbers. Summary must
  mention the restored deltas and the new CI guard, so the reviewer knows why `docs/decisions.md` grows by ~150 lines.
- Close-out block as AGENTS.md requires, including:
  `git push -u origin feat/submission-idempotency-and-current-version` and
  `gh pr create --base main --head feat/submission-idempotency-and-current-version --title "feat: add idempotent uploads, current submission per cut-off date and error details" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first; wait for approval. At the end: restored-delta check output, CI guard proof, `EXPLAIN` note, test counts by
project, two E2E runs, screenshots (history with "Current", rejected detail page, duplicate message; light and dark),
the execution delta, and the Close-out block.

---

## Appendix A — Decisions delta (session 20: Brief #11 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-137 | `docs/decisions.md` is append-only; corrections supersede through a new delta; CI `hygiene` fails PRs that delete or change existing lines | PR #18 silently dropped the Brief #10 deltas while resolving a conflict |
| D-138 | Two branches in parallel only if at most one adds a decisions delta | Root cause of the loss |
| D-139 | Duplicate = same tenant + cut-off date + SHA-256 of raw bytes; returns the earliest match whatever its status; `200 OK` + `duplicate: true` + `Location`; nothing stored | Implements D-11 |
| D-140 | Enforced by a pre-check and a non-unique index `(tenant_id, cut_off_date, sha256)`; a simultaneous identical upload may be stored twice (accepted race) | Unique constraint rejected: existing duplicate rows in dev/test data; one user per tenant |
| D-141 | `isCurrent` is derived at read time: latest accepted by `received_at` (tie: `id`) per tenant and cut-off date; rejected never current | Not stored |
| D-142 | SPA shows `title` + `detail` for 4xx through one helper; 5xx stays generic; codes only as `data-code` | Closes R-28; supersedes D-80's "only title" |
| D-143 | README is the reviewer's landing page and is updated when a phase or a visible feature changes (Definition of done) | |
| D-144 | Phase 5 hosting (planned, ADR in Phase 5): Angular SPA on **Azure Static Web Apps Free**, Api on **Azure Container Apps** (Consumption); SPA calls the Api cross-origin with bearer tokens; the Api gets a CORS policy for the SWA origin only; `staticwebapp.config.json` carries the SPA fallback and security headers (CSP, R-22) | Closes open question 1 of session 7. Rejected: second container app with nginx (cold start for static files, an image to maintain); SPA served by the Api container (couples deploys, blank page while the Api scales from zero); SWA Standard linked backend (~monthly fee; not zero-cost) |

### Verified on 2026-11-20
| Area | Finding | Source |
| --- | --- | --- |
| Static Web Apps | Free plan has no SLA; Standard is for production with SLA | https://azure.microsoft.com/pricing/details/app-service/static/ |
| Static Web Apps | Quotas: 100 GB bandwidth per month, 10 apps per subscription (Free), 250 MB per app | https://learn.microsoft.com/azure/static-web-apps/quotas |
| Static Web Apps | "Bring your own API" (linked Container Apps backend) needs the Standard plan; not supported on pull-request environments | https://learn.microsoft.com/azure/static-web-apps/apis-overview , https://learn.microsoft.com/azure/static-web-apps/apis-container-apps |
| Container Apps | Consumption: first 180,000 vCPU-s, 360,000 GiB-s and 2 million requests per subscription per month are free | https://learn.microsoft.com/azure/container-apps/billing |

### Risks and flags
- **R-32 Cross-origin in Phase 5.** SWA Free + ACA means CORS and two origins; Entra redirect URIs must list the SWA
  production URL (and each preview URL used for testing). Mitigation: one CORS origin from configuration, a CORS
  integration test, preview environments without real login unless registered.
- **R-33 Cold start.** The Api scales to zero; the first request after idle is slow. The SPA (static, always on) must
  show a "waking up" state instead of an error. Design in Phase 5.

### Open questions
1. Per-module local queues and event naming (`SubmissionAccepted` vs `PortfolioSubmitted`): Brief #12.
2. What Compliance receives (class totals in the event, or a Contracts query): Brief #12.

### Next step
Brief #12 (closes Phase 2): integration event from Portfolio to Compliance for the current accepted submission,
idempotent consumer stub in Compliance, local-queue and naming decisions.
