# Brief #21 — Rate limiting, forwarded headers, row versioning and the theme toggle

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-21-protection-and-theme-toggle.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-21-protection-and-theme-toggle.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/rate-limiting-and-theme-toggle` |
| PR title (exact, becomes the squash commit) | `feat: add rate limiting, forwarded headers, advisory row versioning and an icon theme toggle` |
| Phase | Pre-cloud hardening, second of two (#20 API surface done, **#21 protection**). Phase 5 (Azure) starts at #22 |

---

## Goal
Before the API gets a public URL: requests are rate limited per tenant and per IP with a 429 the SPA explains; the
client IP is read correctly behind a reverse proxy; the one updated row (`advisory.advisories`) has optimistic
concurrency through PostgreSQL's `xmin`; the three-button theme selector becomes one icon button (sun/moon) that
follows the operating system until the user chooses.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/backend-vsa.md`,
  `.claude/rules/frontend.md`, `.claude/rules/testing.md`, `.claude/rules/advisory.md`, `docs/decisions.md` (newest
  delta: session 32, Brief #20 executed), ADR-0003, ADR-0007, ADR-0015.
- Api pipeline today (`Program.cs`): `UseExceptionHandler`, `UseStatusCodePages`, (Development: migrations, seed),
  `UseAuthentication`, `UseTenancy`, `UseAuthorization`, then `MapDefaultEndpoints`, `MapFinGuardApiDocs`,
  `MapDevTokenEndpoints` (Development), module endpoints. A fallback policy answers 401 before routing (D-310).
- No rate limiter, no forwarded-headers handling, no concurrency token anywhere.
- `StoreRefinedNarrativeHandler` updates an advisory only while `refinement_status = Pending` (D-281/D-293); the
  refinement queue has parallelism 1, the store message does not.
- Theme: `ThemeService` owns `color-scheme` on `<html>` (`light dark` / `light` / `dark`), tokens are `light-dark()`
  values (D-210), the choice is stored under `finguard.theme` (`system` / `light` / `dark`), applied at startup
  through `provideAppInitializer`. `app.html` shows a `mat-button-toggle-group` with three text buttons, also on
  `/login`. The app uses no icons today.
- E2E `history.e2e.ts` seeds 27 uploads through the API in its setup (D-312 results), which matters for upload limits.
- Docs endpoints are already `AllowAnonymous` when mapped (D-310).

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Rate limiter | Built-in `Microsoft.AspNetCore.RateLimiting`. `UseRateLimiter()` after `UseTenancy()` and before `UseAuthorization()`, so the tenant is known and unauthenticated floods are counted before the 401 | A gateway (API Management costs money; Cloudflare proxy is off by D-273) |
| Partitions | Global limiter: authenticated → partition by tenant id, fixed window `Reads` (default 120 per minute); anonymous → partition by client IP, fixed window `Anonymous` (default 20 per minute). Named policy `uploads` on `POST /api/v1/portfolio/submissions`: token bucket per tenant (default 10 tokens, refill 10 per minute), chained with the global limiter. `/health` and `/alive` are excluded | One limit for everything (uploads are the expensive path: CSV, evaluation, AI) |
| Configuration | `RateLimiting` options section, validated on start (positive values). Defaults above in `appsettings.json`. `appsettings.Development.json` raises `Uploads` to 120 per minute so the E2E seed of 27 uploads and manual testing never hit it; integration tests set small limits explicitly | Hard-coded limits; disabling the limiter in Development (then it is never exercised) |
| Rejection | 429 ProblemDetails, `type` and `title` consistent with the global handler, code `RateLimit.Exceeded`, `Retry-After` header in whole seconds when the lease has it. Log at Information: partition kind (`tenant` / `ip`) and policy name only, never the tenant id, IP or path parameters | Plain-text 429 (inconsistent with the rest of the API) |
| Multi-replica | Counters live in each replica's memory. Accepted: Phase 5 runs one replica; with more, the effective limit multiplies by the replica count. Recorded as a risk, no distributed store | Redis (cost and a new service for a demo) |
| Forwarded headers | `UseForwardedHeaders` first in the pipeline, enabled by option `ForwardedHeaders:Enabled` (false in Development, true elsewhere). Forward `X-Forwarded-For` and `X-Forwarded-Proto`, `ForwardLimit = 1`. Known proxies/networks cleared **only** because the Api is reachable solely through the platform proxy (Container Apps ingress, Traefik in Phase 6); this assumption is written in the code comment and the ADR. Verify the .NET 10 property names for known networks in the official docs | Trusting forwarded headers in Development (no proxy there) |
| Row versioning | `uint` property mapped with `IsRowVersion()` on the `Advisory` entity (Npgsql maps it to `xmin`; no schema change; confirm `has-pending-model-changes` stays clean or add the empty migration EF requires). Wolverine retries `DbUpdateConcurrencyException` a few times for Advisory handlers; on retry the handler reloads, sees the status is no longer `Pending` and does nothing | Pessimistic locks; a version column of our own |
| SPA 429 | The HTTP error mapping shows a non-blocking message "Too many requests. Try again in N seconds." (N from `Retry-After`, "in a moment" when absent). The upload form keeps the selected file. No automatic retry | Silent retries (hide abuse and confuse users) |
| Theme toggle | One `mat-icon-button` in the toolbar (also on `/login`): no stored choice → follows the OS, live (CSS already does it through `color-scheme: light dark`); the button shows the icon of the theme it switches **to** (moon in light, sun in dark), with `aria-label` and tooltip "Switch to dark theme" / "Switch to light theme". A click stores `light` or `dark`. A stored legacy `system` value counts as no choice. No "follow the system again" control (accepted limitation of the pattern) | Three text buttons (current); a menu with three options (more than needed) |
| Icons | Two inline SVGs (Material Symbols `light_mode` and `dark_mode`, Apache 2.0) registered once with `MatIconRegistry.addSvgIconLiteral` through `DomSanitizer.bypassSecurityTrustHtml` on constant strings only. Attribution line in `THIRD-PARTY-NOTICES.md` (create if missing) | Font Awesome (new dependency and a second visual style); the Material Symbols web font from Google (third-party request, CSP, whole font for two icons) |
| No flash | Keep applying the theme in `provideAppInitializer`; no inline script in `index.html` (a strict CSP in Phase 5 would block it). A brief flash only when a stored choice differs from the OS is accepted | Inline script (needs a CSP hash) |

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot` first, Aspire only through `-StartAspire`
  (default, AI off), `-Stop` before the final message, Close-out block with "Still running: none".
- pnpm only, from inside `src/frontend/finguard-web`; Git Bash uses `aspire.cmd` (D-135). Build `FinGuard.slnx`.
- Pinned-package check in the plan (R-45, R-51), report only. No new NuGet or npm packages are expected; ask before
  adding one.
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Logs: never tenant ids, IPs, user ids or request paths with ids in rate-limit or forwarded-header messages.
- `docs/decisions.md` is append-only (D-137). Planning IDs in Appendix A start after the newest ID in the log
  (D-312); if the log moved on, renumber and say so. Ask before deviating; record approved deviations.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/rate-limiting-and-theme-toggle
scripts/dev-session.ps1 -Snapshot
dotnet build FinGuard.slnx -c Release; dotnet test --no-build -c Release      # baseline 655
```
Frontend baselines: Vitest 161, E2E 13.

## Tasks

### 0. E2E flake first (R-55)
- In the dependency chore run, `e2e/duplicate.e2e.ts:44` failed once (the first submission's status cell was not
  exactly `Accepted`) and passed on re-run. It may be a cold start or a random-date collision in the persistent dev
  database, or a real defect: since #20 the history page accumulates pages, so check that the list is reset (not
  merged with stale rows) when it reloads after an upload.
