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
