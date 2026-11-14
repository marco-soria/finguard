# Brief #3 — Angular 22 web app with pnpm, orchestrated by Aspire

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` (see Preflight) and save this file as `docs/briefs/brief-03-angular-web-app.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-03-angular-web-app.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/angular-web-app` |
| PR title (exact, becomes the squash commit) | `feat: add Angular web app with pnpm orchestrated by Aspire` |
| GitHub PR number (expected) | #4 (refer to work by brief number, D-28) |

---

## Goal
Add the Angular 22 single-page app at `src/frontend/finguard-web`, managed only with pnpm 11, and run it from the Aspire
AppHost next to the Api and PostgreSQL. In development the Angular dev server proxies `/api` to the Api, so the browser
never needs CORS. The app is an empty shell: one lazy-loaded home page, no API calls, no auth, no UI library.
CI's existing `frontend` job starts running because `package.json` now exists.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/frontend.md`, `.claude/rules/testing.md`,
  `docs/decisions.md` (newest delta first), `docs/git-workflow.md`, `docs/ai-rules.md`, ADR-0005.
- Brief #2 is merged: `FinGuard.slnx`, AppHost (`AppHost.cs`, Aspire 13.6.5), Api resource named `api`,
  PostgreSQL resource. `Aspire.Hosting.JavaScript` already has a `PackageVersion` in `Directory.Packages.props`.
- CI (`.github/workflows/ci.yml`) already has a `frontend` job: `pnpm/action-setup` reads `packageManager` from
  `src/frontend/finguard-web/package.json`, `setup-node` uses `.node-version` (`24`), then
  `pnpm install --frozen-lockfile`, `pnpm run --if-present lint`, `pnpm run --if-present test`, `pnpm run build`.
  The `detect` job switches it on when that `package.json` exists. Do not change the job unless it fails.
- Decisions for this brief (Appendix A): pnpm **11.x** pinned through the legacy `packageManager` field (single-document
  lockfile, full Dependabot support); dev proxy instead of CORS; pnpm `shellEmulator` so `$PORT` works on Windows.
- Facts verified on 2026-11-14 (re-check at execution):
  - Angular 22 requires TypeScript `>=6.0.0 <6.1.0` and Node `^22.22.3 || ^24.15.0 || ^26.0.0`.
    Source: https://angular.dev/reference/versions
  - Vitest is the default unit-test runner for new Angular projects. Source: https://angular.dev/guide/testing/migrating-to-vitest
  - angular-eslint 22 targets Angular 22 + TS 6, drops Node 20 and the legacy eslintrc format (flat config only).
    Source: https://github.com/angular-eslint/angular-eslint/releases
  - Aspire: `AddJavaScriptApp(...).WithPnpm(installArgs: ["--frozen-lockfile"])`; `runScriptName` defaults to `dev`;
    `WithHttpEndpoint(env: "PORT")` passes the port through an environment variable. Aspire's Angular sample proxies
    `/api` with a `proxy.conf.js` that reads `API_HTTPS` or `API_HTTP`.
    Sources: https://aspire.dev/integrations/frameworks/javascript , https://aspire.dev/fundamentals/environment-variables
  - pnpm writes a two-document `pnpm-lock.yaml` when a project declares `devEngines.packageManager` or pins pnpm 12+
    through `packageManager`. Dependabot does not read the dependencies in that layout yet (no security alerts).
    Sources: https://pnpm.io/lockfile , https://github.blog/changelog/2025-01-09-dependabot-now-supports-npm-12-pnpm-11-and-12-and-bun-1-4/
  - pnpm `shellEmulator: true` (in `pnpm-workspace.yaml`) runs scripts with a cross-platform bash-like shell.
    Source: https://pnpm.io/cli/run
- The user works on Windows. Use PowerShell 7 syntax. Repo files use LF.

## Environment rules
- Never run `npm`, `yarn` or `npx`. Use `pnpm`, `pnpm exec`, `pnpm dlx`. No `package-lock.json`, `yarn.lock` or
  `npm-shrinkwrap.json` may exist anywhere.
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Never invent versions. Resolve them from the registry at execution time (`pnpm view <pkg> version`,
  `pnpm view <pkg> versions --json`) and record them. Stable releases only.
- Do not use `devEngines.packageManager` (it forces the two-document lockfile).

## Preflight (run first, report results, stop if something is missing)
```powershell
git switch main; git pull; git status --short     # clean tree, Brief #2 squash commit on top
node --version                                    # must satisfy ^24.15.0
pnpm --version                                    # any; the pinned 11.x is switched in automatically
aspire --version                                  # 13.6.x (.NET global tool Aspire.Cli)
docker version --format '{{.Server.Version}}'
pnpm view pnpm dist-tags --json                   # take the latest 11.x stable for packageManager
pnpm view @angular/cli dist-tags --json           # take the latest 22.x stable
pnpm view angular-eslint dist-tags --json         # latest 22.x stable
```
Stop and report if the latest stable `@angular/cli` is not 22.x, if `angular-eslint` has no stable 22.x, or if Node does
not satisfy the Angular 22 range.

## Tasks

