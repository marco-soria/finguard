# Brief #4 — Test stack, architecture tests and skeleton tests

How to run it (Windows, PowerShell 7, from the repo root):
1. Merge the Dependabot PR first (`chore: reduce dependabot noise during development`), then start from an up-to-date
   `main` and save this file as `docs/briefs/brief-04-test-stack.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-04-test-stack.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `test/test-stack-and-architecture-tests` |
| PR title (exact, becomes the squash commit) | `test: add test stack, architecture tests and skeleton tests` |
| GitHub PR number (expected) | next free number (refer to work by brief number, D-28) |

---

## Goal
Choose and wire the backend test stack, then prove what Briefs #2 and #3 built: unit tests for `Result`/`Error`, the
HTTP mapping and the cloud-seam switches; integration tests that boot the real Api against PostgreSQL in a container;
architecture tests that encode the module boundaries from `AGENTS.md`; and a banned-API analyzer for the rules that a
compiler can enforce. CI runs all of it. No production behaviour changes.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/testing.md`, `.claude/rules/backend-vsa.md`,
  `.claude/rules/compliance-domain.md`, `docs/decisions.md` (newest delta first), ADR-0001, ADR-0004, ADR-0007,
  `docs/git-workflow.md`, `docs/ai-rules.md`.
- Current code (Brief #2): `FinGuard.slnx`; `src/backend/BuildingBlocks` (`Results/Result.cs`, `Results/Error.cs`,
  `Results/ErrorType.cs`, `Http/ResultHttpExtensions.cs`, `Persistence/ModuleDbContextOptions.cs`);
  `FinGuard.Api` (`Program.cs` with top-level statements, `Errors/GlobalExceptionHandler.cs`,
  `Persistence/ModuleMigrations.cs`); `FinGuard.ServiceDefaults/Extensions.cs` (Key Vault and Azure Monitor seams
  inline) and `CloudSeamsStartupLogger.cs`; four modules, each with an `internal sealed` DbContext and `const Schema`.
  `tests/` holds only `.gitkeep`. The frontend tests from Brief #3 already run in CI and are not touched.
- `global.json` pins SDK `10.0.403` and has no `test` section yet. CI `backend` job: restore, format, build, then
  `dotnet test --no-build -c Release`.
- Decisions for this brief (Appendix A): xUnit v3 4.x on Microsoft Testing Platform v2; xUnit's built-in `Assert`;
  ArchUnitNET core plus reflection for architecture tests; `WebApplicationFactory<Program>` plus
  Testcontainers PostgreSQL for integration tests; BannedApiAnalyzers for `TimeProvider` and EF sensitive logging.
- Facts verified on 2026-11-14 (re-check at execution):
  - .NET 10 SDK runs `dotnet test` on Microsoft.Testing.Platform only when `global.json` contains
    `"test": { "runner": "Microsoft.Testing.Platform" }`. Source: https://learn.microsoft.com/dotnet/core/testing/migrating-vstest-microsoft-testing-platform
  - xUnit.net v3 4.0 makes MTP v2 the default and drops official MTP v1 support; `xunit.v3` installs `xunit.v3.mtp-v2`.
    Sources: https://xunit.net/releases/v3/4.0.0 , https://www.nuget.org/packages/xunit.v3
  - ArchUnitNET (`TngTech.ArchUnitNET`) works without a framework extension: `rule.HasNoViolations(architecture)`
    returns a bool. It reads compiled binaries and its docs recommend Debug builds.
    Sources: https://archunitnet.readthedocs.io/en/stable/guide/ , https://www.nuget.org/packages/TngTech.ArchUnitNET.xUnitV3/
  - Testcontainers for .NET ships `Testcontainers.PostgreSql` (4.x). Source: https://dotnet.testcontainers.org/modules/postgres
  - FluentAssertions 8+ needs a paid licence for commercial use. Source: https://www.infoq.com/news/2025/01/fluent-assertions-v8-license
  - `Microsoft.CodeAnalysis.BannedApiAnalyzers` reads `BannedSymbols.txt` added as `AdditionalFiles`; latest stable 3.3.4.
    Source: https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers
- The user works on Windows. Use PowerShell 7 syntax. Docker Desktop must run for integration tests.

## Environment rules
- Never run `npm`, `yarn` or `npx`. Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Never invent versions or APIs. Resolve versions with `scripts/Get-LatestNuGetVersion.ps1`, stable only, and keep the
  .NET 10 line for `Microsoft.*` packages that also ship 11.x (D-19, D-20).
