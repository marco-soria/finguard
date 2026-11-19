# Brief #9 — Submission history, upload screen and first end-to-end test (closes Phase 1)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-09-submission-history-ui-e2e.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-09-submission-history-ui-e2e.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/submission-history-and-upload-ui` |
| PR title (exact, becomes the squash commit) | `feat: add submission history, upload screen and first end-to-end test` |
| Phase | Phase 1, last brief. Exit criteria: end-to-end flow locally; tenant isolation test green |

---

## Goal
Close the walking skeleton. Add two Dapper read slices (`GET /api/portfolio/submissions` and
`GET /api/portfolio/submissions/{id}`), adopt Angular Material, build the upload screen and the submission history in
Angular, and add the first browser end-to-end test with Playwright: Alice uploads a CSV and sees it in her history; Bob
does not see it.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/frontend.md`,
  `.claude/rules/testing.md`, `docs/decisions.md` (newest delta first: session 16 executed), ADR-0004, ADR-0007,
  ADR-0009.
- Reference slices: `GET /api/tenancy/me` (read slice layout, D-81) and `POST /api/portfolio/submissions` (Brief #8).
  Wolverine discovers only public handler and message types; the endpoint class stays `internal`. `ITenantContext` is
  resolved from the message scope (D-95). Every message type overrides `ToString()` with identifiers only (D-77).
- Current tables: `portfolio.submissions` (`id`, `tenant_id`, `cut_off_date` date, `file_name`, `size_bytes`, `sha256`,
  `status` varchar via `HasConversion<string>()`, `received_at` timestamptz; index on
  `(tenant_id, cut_off_date, received_at)`) and `portfolio.submission_contents` (bytes). Read queries never touch
  `submission_contents`.
- Frontend today: login page, header with tenant name and sign-out, placeholder `features/home`, `TenancyApi`,
  `AuthService` + `DevAuthProvider`, `authInterceptor` then `problemDetailsInterceptor` (`ApiError`, only `title` is
  shown). No UI library. ESLint is not type-aware.
- Facts verified on 2026-11-19 (re-check at execution; record the resolved versions in the delta):
  - `@angular/material` and `@angular/cdk`: latest `22.3.0` (published 2026-11-18); peer `@angular/core ^22.0.0 || ^23.0.0`;
    no `@angular/animations` peer. pnpm 11's `minimumReleaseAge` may resolve an older 22.x; accept what pnpm resolves
    inside 22.x and record it.
  - Material `ng-add` (22.3.0 schema) has one option, `theme` (`azure-blue` default, also `rose-red`, `magenta-violet`,
    `cyan-orange`). In a CSS project it creates `src/material-theme.scss` (Sass `mat.theme`) and adds it to the build
    styles, and adds Google Fonts links (Roboto, Material Symbols) to `index.html`. Source: package tarball
    `schematics/ng-add/theming/theming.js`, `fonts/material-fonts.js`; https://material.angular.dev/guide/getting-started
  - Material buttons: `matButton` attribute API (`button[matButton]`), plus the legacy `mat-flat-button` forms. Use the
    `matButton` API.
  - `httpResource` in `@angular/common/http` is `@publicApi 22.0` (stable) in Angular 22.2.6.
  - `@playwright/test` latest `1.65.1` (2026-11-13), engines `node >=20`, no install script; browsers are installed
    explicitly. Source: npm registry; https://playwright.dev/docs/intro
  - Dapper 2.1.86+ maps `DateOnly` natively again, and Npgsql 10 reads `date` as `DateOnly`; the repo pins Dapper
    2.1.89, so no type handler is needed. Source: https://www.npgsql.org/doc/types/datetime.html

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| UI library | Angular Material 22 + CDK, theme `azure-blue` through the generated `material-theme.scss`; system font stack; no Google Fonts and no icon font | PrimeNG (own release train and theming layer); CDK only (more custom code for accessible forms and tables); Tailwind (styling only, no components) |
| E2E tool | Playwright Test (TypeScript, pnpm) in `src/frontend/finguard-web/e2e`, Chromium only, run against a running `aspire run` through `E2E_BASE_URL`; not in CI until Phase 5 | Cypress (weaker multi-user flows); Playwright .NET + `Aspire.Hosting.Testing` (browsers and the whole AppHost inside `dotnet test` now; reverses D-52 early) |
| List query | Newest 100 submissions of the tenant, `received_at DESC, id DESC`; response envelope `{ items: [...] }`; no paging and no filters yet | Paging now (YAGNI; the envelope lets it arrive without a breaking change) |
| Details query | Unknown id or another tenant's id: 404 `Portfolio.Submission.NotFound` | 403 (reveals that the id exists) |
| Response fields | `submissionId`, `cutOffDate` (`yyyy-MM-dd`), `fileName`, `sizeBytes`, `status`, `receivedAt`; never the bytes or the SHA-256 | Exposing the hash (no UI use) |
| Reads in Angular | `httpResource` for list and details; `HttpClient` for the upload (`FormData`) | Hand-written signal stores |
| Dates in the UI | `cutOffDate` is a calendar date: shown exactly as returned, never through `Date` or `DatePipe`; `receivedAt` shown in local time with `DatePipe` | Parsing `cutOffDate` as a `Date` (shifts one day west of UTC, e.g. Lima) |
| Client-side file checks | `.csv`, non-empty, ≤ 5 MB checked in the form for fast feedback only; the API stays the authority | Duplicating server rules beyond input constraints |
| Dark mode | Follow the operating system: `color-scheme: light dark` on `html` (the `mat.theme` tokens are `light-dark()` values). Component CSS uses `--mat-sys-*` tokens only, no literal colors. No toggle yet (Phase 3, with the status colors) | Light only (a later switch means hunting literal colors); a toggle now (UI and state before there is content worth checking in both modes) |

## Environment rules
- Never run `npm`, `yarn` or `npx`; use `pnpm`, `pnpm exec`, `pnpm dlx`. Never push, never open PRs, never change GitHub
  settings. Stop after local commits.
- Never invent APIs or versions. Stable versions only. Check the installed `.d.ts` files when an Angular, Material or
  Playwright API name is in doubt, and record differences in the delta.
- Never log file contents, file names or hashes (backend). No `console.log` of API data in the frontend.
- Ask before deviating from this brief; record every approved deviation in the delta.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/submission-history-and-upload-ui
dotnet --version; node --version; pnpm --version   # Node 24.x; pnpm 11.28.9 inside src/frontend/finguard-web
docker info --format '{{.ServerVersion}}'
dotnet build -c Release; dotnet test --no-build -c Release
Push-Location src/frontend/finguard-web; pnpm install --frozen-lockfile; pnpm lint; pnpm test --watch=false; pnpm build; Pop-Location
pnpm view @angular/material dist-tags; pnpm view @playwright/test dist-tags
```
Everything must be green before the first change. Report the resolved versions.

