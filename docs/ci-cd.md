# CI/CD

## CI today (`.github/workflows/ci.yml`)
Runs on every push to `main` and on every pull request. Superseded runs are cancelled. The token is read-only.

| Job | What it does | When it runs |
| --- | --- | --- |
| `hygiene` | Fails if `package-lock.json`, `yarn.lock` or `npm-shrinkwrap.json` is tracked (pnpm only). | Always |
| `detect` | Sets `dotnet` (a `.sln`/`.slnx` is tracked) and `frontend` (`src/frontend/finguard-web/package.json` exists). | Always |
| `backend` | `dotnet restore`, `build -c Release`, `test`, with the SDK from `global.json`. | `dotnet == 'true'` |
| `frontend` | pnpm (version from `packageManager`), Node from `.node-version`, `install --frozen-lockfile`, lint, test, build. | `frontend == 'true'` |
| `ci-gate` | Fails if any job failed or was cancelled; skipped jobs are fine. | Always |

`ci-gate` is the only status check to require in branch protection. Until PR #2 adds a solution and PR #3 adds
the Angular app, `backend` and `frontend` are skipped and CI is green.

Dependabot (`.github/dependabot.yml`) opens weekly grouped minor/patch updates for NuGet and GitHub Actions.
The `npm` ecosystem (served by pnpm lockfiles) is added with the Angular app in PR #3.

## CD (`.github/workflows/cd.yml`)
A manual placeholder until Phase 5 (ADR-0005). No secrets and no Azure steps.

### Phase 5 plan
1. Build container images for the API and the Angular app on `main` or on a release tag.
2. Push the images to a container registry.
3. Log in to Azure with GitHub OIDC federated credentials (`azure/login` with `id-token: write`); no stored secrets.
4. Deploy infrastructure as code and the images to Azure Container Apps through a protected `demo` environment
   that requires manual approval.
5. Provide a manual `teardown` workflow (`workflow_dispatch`) that deletes the demo resources to keep cost near zero.
