# Brief #5 — Tenant context, query filters and development authentication

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-05-tenant-context-dev-auth.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-05-tenant-context-dev-auth.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/tenant-context-and-dev-auth` |
| PR title (exact, becomes the squash commit) | `feat: add tenant context, query filters and development authentication` |
| Phase | Last brief of Phase 0 |

---

## Goal
Make tenant isolation real before any business table exists: every request is authenticated with a JWT, the
identity (`iss` + `sub`) is mapped to a tenant by the Tenancy module, `ITenantContext` carries that tenant, EF Core
filters and stamps `tenant_id` on every tenant-owned entity, and tests prove that tenant A never sees or writes tenant
B's rows. Local development gets an in-process token issuer and two seeded tenants so the SPA (Phase 1) and the tests
can sign in without Azure. No business slice, no frontend change, no Wolverine change.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`,
  `docs/decisions.md` (newest delta first), ADR-0004 (multi-tenancy), ADR-0005 (local-first), ADR-0007 (persistence),
  `docs/git-workflow.md`, `docs/ai-rules.md`.
- Current code: four modules with `internal sealed` DbContexts registered through `UseModuleNpgsql` +
  `EnrichNpgsqlDbContext` and exposed as `DbContext` for the migration runner (D-38); `BuildingBlocks` has `Results/`,
  `Http/`, `Persistence/`; the Api pipeline is `UseExceptionHandler` → `UseStatusCodePages` → Development migrations →
  endpoint maps; tests: `FinGuard.UnitTests`, `FinGuard.Api.IntegrationTests` (one PostgreSQL container and one
  `WebApplicationFactory<Program>` per assembly, `IStartupFilter` for test-only routes), `FinGuard.ArchitectureTests`
  (modules discovered by convention). `src/backend/BannedSymbols.txt` is active (RS0030).
- `Microsoft.AspNetCore.Authentication.JwtBearer` already has a `PackageVersion` (10.x line).
- Decisions for this brief (Appendix A, ADR-0008):
  - Development tokens come from an **in-process issuer in the Api** (`POST /dev/token`), mapped only in Development,
    signing with a random key created at startup. Not `dotnet user-jwts` (the SPA cannot call a CLI) and not Keycloak
    (`Aspire.Hosting.Keycloak` is preview only; stable-only rule).
  - Two seeded development tenants and users (Development only). Real onboarding arrives with Entra External ID in Phase 5.
  - EF Core 10 **named** query filter `"Tenant"`; the filter reads a property of the DbContext instance, never a value
    captured when the model is built.
- Facts verified on 2026-11-14 (re-check at execution):
  - EF Core 10 supports named query filters, `HasQueryFilter("TenantFilter", ...)`, and `IgnoreQueryFilters([...])` by
    name. Source: https://learn.microsoft.com/ef/core/querying/filters
  - ASP.NET Core reads JWT bearer options from `Authentication:Schemes:Bearer` in configuration.
    Source: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/security
  - `Aspire.Hosting.Keycloak` is a preview package. Source: https://aspire.dev/integrations/security/keycloak
- The user works on Windows. Use PowerShell 7 syntax. Docker must run for integration tests.

## Environment rules
- Never run `npm`, `yarn` or `npx`. Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Never invent versions or APIs; stable packages only; `.NET 10` line for `Microsoft.*` packages (D-19).
- Never log tokens, signing keys, claim values or connection strings. Tenant ids may be logged at Debug only.
- Never take the tenant from a request body, route, query string or header (AGENTS.md rule 7).

## Preflight (run first, report results, stop if something is missing)
```powershell
git switch main; git pull; git status --short
dotnet build -c Release            # zero warnings
dotnet test --no-build -c Release  # 43 tests green (Docker running)
```

## Tasks

### 1. Branch
`git switch -c feat/tenant-context-and-dev-auth`

### 2. BuildingBlocks: tenancy primitives (`src/backend/BuildingBlocks/Tenancy/`)
- `ITenantContext`: `Guid? TenantId`, `bool HasTenant`, `Guid RequiredTenantId` (throws `InvalidOperationException`
  when no tenant; that is a bug, not an expected failure).