- No FluentAssertions, Moq, NSubstitute or other mocking library: nothing in this brief needs a mock, and the testing
  rules forbid mocking the DbContext.
- Do not change production behaviour. The only production edits allowed are listed in tasks 4 and 5.

## Preflight (run first, report results, stop if something is missing)
```powershell
git switch main; git pull; git status --short     # clean tree; Dependabot PR merged
dotnet --list-sdks                                # 10.0.403 or a later 10.0 feature band
docker version --format '{{.Server.Version}}'     # Docker must be running
dotnet build -c Release                           # baseline: zero warnings
```
Resolve and print the latest stable version of: `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing` (10.x),
`Testcontainers.PostgreSql`, `TngTech.ArchUnitNET`, `Microsoft.Extensions.Diagnostics.Testing` (10.x),
`Microsoft.CodeAnalysis.BannedApiAnalyzers`. Stop and report if `xunit.v3` has no stable 4.x.

## Tasks

### 1. Branch
`git switch -c test/test-stack-and-architecture-tests`

### 2. Test runner configuration
- `global.json`: add `"test": { "runner": "Microsoft.Testing.Platform" }` (lower-case keys, keep the `sdk` section).
- `tests/Directory.Build.props`: import the root props
  (`$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))`), then set
  `IsPackable=false`, `IsTestProject=true` and `OutputType=Exe` (xUnit v3 test projects are executables).
- `.editorconfig`: add a `[tests/**.cs]` section that relaxes only the analyzer rules test code cannot follow (for
  example CA1707 for `Method_Scenario_ExpectedResult` names, CA2007 `ConfigureAwait`). One comment per rule saying why.
  Do not relax anything else; do not touch `TreatWarningsAsErrors`.
- `Directory.Packages.props`: add the packages from preflight in a `Tests` item group; remove the unused
  `Aspire.Hosting.Testing` entry (D-52, R-07).

### 3. Test projects (add all three to `FinGuard.slnx` under a `/tests/` solution folder; delete `tests/.gitkeep`)
| Project | References | Packages |
| --- | --- | --- |
| `tests/FinGuard.UnitTests` | BuildingBlocks, ServiceDefaults | `xunit.v3` |
| `tests/FinGuard.Api.IntegrationTests` | Api | `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Microsoft.Extensions.Diagnostics.Testing` |
| `tests/FinGuard.ArchitectureTests` | Api, every module and Contracts project, BuildingBlocks, ServiceDefaults | `xunit.v3`, `TngTech.ArchUnitNET` |

Rules: test names `Method_Scenario_ExpectedResult`, Arrange-Act-Assert, one behaviour per test, xUnit `Assert` only.
Module test projects (for example a Compliance domain test project) are created with the first slice of each module.

### 4. Production edits needed for testability (no behaviour change)
- `FinGuard.Api/Program.cs`: append `public partial class Program;` so `WebApplicationFactory<Program>` can see it.
- `FinGuard.ServiceDefaults`: extract the two inline checks into `public static class CloudSeams` with
  `IsKeyVaultEnabled(IConfiguration)` and `IsAzureMonitorEnabled(IConfiguration)`; `Extensions.cs` and the startup
  logger call them. Behaviour and log lines stay identical.

### 5. Banned APIs (analyzer instead of prose, AGENTS.md conventions)
- Add `src/backend/Directory.Build.props` that imports the root props and adds
  `Microsoft.CodeAnalysis.BannedApiAnalyzers` (`PrivateAssets="all"`) plus `<AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />`.
- `src/backend/BannedSymbols.txt`, one line per symbol with a message:
  - `P:System.DateTime.Now`, `P:System.DateTime.UtcNow`, `P:System.DateTimeOffset.Now`, `P:System.DateTimeOffset.UtcNow`
    → "Use TimeProvider (AGENTS.md)".
  - `M:Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.EnableSensitiveDataLogging(System.Boolean)` → "Never log
    query parameters (D-35)".
- Prove it works: a temporary `DateTime.UtcNow` in any src project must fail the build with RS0030; remove it and report.
  Only `src/backend` gets the analyzer; tests may use the clock freely for now.

