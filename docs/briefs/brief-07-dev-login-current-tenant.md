# Brief #7 — Development login in the web app and the first real slice (`GET /api/tenancy/me`)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-07-dev-login-current-tenant.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-07-dev-login-current-tenant.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/dev-login-and-current-tenant` |
| PR title (exact, becomes the squash commit) | `feat: add development login and current tenant endpoint` |
| Phase | Phase 1, second brief |

---

## Goal
Make the first vertical cut that a person can see: open the web app, sign in as Alice or Bob through the development
issuer, and see the shell show "Acme Pension Fund" or "Globex Insurance", read from the first real backend slice
`GET /api/tenancy/me` (Minimal API → Wolverine query handler → Dapper). The slice becomes the template every later
slice copies. Also close R-21: Wolverine must never write message contents to the logs.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/frontend.md`,
  `.claude/rules/testing.md`, `docs/decisions.md` (newest delta first: session 14), ADR-0003, ADR-0004, ADR-0007,
  ADR-0008, ADR-0009, `docs/git-workflow.md`, `docs/ai-rules.md`.
- Backend today: `POST /dev/token` (Development only, `{ "user": "alice" | "bob" }` → `{ accessToken, expiresAt }`,
  1 hour, signing key per process, so tokens die when the Api restarts: R-18); fallback policy requires authentication;
  module groups require `TenantMember`; `/api/tenancy` group exists with no endpoints; `MessageBusTenantExtensions`
  offers `InvokeForCurrentTenantAsync` and `InvokeForCurrentTenantAsync<T>`; Wolverine config in
  `FinGuard.Api/Messaging/WolverineSetup.cs`; codegen runs with `ServiceLocationPolicy.NotAllowed` (D-74).
- Frontend today: Angular 22 shell, lazy `home` route, empty `core/` and `shared/`, `provideHttpClient(withFetch())`,
  dev proxy for `/api`, Vitest, angular-eslint. No UI library (D-45).
- Facts verified on 2026-11-19 (re-check at execution):
  - Angular: functional interceptors (`HttpInterceptorFn`) are registered with `provideHttpClient(withInterceptors([...]))`.
    Source: https://angular.dev/api/common/http/withInterceptors , https://angular.dev/guide/http/interceptors
  - Wolverine: execution start/finish and success logging levels are configurable
    (`opts.Policies.MessageExecutionLogLevel(...)`, per-chain `SuccessLogLevel`), but **failures are always logged at
    Error with the message**. Source: https://wolverine.netlify.app/guide/logging
  - NServiceBus documents the same mechanism: message loggers write the message's `ToString()`. C# records print every
    property by default. Source: https://docs.particular.net/nservicebus/logging/message-contents

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Alternatives rejected |
| --- | --- | --- |
| R-21 message logging | Every Wolverine message type in `src/backend` overrides `ToString()` to return its type name and identifiers only; an architecture test enforces it; success logging turned down to Debug | Logging filter on message categories (also hides real failures); payload-free messages only (too restrictive for commands) |
| Auth in the SPA | `AuthService` (signals) over an abstract `AuthProvider`; this brief ships `DevAuthProvider` (calls `/dev/token`); Phase 5 adds an MSAL provider for Entra External ID. Closes the open question of session 11 | Generic OIDC client now: the dev issuer is not OIDC, so it adds a library with nothing to talk to |
| Token storage (dev) | `sessionStorage`, behind the provider; cleared on sign-out and on any 401 | Memory only (sign in again on every reload); `localStorage` (survives across tabs and restarts for no benefit) |
| UI library | Still none: plain semantic HTML and minimal CSS; decided in Brief #9 with the first table and form | Adopting one for a login page and a header |

## Environment rules
- Never run `npm`, `yarn` or `npx`; use `pnpm`, `pnpm exec`, `pnpm dlx`. Never push, never open PRs. Stop after local commits.
- Never invent APIs or versions; stable only; no new frontend dependency is expected (report and stop if you think one is needed).
- Never log or display tokens. Never put a token in a URL. No `console.log` left in committed code.

## Preflight
```powershell
git switch main; git pull; git status --short
dotnet build -c Release; dotnet test --no-build -c Release   # 90 tests green
cd src/frontend/finguard-web; pnpm install --frozen-lockfile; pnpm lint; $env:CI="true"; pnpm test; Remove-Item Env:CI; pnpm build; cd ../../..
```

## Tasks

### 1. Branch
`git switch -c feat/dev-login-and-current-tenant`

### 2. R-21: message logging (backend, first commit)
- Override `ToString()` on every message type that a module handler receives and on every integration event, returning
  `"<TypeName> { <Id names and values> }"` with identifiers only (GUIDs, dates). Never amounts, references, descriptions
  or any free text. Do the same for the test probe messages.
- `WolverineSetup`: `opts.Policies.MessageSuccessLogLevel(LogLevel.Debug)` and keep execution start/finish at Debug
  (check the exact API names in the 6.64 XML docs; report differences).
- Architecture test `MessageTypes_Always_OverrideToString`: every type used as the first parameter of a public
  `Handle`/`HandleAsync`/`Consume`/`ConsumeAsync` method in a module assembly, and every type in `*.Contracts.Events`,
  declares its own `ToString()` override (`DeclaringType == type`). Passes today only after the overrides above.
- Integration test `FailedMessage_WithFreeText_DoesNotLogPayload`: invoke the probe command with `FailAfterSave = true`
  and a unique `Reference`; assert no captured log record (message or state values) contains that reference.

### 3. Backend slice `GET /api/tenancy/me` (template for every slice)
```
src/backend/Modules/Tenancy/FinGuard.Tenancy/Features/GetCurrentTenant/
  GetCurrentTenant.cs              sealed record query (no properties), ToString override
  CurrentTenantResponse.cs         sealed record (Guid TenantId, string TenantName)
  GetCurrentTenantHandler.cs       Wolverine handler: Dapper over NpgsqlDataSource, const parameterized SQL,
                                   tenant from ITenantContext.RequiredTenantId; returns Result<CurrentTenantResponse>;
                                   Error.NotFound("Tenancy.Tenant.NotFound", ...) if no row
  GetCurrentTenantEndpoint.cs      MapGet("/me") on the /api/tenancy group (TenantMember);
                                   bus.InvokeForCurrentTenantAsync<Result<CurrentTenantResponse>>(...) → ToHttpResult(TypedResults.Ok)