- `TenantContext : ITenantContext` (scoped): `SetTenant(Guid)` succeeds once; the same id again is a no-op; a different
  id throws. Register `TenantContext` scoped and `ITenantContext` as the same instance.
- `ITenantOwned`: `Guid TenantId { get; }` (setter may be private; the interceptor writes through the EF entry).
- `TenantAwareDbContext : DbContext`: takes `ITenantContext`; exposes `protected Guid? CurrentTenantId`; in
  `OnModelCreating` applies the named filter `TenantQueryFilter.Name = "Tenant"` to every entity type implementing
  `ITenantOwned`, built as an expression over **this instance's** `CurrentTenantId` (so EF parameterizes it per context).
  With no tenant the filter matches nothing (fail closed). Derived contexts call `base.OnModelCreating` and keep their
  `HasDefaultSchema`.
- `TenantStampingInterceptor : SaveChangesInterceptor` (scoped): for `Added` tenant-owned entries with an empty
  `TenantId`, set it from `RequiredTenantId`; throw `InvalidOperationException` when an added entry already carries a
  different tenant, when a `Modified` entry changes `TenantId`, or when there is no tenant. Cover both
  `SavingChanges` and `SavingChangesAsync`.
- `AuthorizationPolicies.TenantMember` (const string) for endpoint groups.

### 3. Modules
- Portfolio, Compliance and Advisory DbContexts derive from `TenantAwareDbContext`; their design-time factories pass a
  context without a tenant. Registration becomes
  `AddDbContext<X>((sp, o) => o.UseModuleNpgsql(cs, X.Schema).AddInterceptors(sp.GetRequiredService<TenantStampingInterceptor>()))`.
  No migration changes are expected for these three (no entities yet); verify with `dotnet ef migrations has-pending-model-changes` or an empty diff.
- Each module's endpoint group requires the tenant: `MapGroup("/api/<module>").RequireAuthorization(AuthorizationPolicies.TenantMember)`.
- **Tenancy** keeps a plain `DbContext` (its tables are global by design) and adds:
  - `Tenant` (`id`, `name` max 200, `created_at`) and `User` (`id`, `tenant_id`, `issuer` max 500, `subject` max 200,
    `created_at`; unique index on `issuer, subject`). `User` is **not** `ITenantOwned`: it is the lookup that finds the
    tenant. Use `TimeProvider` for timestamps. Migration `AddTenantsAndUsers`.
  - `DevIdentities` (public static): issuer `finguard-dev`, audience `finguard-api`, two identities with fixed subjects
    and tenants: `alice` → "Acme Pension Fund", `bob` → "Globex Insurance".
  - `SeedDevelopmentTenantsAsync` (idempotent upsert of the two tenants and users), called by the Api in Development
    after migrations.
  - `TenantResolutionMiddleware` + `app.UseTenancy()`: when the user is authenticated, read `iss` and `sub`, look up
    `tenancy.users` with Dapper over `NpgsqlDataSource` (parameterized `const string` SQL; this bootstrap lookup is the
    one documented exception to the `tenant_id` filter, because it is how the tenant is found), and call
    `TenantContext.SetTenant`. Unknown identity: leave the context empty.
  - `TenantMemberRequirement` + handler: succeeds only when `ITenantContext.HasTenant`. When it fails for an
    authenticated user, respond 403 ProblemDetails with `code` = `Tenancy.User.NotProvisioned` (through an
    `IAuthorizationMiddlewareResultHandler`; keep the default behaviour for every other failure).
  - `AddTenancyModule` registers all of the above plus the policy.

### 4. Api: authentication and the development issuer
- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with `MapInboundClaims = false`.
  - Development: validate issuer `DevIdentities.Issuer`, audience `DevIdentities.Audience`, lifetime, and the signing key
    of a singleton `DevTokenIssuer` (random 256-bit HMAC key generated at startup, never written anywhere).
  - Any other environment: bind `Authentication:Schemes:Bearer` (`Authority`, `Audience`); fail fast at startup with a
    clear message if `Authority` is missing. Nothing Azure-specific in code (Entra External ID arrives in Phase 5 as
    configuration only).