## Tasks

### 1. Backend: `ListSubmissions` slice (`Features/ListSubmissions/`)
- `ListSubmissions` query record (no properties; `ToString()` override), `SubmissionListResponse(IReadOnlyList<SubmissionListItem> Items)`,
  `SubmissionListItem(Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTimeOffset ReceivedAt)`.
- Handler: static, Dapper over `NpgsqlDataSource`, tenant from `ITenantContext.RequiredTenantId`:
  `SELECT ... FROM portfolio.submissions WHERE tenant_id = @TenantId ORDER BY received_at DESC, id DESC LIMIT @Max`
  with `Max = 100` as a named constant. Column aliases match the record (same style as `GetCurrentTenantHandler`).
  Returns `Result<SubmissionListResponse>`.
- Endpoint: `group.MapGet("/submissions", ...)` through `InvokeForCurrentTenantAsync<Result<SubmissionListResponse>>`,
  `TypedResults.Ok`.
- Check the JSON shape: `cutOffDate` must serialize as `"yyyy-MM-dd"` (System.Text.Json does this for `DateOnly`).

### 2. Backend: `GetSubmission` slice (`Features/GetSubmission/`)
- `GetSubmission(Guid SubmissionId)` query record (`ToString()` shows the id only), `SubmissionDetailsResponse` with the
  same six fields (own type: slices do not share response records).
- Handler: Dapper, `WHERE id = @SubmissionId AND tenant_id = @TenantId`; not found →
  `Error.NotFound("Portfolio.Submission.NotFound", "The submission does not exist.")`.
- Endpoint: `group.MapGet("/submissions/{id:guid}", ...)`. The `Location` header of the upload now resolves.
- Register both endpoints in `PortfolioModule.MapPortfolioEndpoints`.

### 3. Frontend: Angular Material
- From `src/frontend/finguard-web`: `pnpm exec ng add @angular/material --skip-confirmation --theme azure-blue`.
  Confirm afterwards: only `pnpm-lock.yaml` changed (no `package-lock.json`), versions are 22.x, `pnpm install
  --frozen-lockfile` passes, and pnpm reports no new build scripts to approve.