### 6. Unit tests (`FinGuard.UnitTests`)
| Area | Tests |
| --- | --- |
| `Result` | `Success_NoError_IsSuccessTrue`; `Failure_WithError_IsFailureAndKeepsError`; `Failure_NullError_Throws` |
| `Result<T>` | `Value_OnSuccess_ReturnsValue`; `Value_OnFailure_ThrowsInvalidOperationException`; implicit conversion from `T` succeeds; implicit conversion from `Error` fails |
| `Error` | each factory sets the matching `ErrorType`, code and description (one `[Theory]`) |
| `ResultHttpExtensions` | `[Theory]` over the four `ErrorType` values: status 400/404/409/422, `detail` = description, extension `code` = error code; success without value → 204; success with value → the `onSuccess` result |
| `CloudSeams` | Key Vault enabled only for a non-blank `KeyVault:Uri`; Azure Monitor enabled only for a non-blank `APPLICATIONINSIGHTS_CONNECTION_STRING` (`[Theory]` with null, empty, whitespace, value) |

Inspect the returned `IResult` through its typed result (`ProblemHttpResult`, `NoContent`); no HTTP server needed.

### 7. Integration tests (`FinGuard.Api.IntegrationTests`)
- Fixture: one `PostgreSqlContainer` (Testcontainers, image `postgres:18.3`, the tag Aspire uses per the session 6 delta) and one
  `WebApplicationFactory<Program>` per test assembly (xUnit v3 assembly fixture). Environment `Development`, so
  migrations run and `/health` is mapped. Pass the connection string with
  `builder.UseSetting("ConnectionStrings:finguard", ...)` (the Api reads it while building; verify that `UseSetting`
  reaches that read and report if another hook is needed).
- A test-only endpoint that throws is added through an `IStartupFilter` registered in the factory, never in the Api.
- Capture logs with `FakeLogCollector` (`Microsoft.Extensions.Diagnostics.Testing`).

| Test | Proves |
| --- | --- |
| `Health_DatabaseUp_Returns200` | Api boots against real PostgreSQL; health checks pass |
| `Startup_Development_AppliesInitialSchemaPerModule` | schemas `tenancy`, `portfolio`, `compliance`, `advisory` each have `__ef_migrations_history` with exactly one row (`InitialSchema`) |
| `Startup_Always_CreatesWolverineStorage` | schema `wolverine` contains `wolverine_incoming_envelopes` and `wolverine_outgoing_envelopes` |
| `UnhandledException_AnyRequest_Returns500ProblemWithoutDetails` | 500, `application/problem+json`, has `traceId`, no `detail`, the exception message is absent from the body |
| `UnhandledException_AnyRequest_DoesNotLogExceptionMessage` | the log record has the exception type and trace id; no captured log contains the exception message |
| `UnknownRoute_AnyRequest_Returns404Problem` | 404 `application/problem+json` with `traceId` |
| `Startup_WithoutCloudSettings_DisablesBothSeams` | no Key Vault configuration provider is registered; the startup logger reports both seams disabled |

Use a unique exception message per test (for example a GUID) so the "not in body / not in logs" checks are meaningful.
Query PostgreSQL with Npgsql directly from the test (read-only `information_schema` and history tables).

### 8. Architecture tests (`FinGuard.ArchitectureTests`)
Load the architecture once (static field). Assembly-level rules use reflection (`Assembly.GetReferencedAssemblies()`);
type-level rules use ArchUnitNET core with `Assert.True(rule.HasNoViolations(Architecture))`. For a rule whose subject
set is still empty (Compliance `Domain/` has no types yet), make it pass on an empty set (ArchUnitNET's
`WithoutRequiringPositiveResults()` or the current equivalent) and keep it, so it starts guarding in Phase 3.

| Rule (from `AGENTS.md`) | Test |
| --- | --- |
| Contracts depend on nothing internal | every `FinGuard.*.Contracts` assembly references no other `FinGuard.*` assembly |
| Modules talk only through Contracts (3) | a module assembly references, among `FinGuard.*`, only `FinGuard.BuildingBlocks`, its own Contracts and other modules' Contracts |
| BuildingBlocks has no business rules (ADR-0001) | `FinGuard.BuildingBlocks` references no other `FinGuard.*` assembly |
| No MediatR (9) | no loaded `FinGuard.*` assembly references an assembly whose name starts with `MediatR` |
| No generic repositories (9) | no type in `FinGuard.*` is a generic type definition whose name contains `Repository` |
| One schema and DbContext per module (4) | each module assembly has exactly one non-public `DbContext` subclass whose `const Schema` equals the lowercase module name |
| Compliance domain depends on nothing (2) | types in namespace `FinGuard.Compliance.Domain` do not depend on `Microsoft.EntityFrameworkCore`, `Wolverine` or `Microsoft.AspNetCore` |