- `AddAuthorization` with a **fallback policy that requires an authenticated user** (secure by default) and the
  `TenantMember` policy.
- Pipeline order: `UseExceptionHandler` → `UseStatusCodePages` → `UseAuthentication` → `UseTenancy` →
  `UseAuthorization` → endpoints. Health endpoints in ServiceDefaults get `.AllowAnonymous()`.
- `DevAuth/DevTokenEndpoints.cs`: `POST /dev/token` with body `{ "user": "alice" }`, mapped **only** when
  `IsDevelopment()`, `.AllowAnonymous()`. Returns `{ accessToken, expiresAt }` (1 hour, `TimeProvider`, claims `iss`,
  `aud`, `sub`, `name`; created with `JsonWebTokenHandler`). Unknown user → 400 through `Result`/`Error.Validation`.
- `src/backend/BannedSymbols.txt`: ban both `IgnoreQueryFilters` overloads with the message "Tenant filter must not be
  bypassed (ADR-0004)". No production code needs them; tests are not covered by the analyzer.

### 5. Tests (`.claude/rules/testing.md`)
Unit (`FinGuard.UnitTests`):
| Test | Proves |
| --- | --- |
| `SetTenant_FirstCall_SetsTenant` / `SetTenant_SameIdTwice_IsNoOp` / `SetTenant_DifferentId_Throws` | context can be set once |
| `RequiredTenantId_NoTenant_Throws` | no silent default |
| `DevIdentities_All_HaveUniqueSubjectsAndTenants` | seed data cannot collide |

Integration (`FinGuard.Api.IntegrationTests`). Add a test-only route group `/api/_test/tenant` through the existing
`IStartupFilter` pattern, with `RequireAuthorization(TenantMember)`, returning the resolved tenant id. Add a test-only
`ProbeDbContext : TenantAwareDbContext` (schema `tenancy_probe`, entity `ProbeItem : ITenantOwned`) created once by the
fixture from its generated create script; it uses the real `TenantStampingInterceptor`.
| Test | Proves |
| --- | --- |
| `Health_NoToken_Returns200` | health stays anonymous |
| `ProtectedRoute_NoToken_Returns401` | fallback policy |
| `ProtectedRoute_TokenSignedWithOtherKey_Returns401` | signature is validated |
| `ProtectedRoute_ExpiredToken_Returns401` | lifetime is validated (issue the token with a past time) |
| `ProtectedRoute_AliceToken_ResolvesAcme` / `..._BobToken_ResolvesGlobex` | identity → tenant mapping |
| `ProtectedRoute_TenantIdInQueryOrHeader_IsIgnored` | AGENTS.md rule 7 |
| `ProtectedRoute_UnprovisionedIdentity_Returns403WithCode` | `Tenancy.User.NotProvisioned` |
| `DevToken_UnknownUser_Returns400` | issuer only serves seeded users |
| `DevToken_ProductionEnvironment_IsNotServed` | a second factory in `Production` (dummy `Authority`) returns no token |
| `SeedDevelopmentTenants_RunTwice_KeepsTwoTenants` | seeding is idempotent |
| `QueryFilter_TenantA_SeesOnlyOwnRows` (and B) | filter isolates tenants |
| `QueryFilter_NoTenant_SeesNoRows` | fail closed |
| `QueryFilter_SequentialContextsWithDifferentTenants_DoNotLeak` | the filter is per context, not captured in the cached model |
| `SaveChanges_AddedWithoutTenantId_StampsCurrentTenant` | interceptor stamps |
| `SaveChanges_NoTenant_Throws` / `SaveChanges_AddedWithOtherTenantId_Throws` / `SaveChanges_TenantIdModified_Throws` | interceptor guards |
| `UnknownRoute_AnyRequest_Returns404Problem` (existing) | update: send Alice's token; without one the fallback policy now answers 401 (likely; confirm and report) |

