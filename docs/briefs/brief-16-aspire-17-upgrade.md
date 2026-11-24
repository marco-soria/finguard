# Brief #16 — Upgrade Aspire 13.6 to 17 and migrate the AppHost off the obsolete SDK (chore, between Phases 3 and 4)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-16-aspire-17-upgrade.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-16-aspire-17-upgrade.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `chore/aspire-17` |
| PR title (exact, becomes the squash commit) | `chore: upgrade Aspire to 17 and move the AppHost to Microsoft.NET.Sdk` |
| Scope | Tooling only. No feature, no domain change. Closes R-08 and R-31 (D-185) |

---

## Goal
Move every Aspire package and the Aspire CLI from 13.6.5 to the latest stable 17.0.x, replace the obsolete
`Aspire.AppHost.Sdk` with `Microsoft.NET.Sdk` plus explicit packages, replace `AddProject<Projects.FinGuard_Api>` with
`AddDotnetProject`, keep the local developer experience identical (`aspire run`, `scripts/dev-session.ps1`, the web
proxy, the dev database), and update the docs.

## Context
- Read first: `AGENTS.md`, `CLAUDE.md`, `docs/dev-environment.md`, `scripts/dev-session.ps1`, `docs/decisions.md`
  (newest delta: session 24 executed; D-37, D-47, D-132 to D-135, D-185, the session 22 majors review).
- Today: `src/backend/FinGuard.AppHost/FinGuard.AppHost.csproj` uses `Sdk="Aspire.AppHost.Sdk/13.6.5"` (literal version,
  R-08), `AspireUseCliBundle=true`, a `UserSecretsId`, packages `Aspire.Hosting.JavaScript` and
  `Aspire.Hosting.PostgreSQL`, and a `ProjectReference` to the Api. `AppHost.cs`: `AddPostgres("postgres")
  .WithDataVolume()`, `AddDatabase("finguard")`, `AddProject<Projects.FinGuard_Api>("api")` with `WithReference`,
  `WaitFor`, `WithHttpHealthCheck("/health")`, and `AddJavaScriptApp("web", ...).WithPnpm(...).WithHttpEndpoint(env:
  "PORT").WithReference(api).WaitFor(api)`. `Directory.Packages.props` has `AspireVersion` 13.6.5 for
  `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.JavaScript`, `Aspire.Npgsql`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`;
  no `Aspire.Hosting.AppHost` entry (D-37). Integration tests use Testcontainers `postgres:18.3`, not Aspire.
- The web dev proxy reads the Api URL from the environment variables Aspire injects for `WithReference(api)` (D-43,
  D-47). The Aspire CLI is the .NET global tool `aspire.cli` 13.6.5 (D-135). `dev-session.ps1` calls `aspire start`,
  `aspire stop --apphost`, `aspire ps --format Json`.
- Facts checked on 2026-11-24 (re-check on NuGet and aspire.dev at execution):
  - Aspire 17.0 needs .NET 10 for AppHosts and Aspire packages; orchestrated apps need not retarget. `AddProject`,
    `AddCSharpApp` and `Aspire.AppHost.Sdk` are obsolete but still work (`ASPIRE012`, CS0618). C# AppHosts require the
    CLI bundle (`ASPIRE013`). Sources: https://devblogs.microsoft.com/aspire/whats-new-aspire-17/,
    https://github.com/microsoft/aspire/releases
  - Migration: set `Sdk="Microsoft.NET.Sdk"`, add `PackageReference` to `Aspire.Hosting.AppHost` and
    `Aspire.Hosting.Dotnet`, replace resource `ProjectReference` items with `AddDotnetProject("api",
    "<path to csproj>")` returning `DotnetProjectResource`; `aspire update` does not remove the SDK for you;
    `aspire update --migrate` reports ASPIRE012. Source: https://aspire.dev/whats-new/aspire-17/
  - Required parameters now reject empty values (`MissingParameterValueException`). Default PostgreSQL image moves from
    `18.3` to `18.6`. Source: https://aspire.dev/whats-new/aspire-17/
  - Moving to `AddDotnetProject` changes resource types and build ownership (Aspire builds the project);
    `aspire run --watch` does not support it yet. Source: https://devblogs.microsoft.com/aspire/whats-new-aspire-17/
  - Secret storage moves to `~/.aspire/secrets` (secondary source:
    https://startdebugging.net/2026/11/aspire-17-addproject-is-obsolete-move-to-adddotnetproject/). The `postgres`
    password Aspire generated in 13.x lives in the AppHost user secrets (`Parameters:postgres-password`); the existing
    data volume was initialised with it. `aspire secret set|get|list|path` manage the new store
    (https://aspire.dev/reference/cli/commands/aspire-secret/).

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Depth of migration | Full: `Microsoft.NET.Sdk` + explicit `Aspire.Hosting.AppHost` and `Aspire.Hosting.Dotnet`, `AddDotnetProject` for the Api, no `ProjectReference` from the AppHost | Staged: bump packages and suppress ASPIRE012 / CS0618 (warnings as errors would need suppressions that stay forever) |
| Versions | All Aspire packages on `AspireVersion` = latest stable 17.0.x; `Aspire.Hosting.AppHost` and `Aspire.Hosting.Dotnet` get CPM entries (supersedes D-37; closes R-08: no literal version in the csproj any more) | `aspire update` rewriting versions per project (CPM owns versions) |
| Postgres image | Accept 18.6 for Aspire and move Testcontainers to `postgres:18.6` too (same minor everywhere). 18.3 → 18.6 is a minor upgrade inside 18: the data directory format does not change | Pinning 18.3 with `WithImageTag` |
| Dev database password | Preserve the existing volume: before the upgrade, copy `Parameters:postgres-password` from the 13.x user secrets into the Aspire 17 store with `aspire secret set` **if 17 does not pick it up by itself** (check first with `aspire secret list`, never print the value). If the container still fails to authenticate, ask the owner to approve a dev volume reset (data is disposable) | Resetting the volume without checking; printing the password in the report |
| `UserSecretsId` | Keep it in the AppHost csproj unless the 17 docs say it must go; record what 17 actually uses (`aspire secret path`) | |
| Aspire CLI | `dotnet tool update -g Aspire.Cli --version <17.0.x>`; it changes the owner's machine, so ask the owner before running it. D-135's Git Bash workaround (`aspire.cmd`) is re-checked, not assumed | `aspire update --self` (meant for script/native installs; ours is a .NET tool) |
| `dev-session.ps1` | Keep its interface (`-Snapshot`, `-StartAspire`, `-Report`, `-Stop`, `-WhatIf`); adjust only what 17 changes (CLI JSON shape, process names, DCP command line, where the Api now runs from) | Rewriting it |
| `--watch` | Not used today; note in `docs/dev-environment.md` that 17 does not support it with `AddDotnetProject` | |

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot` first, Aspire only through `-StartAspire`,
  `-Stop` before the final message, Close-out block with "Still running: none". Run the proof below with the **new**
  CLI.