- Remove the Google Fonts `<link>` elements the schematic added to `index.html` (no third-party requests; CSP review in
  Phase 5). In `material-theme.scss`, set the theme typography to a system font stack
  (`system-ui, -apple-system, "Segoe UI", Roboto, sans-serif`) and add `color-scheme: light dark;` to the `html` rule so
  the app follows the operating system theme. Existing component CSS (header, login) moves to `--mat-sys-*` tokens
  (surface, on-surface, error, outline); no literal colors remain. Keep `styles.css` for global CSS.
- Convert the existing header sign-out and login buttons to `matButton`. Keep the semantic structure; the header may use
  `mat-toolbar` if it stays a `<header>` landmark.

### 4. Frontend: API service
- `core/api/portfolio-api.ts` (`PortfolioApi`, `providedIn: 'root'`), with interfaces matching the API JSON
  (`SubmissionListItem`, `SubmissionList`, `SubmissionDetails`, `SubmissionReceived`):
  - `submissions()`: `httpResource<SubmissionList>(() => '/api/portfolio/submissions')` created in an injection context
    (or expose a factory the page calls; pick the cleaner one and say which).
  - `submission(id: Signal<string>)`: `httpResource` for `/api/portfolio/submissions/{id}`.
  - `upload(file: File, cutOffDate: string): Observable<SubmissionReceived>`: `POST` with `FormData` fields `file` and
    `cutOffDate`; never set `Content-Type` by hand.
- Confirm that `httpResource` requests go through `withInterceptors` (bearer header, `ApiError` mapping); the resource's
  `error()` must be the `ApiError`. If it is not, map it in one place and say so.

### 5. Frontend: screens
- Replace `features/home` with `features/submissions`:
  - `submissions-page` (route `''`, guarded): upload form above, history below.
  - `upload-form` component: typed reactive form with `cutOffDate` (`<input matInput type="date">`, required; the value
    is the `yyyy-MM-dd` string) and a labelled native file input (`accept=".csv"`, label "Portfolio file"). Client checks
    (UX only): file required, `.csv` extension, size > 0 and ≤ 5 MB. Submit button disabled while sending (no double
    submit). On success: status message `Uploaded {fileName}.` (`role="status"`), reset the form, emit `uploaded`. On
    failure: show `ApiError.title` (`role="alert"`).
  - `submission-history` component: `mat-table` with columns Cut-off date, File, Size, Status, Received. File links to
    `/submissions/:id`. States: loading (`mat-progress-bar` or text), empty (`No submissions yet.`), error
    (`ApiError.title`, `role="alert"`). Reload after each upload.
  - `submission-detail` page (route `submissions/:id`, guarded): definition list with the six fields; 404 shows the
    `ApiError.title`; a "Back to submissions" link.
- `shared/file-size.pipe.ts`: bytes → `B` / `KB` / `MB` (one decimal), pure.
- `cutOffDate` is rendered as returned (no `Date`, no `DatePipe`); `receivedAt` with `DatePipe` (`medium`).
- Lazy routes; `OnPush`; signals for local state; built-in control flow; UI text in English.