- Reproduce it (run the spec in a loop, for example 10 times with `--repeat-each`), find the cause, and fix the test or
  the app. If the app is wrong, add a Vitest case for the reload after upload. Report the cause in the delta.

### 1. Rate limiting
- `RateLimitingOptions` + validation; registration with the global partitioned limiter and the `uploads` policy;
  `RequireRateLimiting("uploads")` on the upload endpoint; `DisableRateLimiting()` on health endpoints; `OnRejected`
  writing the ProblemDetails and `Retry-After`; Information log as decided.
- OpenAPI: the upload and list operations declare 429 (`ProducesProblem(429)`); update the snapshot with the script.

### 2. Forwarded headers
- Option, middleware first in the pipeline, documented trust assumption; no effect in Development.

### 3. Advisory row versioning
- Entity property + mapping; Wolverine retry policy for `DbUpdateConcurrencyException` scoped to Advisory handlers
  (or global if the existing error policy makes scoping awkward; say which).

### 4. SPA
- 429 message in the shared HTTP error handling; upload form keeps its state.
- Theme: `ThemeService` keeps `color-scheme` ownership; new API `effective()` (signal: stored choice or OS via
  `matchMedia('(prefers-color-scheme: dark)')` with a change listener) and `toggle()`; remove the three-button group;
  add the icon button; register the two icons at startup.