- Never print secrets (passwords, connection strings, tokens) in the terminal transcript or the report; check
  existence and length only.
- Never run `npm`, `yarn` or `npx`. Never push, never open PRs, never change GitHub settings.
- No `docker volume rm` without the owner's explicit "ok" naming the volume.
- `docs/decisions.md` is append-only (D-137). Ask before deviating; record approved deviations.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c chore/aspire-17
scripts/dev-session.ps1 -Snapshot
dotnet build -c Release; dotnet test --no-build -c Release      # baseline counts: 442 backend
aspire --version                                                 # 13.6.5
dotnet user-secrets list --project src/backend/FinGuard.AppHost | Select-String 'Parameters:postgres-password' | Measure-Object   # existence only
docker volume ls --format '{{.Name}}' | Select-String -Pattern 'postgres'                                                            # name only
```
Then read the Aspire 17 release notes, the 17.0 change log and the `DotnetProjectResource` migration guide on
aspire.dev, and list in the plan every item that touches this repo.

## Tasks

### 1. Packages and AppHost project
- `Directory.Packages.props`: `AspireVersion` → 17.0.x; add `Aspire.Hosting.AppHost` and `Aspire.Hosting.Dotnet`
  on `$(AspireVersion)`; replace the D-37 comment.
- `FinGuard.AppHost.csproj`: `Sdk="Microsoft.NET.Sdk"`, `TargetFramework` (net10.0, from `Directory.Build.props` if it
  sets it), `OutputType Exe`, `AspireUseCliBundle` kept, the two new package references, no `ProjectReference` to the
  Api. Remove the "keep equal to AspireVersion" comment (R-08 gone).
- `AppHost.cs`: `builder.AddDotnetProject("api", "../FinGuard.Api/FinGuard.Api.csproj")` with the same chain
  (`WithReference(database)`, `WaitFor(database)`, `WithHttpHealthCheck("/health")`). Resource names unchanged (D-47).
  If an API in the chain does not exist on `DotnetProjectResource`, stop and report.
- Build with warnings as errors: no ASPIRE012/ASPIRE013/CS0618 left, no suppressions added.

### 2. Run-time checks
- Dev password (decision above), then `-StartAspire` with the new CLI.
- Verify: `postgres` image tag 18.6, the existing dev data is still there (count rows in `portfolio.submissions`, report
  the count only), migrations up to date, `api` healthy, `web` ready, the dev proxy reaches the Api (the environment
  variables it reads still exist with the same names; if they changed, update `proxy.conf.js` and say so),
  `/dev/token` works, an upload of `samples/portfolios/valid-balanced.csv` is evaluated (status only).
- `pnpm e2e` twice: all 8 flows green.

### 3. `dev-session.ps1`
- `-Report` must still show the AppHost as `agent`, DCP, the Api process and the Aspire container as `new`; `-Stop` must
  leave "Nothing of this repo is running." Fix only what 17 changed. Paste the proof
  (`-Snapshot`, `-StartAspire`, `-Report`, `-Stop`, `-Report`) into the delta, with PIDs and tokens redacted.

### 4. Tests
- Testcontainers image `postgres:18.6` in `ApiFixture` (and anywhere else `18.3` appears in tests or docs).
- Full `dotnet test -c Release`: same counts as the baseline (442) and green.

### 5. Docs
- `docs/dev-environment.md`: CLI install/update with the 17 version, secret store (`aspire secret`), `--watch` note,
  Git Bash note re-checked.
- `AGENTS.md` / `CLAUDE.md`: only if a command they list changed.
- `README.md`: versions line (Aspire 17) if it mentions one.
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta (resolved versions, CLI checks,
  password outcome, proof) on top.

## Tests that prove it
No new behaviour, so no new tests. Proof is: the build with warnings as errors, the unchanged backend suite (442) on
`postgres:18.6`, Vitest (103), `pnpm e2e` twice (8 flows), the manual run-time checks of task 2 and the
`dev-session.ps1` proof of task 3. CI must stay green with no workflow change (CI does not run Aspire).

## Acceptance criteria
- No `Aspire.AppHost.Sdk`, no `AddProject<`, no `Projects.` namespace and no ASPIRE0xx/CS0618 suppression in the repo.
- `aspire --version` reports 17.0.x in PowerShell (and through `aspire.cmd` in Git Bash if D-135 still applies).
- Dev data preserved (or reset with the owner's written approval, recorded in the delta).
- Backend 442 green, Vitest 103 green, E2E 8 green twice; Close-out with "Still running: none".

## Out of scope
Any feature; Aspire 17's new dashboard features, Kubernetes or deploy commands (Phase 5); `aspire agent init`;
Node 26 and pnpm 12 (Phase 5, D-186/D-187); other dependency updates (Dependabot, D-189).

## Commit plan
1. `chore: upgrade Aspire packages to 17` (CPM versions, AppHost project and `AppHost.cs`)
2. `chore: align dev tooling with Aspire 17` (`dev-session.ps1`, proxy if needed, Testcontainers image)
3. `docs: update the dev environment guide for Aspire 17` (docs and decisions deltas)

## Pull request
- **Title (exact):** `chore: upgrade Aspire to 17 and move the AppHost to Microsoft.NET.Sdk`
- **Body:** `.git/finguard-pr-body.md` from the template; Summary says "tooling only, no behaviour change" and names
  R-08/R-31 closed.
- Close-out block with:
  `git push -u origin chore/aspire-17` and
  `gh pr create --base main --head chore/aspire-17 --title "chore: upgrade Aspire to 17 and move the AppHost to Microsoft.NET.Sdk" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first (with the list of 17 changes that touch this repo); wait for approval. Ask before the global CLI update and