```
- SQL: `SELECT id AS "TenantId", name AS "TenantName" FROM tenancy.tenants WHERE id = @TenantId` (or snake_case mapping
  per D-32, whichever the module already uses). `tenancy.tenants` is global, but the query still filters by the
  context's tenant, so nobody can read another tenant's name.
- The handler takes no DbContext, so `AutoApplyTransactions` does not wrap it; confirm codegen succeeds with
  `NotAllowed` (no new service-location exceptions).
- Add a short "Slice template" section to `.claude/rules/backend-vsa.md` + `.cursor/rules/backend-vsa.mdc` (same commit)
  pointing at this folder as the reference layout.

### 4. Frontend: authentication core (`src/app/core/auth/`)
- `auth-provider.ts`: abstract class `AuthProvider` with `signIn(user: string): Promise<AuthSession>`,
  `restore(): AuthSession | null`, `signOut(): void`. `AuthSession = { accessToken: string; expiresAt: string }`.
- `dev-auth-provider.ts`: `POST /dev/token`; stores the session in `sessionStorage` under one key; `restore()` ignores
  expired sessions. Exposes the development users `[{ id: 'alice', label: 'Alice' }, { id: 'bob', label: 'Bob' }]`.
- `auth.service.ts` (root): signals `session`, computed `isAuthenticated`; `signIn`, `signOut` (clears storage, navigates
  to `/login`), `accessToken()`. Components and interceptors depend on `AuthService` only, never on a provider
  (`frontend.md`).
- `auth.guard.ts`: `CanActivateFn`; unauthenticated → `UrlTree` to `/login`.
- `app.config.ts`: `{ provide: AuthProvider, useClass: DevAuthProvider }` with a comment that Phase 5 swaps in the MSAL
  provider.

### 5. Frontend: HTTP core (`src/app/core/http/`)
- `auth.interceptor.ts`: adds `Authorization: Bearer <token>` only to same-origin `/api/` requests; on 401 from `/api/`
  calls `AuthService.signOut()` (R-18: the dev key dies with the Api process).
- `problem-details.interceptor.ts`: turns error responses into a typed `ApiError { status, code?, title, traceId? }`
  read from RFC 9457 ProblemDetails; non-ProblemDetails errors map to a generic `ApiError` with status only. No raw
  server text is shown to the user beyond `title`.
- Register both with `provideHttpClient(withFetch(), withInterceptors([authInterceptor, problemDetailsInterceptor]))`;
  document the order in a comment.
- `src/app/core/api/tenancy-api.ts`: typed service `getCurrentTenant(): Observable<CurrentTenant>`.

### 6. Frontend: screens
- `features/login/`: page "Sign in (development)" with one button per development user, a short note that this screen
  exists only for local development, an error area for `ApiError.title`. On success navigate to `/`.
- Routes: `/login` public; `''` (home) and everything else behind `authGuard`; `**` → `''`.
- Shell (`app.html`): header with "FinGuard", the tenant name from `getCurrentTenant()` once signed in, and a
  "Sign out" button. While loading show nothing; on error show a neutral "Could not load your organization." Use signals,
  `OnPush`, built-in control flow, semantic HTML, labelled buttons, keyboard accessible.

### 7. Tests
Backend (`FinGuard.Api.IntegrationTests`):
| Test | Proves |
| --- | --- |
| `GetCurrentTenant_AliceToken_ReturnsAcme` / `..._BobToken_ReturnsGlobex` | slice end to end, tenant-scoped |
| `GetCurrentTenant_NoToken_Returns401` | fallback policy |
| `GetCurrentTenant_UnprovisionedIdentity_Returns403WithCode` | `Tenancy.User.NotProvisioned` |
| `GetCurrentTenant_TenantIdInQuery_IsIgnored` | AGENTS.md rule 7 |
| `FailedMessage_WithFreeText_DoesNotLogPayload` | R-21 |
Architecture: `MessageTypes_Always_OverrideToString` (seen failing once with a temporary record, then reverted).

Frontend (Vitest; test logic and services, no snapshots):
| Test | Proves |
| --- | --- |
| `DevAuthProvider`: signs in via `/dev/token`, stores, restores, ignores expired, signs out | provider contract |
| `AuthService`: `isAuthenticated` follows the session; `signOut` clears and navigates | state |
| `authGuard`: redirects to `/login` when signed out | routing |
| `authInterceptor`: header only on `/api/`; 401 triggers sign-out | security |
| `problemDetailsInterceptor`: maps ProblemDetails (with `code`, `traceId`) and non-ProblemDetails errors | error contract |
| Login page: a user button calls `signIn` and navigates; failure shows the title | screen |
| Shell: shows tenant name; sign out calls the service | screen |
Browser end-to-end tests: Brief #9 (tool chosen there).

### 8. Docs
- `AGENTS.md` → Commands: how to sign in locally (open the `web` URL, choose Alice or Bob).
- `docs/decisions.md`: prepend Appendix A verbatim, then your execution delta on top.

## Acceptance criteria
- [ ] Branch `feat/dev-login-and-current-tenant`, working tree clean, commits follow the commit plan.
- [ ] Backend: format, build (zero warnings), tests pass; report counts. Frontend: `pnpm lint` (0 problems),
      `pnpm test` with `CI=true`, `pnpm build` pass.
- [ ] `aspire run`, open `web`: unauthenticated visit redirects to `/login`; Alice sees "Acme Pension Fund", Bob sees
      "Globex Insurance"; sign out returns to `/login`; restarting the `api` resource then reloading leads back to
      `/login` (R-18). Report what you saw for each step.
- [ ] The browser's network tab shows the token only in the `Authorization` header of `/api/` calls; nothing in URLs.
- [ ] `git grep` shows no `console.log` in `src/frontend` and no message type in `src/backend` without its `ToString`.

## Rules to respect
`AGENTS.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/frontend.md`, `.claude/rules/testing.md`, ADR-0003, ADR-0004,
ADR-0007, ADR-0008, ADR-0009, `docs/git-workflow.md`, `docs/ai-rules.md`. English everywhere (UI text too).

## Out of scope
Upload and submissions (Brief #8); lists, tables, a UI library and browser E2E tests (Brief #9); MSAL and Entra External
ID (Phase 5); refresh tokens; roles; i18n; a profile page; changing the dev issuer.

## Commit plan (Conventional Commits, no push)
1. `fix: keep message contents out of wolverine logs`
2. `feat: add current tenant query endpoint`
3. `feat: add auth service, development provider and http interceptors to the web app`
4. `feat: add development login page and tenant header`
5. `docs: update slice template, commands and decisions log`

## Report back (final message)
- Plan versus what you did; every deviation with the reason and the doc you followed.
- Output of the acceptance checks, including the manual `aspire run` walk-through and the R-21 test evidence.
- Your execution delta (also written into `docs/decisions.md`).
- The exact commands for the owner, not run by you:
  ```powershell
  git push -u origin feat/dev-login-and-current-tenant
  gh pr create --base main --head feat/dev-login-and-current-tenant `
    --title "feat: add development login and current tenant endpoint" `
    --body-file .github/pull_request_template.md
  ```

---

## Appendix A — Decisions delta (planning session 15), paste verbatim into `docs/decisions.md`

## Delta 2026-11-19 (session 15: Brief #7 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-77 | R-21 mitigation: every Wolverine message type in `src/backend` overrides `ToString()` with type name and identifiers only; enforced by an architecture test; success logging at Debug | Wolverine always logs failures at Error with the message; a category filter would hide real failures |
| D-78 | SPA authentication: `AuthService` over an abstract `AuthProvider`; `DevAuthProvider` now, MSAL provider for Entra External ID in Phase 5; components never import a provider | Closes open question 1 of session 11 |
| D-79 | Development session stored in `sessionStorage`, cleared on sign-out and on any 401 from `/api/` | R-18: dev tokens die on Api restart |
| D-80 | Interceptors: `authInterceptor` (bearer on same-origin `/api/` only, 401 → sign out) then `problemDetailsInterceptor` (typed `ApiError` from RFC 9457) | Only `title` reaches the user |
| D-81 | `GET /api/tenancy/me` is the reference slice layout: `Features/<UseCase>/` with query record, response record, Wolverine handler (Dapper, tenant from `ITenantContext`) and Minimal API endpoint using `InvokeForCurrentTenantAsync<Result<T>>` | Documented in `backend-vsa` rules |
| D-82 | UI library decision moves to Brief #9 (first table and form) | Login and header use plain semantic HTML |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| Wolverine logging | Execution/success log levels configurable; failures logged at Error with the exception | https://wolverine.netlify.app/guide/logging |
| Message logging pattern | Message loggers write `ToString()`; override it to control content | https://docs.particular.net/nservicebus/logging/message-contents |
| Angular HTTP | Functional interceptors via `provideHttpClient(withInterceptors([...]))` | https://angular.dev/api/common/http/withInterceptors |

### Risks and flags
- **R-21** expected to close with this brief.
- **R-22 Token in `sessionStorage`.** Readable by any script on the page (XSS). Acceptable for the development provider;
  Phase 5 reviews MSAL's cache location and the Content Security Policy.

### Open questions
1. Browser end-to-end test tool (Playwright or other): Brief #9.
2. UI library: Brief #9.

### Next step
Brief #8: minimal upload slice (`POST /api/portfolio/submissions` storing the raw CSV and cut-off date, size limit,
`SubmissionReceived` event), without validation rules yet.
