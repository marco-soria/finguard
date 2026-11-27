# Development environment

## Prerequisites (Windows)
- Docker Desktop, running (PostgreSQL for Aspire and for the integration tests).
- .NET SDK from `global.json` (10.0.403, `rollForward: latestFeature`).
- Node.js 24 LTS (`.node-version`).
- pnpm through Corepack (`corepack enable`); the version comes from `packageManager` in
  `src/frontend/finguard-web/package.json`. Never npm, yarn or npx.
- Aspire CLI as a .NET global tool, at the AppHost's Aspire version (`AspireVersion` in `Directory.Packages.props`,
  17.0.0 today). It must be the only Aspire CLI on the machine. Do not use `aspire update --self`: it is for the
  script and native installs, not for a .NET tool:
  ```powershell
  dotnet tool install -g Aspire.Cli --version 17.0.0   # first install
  dotnet tool update  -g Aspire.Cli --version <new>    # when AspireVersion changes
  aspire --version
  aspire doctor                                        # environment check
  ```

## Secrets
Since Aspire 17 the AppHost parameters (for example the generated `Parameters:postgres-password`, which the dev
database volume was initialised with) live in the Aspire secret store, not in .NET user secrets:
`%USERPROFILE%\.aspire\secrets\<UserSecretsId>\Development.json`. Aspire 17 imported the old user secrets the first
time it ran. Manage them with `aspire secret list|get|set|delete|path --apphost
src/backend/FinGuard.AppHost/FinGuard.AppHost.csproj`. Never print a value in a shared transcript.

## Shells
- **PowerShell 7 (recommended).** Every command in `AGENTS.md` is written for it.
- **Git Bash works**, with one difference: `Aspire.Cli` is a native tool (`Runner="executable"`), so the .NET SDK
  writes an `aspire.cmd` shim in `%USERPROFILE%\.dotnet\tools`, not an `aspire.exe`. Git Bash finds `.exe` files
  without the extension but not `.cmd`, so type `aspire.cmd`, or add `alias aspire=aspire.cmd` to `~/.bashrc`.
  Re-checked with Aspire CLI 17.0.0: still true. `scripts/dev-session.ps1` runs with `pwsh` from either shell.

## Run
```powershell
dotnet tool restore
aspire run          # owner; agents use: pwsh scripts/dev-session.ps1 -StartAspire
```
The dashboard URL (with login token) is printed on start; the `web` URL is in the dashboard. See `AGENTS.md`,
Commands, for tests, E2E and migrations.

The AppHost registers the Api with `AddDotnetProject` (Aspire 17), so Aspire builds it and runs it as
`FinGuard.Api.exe`. `aspire run --watch` does not support `AddDotnetProject` projects yet; we do not use it.

### Local model (ADR-0015)
The model is off by default (D-293). Opt in and the AppHost also starts an `ollama` container with the model
`narrator` (`llama3.2:3b`). The first start downloads the `ollama/ollama` image (about 1.5 to 3 GB) and the model (about
2.0 GB); both are cached, the model in the `ollama` data volume (never remove it to "clean up"). The Api does not wait
for the model: advisories are written from the template at once, and the model only adds a context paragraph when it
passes validation.

Cost on this machine: while it generates, the model uses about 3 GB of RAM and all 16 cores; one paragraph takes
about 6 to 12 seconds. `OLLAMA_KEEP_ALIVE=2m` (set by the AppHost) unloads it after two idle minutes instead of
Ollama's default five.
```powershell
pwsh scripts/dev-session.ps1 -StartAspire -WithAi   # agents: with the Ollama container and the AI context
$env:FinGuard__Ai__Enabled = 'true'; aspire run     # owner: the same switch by hand
dotnet user-secrets set "Parameters:narrator-model" "qwen2.5:3b" --project src/backend/FinGuard.AppHost  # another model
```
CI, the tests, the E2E run and a plain `-StartAspire` run without the model; nothing else changes.

## Pinned packages
Packages held below the line the rest of the solution uses. Each brief's preflight checks whether the condition to
move is met (`AGENTS.md`, Stack) and reports it; nobody upgrades them without approval.

| Package | Pinned | Reason | Move when |
| --- | --- | --- | --- |
| `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | 13.6.5 | No stable 17.x (R-45) | A stable 17.x is on NuGet |
| `CommunityToolkit.Aspire.OllamaSharp` | 13.6.1 | 17.0.0 needs `Microsoft.Extensions.AI` 11.x, which pulls .NET 11 abstractions into net10.0 (R-51, D-290) | A version needs only 10.x abstractions, or the solution moves to .NET 11 |

## Close a session
```powershell
pwsh scripts/dev-session.ps1 -Report   # what this repo has running: baseline / agent / new / unknown owner
pwsh scripts/dev-session.ps1 -Stop     # stops agent and new items; add -WhatIf to preview
```
`-Stop` never removes Docker volumes, never stops baseline items and leaves an "unknown owner" AppHost alone. Rules
for agents: `AGENTS.md`, "Closing a work session".

## Reset the dev database (only when asked)
The AppHost keeps PostgreSQL data in a named volume, `finguard.apphost-<hash>-postgres-data`. Reset it only when the
owner asks (for example after a regenerated, unreleased migration):
```powershell
pwsh scripts/dev-session.ps1 -Stop          # or stop your own `aspire run` with Ctrl+C
docker volume ls --filter name=finguard.apphost
docker volume rm <the finguard.apphost-...-postgres-data volume>
```
The next `aspire run` creates an empty database and the Api applies the migrations. Never remove other volumes.