before any volume reset. At the end: resolved versions, CLI checks in both shells, password outcome (no values), test
counts, E2E runs, `dev-session.ps1` proof, the execution delta and the Close-out block.

---

## Appendix A — Decisions delta (session 25: Brief #16 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-222 | Aspire 17.0.x everywhere; AppHost on `Microsoft.NET.Sdk` with explicit `Aspire.Hosting.AppHost` and `Aspire.Hosting.Dotnet`; Api registered with `AddDotnetProject`; no AppHost `ProjectReference` | Supersedes D-36 (AppHost SDK) and D-37 (implicit AppHost package). Closes R-08 and R-31 |
| D-223 | PostgreSQL 18.6 for both Aspire and Testcontainers | Minor upgrade within 18 |
| D-224 | The dev database volume is preserved across the upgrade by carrying the generated password into the Aspire 17 secret store; a reset needs the owner's approval | |
| D-225 | The Aspire CLI is updated as a .NET global tool only with the owner's approval; `dev-session.ps1` keeps its interface | |
| D-226 | Phase 4 starts after this chore; the decisions log copy in the Claude Project is synced at each phase close (session 25: synced after Phase 3) | Amends D-56's "every session" |

### Risks and flags
- **R-43 Generated password vs existing volume.** If Aspire 17 does not read the 13.x user secret, the new password
  will not match the volume and PostgreSQL rejects the login. Mitigation as in D-224.
- **R-44 `DotnetProjectResource` differences.** Build ownership moves to Aspire; health checks, environment variable
  names for references and launch-profile handling may differ. The run-time checks of task 2 cover them.

### Next step
Phase 4 planning: rebalance engine in Compliance (deterministic, target ratio, headroom allocation, D-03/D-04),
Advisory module with `IAdvisoryNarrator` (template narrator first), the R-40 backfill question, and the local model
choice (open question 1 of the kickoff).
