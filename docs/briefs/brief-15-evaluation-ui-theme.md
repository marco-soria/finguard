# Brief #15 — Evaluation result in the web app and the theme toggle (closes Phase 3)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-15-evaluation-ui-theme.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-15-evaluation-ui-theme.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/evaluation-result-ui` |
| PR title (exact, becomes the squash commit) | `feat: show the compliance evaluation in the web app and add a theme toggle` |
| Phase | Phase 3, last brief. Exit: result visible in the UI (this brief); boundary tables green (#13) |

---

## Goal
The submission detail page shows the compliance result of an accepted submission: overall status, one row per asset
class (book value, share, limit, utilization, status), alerts and the policy version. While the evaluation is not
ready it shows "Evaluating…". Percentages never contradict the status (R-39). The header gets a three-state theme
toggle (system / light / dark, D-106). README gets screenshots and Phase 3 is marked done.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/testing.md`,
  `.claude/rules/backend-vsa.md`, `docs/decisions.md` (newest delta: session 23 executed), ADR-0012.
- API (Brief #14): `GET /api/compliance/submissions/{submissionId}/evaluation` returns `EvaluationResponse(evaluationId,
  submissionId, cutOffDate, overallStatus, policyVersion, evaluatedAt, totalBookValue, classes[assetClass, bookValue,
  share, limit, utilization, status], alerts[assetClass, status, utilization])`, enum names as strings (D-202), exact
  decimals (D-197); 404 `Compliance.Evaluation.NotFound` when unknown, other tenant or **not evaluated yet**. It does
  not carry the alert threshold.
- Frontend today: Angular Material (D-98), `httpResource` reads (D-102), `ApiError` with `title`/`detail`/`code` and
  `errorMessage()` (D-142), `color-scheme: light dark` in `material-theme.scss` and `--mat-sys-*` tokens only in
  component CSS (D-106), `FileSizePipe` in `shared/`. Frontend rule: "Format money in one place (a pipe), always USD.
  Percentages come pre-computed from the API." There is no money pipe yet.
- Evaluation is asynchronous: in local runs it usually lands within a second of the upload, but the page can open
  before it exists.

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Where the result shows | Submission detail page only, in an "Compliance" section, for `Accepted` submissions; rejected ones never request it | A status column in the history (needs a list endpoint or N requests per page; later if wanted) |
| Pending state | On 404 with code `Compliance.Evaluation.NotFound` the section shows "Evaluating…" (`role="status"`, `aria-live="polite"`) and retries after 1 s, 2 s, 4 s, 8 s (about 15 s in total), then shows "The evaluation is not available yet." with a "Check again" button. Any other error shows `errorMessage()`. Polling stops on success, on another error and when the page is destroyed | WebSockets/SSE (no real-time need); polling forever |
| API addition | Add `alertThreshold` (from the stored policy snapshot) to `EvaluationResponse`, additive | Hard-coding 0.90 in the SPA (the policy is versioned) |
| Percent display (R-39) | One pure function `formatRatio(value, status, alertThreshold)` → percent text. Start with 2 decimals; if the text would contradict the API status (Compliant shown ≥ threshold; Warning shown < threshold or > 100.00 %; Breached shown ≤ 100.00 %), add decimals until it does not (max 8). It never changes the status; it only checks the displayed text against the status the API gave. Shares and limits use plain 2 decimals | Rounding only (0.89999996 shows 90.00 % next to "Compliant"); truncation only (1.00000004 shows 100.00 % next to "Breached") |
| Money | `UsdPipe` in `shared/`: `en-US`, USD, 2 decimals, from the JSON number. R-42 notes the precision limit | Formatting inline in templates |
| Status presentation | Text label always, plus a status "pill" coloured with app-level semantic tokens defined once in `styles.css` using `light-dark()` (`--fg-status-compliant-*`, `--fg-status-warning-*`, `--fg-status-breached-*`); breached may reuse `--mat-sys-error`/`--mat-sys-on-error`. Each pair meets WCAG AA contrast (4.5:1) in both modes. Component CSS uses only `--mat-sys-*` and these `--fg-*` tokens | Colour only; literal colours in component CSS (D-106) |
| Theme toggle | `ThemeService` (`core/theme/`): `system` / `light` / `dark`, stored in `localStorage` key `finguard.theme` (reads and writes wrapped in try/catch; default `system`), applied by setting `document.documentElement.style.colorScheme` to `light dark`, `light` or `dark`. Applied at startup with `provideAppInitializer`. UI: `mat-button-toggle-group` in the header with `aria-label="Theme"` and text labels | An inline script in `index.html` to avoid the first-paint flash (conflicts with a strict CSP in Phase 5; the flash is a few ms) |
| E2E | New `evaluation.e2e.ts`: upload `samples/portfolios/valid-balanced.csv` and `valid-shares-heavy.csv` (read from the repo) with a random cut-off date ≥ 2000-01-01 (avoids duplicates across runs), open the detail page, expect "Compliant" / "Breached" with a Shares alert and policy version 1. New `theme.e2e.ts`: choose Dark → `color-scheme` is `dark`, survives a reload; choose System with `page.emulateMedia({ colorScheme: 'dark' })` → dark | |
| README | Phase 3 ✓; two screenshots (detail page of a breached submission, light and dark) in `docs/images/`, made with Playwright from sample data only | |

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot`, Aspire only through `-StartAspire`, `-Stop`
  before the final message, Close-out block with "Still running: none".
- pnpm only, from inside `src/frontend/finguard-web`; no new npm dependency (Material's button toggle is already in
  `@angular/material`). Git Bash uses `aspire.cmd` (D-135).
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- `docs/decisions.md` is append-only (D-137). Ask before deviating; record approved deviations.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/evaluation-result-ui
scripts/dev-session.ps1 -Snapshot
dotnet build -c Release; dotnet test --no-build -c Release
Push-Location src/frontend/finguard-web; pnpm install --frozen-lockfile; pnpm lint; pnpm test --watch=false; pnpm build; Pop-Location
```

## Tasks

### 1. Backend: alert threshold in the response
- `EvaluationResponse` gains `AlertThreshold` (decimal, from the evaluation's policy snapshot). Dapper query updated;
  `tenant_id` filter unchanged. Extend `GetEvaluation_Own_Returns200WithClassesAndAlerts` to assert it.

### 2. Frontend: API and formatting
- `core/api/compliance-api.ts`: interfaces for the response; `evaluation(submissionId: Signal<string>)` with
  `httpResource`.
- `shared/usd.pipe.ts` (`UsdPipe`), `shared/format-ratio.ts` (`formatRatio`, pure) with the rule above.
- Polling helper in the detail component (or a small `core/` utility if reused): exponential delays, cancel on destroy
  (`DestroyRef`), no `setInterval` left running.

### 3. Frontend: evaluation section
- In `submission-detail`, for `Accepted` only: heading "Compliance", overall status pill + text, "Policy version {n}",
  evaluated at (`DatePipe` medium, local time; `cutOffDate` stays a plain string, D-103), total book value.
- `mat-table` with caption: Asset class (display names: Cash, Fixed income, Shares, Real estate), Book value, Share,
  Limit, Utilization, Status. Alerts list ("Shares is breached at 104.20 % of its limit."); "No alerts." when empty.
- Pending and timeout states as decided; `data-status` attributes for tests.

### 4. Frontend: theme toggle
- `core/theme/theme.service.ts` and the header toggle as decided; `provideAppInitializer` applies the stored choice.
- Semantic status tokens in `styles.css` with `light-dark()`; contrast checked (write the ratios in the delta).

### 5. E2E and screenshots
- `evaluation.e2e.ts` and `theme.e2e.ts` as decided; existing flows unchanged.
- Screenshots: a small Playwright script or a test tagged `@screenshots` (excluded from the default run) that saves
  `docs/images/evaluation-light.png` and `docs/images/evaluation-dark.png` (viewport 1280×800, sample data, Alice).

### 6. Docs
- `README.md`: Phase 3 done; the two screenshots; one line on what the detail page shows.
- `.claude/rules/frontend.md` + `.cursor/rules/frontend.mdc` (same commit): status shown as text plus a pill with
  `--fg-status-*` tokens; percentages through `formatRatio`, money through `UsdPipe`; the theme service owns
  `color-scheme`. Replace "Percentages come pre-computed from the API" with "Ratios come from the API unrounded; the
  SPA formats them with `formatRatio` and never derives a status".
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta on top.

## Tests that prove it
Unit (Vitest):
- `formatRatio`: `0.89999996` Compliant (threshold 0.9) → `89.999996 %` (no `90.00 %`); `0.9` Warning → `90.00 %`;
  `1` Warning → `100.00 %`; `1.00000004` Breached → `100.000004 %`; `1.0001` Breached → `100.01 %`; `0.5` Compliant →
  `50.00 %`; a value that needs more than 8 decimals returns the 8-decimal text (and the test documents it).
- `UsdPipe`: 0, 0.1 + 0.2 shown as `$0.30`, 1234567.89, a large value.
- `ThemeService`: default `system`; set/get; storage throwing does not break; applies `color-scheme`.
- Detail component: accepted → requests the evaluation; rejected → does not; 404 NotFound → "Evaluating…" then success
  after a retry (fake timers); timeout → "not available yet" and "Check again" restarts; other error → message;
  table rows in class order; alerts text; no polling after destroy.
- Header: toggle changes the theme and has an accessible name.
Integration (backend): `alertThreshold` in the response (task 1).
E2E (Playwright, local, twice): `evaluation.e2e.ts`, `theme.e2e.ts`, plus the existing four.

## Acceptance criteria
- `dotnet build -c Release`, `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm test --watch=false`, `pnpm build`,
  `pnpm exec prettier --check src e2e` green.
- With `-StartAspire`: the detail pages of the three samples show Compliant / Breached (Shares alert) / no compliance
  section (rejected); the theme toggle works and persists; no request to a third-party host; `pnpm e2e` green twice;
  Aspire stopped; Close-out with "Still running: none".
- README shows both screenshots on GitHub.

## Rules to respect
- No business rule in the SPA: statuses, alerts and the threshold come from the API; `formatRatio` only formats.
- D-103 dates; D-142 error display; D-106 tokens (extended by the `--fg-status-*` tokens of this brief).
- Accessibility: status never by colour alone; table caption; live region for "Evaluating…"; keyboard-usable toggle.
- No amounts, statuses or dates in `console` output.

## Out of scope
A compliance column in the history or a dashboard; rebalance proposal and narrative (Phase 4); notifications;
evaluation list endpoints; Aspire 17 (next chore brief).

## Commit plan
1. `feat: add the alert threshold to the evaluation response` (task 1)
2. `feat: show the compliance evaluation on the submission detail page` (tasks 2-3 with Vitest)
3. `feat: add a system, light and dark theme toggle` (task 4 with Vitest)
4. `test: add evaluation and theme end-to-end flows and README screenshots` (task 5)
5. `docs: close Phase 3 in the README and update frontend rules` (task 6)

## Pull request
- **Title (exact):** `feat: show the compliance evaluation in the web app and add a theme toggle`
- **Body:** `.git/finguard-pr-body.md` from the template with real numbers and the two screenshots referenced; Summary
  says Phase 3 is complete.
- Close-out block with:
  `git push -u origin feat/evaluation-result-ui` and
  `gh pr create --base main --head feat/evaluation-result-ui --title "feat: show the compliance evaluation in the web app and add a theme toggle" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first; wait for approval. At the end: test counts (backend by project, Vitest, E2E), contrast ratios of the
status tokens, the two E2E runs, the screenshots, the execution delta and the Close-out block.

---

## Appendix A — Decisions delta (session 24: Brief #15 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-204 | The compliance result shows on the submission detail page only, for accepted submissions | History column deferred |
| D-205 | Pending evaluation: 404 `Compliance.Evaluation.NotFound` → "Evaluating…" with retries at 1, 2, 4, 8 s, then "not available yet" + "Check again" | |
| D-206 | `EvaluationResponse` gains `alertThreshold` from the policy snapshot (additive) | |
| D-207 | `formatRatio` widens the decimals until the shown percentage agrees with the API status (max 8); it never derives a status | Resolves R-39 |
| D-208 | `UsdPipe` is the only money formatter (en-US, USD, 2 decimals) | |
| D-209 | Status = text + pill with `--fg-status-*` semantic tokens (`light-dark()`, WCAG AA) defined once in `styles.css` | Extends D-106 |
| D-210 | `ThemeService` with system / light / dark in `localStorage` (`finguard.theme`), applied through `color-scheme` at startup; no inline script | Closes the Phase 3 part of D-106 |
| D-211 | README carries Playwright screenshots made from sample data only | |
| D-212 | Phase 3 closes with Brief #15; next is the Aspire 17 chore brief (D-185), then Phase 4 | |

### Risks and flags
- **R-39** expected to close with D-207.
- **R-42 JSON numbers for money.** Amounts reach the SPA as JSON numbers (IEEE double). Cent-exact display holds up to
  about 9×10^13 USD, far above realistic portfolios, but the CSV allows up to 10^16. Revisit in Phase 5 (string
  decimals) if real data could approach it.

### Next step
Chore brief: Aspire 13.6 → 17 (AppHost SDK change, `AddProject` replacement, parameters, secrets path,
`dev-session.ps1`, `docs/dev-environment.md`), then Phase 4 planning (rebalance engine, Advisory, R-40 backfill).
