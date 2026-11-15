# FinGuard — decisions log

Paste this file into the Claude Project knowledge and update it at the end of every session
("Decisions delta"). Newest delta first.

## Delta 2026-11-15 (session 12: Brief #5 executed)

### Resolved versions
No new package versions. New references to packages already pinned in `Directory.Packages.props`:
`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15 (Api) and `Dapper` 2.1.89 (Tenancy).

### Deviations
- Design-time factories pass `NoTenantContext.Instance` (new, BuildingBlocks) instead of `new TenantContext()`: the new
  architecture rule forbids depending on the concrete `TenantContext` outside BuildingBlocks and Tenancy. Approved by
  the owner before execution.
- `TenantContext`, `ITenantContext` and `TenantStampingInterceptor` are registered by `AddTenancyModule` from commit 1,
  so every commit boots. The `TenantMember` policy, its handler and the 403 result handler are registered by Tenancy;
  the Api registers authentication and the fallback policy (`DevAuth/AuthenticationSetup.cs`).
- Existing tests touched outside commit 4, so every commit stays green: `StartupTests` now checks that the first
  migration per schema is `InitialSchema` (Tenancy has two); `ErrorHandlingTests` send Alice's token (commit 3).
- Extra tests beyond the brief: `UnknownRoute_NoToken_Returns401` (confirms D-61) and
  `DevToken_SeededUser_ReturnsUsableToken` (the only test of the issuer's success path through HTTP).
- `TenantContext.SetTenant(Guid.Empty)` throws `ArgumentException`; the interceptor also refuses a `Modified` entry
  whose original tenant differs from the current one (defence in depth; not reachable through the filter).
- `DevTokenIssuer.CreateToken(identity, issuedAt)` is public so tests can issue expired and unprovisioned tokens; the
  endpoint always passes `TimeProvider.GetUtcNow()`. Unknown user error code: `DevAuth.User.Unknown`.
- `Health_NoToken_Returns200` calls `/alive`; the existing `Health_DatabaseUp_Returns200` keeps covering `/health`.

### Findings
- The filter must compare `(Guid?)e.TenantId == this.CurrentTenantId`. A first version used `CurrentTenantId.Value`;
  EF evaluates that member client-side as a parameter and threw `Nullable object must have a value` without a tenant.
  `QueryFilter_NoTenant_SeesNoRows` caught it. `QueryFilter_SequentialContextsWithDifferentTenants_DoNotLeak` proves
  the filter reads the context instance, not a value cached in the model.
- `has-pending-model-changes` reports no changes for Portfolio, Compliance and Advisory (filters are not part of
  migrations; no entities yet).
- With the fallback policy, unknown routes answer 401 without a token and 404 ProblemDetails with one. In `Production`,
  `POST /dev/token` answers 401 (no endpoint, fallback policy) and never returns a token.
- Test-only routes added by an `IStartupFilter` after `next(app)` get the Api's authentication and tenant resolution;
  adding `UseRouting` → `UseAuthorization` → `UseEndpoints` there applies endpoint policies (`TenantMember`, 403 handler).
- `aspire run`: `api` healthy; `/dev/token` issued Alice's token; `/api/portfolio` answered 401 without a token and 404
  ProblemDetails with Alice's token (the group has no endpoints yet); `tenancy.tenants` holds Acme Pension Fund and
  Globex Insurance; `web` reached ready.
- Architecture probes (temporary, reverted): Portfolio registering `TenantContext` →
  `FinGuard.Portfolio.PortfolioModule does depend on "FinGuard.BuildingBlocks.Tenancy.TenantContext"`; Advisory context
  on plain `DbContext` → `Module DbContexts must derive from TenantAwareDbContext: FinGuard.Advisory.Persistence.AdvisoryDbContext`;
  an `ITenantOwned` entity in Tenancy → `Tenant-owned entities must be mapped only by a TenantAwareDbContext:
  FinGuard.Tenancy.Persistence.ProbeOwned in FinGuard.Tenancy.Persistence.TenancyDbContext`.
- RS0030 probe: both overloads in Portfolio failed with `The symbol 'EntityFrameworkQueryableExtensions.IgnoreQueryFilters<TEntity>(...)'
  is banned in this project: Tenant filter must not be bypassed (ADR-0004)`.
- An IDE-generated `finguard.sln` next to `FinGuard.slnx` makes bare `dotnet build` fail with MSB1011; the owner
  deleted it; `*.sln` is now in `.gitignore`.
- Tests: 72 (unit 30, integration 31, architecture 11), all green.

### Risks and flags
- **R-17** mitigated as planned (Development-only mapping, Production test, fail-fast `Authority`).
- **R-18 Dev tokens die on restart.** The signing key is per process, so the SPA (Brief #7) must request a new token
  after a 401.

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

## Delta 2026-11-14 (session 10: Brief #4 executed)

### Resolved versions (2026-11-14, `scripts/Get-LatestNuGetVersion.ps1`, stable only)
| Package | Version | Notes |
| --- | --- | --- |
| xunit.v3 | 4.0.3 | MTP v2 (`xunit.v3.mtp-v2`) |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.15 | `$(MicrosoftNet10Version)`; 11.0.0 is the .NET 11 line |
| Microsoft.Extensions.Diagnostics.Testing | 10.11.0 | Same 10.x line as Http.Resilience; 11.0.0 skipped |
| Testcontainers.PostgreSql | 4.16.0 | Image `postgres:18.3` |
| TngTech.ArchUnitNET | 0.13.5 | Core only, no framework extension |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | Not 3.3.4, see findings |

### Deviations
- BannedApiAnalyzers 5.6.0 instead of the 3.3.4 the brief named: the resolver returned 5.6.0 (stable 4.14.0, 5.0.0,
  5.3.0 and 5.6.0 exist). 5.6.0 needs Roslyn 5.6+; SDK 10.0.403 ships Roslyn 5.9. Approved by the owner before execution.
- `CloudSeamsStartupLogger` still receives two bools; `Extensions.cs` computes them once through `CloudSeams`, so the
  check runs once per seam and the log lines are byte-identical.
- The generic-repository and one-DbContext rules use reflection, not ArchUnitNET: they inspect generic type definitions
  and a `const` value, which reflection reads directly. ArchUnitNET is used where it adds value: type dependencies
  (Compliance `Domain/`).
- `tests/Directory.Build.props` also adds a global `using Xunit` (xunit.v3 does not add one).
- `.editorconfig` `[tests/**.cs]` relaxes only CA1707. CA2007 never fired, so it stays on.
- The integration fixture blanks `KeyVault:Uri` and `APPLICATIONINSIGHTS_CONNECTION_STRING` so local user-secrets or
  environment variables cannot switch a seam on during tests.

### Findings
- `dotnet test --no-build -c Release` works unchanged in MTP mode from the repo root; CI needs no edit.
- `UseSetting("ConnectionStrings:finguard", ...)` reaches the module registration that reads the connection string while
  the builder runs; no other hook was needed.
- EFCore.NamingConventions also snake-cases the migrations history columns (`migration_id`, `product_version`).
- With an `IExceptionHandler` that handles the exception, .NET 10 does not log the exception from the middleware;
  the only record is `GlobalExceptionHandler`'s (type + trace id). Proven by `UnhandledException_AnyRequest_DoesNotLogExceptionMessage`.
- The test-only throwing route is added after the Api pipeline (`next(app)` first in the `IStartupFilter`) so the Api's
  exception handler wraps it.
- RS0030 probe: a temporary `DateTime.UtcNow` in BuildingBlocks failed the build with
  `error RS0030: The symbol 'DateTime.UtcNow' is banned in this project: Use TimeProvider (AGENTS.md)`; a temporary
  `EnableSensitiveDataLogging()` in Compliance failed with `... Never log query parameters (D-35)`. Both removed.
- Architecture probes (temporary, reverted) failed all seven rules with messages naming the offender, identically in
  Debug and Release (R-14: no difference seen). Note that an unused `ProjectReference` is dropped by the compiler in both
  configurations, so assembly rules see only references that code actually uses.
- Tests: 43 (unit 25, integration 10, architecture 8), all green; `dotnet test` about 6 s locally with the image cached.

### Risks and flags
- **R-15 updated.** BannedApiAnalyzers has stable 5.x releases; the risk becomes "keep the analyzer at or below the
  SDK's Roslyn version", not "the analyzer is stuck at 3.3.4".

## Delta 2026-11-14 (session 9: Dependabot tuning and Brief #4 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-48 | Dependabot during Phases 0-4: `monthly` for nuget, npm and github-actions; one grouped minor/patch PR per ecosystem; `semver-major` version updates ignored for nuget and npm | Supersedes the schedule and ignore list of D-46. Security updates are unaffected. Majors (.NET 11, Aspire 14, Angular 23) arrive through dedicated briefs. Revisit before Phase 5 |
| D-49 | Backend tests use xUnit v3 4.x on Microsoft Testing Platform v2; `global.json` selects MTP for `dotnet test` | Closes open question 3 of the kickoff delta (with D-50 to D-52) |
| D-50 | Assertions: xUnit's built-in `Assert`; no FluentAssertions (commercial licence from v8) and no mocking library | Shouldly can be adopted later by ADR if tests become hard to read |
| D-51 | Architecture tests: ArchUnitNET core (no framework extension) for type rules, reflection for assembly rules; modules discovered by convention | Avoids coupling the arch library to the xUnit major |
| D-52 | Integration tests: `WebApplicationFactory<Program>` + Testcontainers PostgreSQL (`postgres:18.3`), one container per test assembly; `Aspire.Hosting.Testing` not used now and removed from CPM | AppHost-level tests reconsidered in Phase 5 |
| D-53 | Test projects now: `FinGuard.UnitTests`, `FinGuard.Api.IntegrationTests`, `FinGuard.ArchitectureTests`; module test projects arrive with each module's first slice | Compliance rule glob becomes `tests/**/*Compliance*/**` so it matches `tests/FinGuard.Compliance.Tests/` |
| D-54 | `Microsoft.CodeAnalysis.BannedApiAnalyzers` on `src/backend`: bans `DateTime/DateTimeOffset.Now/UtcNow` and `EnableSensitiveDataLogging` | Prose rule turned into a build error |
| D-55 | Cloud-seam checks live in `CloudSeams` (ServiceDefaults) and are unit tested; the enabled Key Vault path is only proven in Phase 5 | R-03 stays open |

### Verified on 2026-11-14
| Area | Finding | Source |
| --- | --- | --- |
| Dependabot | Adding or changing `dependabot.yml` triggers an immediate version-update check (explains the burst of PRs #5-#9) | https://docs.github.com/en/code-security/dependabot/working-with-dependabot/dependabot-options-reference |
| Dependabot | `ignore` with `update-types` applies to version updates, not security updates | https://pydevtools.com/handbook/how-to/how-to-ignore-a-dependency-in-dependabot-without-blocking-security-updates/ |
| .NET 10 testing | `dotnet test` uses MTP only with `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` | https://learn.microsoft.com/dotnet/core/testing/migrating-vstest-microsoft-testing-platform |
| xUnit v3 | 4.0 defaults to MTP v2 and drops official MTP v1 support | https://xunit.net/releases/v3/4.0.0 |
| ArchUnitNET | Core API `HasNoViolations(architecture)` works without an extension; Debug builds recommended | https://archunitnet.readthedocs.io/en/stable/guide/ |
| Testcontainers | `Testcontainers.PostgreSql` 4.x current | https://www.nuget.org/packages/Testcontainers.PostgreSql |
| FluentAssertions | v8+ requires a paid licence for commercial use | https://www.infoq.com/news/2025/01/fluent-assertions-v8-license |

### Risks and flags
- **R-13 Majors pile up.** With majors ignored, .NET 11 / Aspire 14 / Angular 23 drift silently. Mitigation: a
  "dependency majors" review at the start of each phase, and before Phase 5 at the latest.
- **R-14 Release-mode architecture analysis.** ArchUnitNET reads binaries; CI tests a Release build. Watch for rules
  that pass in Release only because the compiler removed a dependency.
- **R-15 Old banned-API analyzer.** Latest stable BannedApiAnalyzers is 3.3.4; newer builds are prerelease only. Replace
  it if it ever breaks with a newer Roslyn.

### Open questions
1. Whether to add a single Aspire AppHost smoke test (`Aspire.Hosting.Testing`) in Phase 5, when images are built.

### Next step
Brief #5: `ITenantContext`, Tenancy mapping, `ITenantOwned` with EF global query filters and stamping interceptor,
development-only JWT issuer, and the first tenant-isolation tests.

## Delta 2026-11-14 (session 8: Brief #3 executed)

### Resolved versions (2026-11-14, `pnpm view`, stable only)
| Package | Version | Notes |
| --- | --- | --- |
| pnpm (`packageManager`) | 11.28.9 | `latest-11`; global pnpm 12.18.0 switches to it automatically |
| Node | 24.16.0 (local) | `engines.node` = `^24.15.0` |
| @angular/cli, @angular/build | 22.2.7 | |
| @angular/core and framework packages | 22.2.6 | Caret ranges from the CLI; 22.2.7 is newer than pnpm 11's `minimumReleaseAge` window |
| TypeScript | 6.0.3 | `~6.0.2`, inside Angular's `>=6.0.0 <6.1.0` |
| Vitest | 5.1.0 | jsdom 30.1.3 |
| angular-eslint, @angular-eslint/builder | 22.6.0 | |
| ESLint | 10.14.0 | Chosen by the schematic (`^10.12.0`); typescript-eslint 8.71.0, @eslint/js 10.0.1 |

### Build scripts
None approved. The Angular CLI writes `allowBuilds` with `@parcel/watcher`, `esbuild`, `lmdb` and `msgpackr-extract`
set to `false`; install, build, test and `ng serve` work with their prebuilt binaries, so the denials stay (with a
comment) in `pnpm-workspace.yaml`. pnpm reports no ignored build scripts.

### Deviations from the brief
- `ng new` ran with `--skip-install --interactive false --zoneless --test-runner vitest --ai-config none` so the first
  install used the pinned pnpm 11 (the CLI wrote `packageManager: pnpm@12.18.0` from the global pnpm).
- The generated `pnpm-workspace.yaml` had `packages: ['.']`; replaced by the brief's file (no `packages:` key) plus the
  CLI's `allowBuilds` denials. The generated `watch` and `ng` scripts are kept.
- `AppHost.cs` had no Api variable; `var api =` was added to the existing chain. The `web` resource matches the brief
  (the 13.6.5 API is unchanged).
- Dependabot `cooldown.default-days: 3`: Dependabot's own default is 3 days, stated explicitly (brief asks for at least 2).
- `.gitignore` entries from the CLI were merged into the root file; root-anchored ones are prefixed with
  `src/frontend/finguard-web/`. `index.html` title is `FinGuard`.

### Findings
- `pnpm start` outside Aspire: without `PORT`, the pnpm shell emulator stops at once with `Unbound variable "PORT"`.
  With `PORT` set, `ng serve` prints the proxy error (`API endpoint is not configured. Run the app through Aspire
  (aspire run).`), does not listen, but keeps its watch process alive until stopped. No silent fallback URL.
- Peer warning on install: `@napi-rs/wasm-runtime@1.2.7` wants `@emnapi/core`/`@emnapi/runtime` `^1.7.1`, gets
  `2.0.0-alpha.6` (transitive WASM fallback, unused on supported platforms).
- `aspire run` shows `web-installer` (the pnpm install step) as `Finished` next to `postgres`, `finguard`, `api`, `web`.
- Proxy check: `GET http://localhost:<web port>/api/does-not-exist` returned the Api's `404 application/problem+json`
  with `traceId`.
- `pnpm-lock.yaml` is a single document (`lockfileVersion: '9.0'`). Proxy config stays CommonJS (`proxy.conf.js`).

## Delta 2026-11-14 (session 7: Brief #3 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-42 | Frontend pins **pnpm 11.x** through the legacy `packageManager` field; `devEngines.packageManager` is not used | Pinning pnpm 12+ writes a two-document lockfile that Dependabot cannot read for alerts. Revisit when GitHub fixes it (R-11) |
| D-43 | Development uses the Angular dev-server proxy for `/api` (target from Aspire `API_HTTPS`/`API_HTTP`); the Api gets no CORS policy | Production serving is decided in Phase 5 |
| D-44 | `shellEmulator: true` in the app's `pnpm-workspace.yaml`; `start` = `ng serve --port $PORT --proxy-config proxy.conf.js` | Cross-platform scripts without extra packages |
| D-45 | Angular app: no SSR, no zone.js, Vitest, angular-eslint 22 (flat config), no UI library yet, no CLI AI configuration | UI library decided with the first real screen (Phase 1) |
| D-46 | Dependabot `npm` ecosystem for `src/frontend/finguard-web`: weekly, grouped minor/patch, majors of Angular/angular-eslint/TypeScript ignored, cooldown ≥ 2 days | Angular majors go through `ng update` briefs |
| D-47 | Aspire resource names: `api`, `web`, `postgres`, `finguard` | Env var prefixes derive from them (`API_HTTPS`) |

### Verified on 2026-11-14
| Area | Finding | Source |
| --- | --- | --- |
| Aspire JS | `WithPnpm(installArgs: [...])`, `runScriptName` default `dev`, Angular sample proxy reads `API_HTTPS`/`API_HTTP` | https://aspire.dev/integrations/frameworks/javascript |
| Aspire env vars | `WithReference(api)` injects `{RESOURCE}_{ENDPOINT}` variables such as `API_HTTPS` | https://aspire.dev/fundamentals/environment-variables |
| Angular testing | Vitest is the default runner for new projects | https://angular.dev/guide/testing/migrating-to-vitest |
| angular-eslint | 22.x: Angular 22 + TS 6, flat config only, Node 20 dropped | https://github.com/angular-eslint/angular-eslint/releases |
| pnpm lockfile | Two documents when `devEngines.packageManager` is set or `packageManager` pins pnpm 12+ | https://pnpm.io/lockfile |
| Dependabot | Supports pnpm 11 and 12; two-document lockfiles get no alerts or security updates yet | https://github.blog/changelog/2025-01-09-dependabot-now-supports-npm-12-pnpm-11-and-12-and-bun-1-4/ |
| pnpm scripts | `shellEmulator` runs scripts in a cross-platform bash-like shell | https://pnpm.io/cli/run |

### Risks and flags
- **R-11 pnpm 12 and Dependabot.** Staying on pnpm 11 until Dependabot reads two-document lockfiles; re-check each quarter.
- **R-12 `minimumReleaseAge` vs Dependabot.** pnpm 11 rejects lockfile entries younger than its release-age window; a
  Dependabot PR can fail CI until the window passes. Mitigated by the Dependabot cooldown; re-run CI if it still happens.

### Open questions
1. Production serving of the SPA (Phase 5): static files from the Api container, or a separate static container. Decide
   with the Container Apps design.
2. UI component library (Angular Material, none, other): decide at the first real screen.
3. Browser end-to-end test tool: decide in Phase 1.

### Next step
Brief #4: test stack and architecture tests (`tests/FinGuard.ArchitectureTests`), plus the tests named in Briefs #2 and #3.

## Delta 2026-11-14 (session 6: Brief #2 executed)

### Resolved versions (2026-11-14, `scripts/Get-LatestNuGetVersion.ps1`, stable only)
| Package | Version | Notes |
| --- | --- | --- |
| Aspire.* (`AspireVersion`), `Aspire.AppHost.Sdk` | 13.6.5 | Unchanged; Aspire CLI 13.6.5 (now a .NET global tool `Aspire.Cli`) |
| WolverineFx, .Postgresql, .EntityFrameworkCore, .RuntimeCompilation (`WolverineVersion`) | 6.64.0 | Unchanged; all four equal |
| EFCore.NamingConventions | 10.0.1 | |
| dotnet-ef (local tool) | 10.0.15 | 11.0.0 is latest but targets EF 11; matches `MicrosoftNet10Version` |
| Microsoft.Extensions.Http.Resilience / ServiceDiscovery | 10.11.0 | Template uses 10.10.0; 11.0.0 skipped (pulls .NET 11 abstractions, same reason as D-20) |
| Microsoft.EntityFrameworkCore.Relational (transitive pin) | 10.0.15 | New entry; otherwise 10.0.14 and 10.0.15 both resolve (MSB3277) |
| PostgreSQL container image (Aspire default) | `postgres:18.3` | |

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-37 | No `PackageVersion` for `Aspire.Hosting.AppHost`: the SDK references it implicitly and CPM fails restore with NU1009 | Version comes only from the `Aspire.AppHost.Sdk/13.6.5` literal (R-08) |
| D-38 | Modules register `AddScoped<DbContext>(sp => sp.GetRequiredService<XxxDbContext>())` so the Api applies migrations without seeing the internal DbContext types | Never inject the base `DbContext` elsewhere |
| D-39 | Connection name and the "fail fast when missing" lookup live in BuildingBlocks (`ModuleDbContextOptions.ConnectionName`, `GetRequiredFinGuardConnectionString`); design-time factories use the placeholder `DesignTimeConnection.Placeholder` (no credentials) | One place for the four modules |
| D-40 | `Npgsql.Command` logs at Warning in `appsettings.json` | Health checks otherwise log every `SELECT 1` at Information |
| D-41 | `Error` keeps its name; CA1716 suppressed on the type with a justification | Brief/D-35 name; C#-only codebase |

### Deviations from the brief
- Tool manifest: .NET 10 `dotnet new tool-manifest` writes `dotnet-tools.json` at the root; moved to `.config/dotnet-tools.json` as the brief asks (both locations are honored by `dotnet tool`).
- Wolverine runtime compilation: `WolverineFx.RuntimeCompilation` auto-registers when referenced (`RuntimeCompilationModule`); `opts.UseRuntimeCompilation()` is also called explicitly (idempotent) to make the opt-in visible.
- Wolverine storage auto-create: no setting needed. `AutoBuildMessageStorageOnStartup` defaults to `CreateOrDefault` (Wolverine 6.64 XML docs).
- Wolverine telemetry: tracing source `Wolverine`, meter wildcard `Wolverine*`; the meter is named `Wolverine:{ApplicationName}`, a bare `AddMeter("Wolverine")` matches nothing (https://wolverine.netlify.app/guide/logging.html).
- Aspire 13.6 AppHost template entry file is `AppHost.cs` (not `Program.cs`) and adds `<AspireUseCliBundle>true</AspireUseCliBundle>`; both kept.
- Migration logger uses its own category: `ILogger<WebApplication>` falls under `Microsoft.AspNetCore` (Warning) and was silent.
- Module libraries work as their own dotnet-ef startup project; the Api was not needed.

### Findings
- `__ef_migrations_history` columns are snake-cased by the naming convention: `migration_id`, `product_version`.
- On a fresh database EF logs one `fail` (`Microsoft.EntityFrameworkCore.Database.Command[20102]`) per module for the first `SELECT ... FROM <schema>.__ef_migrations_history`, then creates the table and applies the migration. It does not recur on later runs. Expected noise, not an error.
- `wolverine` schema tables created at startup: `wolverine_agent_restrictions`, `wolverine_control_queue`, `wolverine_dead_letter_history`, `wolverine_dead_letters`, `wolverine_incoming_envelopes`, `wolverine_node_assignments`, `wolverine_node_records`, `wolverine_nodes`, `wolverine_outgoing_envelopes`.
- Wolverine logs "found no handlers" as a warning; expected until the first slice.
- Probe: unhandled exception returns 500 `application/problem+json` with `title`, `status`, `traceId` only; the exception message did not appear in the response or the logs. Unknown route returns 404 ProblemDetails with `traceId`.
- Aspire CLI installed from npm cannot self-update and conflicts with the no-npm rule; replaced with the `Aspire.Cli` .NET global tool. In Git Bash the tool shim is `aspire.cmd`; use PowerShell.

### Next step
Brief #3: Angular 22 app with pnpm, added to the AppHost with `AddJavaScriptApp(...).WithPnpm()`.

## Delta 2026-11-14 (session 5: Brief #2 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-28 | Every brief states its branch and exact PR title (Conventional Commits). Squash merge uses "Pull request title". Brief numbers and GitHub PR numbers differ (Brief #2 = PR #3); refer to work by brief number | PR #1 squash title "Chore/repo hygiene (#1)" stays; `main` is not rewritten |
| D-29 | Wolverine in Brief #2 = durable message storage only, schema `wolverine`, via `PersistMessagesWithPostgresqlUsingDataSourceFromContainer` on the Aspire `NpgsqlDataSource`; plus `WolverineFx.RuntimeCompilation` | EF transactional integration stays in the Phase 1 spike (R-02) |
| D-30 | Wolverine/Weasel-managed EF schema (`UseEntityFrameworkCoreWolverineManagedMigrations`) is not used; EF Core migrations own module schemas | ADR-0007; Wolverine docs say not to mix both |
| D-31 | One empty `InitialSchema` migration per module; history table `__ef_migrations_history` in the module schema; applied at Api startup in Development only | Production strategy decided in Phase 5 |
| D-32 | snake_case via `EFCore.NamingConventions` (10.x); Dapper `MatchNamesWithUnderscores = true` | Closes the choice left open in ADR-0007 |
| D-33 | DbContexts registered with `AddDbContext` + shared `UseModuleNpgsql` + `EnrichNpgsqlDbContext` (DbContext health checks off); the `NpgsqlDataSource` health check covers the database | Same options for runtime and design time |
| D-34 | Cloud seams (Key Vault source, Azure Monitor toggle) move into Brief #2 (ServiceDefaults); Brief #5 keeps `ITenantContext` and the dev JWT | Roadmap item 5 narrowed |
| D-35 | `ErrorType` to HTTP: Validation 400, NotFound 404, Conflict 409, BusinessRule 422; ProblemDetails carry `code` and `traceId`; unexpected exceptions are a generic 500 in every environment | Correlation id = W3C trace id |
| D-36 | Solution file is `FinGuard.slnx` at the repo root; AppHost uses `Aspire.AppHost.Sdk` as its only SDK | |

### Closed open questions
- `main` is unborn: closed. `main` has the initial commit and the PR #1 squash commit.

### Verified on 2026-11-14
| Area | Finding | Source |
| --- | --- | --- |
| Aspire | 13.6 released 2026-09-29; 13.6.5 is the latest patch on NuGet (`Aspire.Npgsql`) | https://devblogs.microsoft.com/aspire/whats-new-aspire-13-6 , https://nuget.org/packages/Aspire.Npgsql/13.6.5 |
| Aspire AppHost | Since 13.0 `Aspire.AppHost.Sdk` can be the sole SDK and implicitly references `Aspire.Hosting.AppHost` | https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview |
| Aspire EF | `EnrichNpgsqlDbContext<T>` adds retries, health check, logging, telemetry to a self-registered DbContext | https://aspire.dev/integrations/databases/efcore/postgres/postgresql-client |
| Wolverine 6 | Requires .NET 9/10; Roslyn moved to `WolverineFx.RuntimeCompilation`; `ServiceLocationPolicy` default `NotAllowed` | https://wolverine.netlify.app/guide/migration |
| Wolverine PG | `PersistMessagesWithPostgresqlUsingDataSourceFromContainer(schemaName:)` uses a DI-registered `NpgsqlDataSource` | https://wolverine.netlify.app/guide/durability/postgresql |
| Wolverine EF | Weasel-managed EF migrations are opt-in; docs advise choosing one approach | https://wolverine.netlify.app/guide/durability/efcore/migrations |
| EF naming | `EFCore.NamingConventions` 10.0.1 stable, community-maintained | https://www.nuget.org/packages/EFCore.NamingConventions/ |
| .slnx | .NET 10 `dotnet new sln` creates `.slnx` | https://learn.microsoft.com/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default |

### Risks and flags
- **R-08 AppHost SDK version outside CPM.** The `Sdk` attribute holds a literal version; keep it equal to `AspireVersion`
  on every Aspire upgrade (Dependabot may not bump it).
- **R-09 Wolverine 6 cadence and defaults.** Very frequent 6.x releases; Roslyn opt-in and `ServiceLocationPolicy.NotAllowed`
  change behaviour from older samples. Pin, read the changelog, never copy 5.x snippets.
- **R-10 Community naming package.** `EFCore.NamingConventions` is not Microsoft-supported and may lag EF majors.

### Open questions
1. Compliance `Domain/` "depends on nothing", but `Result`/`Error` live in BuildingBlocks next to ASP.NET-specific
   mapping. Decide in Phase 3: allow BuildingBlocks `Results/` in the domain, or move HTTP mapping out.
2. Production migration strategy (migration bundle, job or startup with lock): Phase 5.

### Next step
Brief #3: Angular 22 app with pnpm, added to the AppHost with `AddJavaScriptApp(...).WithPnpm()`.

## Delta 2026-11-14 (session 4: AI rules guide)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-27 | Path-scoped AI rules policy documented in `docs/ai-rules.md`: no always-loaded rules, Claude and Cursor rule files mirrored and edited in the same commit | Cursor `compliance-domain.mdc` globs fixed to `src/backend/Modules/Compliance/**,tests/**/Compliance*/**` to match `.claude/rules/compliance-domain.md` |

## Delta 2026-11-13 (session 3: Brief #1 executed)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-19 | .NET 10 package line: EF Core, EF Core Design, JwtBearer pinned to latest 10.x (10.0.15) via `MicrosoftNet10Version`; 11.x skipped | 11.0.0 targets .NET 11 |
| D-20 | `Microsoft.Extensions.AI` pinned to 10.11.0 to match `Microsoft.Agents.AI` 1.28.0 | 11.0.0 pulls .NET 11 abstractions into net10.0 |
| D-21 | GitHub Actions majors: checkout@v7, pnpm/action-setup@v6, setup-dotnet@v6, setup-node@v7 | Resolved via gh api 2026-11-13 |
| D-22 | `ci-gate` is the single required status check | |
| D-23 | EF Core for commands, Dapper for queries (ADR-0007) | Dapper 2.1.89 and `Aspire.Npgsql` added to `Directory.Packages.props` |
| D-24 | Add `dotnet format --verify-no-changes` to the CI `backend` job in PR #2 | `dotnet build` does not report IDE0003 or using order |
| D-25 | PascalCase for private `const` / `static readonly` fields | Naming rule placed before the `_camelCase` rule in `.editorconfig` |
| D-26 | Repository visibility: public (portfolio; enables branch protection on GitHub Free). LICENSE: MIT | |

### Verified on 2026-11-13
| Area | Finding | Source |
| --- | --- | --- |
| Angular 22 | Node `^22.22.3 \|\| ^24.15.0 \|\| ^26.0.0`, TypeScript `>=6.0.0 <6.1.0`; no package-manager requirement | https://angular.dev/reference/versions |
| pnpm | 12.18.0 installed locally; engines `node >=18` | `pnpm view pnpm@12.18.0 engines` |
| Code style | `dotnet build` enforces IDE0161, IDE0065, IDE0011, IDE1006; IDE0003 and using order only via `dotnet format --verify-no-changes` | Probe in Brief #1 |

### Open questions
- `main` is unborn locally; create an initial commit before the first push.

### Next step
PR #2: .NET solution, AppHost, ServiceDefaults, Api, module projects.

## Delta 2026-11-13 (session 2: AI migration and Brief #1)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-15 | AI library is **Microsoft Agent Framework 1.x** (`Microsoft.Agents.AI`) over `Microsoft.Extensions.AI`, behind `IAdvisoryNarrator`. Supersedes D-08. | ADR-0006 rewritten. One agent, no tools. Stable packages only |
| D-16 | Frontend baseline is **Angular 22**, TypeScript 6, Node 24 LTS, pnpm | Angular 22 dropped Node 20 |
| D-17 | The six Phase 0 PRs are approved | Listed under Next step |
| D-18 | Brief #1 covers repo hygiene, folder skeleton, `.gitignore`, `.editorconfig`, `.gitattributes`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` (Central Package Management) and a base CI/CD | `docs/briefs/brief-01-repo-hygiene.md` |

### Verified on 2026-11-13
| Area | Finding | Source |
| --- | --- | --- |
| Angular 22 | Released June 2026. Requires TypeScript 6. Sources disagree on the Node floor (22.12 versus 22.22.3 / 24.15.0 / 26), so pin Node 24 and read the official versions table. | https://angular.dev/reference/versions , https://blog.ninja-squad.com/2026/06/03/what-is-new-angular-22.0 |
| Agent Framework | Core package `Microsoft.Agents.AI` is on 1.x; sources quote different latest numbers (1.13 to 1.25), so resolve the latest stable from NuGet when adding it. Many satellite packages are still preview or rc. | https://www.nuget.org/packages/Microsoft.Agents.AI/ |
| pnpm | pnpm 11 is current (12 mentioned). `pnpm/action-setup@v6` reads `packageManager` and does not install Node. | https://git.bitcart.ai/pnpm/action-setup |
| .NET SDK pinning | `global.json` supports `rollForward` (for example `latestFeature`). | https://learn.microsoft.com/en-us/dotnet/core/tools/global-json |
| Central Package Management | Enabled with `ManagePackageVersionsCentrally` in `Directory.Packages.props`. | https://startdebugging.net/ja/2026/08/migrate-a-dotnet-solution-to-central-package-management-with-directory-packages-props/ |

### Risks and flags
- **R-05 Agent Framework cadence.** Frequent 1.x releases and mixed preview packages. Pin versions, stable only, review changelog per upgrade.
- **R-06 Node floor ambiguity for Angular 22.** CI and local machines must use Node 24 (or a version confirmed by the official table).
- **R-07 Unused CPM entries drift.** Dependabot updates them weekly; PR #2 to #6 remove entries nobody ended up using.

---

## Delta 2026-11-13 (session: Phase 0 kickoff)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-01 | Architecture approved: modular monolith, VSA, Clean + DDD + Specification in Compliance | ADR-0001 |
| D-02 | Evaluation and rebalance rules approved: utilization, 90% alert, 100% exact is warning, >100% breached | In `AGENTS.md` |
| D-03 | Rebalance target 85% of limit, configurable, relax to 90% and mark `Partial` | Invariant `ratio x sum(limits) >= 1` |
| D-04 | Rebalance allocation proportional to headroom, cent rounding preserves total | Remainder to largest headroom |
| D-05 | Cut-off date is an upload form field, not a CSV column | |
| D-06 | CSV per position: `position_id, asset_class, market_value_usd, currency, description`; max 5 MB, 10,000 rows, UTF-8 | |
| D-07 | Endpoints are Minimal APIs; Wolverine handles commands, queries and messaging | `Wolverine.HTTP` only if it respects Minimal API conventions |
| D-08 | ~~AI library is Semantic Kernel~~ | Superseded by D-15 |
| D-09 | UI, errors and LLM explanations are in English | |
| D-10 | Rejected submissions are stored with their errors | |
| D-11 | Idempotency by SHA-256 + cut-off date; return the existing submission | |
| D-12 | Limit policy is a versioned table with an initial seed; admin UI is post-MVP | |
| D-13 | Rules files strategy: `AGENTS.md` shared, `CLAUDE.md` imports it, `.claude/rules/` and `.cursor/rules/` scoped | |
| D-14 | No Whiteboard; module and phase tables plus Mermaid in `overview.md` are enough | |

### Roadmap change (zero-cost strategy, ADR-0005)
All Azure infrastructure moves to the final phase. Phases 0 to 4 run locally with Aspire.

| Phase | Scope | Exit criteria |
| --- | --- | --- |
| 0 Foundations | ADRs, `AGENTS.md`, `CLAUDE.md`, Claude and Cursor rules, solution skeleton (AppHost, ServiceDefaults, empty modules with Contracts), Angular with pnpm via Aspire, PostgreSQL container, CI (build, test, lint), architecture tests, `ITenantContext` plus dev JWT, cloud seams (Key Vault config source, Azure Monitor exporter toggle) | `aspire run` starts API, web and PostgreSQL; CI green; architecture tests green |
| 1 Walking skeleton (local) | Wolverine outbox spike with two DbContexts, Tenancy, dev auth, minimal upload, list submissions in Angular | End-to-end flow locally; tenant isolation test green |
| 2 Portfolio ingestion | Parsing, validation, versioning, rejected submissions, SHA-256 idempotency, upload and history UI | All validation cases tested; two submissions for one date coexist |
| 3 Compliance core | Rich domain, versioned policy, evaluation, alerts, specifications, `EvaluationCompleted`, result UI | Boundary tables green (0.8999 to 1.0001); result visible in UI |
| 4 Rebalance and advisory | Deterministic engine, `IAdvisoryNarrator` with `TemplateNarrator` and Agent Framework adapter on a local or free model, output validation, fallback | Proposal always present; narrative async and degrades gracefully |
| 5 Azure and hardening | IaC, Container Apps, registry, PostgreSQL Flexible Server, Key Vault, Application Insights, Azure AI Foundry, Entra External ID, OIDC CD, security review, E2E, audit log, runbook, teardown and budget alert | Public demo URL with real login; teardown script works |

### Verified on 2026-11-13 (sources are official or package feeds; re-verify before upgrading)
| Area | Finding | Source |
| --- | --- | --- |
| Aspire | 13.x line. `Aspire.Hosting.JavaScript` 13.6.4 published 2026-10-24. `AddJavaScriptApp(...).WithPnpm()` exists; publish uses `pnpm install --frozen-lockfile` when `pnpm-lock.yaml` exists and enables pnpm through corepack in Docker. | https://www.nuget.org/packages/aspire.hosting.javascript , https://devblogs.microsoft.com/aspire/aspire-for-javascript-developers |
| .NET | .NET 10 assumed as baseline (Aspire 13 community packages require it). Not independently checked against the .NET support page. | Community package metadata |
| Wolverine | 6.x line (`WolverineFx.Postgresql` 6.x, `WolverineFx.EntityFrameworkCore` 6.17.x in July 2026). EF Core integration lists transactional outbox and an optional conjoined multi-tenancy mode. Exact newest version not confirmed; take the latest stable at scaffold time. | https://www.nuget.org/packages/WolverineFx.EntityFrameworkCore , https://feed.nuget.org/packages/WolverineFx.Postgresql/6.3.2 |
| Semantic Kernel | Microsoft Agent Framework reached 1.0 GA on 2026-04-03 and is described as the successor to Semantic Kernel and AutoGen. Independent sources report Semantic Kernel in maintenance mode; Visual Studio Magazine says it remains active. | https://devblogs.microsoft.com/agent-framework/migrate-your-semantic-kernel-and-autogen-projects-to-microsoft-agent-framework-release-candidate/ , https://visualstudiomagazine.com/articles/2026/04/06/microsoft-ships-production-ready-agent-framework-1-0-for-net-and-python.aspx |
| Entra External ID | Angular SPA uses MSAL Angular with authority `https://<tenant-subdomain>.ciamlogin.com/`; custom domains are supported. The Microsoft tutorial pins old library versions (msal-browser 2.37, msal-angular 2.5.7), so do not copy them. | https://learn.microsoft.com/en-us/entra/external-id/customers/tutorial-single-page-app-angular-sign-in-configure-authentication |
| Claude Code | Does not read `AGENTS.md` directly; `CLAUDE.md` with `@AGENTS.md` imports it. Imports inside code blocks are ignored. `.claude/rules/*.md` support `paths:` frontmatter. Target under about 200 lines per file. | https://code.claude.com/docs/en/memory |
| Cursor | Project rules are `.cursor/rules/*.mdc` with `description`, `globs`, `alwaysApply`; keep under 500 lines. `AGENTS.md` is supported and Cursor also reads `CLAUDE.md`, so `CLAUDE.md` stays tiny. One doc version says plain `.md` rules are ignored, another says supported: we use `.mdc`. | https://cursor.com/docs/context/rules |
| Angular | Not verified. Use the current stable CLI at scaffold time and record the version here. | |

### Risks and flags
- ~~R-01 Semantic Kernel lifecycle~~ Closed: migrated to Agent Framework (D-15).
- **R-02 Wolverine with several DbContexts.** Outbox behaviour with one DbContext per module needs a spike in Phase 1.
- **R-03 Cloud surprises late.** Identity, secrets and containers are only proven in Phase 5. Run the Docker images
  locally in Phases 0 to 4 and keep the cloud seams tested.
- **R-04 Windows specifics.** Docker Desktop (or compatible) must run for PostgreSQL. Add `.gitattributes` with
  `* text=auto eol=lf` and an `.editorconfig` in Phase 0 to avoid line-ending noise.

### Open questions
1. Local model for Phase 4: Ollama (check its Aspire integration) or GitHub Models free tier. Decide at Phase 4 start.
2. Container registry in Phase 5: GitHub Container Registry or Azure Container Registry, after a price check.
3. Test stack details (xUnit, Testcontainers, NetArchTest vs ArchUnitNET): confirm during the Phase 0 scaffold.

### Next step
Start Phase 0 in Claude Code with the implementation briefs below, one per PR:
1. Repo hygiene: `.gitattributes`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, copy rules files and ADRs.
2. Solution skeleton: AppHost, ServiceDefaults, Api, BuildingBlocks, four empty modules with Contracts, PostgreSQL container.
3. Angular app with pnpm and `packageManager`, added to the AppHost with `AddJavaScriptApp(...).WithPnpm()`.
4. Architecture tests for the rules in `AGENTS.md`.
5. `ITenantContext`, dev JWT issuer (Development only), cloud seams (Key Vault source, Azure Monitor toggle).
6. GitHub Actions CI: restore, build, test, `pnpm install --frozen-lockfile`, lint, test, build.