### 5. Docs
- ADR-0016 "Request protection: rate limiting and forwarded headers" (+ README index): partitions, defaults, the
  per-replica limitation, the forwarded-headers trust assumption and what Phase 5/6 must verify.
- `README.md`: limits table and the 429 behaviour; theme sentence updated. `.claude/rules/backend-vsa.md` +
  `.cursor/rules/backend-vsa.mdc` (same commit): new endpoints that do expensive work declare a rate-limit policy;
  health endpoints never rate limited. `.claude/rules/frontend.md` + `.mdc`: icons are inline SVGs registered once.
- `docs/decisions.md`: Appendix A, then the execution delta.

## Tests that prove it
Unit: options validation; partition key selection (tenant vs IP, health excluded); `Retry-After` formatting;
`ThemeService` (no stored value follows a mocked `matchMedia`, OS change updates `effective()`, toggle stores the
opposite, legacy `system` value = no choice, blocked storage does not throw).
Integration (real PostgreSQL, `ApiFixture` with small limits, e.g. uploads 2 per minute, reads 5, anonymous 3):
- `Upload_OverTenantLimit_Returns429WithRetryAfter` (ProblemDetails code `RateLimit.Exceeded`).
- `Upload_LimitIsPerTenant` (Alice exhausted, Bob still accepted).
- `Reads_OverLimit_Return429`; `Anonymous_OverIpLimit_Return429` (e.g. unauthenticated calls) and the 429 comes
  before the 401 once the limit is hit.
- `Health_IsNeverRateLimited`.
- `ForwardedFor_Enabled_UsesForwardedIpForAnonymousPartition` and `ForwardedFor_Disabled_IgnoresHeader` (two
  callers with different `X-Forwarded-For` values get separate partitions only when enabled).
- `StoreRefinedNarrative_ConcurrentDeliveries_OneApplies_NoDeadLetter` (two messages for the same advisory processed
  concurrently; one wins, the other retries and no-ops; dead letters 0).
- Existing upload, list and E2E-support tests stay green with the test limits (tests that upload many files use a
  fixture with higher limits).
- OpenAPI snapshot updated and green.
Vitest: 429 mapping with and without `Retry-After`; upload form keeps the file after a 429; theme button icon,
label and tooltip in light and dark; button present on `/login`.
E2E: theme toggle switches the scheme and survives a reload; existing tests unchanged (Development upload limit).
Run twice.