Discover modules by convention (assemblies named `FinGuard.<Module>` with a sibling `FinGuard.<Module>.Contracts`), so
a fifth module is covered without editing the tests. Each failing rule must name the offending assembly or type.
CI builds Release; if any rule behaves differently in Release than in Debug, report it instead of switching CI.

### 9. CI
- Backend job keeps `dotnet test --no-build -c Release`. If MTP mode requires another form (for example
  `--solution FinGuard.slnx`), use the documented one and report it.
- Docker is available on `ubuntu-latest`; no service container is needed (Testcontainers starts PostgreSQL).
- Do not add coverage, TRX upload or test splitting in this brief.

### 10. Rules and docs
- Fix the Compliance rule path so it matches future test projects such as `tests/FinGuard.Compliance.Tests/`:
  `.claude/rules/compliance-domain.md` `paths:` → `tests/**/*Compliance*/**`, and the same glob in
  `.cursor/rules/compliance-domain.mdc` (same commit, `docs/ai-rules.md` sync rule; update its table).
- `.claude/rules/testing.md` and `.cursor/rules/testing.mdc` (same commit): add three short rules: xUnit v3 + built-in
  `Assert`, no mocking libraries without an ADR; integration tests use the shared PostgreSQL fixture, never an
  in-memory provider; architecture tests discover modules by convention.
- `AGENTS.md` → Commands: `dotnet test` needs Docker running; filtering syntax under MTP (one example).
- `docs/decisions.md`: prepend Appendix A verbatim, then your execution delta on top
  ("Delta 2026-11-xx (session 10: Brief #4 executed)": versions, deviations, findings, test count and duration).

## Acceptance criteria
- [ ] Branch `test/test-stack-and-architecture-tests`, working tree clean, commits follow the commit plan.
- [ ] `dotnet restore`, `dotnet format --verify-no-changes --no-restore`, `dotnet build -c Release` (zero warnings) pass.
- [ ] `dotnet test --no-build -c Release` runs in MTP mode (`dotnet test --help` shows the Microsoft.Testing.Platform
      command) and every test passes; report the count per project and the total duration.
- [ ] Each architecture rule was seen failing once: temporarily add a forbidden reference (for example Portfolio →
      Compliance internals) and an extra DbContext, run the tests, revert. Report the failure messages.
- [ ] The RS0030 probe from task 5 failed the build, then was removed.
- [ ] No production behaviour change: `aspire run` still starts `postgres`, `finguard`, `api`, `web`; startup logs
      are unchanged.
- [ ] `Aspire.Hosting.Testing` is gone from `Directory.Packages.props`; no `PackageReference` has a `Version`.
- [ ] Frontend untouched (`git diff --stat main -- src/frontend` is empty).
- [ ] CI `backend` job green on the pushed branch (owner checks after push).

## Rules to respect
`AGENTS.md`, `.claude/rules/testing.md`, `.claude/rules/backend-vsa.md`, ADR-0001, ADR-0004, ADR-0007,
`docs/git-workflow.md`, `docs/ai-rules.md`. English everywhere. Never log or assert on monetary values (none exist yet).

## Out of scope
Tenancy, `ITenantContext`, `ITenantOwned`, tenant query filters and tenant-isolation tests (Brief #5); the Wolverine
outbox spike (Phase 1); slice tests; Aspire AppHost tests (`Aspire.Hosting.Testing`); browser end-to-end tests; code
coverage and reporting; frontend changes; mutation testing; performance tests.

## Commit plan (Conventional Commits, no push)
1. `test: configure microsoft testing platform and test projects`
2. `refactor: extract cloud seam checks and expose program for tests`
3. `chore: ban clock and sensitive logging apis in backend code`
4. `test: add unit tests for results, http mapping and cloud seams`
5. `test: add api integration tests with postgresql container`
6. `test: add architecture tests for module boundaries`
7. `docs: update testing rules, commands and decisions log`

## Report back (final message)
- Plan versus what you did; every deviation with the reason and the doc you followed.
- Table of resolved versions (package, version, date).
- Output of the acceptance checks, the architecture-rule failure messages and the RS0030 probe.
- Your execution delta (also written into `docs/decisions.md`).
- The exact commands for the owner, not run by you:
  ```powershell
  git push -u origin test/test-stack-and-architecture-tests
  gh pr create --base main --head test/test-stack-and-architecture-tests `
    --title "test: add test stack, architecture tests and skeleton tests" `
    --body-file .github/pull_request_template.md
  ```

---

## Appendix A — Decisions delta (planning session 9), paste verbatim into `docs/decisions.md`

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
