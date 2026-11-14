# CI/CD

## CI today (`.github/workflows/ci.yml`)
Runs on every push to `main` and on every pull request. Superseded runs are cancelled. The token is read-only.

| Job | What it does | When it runs |
| --- | --- | --- |
| `hygiene` | Fails if `package-lock.json`, `yarn.lock` or `npm-shrinkwrap.json` is tracked (pnpm only). | Always |
| `detect` | Sets `dotnet` (a `.sln`/`.slnx` is tracked) and `frontend` (`src/frontend/finguard-web/package.json` exists). | Always |
| `backend` | `dotnet restore`, `format --verify-no-changes` (D-24), `build -c Release`, `test`, with the SDK from `global.json`. | `dotnet == 'true'` |
| `frontend` | pnpm (version from `packageManager`), Node from `.node-version`, `install --frozen-lockfile`, lint, test, build. | `frontend == 'true'` |
| `ci-gate` | Fails if any job failed or was cancelled; skipped jobs are fine. | Always |

`ci-gate` is the only status check to require in branch protection. Since Brief #2 (solution) and Brief #3 (Angular app)
both `backend` and `frontend` are active.

Dependabot (`.github/dependabot.yml`) opens weekly grouped minor/patch updates for NuGet, GitHub Actions and the web app
(`npm` ecosystem, which reads `pnpm-lock.yaml`). Web app majors of Angular, angular-eslint and TypeScript are ignored
(they go through `ng update`), and a 3-day cooldown keeps PRs past pnpm 11's `minimumReleaseAge` (D-46, R-12).

## CD (`.github/workflows/cd.yml`)
A manual placeholder until Phase 5 (ADR-0005). No secrets and no Azure steps.

### Phase 5 plan
1. Build container images for the API and the Angular app on `main` or on a release tag.
2. Push the images to a container registry.
3. Log in to Azure with GitHub OIDC federated credentials (`azure/login` with `id-token: write`); no stored secrets.
4. Deploy infrastructure as code and the images to Azure Container Apps through a protected `demo` environment
   that requires manual approval.
5. Provide a manual `teardown` workflow (`workflow_dispatch`) that deletes the demo resources to keep cost near zero.