### 1. Branch
`git switch -c feat/angular-web-app`

### 2. Scaffold with the Angular CLI (from the repo root)
```powershell
pnpm dlx @angular/cli@<22.x resolved> new finguard-web `
  --directory src/frontend/finguard-web `
  --package-manager pnpm --skip-git --routing --style css --ssr false
```
- Choose: no SSR/SSG, no zone.js (Angular's default for new apps), Vitest (default runner), and **no AI tool
  configuration** (pass `--ai-config none` or answer "None"; our rules live in `AGENTS.md` and `.claude/`/`.cursor/`).
- If a flag is rejected by the resolved CLI, check `ng new --help`, use the equivalent and report it.
- Remove the `.gitkeep` from `src/frontend/`.

### 3. Clean the generated workspace
- Delete `src/frontend/finguard-web/.editorconfig` (the root `.editorconfig` applies) and the generated `.vscode/`
  folder (root `.vscode/extensions.json` already exists). Add any missing recommended extension to the root file.
- Merge any entry from the generated `.gitignore` that the root `.gitignore` lacks into the root file, then delete the
  nested `.gitignore`.
- Keep any Prettier config the CLI generates; do not add a formatting check to CI in this brief.
- Delete the generated placeholder template content (the large welcome page) and replace it with the shell in task 6.

### 4. `package.json`
- `"packageManager": "pnpm@<11.x resolved>"` (exact version, legacy field).
- `"engines": { "node": "^24.15.0" }`.
- Scripts (keep the CLI names where they exist):
  ```json
  "start": "ng serve --port $PORT --proxy-config proxy.conf.js",
  "build": "ng build",
  "test": "ng test",
  "lint": "ng lint"
  ```
  `ng test` must run once and exit in CI (Vitest disables watch mode when `CI` is set); verify locally with
  `$env:CI = "true"; pnpm test; Remove-Item Env:CI`.
- `@angular/*` packages on the 22.x line, `typescript` within `>=6.0.0 <6.1.0`.

### 5. `pnpm-workspace.yaml` (in `src/frontend/finguard-web/`)
```yaml
# pnpm settings for the single-project workspace. No `packages:` key: only the root package.
shellEmulator: true     # `$PORT` in scripts works on Windows and Linux
```
- If `pnpm install` reports ignored build scripts, run `pnpm approve-builds`, approve only what the Angular toolchain
  needs (for example `esbuild`, `lmdb`, `@parcel/watcher`), commit the setting pnpm writes, and list the approved
  packages in the report.
- Keep pnpm 11's default `minimumReleaseAge` (supply-chain protection). Do not lower it.

### 6. App shell (current Angular practices, `.claude/rules/frontend.md`)
```
src/app/
  app.ts / app.html / app.css      shell: <header> with the "FinGuard" title, <main> with <router-outlet>
  app.config.ts                    provideRouter(routes), provideHttpClient(withFetch())
  app.routes.ts                    '' → loadComponent(() => import('./features/home/home')), '**' → redirect to ''
  features/home/home.ts|html|css   standalone page: heading "Portfolio compliance" and one line of placeholder text
  core/.gitkeep                    interceptors, auth abstraction and API services arrive later
  shared/.gitkeep
```
- Standalone components, `OnPush` (explicit even if it is the default), signals for any state, built-in control flow,
  semantic HTML. All UI text in English.
- No HTTP calls, no interceptor, no `AuthService`, no UI component library, no environment files with URLs.

### 7. Dev proxy (`src/frontend/finguard-web/proxy.conf.js`)
```js
// Aspire injects API_HTTPS / API_HTTP from .WithReference(api) in the AppHost.
const target = process.env.API_HTTPS || process.env.API_HTTP;

if (!target) {
  throw new Error('API endpoint is not configured. Run the app through Aspire (aspire run).');
}

module.exports = {
  '/api': { target, secure: false, changeOrigin: true },
};
```
If the workspace is ESM-only and `module.exports` fails, rename to `proxy.conf.cjs` (and update the `start` script);
report it. `secure: false` is for the local development certificate only.

### 8. AppHost
In `src/backend/FinGuard.AppHost`: add `<PackageReference Include="Aspire.Hosting.JavaScript" />` (no version), keep
the existing Api variable, and add:
```csharp
builder.AddJavaScriptApp("web", "../../frontend/finguard-web", runScriptName: "start")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithReference(api)
    .WaitFor(api);
```
Adjust only if the 13.6.5 API differs; report the difference. No publish or container settings (Phase 5).

### 9. Lint
`pnpm exec ng add angular-eslint@<22.x resolved>` (flat config `eslint.config.js`, ESLint version chosen by the
schematic). `pnpm lint` must pass on the generated code with zero warnings. Do not disable rules to get there; fix the code.

### 10. Tests (Vitest, `.claude/rules/testing.md`: behaviour, no snapshots)
- `app.spec.ts`: the shell renders a header containing "FinGuard" and a `router-outlet`.
- `app.routes.spec.ts` (or inside the app spec with the router testing harness): navigating to `/` renders the home
  heading "Portfolio compliance"; an unknown URL redirects to `/`.
- `features/home/home.spec.ts`: the page renders its heading.

### 11. Dependabot
In `.github/dependabot.yml`, add the `npm` ecosystem (Dependabot uses it for pnpm):
- `directory: /src/frontend/finguard-web`, weekly, grouped minor and patch updates.
- Ignore `version-update:semver-major` for `@angular/*`, `@angular-devkit/*`, `angular-eslint`, `typescript`
  (majors go through `ng update` in a dedicated brief).
- `cooldown` of at least 2 days for this ecosystem, so new lockfiles do not trip pnpm's `minimumReleaseAge` in CI
  (check the current `cooldown` syntax in the Dependabot options reference).

### 12. Docs
- `AGENTS.md` → Commands: frontend commands run from `src/frontend/finguard-web`; `aspire run` now also starts `web`.
  Keep it short.
- `README.md` → Quick start: Node 24.15+, pnpm (any version; the pinned 11.x is used automatically), the web URL appears
  in the Aspire dashboard.
- `docs/ci-cd.md`: the `frontend` job is now active.
- `docs/decisions.md`: prepend Appendix A verbatim, then your execution delta on top
  ("Delta 2026-11-xx (session 8: Brief #3 executed)": resolved versions, approved build scripts, deviations, findings).
- No rule file changes are required. If you change `.claude/rules/frontend.md`, change `.cursor/rules/frontend.mdc` in
  the same commit (`docs/ai-rules.md`).

## Acceptance criteria
- [ ] Branch `feat/angular-web-app`, working tree clean, commits follow the commit plan.
- [ ] From `src/frontend/finguard-web`: `pnpm install --frozen-lockfile`, `pnpm lint` (0 problems),
      `pnpm test` with `CI=true` (exits, all green), `pnpm build` all succeed.
- [ ] `package.json` pins `pnpm@11.x.y`; `pnpm-lock.yaml` exists and its first line is not `---` (single document).
- [ ] `git ls-files '*package-lock.json' '*yarn.lock' '*npm-shrinkwrap.json'` is empty; no `npm`/`npx`/`yarn` was run.
- [ ] Repo root: `dotnet restore`, `dotnet format --verify-no-changes --no-restore`, `dotnet build -c Release` (zero
      warnings) still pass.
- [ ] `aspire run`: dashboard shows `postgres`, `finguard`, `api` and `web` running; opening the `web` URL shows the
      shell and the home heading; the browser console has no errors.
- [ ] Through the `web` URL, `GET /api/does-not-exist` returns the Api's 404 `application/problem+json` with `traceId`
      (proves the proxy without adding any endpoint). Report the command and the response.
- [ ] Running `pnpm start` outside Aspire fails fast with the proxy error message (no silent fallback URL).
- [ ] `.github/dependabot.yml` is valid; `ci.yml` unchanged or every change justified in the report.

## Tests (summary)
| Level | Test | Where |
| --- | --- | --- |
| Unit (Vitest) | Shell renders header "FinGuard" and router outlet | `app.spec.ts` |
| Unit (Vitest) | `/` lazy-loads Home; unknown URL redirects to `/` | `app.routes.spec.ts` |
| Unit (Vitest) | Home renders heading | `features/home/home.spec.ts` |
| Manual (this brief) | Proxy returns Api ProblemDetails through the `web` URL | Acceptance criteria |
| Later | Browser end-to-end flow (upload → result) | Phase 1+, tool decided then |

## Rules to respect
`AGENTS.md`, `.claude/rules/frontend.md`, `.claude/rules/testing.md`, ADR-0005, `docs/git-workflow.md`.
English everywhere. No business rules in the frontend.

## Out of scope
API calls, typed API services, the ProblemDetails interceptor, `AuthService` and the dev token (Brief #5 / Phase 1);
any UI component library or design system; SSR; i18n; environment files with URLs; production serving of the SPA and
container images (Phase 5); architecture and backend tests (Brief #4); browser end-to-end tests; Prettier check in CI;
pnpm 12.

## Commit plan (Conventional Commits, no push)
1. `feat: scaffold angular 22 web app with pnpm`
2. `chore: add angular-eslint to the web app`
3. `feat: add app shell, home route and dev proxy`
4. `feat: run the web app from the aspire apphost`
5. `chore: enable dependabot for the web app`
6. `docs: update commands, readme and decisions log`

## Report back (final message)
- Plan versus what you did; every deviation with the reason and the doc you followed.
- Table of resolved versions (pnpm, Angular CLI/core, TypeScript, Vitest, angular-eslint, ESLint, Node) and the
  approved build scripts.
- Output of the acceptance checks (install, lint, test, build, dotnet checks, proxy request, lockfile first line).
- Your execution delta (also written into `docs/decisions.md`).
- The exact commands for the owner, not run by you:
  ```powershell
  git push -u origin feat/angular-web-app
  gh pr create --base main --head feat/angular-web-app `
    --title "feat: add Angular web app with pnpm orchestrated by Aspire" `
    --body-file .github/pull_request_template.md
  ```

---

## Appendix A — Decisions delta (planning session 7), paste verbatim into `docs/decisions.md`

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
