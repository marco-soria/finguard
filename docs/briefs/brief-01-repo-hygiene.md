# Brief #1 — Repo hygiene, folder skeleton, Central Package Management, base CI/CD

How to run it (Windows, PowerShell 7, from the repo root):
1. Make sure the Phase 0 draft files (this file included) are already in the repo root.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-01-repo-hygiene.md and execute it. Present your plan first and wait for my approval.`

---

## Goal
Turn an empty folder into a clean, reproducible repository: Git configuration, folder skeleton, shared build
settings with Central Package Management (CPM), editor/line-ending rules, and a base CI pipeline that is green
on day one even though there is no code yet. No application code in this PR.

## Context
- Project: FinGuard (modular monolith, .NET 10, Angular 22, Aspire, PostgreSQL). Read `AGENTS.md`, `CLAUDE.md`,
  `docs/adr/` and `docs/decisions.md` first. They are binding.
- Local-first strategy (ADR-0005): CI only for now. CD is a placeholder until Phase 5. Nothing here touches Azure.
- Stack facts verified on 2026-11-13: Angular 22 needs TypeScript 6 and a recent Node (use Node 24 LTS); pnpm is the
  only package manager; `pnpm/action-setup@v6` reads the `packageManager` field and does not install Node.
- The user works on Windows. Use PowerShell 7 (`pwsh`) syntax, never bash-only syntax. Repo files use LF line endings.

## Environment rules
- Never run `npm`, `yarn` or `npx` (denied in `.claude/settings.json`). Use `pnpm`.
- Never push, never create the GitHub repository, never open PRs. Stop after local commits.
- Never invent versions. Resolve every version from an authoritative source at execution time and record it.
- Use only stable packages (no `-preview`, `-alpha`, `-beta`, `-rc` suffixes).

## Preflight (run first, report results, stop if something is missing)
```powershell
git --version
dotnet --list-sdks          # a 10.0.x SDK must be present
node --version              # expect 24.x; Angular 22 needs a version listed at https://angular.dev/reference/versions
pnpm --version
docker version --format '{{.Server.Version}}'
pwsh --version
gh --version                # optional
```
Also confirm these files exist in the repo root: `AGENTS.md`, `CLAUDE.md`, `.claude/settings.json`,
`.claude/rules/*.md`, `.cursor/rules/*.mdc`, `docs/adr/*.md`, `docs/architecture/overview.md`, `docs/decisions.md`.
If the folder is not a Git repository, run `git init -b main` and `git config core.autocrlf false` (local only).

## Tasks

### 1. Branch
Create branch `chore/repo-hygiene`.

### 2. Folder skeleton (empty folders get a `.gitkeep`)
```
src/backend/FinGuard.AppHost/
src/backend/FinGuard.ServiceDefaults/
src/backend/FinGuard.Api/
src/backend/BuildingBlocks/
src/backend/Modules/Tenancy/
src/backend/Modules/Portfolio/
src/backend/Modules/Compliance/
src/backend/Modules/Advisory/
src/frontend/
tests/
scripts/
docs/briefs/        (this file already lives here)
.github/workflows/
```
Do not create .NET projects, solution files or the Angular app. Those are PR #2 and PR #3.

### 3. `.gitattributes`
```
* text=auto eol=lf
*.cmd text eol=crlf
*.bat text eol=crlf
*.png binary
*.jpg binary
*.ico binary
*.woff2 binary
*.zip binary
```

### 4. `.gitignore`
Start from the official template: `dotnet new gitignore`. Then append these blocks:
```
# Node / Angular / pnpm
node_modules/
.angular/
dist/
coverage/
.pnpm-store/

# Secrets and local settings
.env
.env.*
!.env.example
appsettings.*.local.json
secrets.json
.claude/settings.local.json
CLAUDE.local.md

# Aspire / tooling
.aspire/
*.log
TestResults/
.vs/
.idea/
.vscode/*
!.vscode/extensions.json
```
Do not ignore `.claude/` or `.cursor/` (they are tracked), except `.claude/settings.local.json`.

### 5. `.editorconfig`
Create at the repo root, `root = true`, with:
- `[*]`: utf-8, `end_of_line = lf`, final newline, trim trailing whitespace, spaces, `indent_size = 2`.
- `[*.{cs,csx}]`: `indent_size = 4`. `[*.md]`: do not trim trailing whitespace. `[*.{cmd,bat}]`: `end_of_line = crlf`.
- C# style: file-scoped namespaces (`warning`), usings outside namespace, `System` usings first, braces required,
  `var` preferred (`suggestion`), private fields `_camelCase` (`warning`) via `dotnet_naming_*` rules, no `this.` qualification.
- `[**/Migrations/**.cs]`: `generated_code = true` and `dotnet_analyzer_diagnostic.severity = none`.
Keep it short and only include rules you can verify with the probe in task 9.

### 6. `global.json`
Pin the SDK to the installed 10.0.x version reported by `dotnet --version` and allow feature-band roll-forward:
```json
{
  "sdk": {
    "version": "<output of dotnet --version>",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

### 7. `.node-version` (repo root)
Content: `24`. Used by CI (`setup-node`) and read by Aspire for Docker builds.

### 8. `Directory.Build.props` (repo root)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);NU1900;NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
  </PropertyGroup>
</Project>
```
NuGet audit warnings stay visible but must not break the build. If an analyzer rule is pure noise in the probe,
relax it in `.editorconfig` with a one-line justification; do not turn off `TreatWarningsAsErrors`.

### 9. Version resolver script and `Directory.Packages.props`
Create `scripts/Get-LatestNuGetVersion.ps1`:
```powershell
param(
  [Parameter(Mandatory)][string]$Id,
  [switch]$IncludePrerelease
)
$url = "https://api.nuget.org/v3-flatcontainer/$($Id.ToLowerInvariant())/index.json"
$versions = (Invoke-RestMethod -Uri $url).versions
if (-not $IncludePrerelease) { $versions = $versions | Where-Object { $_ -notmatch '-' } }
$versions | Select-Object -Last 1
```
Use it to resolve the latest **stable** version of every package below. Create `Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <!-- Resolved on <date> with scripts/Get-LatestNuGetVersion.ps1. Stable versions only. -->
    <AspireVersion>…</AspireVersion>
    <WolverineVersion>…</WolverineVersion>
    <MicrosoftNet10Version>10.0.x</MicrosoftNet10Version>
  </PropertyGroup>
  <ItemGroup Label="Aspire">…</ItemGroup>
  <ItemGroup Label="Messaging">…</ItemGroup>
  <ItemGroup Label="Data">…</ItemGroup>
  <ItemGroup Label="Auth and config">…</ItemGroup>
  <ItemGroup Label="Observability">…</ItemGroup>
  <ItemGroup Label="AI">…</ItemGroup>
</Project>
```
Declare `PackageVersion` entries (no `PackageReference` anywhere yet) for:
- Aspire (one shared `$(AspireVersion)`): `Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`,
  `Aspire.Hosting.JavaScript`, `Aspire.Hosting.Testing`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`.
- Messaging (one shared `$(WolverineVersion)`; if the latest stable differs between packages, use the newest version
  that exists for all of them): `WolverineFx`, `WolverineFx.Postgresql`, `WolverineFx.EntityFrameworkCore`.
- Data: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`
  (EF Core 10.x line).
- Auth and config: `Microsoft.AspNetCore.Authentication.JwtBearer` (10.x line), `Azure.Identity`,
  `Azure.Extensions.AspNetCore.Configuration.Secrets`.
- Observability: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`,
  `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`,
  `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Azure.Monitor.OpenTelemetry.AspNetCore`.
- AI: `Microsoft.Agents.AI` (1.x, stable only) and `Microsoft.Extensions.AI`.
Test-stack packages (test framework, Testcontainers, architecture tests) are added in PR #4 once the stack is confirmed.
If a package has no stable version, do not add it; list it in the report.

### 10. Base CI/CD
Resolve the latest major tag of each action at execution time (`gh api repos/<owner>/<repo>/releases/latest`, or the
action's releases page). Known good: `actions/checkout@v6`, `pnpm/action-setup@v6`. Look up `actions/setup-dotnet`
and `actions/setup-node`; do not guess.

`.github/workflows/ci.yml`:
- Triggers: `push` to `main`, and `pull_request`. `concurrency` cancels superseded runs. Top-level
  `permissions: contents: read`.
- Job `hygiene` (ubuntu): checkout; fail if the repo contains `package-lock.json`, `yarn.lock` or
  `npm-shrinkwrap.json` (only `pnpm-lock.yaml` is allowed).
- Job `detect` (ubuntu): outputs `dotnet` = true when `git ls-files '*.sln' '*.slnx'` is non-empty, and `frontend` = true
  when `src/frontend/finguard-web/package.json` exists.
- Job `backend` (needs `detect`, runs if `dotnet == 'true'`): checkout; `actions/setup-dotnet` with
  `global-json-file: global.json`; `dotnet restore`; `dotnet build --no-restore -c Release`;
  `dotnet test --no-build -c Release`.
- Job `frontend` (needs `detect`, runs if `frontend == 'true'`): checkout; `pnpm/action-setup` with
  `package_json_file: src/frontend/finguard-web/package.json` (no `version`, it comes from `packageManager`);
  `actions/setup-node` with `node-version-file: .node-version`, `cache: pnpm`,
  `cache-dependency-path: src/frontend/finguard-web/pnpm-lock.yaml`; then, with `working-directory`
  `src/frontend/finguard-web`: `pnpm install --frozen-lockfile`, `pnpm run --if-present lint`,
  `pnpm run --if-present test`, `pnpm run build`.
- Job `ci-gate` (`if: always()`, needs all others): fails when any needed job failed or was cancelled; skipped is OK.
  This is the single status check to require in branch protection.

`.github/workflows/cd.yml`: placeholder, `on: workflow_dispatch` only, one job that echoes that CD arrives in Phase 5.
No secrets, no Azure steps.

`docs/ci-cd.md` (short): what CI does today, and the Phase 5 CD plan: build images, push to a registry, deploy with
GitHub OIDC federated credentials (no stored secrets), a protected `demo` environment, and a manual teardown workflow.

### 11. Repo meta
- `README.md`: project summary (from `AGENTS.md`), status (Phase 0), prerequisites for Windows (Git, .NET 10 SDK, Node 24,
  pnpm via `corepack enable`, Docker Desktop, PowerShell 7), how AI rules are organized, links to `docs/`.
  Quick start says "arrives in PR #2".
- `.github/pull_request_template.md`: summary, linked ADR/brief, checklist (tests, architecture tests, ADR updated,
  `docs/decisions.md` delta noted).
- `.github/dependabot.yml`: ecosystems `nuget` (directory `/`) and `github-actions` (directory `/`), weekly, grouped
  minor and patch updates. Do NOT add the `npm` ecosystem yet: the frontend directory does not exist (added in PR #3).
- `.vscode/extensions.json` with recommended extensions: C# Dev Kit, Angular Language Service, EditorConfig,
  GitHub Actions.

### 12. Probe (proves props, CPM and analyzers work), then remove it
1. Create a throwaway class library at `src/backend/BuildingBlocks/_probe/_probe.csproj` with only a
   `PackageReference Include="Microsoft.Extensions.AI"` (no version) and one trivial class.
2. Run `dotnet restore`, `dotnet build -c Release`. Both must succeed with zero warnings.
3. Delete the probe folder. Do not commit it.

## Acceptance criteria
- [ ] Branch `chore/repo-hygiene` has the files above and the previously provided draft files; working tree clean.
- [ ] `dotnet --version` satisfies `global.json`; the probe built with zero warnings and zero errors.
- [ ] `Directory.Packages.props` has CPM enabled, versions resolved today, stable only, no `PackageReference` in the repo.
- [ ] `.gitattributes`, `.gitignore`, `.editorconfig`, `.node-version`, `global.json` exist and match the specs.
- [ ] `ci.yml` is valid YAML and passes `actionlint` if available; `cd.yml` has no secrets or Azure steps.
- [ ] With no solution and no frontend, the CI logic would result in `hygiene` green, `backend` and `frontend` skipped,
  `ci-gate` green (explain how you verified this).
- [ ] No npm, yarn or npx was used; no `package-lock.json` exists.
- [ ] No files were pushed; no secrets are present (`git grep -n -i -E "password|secret|connectionstring"` shows only docs).

## Rules to respect
`AGENTS.md`, `.claude/rules/*`, ADR-0001 to ADR-0006. English everywhere. Small commits.

## Out of scope
.NET projects and solution (PR #2), Angular app (PR #3), architecture tests (PR #4), tenant context and auth (PR #5),
frontend CI hardening (PR #6 reuses the job from this PR), any Azure resource, LICENSE choice (ask the owner).

## Commit plan (Conventional Commits, no push)
1. `chore: add repository configuration, folder skeleton and AI rules`
2. `chore: add build props and central package management`
3. `ci: add base CI workflow and CD placeholder`

## Report back (final message)
- Plan versus what you did, and any deviation with the reason.
- Table of resolved versions (package, version, date) and the tool versions found in preflight.
- Anything from the preflight that the owner must install or fix.
- A "Decisions delta" block (new decisions, open questions, next step) ready to paste into `docs/decisions.md`.
- The exact commands the owner should run to create the GitHub repository, push the branch and open the PR, plus a
  suggested branch protection rule that requires the `ci-gate` check. Do not run them.