### 6. End-to-end test (Playwright)
- `pnpm add -D @playwright/test` (exact version recorded); `pnpm exec playwright install chromium`.
- `playwright.config.ts` at the app root: `testDir: './e2e'`, `testMatch: '**/*.e2e.ts'`, Chromium project only,
  `baseURL` from `process.env.E2E_BASE_URL`; throw a clear error when it is missing ("Start the stack with aspire run and
  set E2E_BASE_URL to the web endpoint"). `trace: 'on-first-retry'`, `retries: 0` locally. No `webServer` block.
- Script `"e2e": "playwright test"` in `package.json`. `pnpm test` (Vitest) must not pick up `e2e/` files, and `pnpm lint`
  must lint them. Add Playwright output folders (`test-results/`, `playwright-report/`) to `.gitignore`.
- `e2e/submissions.e2e.ts`, one test, role-based locators only (`getByRole`, `getByLabel`), no fixed waits:
  1. Sign in as Alice through the development login page.
  2. Upload an in-memory CSV via `setInputFiles({ name, mimeType: 'text/csv', buffer })` with a unique name
     (`e2e-<timestamp>.csv`) and a cut-off date of today.
  3. Expect the status message and a history row with that file name; open it and see the details page.
  4. Sign out, sign in as Bob, expect the history not to contain that file name.
- Tests run against the shared development database, so they assert only on their unique file name.

### 7. Rules and docs
- `.claude/rules/frontend.md` and `.cursor/rules/frontend.mdc` (same commit, D-27): Angular Material is the UI library
  (use its components before custom ones; `matButton` API; no third-party font or icon CDNs); component CSS uses
  `--mat-sys-*` tokens only, never literal colors (light and dark both work); reads with `httpResource`;
  calendar dates (`cutOffDate`) never go through `Date`/`DatePipe`; client-side file checks are UX only.
- `.claude/rules/testing.md` and `.cursor/rules/testing.mdc`: E2E with Playwright in `src/frontend/finguard-web/e2e`,
  `*.e2e.ts`, role-based locators, no fixed waits, unique data per run, run locally against `aspire run`; few flows.
- `.claude/rules/backend-vsa.md` and `.cursor/rules/backend-vsa.mdc`: read queries never select
  `submission_contents`; list queries have a named cap and a deterministic order.
- `AGENTS.md` commands section: how to run E2E (`aspire run`, then `$env:E2E_BASE_URL = '<web url>'; pnpm e2e`).
- `docs/decisions.md`: add Appendix A as the session 17 planning delta, then the execution delta on top.

## Tests that prove it
Integration (`FinGuard.Api.IntegrationTests`, real PostgreSQL, through HTTP):
- `ListSubmissions_NoSubmissions_ReturnsEmptyItems`
- `ListSubmissions_TwoUploads_ReturnsNewestFirst`
- `ListSubmissions_AliceAndBob_EachSeesOnlyOwn` (tenant isolation)
- `ListSubmissions_MoreThanCap_ReturnsNewest100` (seed rows through `PortfolioDbContext` with the tenant set; no
  bypass of the filter)
- `ListSubmissions_Always_ReturnsOnlyPublicFields` (exact JSON property set; no content, no hash)
- `ListSubmissions_NoToken_Returns401`
- `GetSubmission_OwnSubmission_Returns200WithFields`
- `GetSubmission_OtherTenantsSubmission_Returns404WithCode` (tenant isolation)
- `GetSubmission_UnknownId_Returns404WithCode`
- `GetSubmission_NotAGuid_Returns404`
- `Upload_LocationHeader_ResolvesToSubmission`
- `ReadSlices_Always_DoNotLogFileNames` (fake log collector, same style as Brief #8)

Architecture: the existing suite must stay green (new query records override `ToString()`; endpoints internal).

Frontend unit (Vitest, `HttpTestingController`, no snapshots):
- `PortfolioApi`: list URL; details URL; upload sends `FormData` with `file` and `cutOffDate` and no manual
  `Content-Type`.
- `UploadForm`: invalid without file or date; rejects non-CSV, empty and > 5 MB files before any request; disables
  submit while sending; shows `ApiError.title` on failure; emits `uploaded` and resets on success.
- `SubmissionHistory`: renders rows; empty state; error state; `cutOffDate` text equals the API string exactly (run
  with a time zone west of UTC if the runner allows, otherwise assert no `Date` conversion happens).
- `SubmissionDetail`: renders fields; shows the error title on 404.
- `FileSizePipe`: 0 B, 1023 B, 1.0 KB, 5.0 MB boundaries.
- Routes: `''` and `submissions/:id` are guarded.

End-to-end (Playwright, local): `submissions.e2e.ts` as described in task 6.

## Acceptance criteria
- `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- In `src/frontend/finguard-web`: `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm test --watch=false`,
  `pnpm build`, `pnpm exec prettier --check src e2e` green; CI needs no change and installs no browsers.
- With `aspire run`: `pnpm e2e` passes twice in a row against the same database.
- Manual: Alice uploads a file and sees it at the top of the history; the detail page opens from the row and from the
  upload `Location` URL; Bob sees only his own submissions; an invalid upload shows the API's title; no request goes to a
  third-party host (browser network list); with the OS in dark mode the app renders dark with readable contrast
  (screenshot of the history page in both modes).
- Phase 1 exit criteria met: end-to-end flow locally, tenant isolation green at integration and E2E level.

## Rules to respect
- ADR-0007: Dapper reads, own schema only, mandatory `tenant_id` filter, every query covered by an isolation test.
- D-77: every new message type overrides `ToString()` with identifiers only.
- The tenant never comes from the route, query string or body.
- No business rules in the frontend; status text comes from the API as-is.
- Accessibility: labelled inputs, keyboard-usable controls, `role="alert"` / `role="status"` for messages.
- pnpm only; commit `pnpm-lock.yaml`; no new `allowBuilds` approvals without asking.

## Out of scope
CSV parsing and validation, rejected submissions, idempotency (Phase 2, Brief #10+); paging, filters, sorting in the
table; download of the original file; E2E in CI and against Azure (Phase 5); MSAL; a light/dark toggle (Phase 3);
Tailwind; Dependabot changes.

## Commit plan
1. `feat: add submission list and details queries` (tasks 1-2 with their integration tests)
2. `build: add Angular Material to the web app` (task 3)
3. `feat: add submission upload and history screens` (tasks 4-5 with their Vitest specs)
4. `test: add Playwright end-to-end test for upload and tenant isolation` (task 6)
5. `docs: update rules and decisions log` (task 7)

Every commit builds and its tests pass. End every commit message with the attribution lines your harness requires.

## Report back
- Plan first; wait for approval.
- At the end: resolved versions (Material, CDK, Playwright, Chromium build), deviations, findings, test counts by
  project, the E2E run output (two runs), screenshots of the history and detail pages, and the execution delta written
  into `docs/decisions.md`.

---

## Appendix A — Decisions delta (session 17: Brief #9 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-98 | UI library: Angular Material 22 + CDK, theme `azure-blue` via the `ng add` Sass theme file; system font stack; no third-party font or icon CDNs | Closes the open question of sessions 7 and 15. Rejected: PrimeNG, CDK only, Tailwind (accessible table, form fields and errors would be hand-built; the project's showcase is architecture and domain, not visual design); Material + Tailwind (two styling systems) |
| D-99 | E2E: Playwright Test (TypeScript) in `src/frontend/finguard-web/e2e`, Chromium, run locally against `aspire run` via `E2E_BASE_URL`; CI E2E decided in Phase 5 | Closes the E2E tool question. Rejected: Cypress, Playwright .NET + `Aspire.Hosting.Testing` |
| D-100 | `GET /api/portfolio/submissions`: newest 100 for the tenant, `received_at DESC, id DESC`, envelope `{ items }`, no paging yet | Read queries never select `submission_contents` |
| D-101 | `GET /api/portfolio/submissions/{id}`: unknown or other tenant's id → 404 `Portfolio.Submission.NotFound` | Existence is not revealed |
| D-102 | Angular reads use `httpResource` (stable since 22.0); writes use `HttpClient` | |
| D-103 | Calendar dates (`cutOffDate`) travel and render as `yyyy-MM-dd` strings, never through `Date`/`DatePipe`; timestamps (`receivedAt`) render in local time | Prevents the one-day shift west of UTC |
| D-104 | Client-side file checks (`.csv`, > 0, ≤ 5 MB) are UX only; the API is authoritative | |
| D-106 | Dark mode follows the operating system (`color-scheme: light dark`); component CSS uses `--mat-sys-*` tokens only; a three-state toggle (system/light/dark) arrives in Phase 3 with the status colors | Tailwind evaluated and rejected for this project (see D-98) |
| D-105 | Phase 1 ends with Brief #9; Brief #10 opens Phase 2 with the dependency-majors review (R-13) in its preflight | |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| Angular Material | 22.3.0 latest; `ng add` option `theme`; CSS projects get a generated `material-theme.scss` and Google Fonts links | npm registry; https://material.angular.dev/guide/getting-started |
| Angular | `httpResource` is `@publicApi 22.0` | `@angular/common` 22.2.6 `types/http.d.ts` |
| Playwright | `@playwright/test` 1.65.1, Node ≥ 20, browsers installed explicitly | npm registry; https://playwright.dev/docs/intro |
| Dapper / Npgsql | Dapper 2.1.86 re-enabled `DateOnly`; Npgsql 10 reads `date` as `DateOnly` | https://www.npgsql.org/doc/types/datetime.html |

### Risks and flags
- **R-25 E2E only local.** The browser flow is not in CI until Phase 5; regressions show up only when someone runs
  `pnpm e2e`. Mitigation: run it before every PR that touches the frontend or the Portfolio slices; Phase 5 adds a CI job.
- **R-26 Material ties to Angular majors.** Angular 23 must wait for Material 23 (Material already accepts core ^23 in
  peers). Covered by the R-13 majors review.

### Open questions
1. Per-module local queues: decide with `SubmissionAccepted` (Phase 2).
2. E2E in CI (Aspire in the runner, or against the deployed environment) and Entra test users for E2E: Phase 5.

### Next step
Brief #10 (Phase 2 start): dependency-majors review, CSV parsing and validation (D-06 columns, rows, encoding,
USD only, no negatives, total > 0), rejected submissions stored with their errors (D-10), and SHA-256 idempotency (D-11).
