# FinGuard — decisions log

Paste this file into the Claude Project knowledge and update it at the end of every session
("Decisions delta"). Newest delta first.

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