Architecture (`FinGuard.ArchitectureTests`):
| Rule | Test |
| --- | --- |
| Only Tenancy sets the tenant | no type outside `FinGuard.BuildingBlocks` and `FinGuard.Tenancy` depends on `TenantContext` (the concrete class) |
| Tenant-owned modules filter by tenant | every module DbContext except Tenancy's derives from `TenantAwareDbContext` |
| Tenant-owned entities live in tenant-aware contexts | every `ITenantOwned` entity type is mapped only by a `TenantAwareDbContext` |

Prove each new architecture rule and the new banned symbol fail once (temporary change, then revert) and report the messages.

### 6. Docs and rules
- `docs/adr/0008-development-identity.md` (context, decision, consequences, alternatives: `dotnet user-jwts`, Keycloak
  container, Entra External ID from day one). Status Accepted.
- ADR-0004: add a "Consequences" line: the `tenancy.users` lookup is the only query without a `tenant_id` filter.
- `.claude/rules/backend-vsa.md` and `.cursor/rules/backend-vsa.mdc` (same commit): module DbContexts derive from
  `TenantAwareDbContext` (except Tenancy); module endpoint groups require `TenantMember`; never call `SetTenant` outside
  Tenancy; Wolverine handlers must not assume the HTTP request's `ITenantContext` until Brief #6 proves propagation.
- `AGENTS.md` → Commands: how to get a development token
  (`Invoke-RestMethod -Method Post -Uri <api>/dev/token -Body '{"user":"alice"}' -ContentType 'application/json'`).
- `docs/decisions.md`: prepend Appendix A verbatim, then your execution delta on top.

## Acceptance criteria
- [ ] Branch `feat/tenant-context-and-dev-auth`, working tree clean, commits follow the commit plan.
- [ ] `dotnet format --verify-no-changes --no-restore`, `dotnet build -c Release` (zero warnings) and
      `dotnet test --no-build -c Release` pass; report counts per project.
- [ ] `aspire run`: `api` healthy; `POST /dev/token` for `alice` returns a token; a request to `/api/portfolio` without
      a token answers 401, with Alice's token reaches the (empty) group; `tenancy.tenants` holds the two seeded tenants.
- [ ] No `IgnoreQueryFilters`, `DateTime.UtcNow` or token/claim value in production code or logs (`git grep` in the report).
- [ ] The `web` resource still starts (no frontend change; it does not call the Api yet).
- [ ] Every new architecture rule and the banned symbol were seen failing once; messages reported.

## Rules to respect
`AGENTS.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`, ADR-0004, ADR-0005, ADR-0007, the new ADR-0008,
`docs/git-workflow.md`, `docs/ai-rules.md`. English everywhere.