## Acceptance criteria
- `dotnet build FinGuard.slnx -c Release` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm lint`, `pnpm test --watch=false`, `pnpm build`, `pnpm exec prettier --check src e2e` green.
- `-StartAspire`: `pnpm e2e` green twice; a manual burst against an endpoint with a temporarily lowered limit (owner
  can skip) shows the SPA message; Aspire stopped; Close-out with "Still running: none".

## Rules to respect
- Security by default: limits on before the API is public; no identifiers or IPs in logs; forwarded headers trusted
  only behind the platform proxy.
- ADR-0007: the advisory write stays in EF; `xmin` is the only concurrency mechanism added.
- Frontend: no new icon library; no inline scripts; Material components.

## Out of scope
Distributed rate limiting; per-plan or per-user tiers; CAPTCHA; Cloudflare proxy or WAF; CSP and security headers
(Phase 5, `staticwebapp.config.json`); "follow system" reset control; other icons.

## Commit plan
1. `feat: rate limit requests per tenant and per IP with a 429 problem response`
2. `feat: read the client IP from forwarded headers behind the platform proxy`
3. `feat: add xmin row versioning to advisories`
4. `feat: explain 429 responses and replace the theme selector with an icon toggle`
5. `docs: add ADR-0016 and update the protection and theme rules`

## Pull request
- **Title (exact):** `feat: add rate limiting, forwarded headers, advisory row versioning and an icon theme toggle`
- **Body:** `.git/finguard-pr-body.md` from the template; Summary lists the default limits, the per-replica
  limitation, the forwarded-headers assumption and ADR-0016.
- Close-out block with:
  `git push -u origin feat/rate-limiting-and-theme-toggle` and
  `gh pr create --base main --head feat/rate-limiting-and-theme-toggle --title "feat: add rate limiting, forwarded headers, advisory row versioning and an icon theme toggle" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first (with the pinned-package check and the verified forwarded-headers property names); wait for approval. At
the end: test counts against the baselines, two E2E runs, screenshots of the toolbar in light and dark, the
execution delta and the Close-out block.

---

## Appendix A — Decisions delta (session 33: Brief #21 planning)

| ID | Decision | Notes |
| --- | --- | --- |
| D-313 | Rate limiter placement and partitions: after tenancy, before authorization; tenant fixed window for reads, IP fixed window for anonymous calls, token-bucket `uploads` policy per tenant chained with the global limiter; health excluded | Details D-306; ADR-0016 |
| D-314 | Limits in a validated `RateLimiting` options section; Development raises the upload limit (E2E seeds 27 uploads); tests set small limits explicitly | |
| D-315 | 429 as ProblemDetails `RateLimit.Exceeded` with `Retry-After`; rejection logs carry partition kind and policy name only | |
| D-316 | Forwarded headers on outside Development, `ForwardLimit = 1`, known proxies cleared only because the Api is reachable solely through the platform proxy | Verify in Phase 5 (ACA) and Phase 6 (Traefik) |
| D-317 | `xmin` row version on advisories; Wolverine retries `DbUpdateConcurrencyException`; the handler no-ops when no longer `Pending` | D-298 |
| D-318 | SPA explains 429 with the retry delay; no automatic retry; the upload form keeps its file | |
| D-319 | Theme: one icon button (sun/moon) that follows the OS until the user chooses, then stores `light` or `dark`; legacy `system` = no choice; no reset control | Replaces the three-button selector of #15 |
| D-320 | Icons: inline SVGs from Material Symbols registered with `MatIconRegistry`; no Font Awesome, no external icon font; attribution in `THIRD-PARTY-NOTICES.md` | |
| D-321 | Dependabot PRs: merge when CI is green; on conflicts comment `@dependabot rebase`; close only with an ignore command and a reason; never delete the branch as a way to skip | Owner process, from the planning chat |

### Risks
- **R-54 Per-replica rate limits.** With N replicas the effective limit is N times the configured one. Accepted
  while Phase 5 runs one replica; revisit if scaling out.

### Next step
Sync `decisions.md` to the Project (end of pre-cloud hardening). Then Phase 5 kickoff: the owner prepares the Azure
budget, the Entra external tenant and the GitHub-to-Azure credential with the runbook delivered before Brief #22.