## Out of scope
Tenant propagation into Wolverine handlers and the outbox spike (Brief #6); the Angular `AuthService`, login screen and
HTTP interceptor (Brief #7); the first business table and slice; real onboarding/sign-up; Entra External ID, MSAL and any
Azure resource (Phase 5); PostgreSQL Row-Level Security (evaluated in Phase 5); refresh tokens; roles.

## Commit plan (Conventional Commits, no push)
1. `feat: add tenant context, tenant-owned contract and tenant query filter`
2. `feat: add tenancy tables, development seed and tenant resolution`
3. `feat: add jwt authentication with a development token issuer`
4. `test: add tenant isolation, authentication and architecture tests`
5. `docs: add adr-0008 and update rules, commands and decisions log`

## Report back (final message)
- Plan versus what you did; every deviation with the reason and the doc you followed.
- Resolved versions of any package added.
- Output of the acceptance checks, the architecture-rule and RS0030 failure messages.
- Your execution delta (also written into `docs/decisions.md`).
- The exact commands for the owner, not run by you:
  ```powershell
  git push -u origin feat/tenant-context-and-dev-auth
  gh pr create --base main --head feat/tenant-context-and-dev-auth `
    --title "feat: add tenant context, query filters and development authentication" `
    --body-file .github/pull_request_template.md
  ```

---

## Appendix A — Decisions delta (planning session 11), paste verbatim into `docs/decisions.md`

## Delta 2026-11-14 (session 11: roadmap, Entra timing and Brief #5 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-56 | The repository is the source of truth for `docs/decisions.md`, `AGENTS.md`, rules and briefs. The planning assistant reads the repo at the start of each session and syncs the Claude Project copies itself; the owner no longer re-uploads them | Project copies of decisions.md, AGENTS.md, testing.md and compliance-domain.md synced on 2026-11-14 |
| D-57 | Microsoft Entra External ID tenant is created in Phase 5 (planned Brief #21), linked to an Azure subscription at creation. No trial tenant now | A trial tenant without subscription lasts 30 days; one active trial per account |
| D-58 | Phase 0 ends with Brief #5. The former roadmap item 6 (CI) was delivered by Briefs #1, #3 and #4 | Tentative map: Phase 1 = #6-#9, Phase 2 = #10-#12, Phase 3 = #13-#15, Phase 4 = #16-#19, Phase 5 = #20-#24 |
| D-59 | Development identity: in-process token issuer in the Api (`POST /dev/token`), Development only, random signing key per process, issuer `finguard-dev`, audience `finguard-api` | ADR-0008. Rejected: `dotnet user-jwts` (no SPA flow), Keycloak (`Aspire.Hosting.Keycloak` preview only) |
| D-60 | Non-Development environments bind `Authentication:Schemes:Bearer` (`Authority`, `Audience`) and fail fast without `Authority` | Entra External ID becomes configuration in Phase 5 |
| D-61 | Authorization: fallback policy requires an authenticated user; module endpoint groups require `TenantMember`; health endpoints anonymous | Unknown routes now answer 401 without a token |
| D-62 | Tenant filter: EF Core 10 named filter `"Tenant"` in `TenantAwareDbContext`, reading the context instance's `CurrentTenantId`; fail closed without a tenant; stamping and guards in `TenantStampingInterceptor` | `IgnoreQueryFilters` banned in `src/backend` (RS0030) |
| D-63 | Tenant resolution: Tenancy maps (`iss`, `sub`) to a tenant through `tenancy.users`; that Dapper lookup is the only query without a `tenant_id` filter; unknown identities get 403 `Tenancy.User.NotProvisioned` | `User` is not `ITenantOwned` |
| D-64 | Two development tenants seeded in Development only: `alice` → Acme Pension Fund, `bob` → Globex Insurance | Real onboarding arrives with Entra in Phase 5 |

### Verified on 2026-11-14
| Area | Finding | Source |
| --- | --- | --- |
| Entra External ID | Core offer free for the first 50,000 MAU | https://learn.microsoft.com/entra/external-id/external-identities-pricing |
| Entra External ID | Trial external tenant without subscription lasts 30 days; one active trial per account | https://learn.microsoft.com/entra/external-id/customers/quickstart-trial-setup |
| EF Core 10 | Named query filters; `IgnoreQueryFilters` can disable filters by name | https://learn.microsoft.com/ef/core/querying/filters |
| ASP.NET Core | JWT bearer options bind from `Authentication:Schemes:Bearer` | https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/security |
| Aspire Keycloak | `Aspire.Hosting.Keycloak` is preview | https://aspire.dev/integrations/security/keycloak |

### Risks and flags
- **R-16 Wolverine scope and the tenant.** `IMessageBus.InvokeAsync` may resolve handler services in a new scope, so the
  HTTP request's `ITenantContext` might not reach handlers. Brief #6 proves propagation (tenant id carried by the message,
  set by Wolverine middleware) before any slice depends on it.
- **R-17 Dev issuer leaking to production.** Mitigated by mapping only in Development, a Production test, and fail-fast
  configuration outside Development.

### Open questions
1. Whether the SPA keeps a generic OIDC client for both the dev issuer and Entra, or uses MSAL only in Phase 5 (frontend
   rule says MSAL behind `AuthService`). Decide in Brief #7.

### Next step
Brief #6 (Phase 1): Wolverine outbox spike with two DbContexts and tenant propagation into handlers.
