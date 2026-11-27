# Brief #19 — Model narrator on a local model (closes Phase 4)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-19-model-narrator.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-19-model-narrator.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/model-narrator` |
| PR title (exact, becomes the squash commit) | `feat: refine the advisory narrative with a validated local model` |
| Phase | Phase 4, last of three (#17 rebalance engine, #18 template narrator, **#19 model narrator**). Phase 4 exit: proposal always present; narrative asynchronous and degrades gracefully |

---

## Goal
After the template advisory is stored (#18), Advisory asks a local model, through Microsoft Agent Framework over an
`IChatClient` served by Ollama in Aspire, to rewrite the narrative from the same facts. The model text replaces the
template **only** if it passes validation; otherwise the template stays and the reason is recorded. The model call
never runs inside a database transaction. The app works the same with the model off (CI, tests, a machine without
Ollama). The brief also adds the manual smoke checklist and a professional footer, and closes Phase 4.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/advisory.md`, `.claude/rules/backend-vsa.md`,
  `.claude/rules/frontend.md`, `.claude/rules/testing.md`, `docs/decisions.md` (newest delta: session 28, Brief #18
  executed), ADR-0002, ADR-0006, ADR-0009, ADR-0014.
- Today: `EvaluationCompletedHandler` builds `AdvisoryFacts`, writes the `TemplateNarrator` text and stores
  `advisory.advisories` (`narrator = Template`). `IAdvisoryNarrator` exists as the port, unregistered (D-261).
  `NarrativeValidator` checks that every number in a text comes from the facts (D-254). The SPA shows the advisory
  with "Written from a template." (D-267).
- Wolverine's Eager EF transaction opens before a handler that uses a DbContext (D-67); the #18 note says a model call
  of several seconds must not run inside it.
- Package facts checked on 2026-11-27 (re-verify in preflight; stop and report if any is not a stable release):
  - `CommunityToolkit.Aspire.OllamaSharp` 17.0.0 exists on NuGet (aligned with Aspire 17). Its documented use:
    `builder.AddOllamaApiClient("<name>").AddChatClient();` registers an `IChatClient`.
  - `CommunityToolkit.Aspire.Hosting.Ollama`: use the same stable 17.x version as the client; `AddOllama(...)` with
    `AddModel(...)` / data volume in the AppHost.
  - `Microsoft.Agents.AI`: 1.x stable releases ship often (1.13.0 seen on NuGet; newer satellites already depend on
    ≥ 1.17.0). Pin the **latest stable 1.x** at execution time; satellites marked preview, alpha or rc are not used
    (ADR-0006). Verify the current API for building an agent over an `IChatClient` (for example `ChatClientAgent`)
    in the official docs, not in blog snippets.

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Flow | `EvaluationCompletedHandler` unchanged except: when a model narrator is configured it sets `refinement_status = Pending` and cascades `RefineNarrative(AdvisoryId)`. `RefineNarrativeHandler` has **no DbContext dependency** (so no transaction): reads the facts with Dapper (own schema, `tenant_id` filter), calls `IAdvisoryNarrator` with a timeout, validates, and cascades `StoreRefinedNarrative(AdvisoryId, Outcome, Narrative?, Model?)`. `StoreRefinedNarrativeHandler` writes with EF in a short transaction | Calling the model inside the #18 handler (transaction held for seconds); writing with Dapper (ADR-0007) |
| Overwrite rule | `StoreRefinedNarrative` applies only when `refinement_status = Pending`; accepted → `narrative`, `narrator = Model`, `model_id`; otherwise only `refinement_status = Rejected` and `refinement_reason`. Idempotent on redelivery | Keeping both texts (one advisory, one current text; the template is reproducible from the facts) |
| Failure policy | Timeout, model error or validation failure → `Rejected` with a reason code (`Timeout`, `ModelError`, `UnknownNumber`, `StatusMismatch`, `Format`, `Length`). The handler catches these and never sends them to the error queue; no automatic retry. Unexpected exceptions still follow the normal error policy | Retrying the model (cost and latency for a cosmetic step) |
| Validation | `NarrativeValidator` gains rules for model text: (1) numbers (existing); (2) status consistency: the text must contain the overall status phrase the template uses ("breaches" / "near the limit" / "within every limit") and must not contain the others; (3) format: plain text only, no `<`, `>`, backticks, `#`, `*`, `|`, URLs; (4) length: 200–1,500 characters, 1–5 paragraphs. The disclaimer sentence is appended by code after validation, never by the model | Trusting the model's wording; asking the model to self-check |
| Prompt | Fixed English instructions in code (versioned constant): explain the evaluation and proposal for a finance reader; use only the facts; copy every number exactly as written in the facts; no advice beyond the proposal; plain text, short paragraphs. Input = JSON of the facts' **display texts** and class names only (no raw decimals, no ids). No free text exists in the facts (D-250) | Few-shot examples with numbers (the model copies them) |
| Agent | One Agent Framework agent over the injected `IChatClient`, instructions only, **no tools, no memory, no MCP** (ADR-0006). All Agent Framework and Ollama types live in `FinGuard.Advisory/Infrastructure/` | Calling `IChatClient` directly (ADR-0006 chose Agent Framework) |
| Model | Default `llama3.2:3b` (small, CPU friendly), set as an AppHost parameter so it can change without code. **Assumption**: if it does not run acceptably on the owner's machine, Claude Code proposes another small instruct model from the Ollama library and records it | A larger model (download size, CPU latency) |
| Configuration | AppHost: Ollama resource with a data volume and the model; the Api gets the reference **without `WaitFor`** (the app must start without the model). Advisory registers `AgentFrameworkNarrator` only when the Ollama connection string exists; otherwise no refinement is requested. AppHost switch `FinGuard:Ai:Enabled` (default `true`) to start without Ollama; `dev-session.ps1 -StartAspire` gains `-NoAi`. Timeout `Advisory:Narrator:TimeoutSeconds` default 90 | Making Ollama required (CI, tests and new clones would break) |
| Telemetry | Chat client wrapped with OpenTelemetry with **sensitive data off** (no prompts or completions in traces; they contain amounts). A metric `finguard.advisory.refinements` with tag `outcome` (`accepted`, `timeout`, `model_error`, `rejected_<rule>`). Logs: advisory id + outcome only | Logging prompts for debugging (amounts in logs) |
| API | `GET .../advisory` adds `model` (null for template) and `refinement` (`NotRequested` / `Pending` / `Accepted` / `Rejected`) | |
| UI | Source line: `Written from a template.` / `Written by an AI model ({model}) and checked against the evaluation's numbers.`. While `refinement = Pending`: the template text plus `An AI-written version is being prepared…`, polling with the shared helper's delays, then it stops silently (the template is already a complete answer). `Rejected` shows nothing extra | Hiding the template until the model answers |
| Persistence | Migration `AddNarrativeRefinement`: `refinement_status varchar(16) NOT NULL DEFAULT 'NotRequested'`, `refinement_reason varchar(32) NULL`, `model_id varchar(64) NULL` | |
| ADR | ADR-0015 "Model narrator: refinement, validation and fallback" (ADR-0006 stays; this one records the flow and the rules) | |

### Also in this brief
- **Manual smoke checklist** `docs/testing/manual-smoke.md` (D-278): the checklist in Appendix B, written as steps with
  expected results, usable against local Aspire, Azure (Phase 5) and the VPS (Phase 6). Link it from `README.md` and
  `docs/testing` index if one exists.
- **Footer** (app shell, every page including `/login`): one line, small, muted, accessible:
  `© 2026 Marco Antonio Soria · Designed and developed by Marco Antonio Soria · MIT License · Source on GitHub`
  - Year range is dynamic: `2026` when the current year is 2026, `2026–{current}` afterwards (computed once at
    startup from the browser clock; a pure function with tests).
  - "MIT License" links to the repo's `LICENSE`; "Source on GitHub" links to `https://github.com/marco-soria/finguard`.
    External links open in a new tab with `rel="noopener noreferrer"`.
  - No "All rights reserved": the code is MIT-licensed.
  - Also update `LICENSE`'s copyright holder from `marco-soria` to `Marco Antonio Soria` (same year).
  - A second short line, same style: `Demo application. Not investment advice.`

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot` first, Aspire only through `-StartAspire`,
  `-Stop` before the final message, Close-out block with "Still running: none". The Ollama container is part of the
  Aspire session and must be stopped with it; its **data volume (model cache) is kept** (no volume removal).
- First model download is large: tell the owner the expected size before the first `-StartAspire` with AI on.
- pnpm only, from inside `src/frontend/finguard-web`; Git Bash uses `aspire.cmd` (D-135). Build `FinGuard.slnx`
  explicitly while R-47 is open.
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Logs, traces and `ToString()`: identifiers and outcome codes only. Never prompts, completions, facts or amounts.
- `docs/decisions.md` is append-only (D-137). Ask before deviating; record approved deviations.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/model-narrator
scripts/dev-session.ps1 -Snapshot
dotnet build FinGuard.slnx -c Release; dotnet test --no-build -c Release      # baseline 502
```
Frontend baseline: Vitest 135, E2E 11. Then verify on NuGet and the official docs the three package facts above and
report the exact versions in the plan.

## Tasks

### 1. AppHost and configuration
- Ollama hosting resource (data volume, model parameter), Api reference without `WaitFor`, `FinGuard:Ai:Enabled`
  switch; `dev-session.ps1 -NoAi`; `docs/dev-environment.md` (how to run with and without the model, model size).

### 2. Advisory infrastructure (`FinGuard.Advisory/Infrastructure/`)
- Client registration (`AddOllamaApiClient(...).AddChatClient()` + OpenTelemetry with sensitive data off) only when
  the connection string exists; `AgentFrameworkNarrator : IAdvisoryNarrator` (agent, fixed instructions, prompt JSON
  builder from facts' display texts, timeout via `CancellationTokenSource`).
- `AdvisoryOptions` (timeout, enabled) validated on start.

### 3. Refinement flow
- `RefineNarrative`, `StoreRefinedNarrative` messages (ids-only `ToString()`; `StoreRefinedNarrative` carries the
  narrative text, which is persisted in Wolverine's envelope tables like other payloads: R-35 applies).
- Handlers as decided; validator rules 2–4; disclaimer appended after validation; metric and logs.
- Migration `AddNarrativeRefinement`; `has-pending-model-changes` clean.

### 4. API and UI
- Read slice returns `model` and `refinement`; `AdvisoryPanel` texts and pending polling as decided.
- Footer component in the app shell (task "Also in this brief").

### 5. Docs
- `docs/adr/0015-model-narrator.md` (+ README index); `.claude/rules/advisory.md` + `.cursor/rules/advisory.mdc` (same
  commit): model text must pass all validator rules; no tools; no prompts in logs or traces; the model never runs
  inside a transaction.
- `docs/testing/manual-smoke.md` (Appendix B). `README.md`: Phase 4 complete, how the AI narrative works, footer
  screenshot not needed. `docs/architecture/overview.md`: refinement step in the advisory sequence.
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta on top.

## Tests that prove it
Unit (fake `IChatClient`, no real model):
- Prompt builder: JSON contains only display texts, class names, statuses, date and policy version; no raw decimals,
  ids or tenant data; instructions constant unchanged (snapshot).
- `AgentFrameworkNarrator`: returns the fake model text; honours the timeout (fake that delays) → `Timeout`.
- Validator rules: accepts a correct paraphrase of the shares-heavy facts; rejects a changed amount, a percentage not
  in the facts, "within every limit" for a Breached evaluation, Markdown (`**`, `#`), HTML (`<b>`), a URL, text
  shorter than 200 or longer than 1,500 characters; errors never contain the offending number or text.
- `RefineNarrativeHandler` outcomes → the right `StoreRefinedNarrative` (accepted / each rejection code); no exception
  escapes for expected failures.
- `StoreRefinedNarrativeHandler`: applies only when `Pending`; a second delivery changes nothing.
- Footer year: 2026 → `2026`; 2027 → `2026–2027`.
Architecture: no `Microsoft.Agents.*`, `OllamaSharp` or `Microsoft.Extensions.AI` reference outside
`FinGuard.Advisory` (and Agent Framework/Ollama types only in its `Infrastructure` namespace); `RefineNarrativeHandler`
has no DbContext dependency (pins "no model call inside a transaction"); message `ToString()`s carry no text.
Integration (real PostgreSQL, fake `IChatClient` registered in `ApiFixture`):
- `Upload_SharesHeavy_ModelTextAccepted_StoresModelNarrative` (narrator `Model`, refinement `Accepted`, API paragraphs
  end with the disclaimer).
- `Upload_SharesHeavy_ModelChangesANumber_KeepsTemplate` (refinement `Rejected`, reason `UnknownNumber`).
- `Upload_SharesHeavy_ModelThrows_KeepsTemplate` and `..._ModelTimesOut_KeepsTemplate`; dead letters: 0 in all.
- `Upload_NoModelConfigured_RefinementNotRequested` (the default fixture).
Vitest: panel source lines for both narrators; pending text and its polling stop; footer content, links and `rel`.
E2E: existing advisory tests run with AI **off** (`-NoAi`) and stay deterministic; add one footer check.
Manual (reported, not automated): with AI on, the three samples × 2 runs: outcome per run, model latency range, and
the accepted shares-heavy text (sample numbers, may appear in the report).

## Acceptance criteria
- `dotnet build FinGuard.slnx -c Release` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm lint`, `pnpm test --watch=false`, `pnpm build`, `pnpm exec prettier --check src e2e` green.
- `-StartAspire -NoAi`: `pnpm e2e` green twice. `-StartAspire` (AI on): the manual runs above; the app stays usable
  while the model is slow or stopped (stop the Ollama container mid-run: the template remains, outcome `ModelError`
  or `Timeout`); dead letters 0; Aspire stopped; Close-out with "Still running: none".
- `docs/testing/manual-smoke.md` executed once locally by Claude Code for the automatable parts; the owner runs the
  rest (Appendix B marks which).

## Rules to respect
- ADR-0002: the model never computes amounts or decides status; it only rewrites validated text from facts.
- ADR-0006: Agent Framework, stable 1.x only, no tools; types only in `Advisory/Infrastructure`.
- ADR-0007: writes with EF, reads with Dapper and the `tenant_id` filter.
- Security: no user free text reaches the model (D-250); no prompts/completions in logs or traces; plain-text
  rendering only (D-253).

## Out of scope
Azure AI Foundry and managed identity (Phase 5); streaming; chat or follow-up questions; tools or retrieval;
regenerating old advisories; choosing the production model; marcosoria.dev portfolio site.

## Commit plan
1. `feat: add a local Ollama model to the AppHost behind a switch`
2. `feat: refine the advisory narrative with a validated model outside the transaction`
3. `feat: show the narrative source and add the site footer`
4. `docs: add ADR-0015, the manual smoke checklist and close Phase 4`

## Pull request
- **Title (exact):** `feat: refine the advisory narrative with a validated local model`
- **Body:** `.git/finguard-pr-body.md` from the template; Summary names ADR-0015, the package versions pinned, the
  `-NoAi` switch, and "Phase 4 complete".
- Close-out block with:
  `git push -u origin feat/model-narrator` and
  `gh pr create --base main --head feat/model-narrator --title "feat: refine the advisory narrative with a validated local model" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first (with the verified package versions and the chosen model with its download size); wait for approval. At
the end: the manual runs table (sample, run, outcome, latency), one accepted text, test counts, two E2E runs with
`-NoAi`, screenshots of the Advisory section (template and model, light) and of the footer, the execution delta and
the Close-out block.

---

## Appendix A — Decisions delta (session 29: Phase 5 outline and Brief #19 planning)

### Owner answers and plan agreed in the planning chat
| ID | Decision | Notes |
| --- | --- | --- |
| D-271 | Region East US 2 for every Azure resource; the Entra external tenant stores data in the United States (confirm on creation) | SWA backend regions and Foundry models available there |
| D-272 | Azure names follow CAF: `<abbr>-finguard-prod-eus2` (`rg`, `log`, `appi`, `kv`, `id`, `cae`, `ca`, `caj`, `cr`, `psql`, `swa`, `aif`/`proj`); model deployment named `narrator`; tags `workload`, `env`, `owner`, `managedBy` | Exact list in the Phase 5 briefs |
| D-273 | Hostnames: `finguard.marcosoria.dev` (SPA) and `api.finguard.marcosoria.dev` (API), Cloudflare DNS-only records. The owner is new to DNS: the Phase 5 brief that needs them gives step-by-step instructions | Domain bought 2026-11-27 on Cloudflare |
| D-274 | Azure infrastructure as code: Aspire-generated Bicep (`aspire publish` for review, deployment from GitHub Actions); a minimal hand-written Bicep or script only for what Aspire does not model. Terraform not used for Azure | |
| D-275 | The Entra external tenant is created by hand with a runbook and kept when the resource group is destroyed (reused in Phase 6) | |
| D-276 | No portfolio seed: self-service sign-up and downloadable sample CSVs | |
| D-277 | Phase 6: Aspire Docker Compose publisher + Dokploy (Aspirate not used); confirmed in Phase 6 | |
| D-278 | `docs/testing/manual-smoke.md` is the manual checklist for local, Azure and VPS | |
| D-279 | Phase 5 plan: deploy with GitHub Actions, record one or two English technical videos (flow, Application Insights), export what is needed, then delete the resource group; Phase 6 keeps the project online on the VPS | Owner plan, confirmed |
| D-280 | Azure account: pay-as-you-go since 2026-02, with about 10 months of 12-month free services left (until about 2027-02); Phase 5 must finish before then. Verify the PostgreSQL B1MS free meter in the portal's Free services view before #20 | |

### Brief #19 decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-281 | Refinement is a separate step: `RefineNarrative` (no DbContext, model call with timeout) → `StoreRefinedNarrative` (short EF write, applies only when `Pending`) | Resolves the #18 note on transactions |
| D-282 | Expected model failures set `Rejected` with a reason code; no retry, no error queue | |
| D-283 | Validator rules for model text: numbers, status phrase, plain-text format, length; disclaimer appended by code | ADR-0015 |
| D-284 | Prompt input = facts' display texts only; fixed versioned instructions; no few-shot numbers | |
| D-285 | One Agent Framework agent, no tools; types only in `Advisory/Infrastructure` | ADR-0006 |
| D-286 | Ollama in the AppHost with a model parameter (default `llama3.2:3b`, assumption), referenced without `WaitFor`; `FinGuard:Ai:Enabled` switch and `-NoAi` | |
| D-287 | OpenTelemetry on the chat client with sensitive data off; metric `finguard.advisory.refinements` by outcome | |
| D-288 | API `model` and `refinement`; UI source line and pending text | |
| D-289 | Footer: `© 2026[–year] Marco Antonio Soria · Designed and developed by Marco Antonio Soria · MIT License · Source on GitHub` + `Demo application. Not investment advice.`; `LICENSE` holder updated | No "All rights reserved" (MIT) |

### Risks and flags
- **R-50 Local model quality and latency.** A 3B model on CPU may be slow or fail validation often. The template
  always remains; the manual runs measure the acceptance rate. Production uses Azure AI Foundry (Phase 5).
- R-35 also covers `StoreRefinedNarrative` (narrative text with amounts in envelope storage).

### Next step
Sync `decisions.md` to the Project (Phase 4 close, D-226). Then Phase 5 kickoff: Brief #20, the walking skeleton on
Azure Container Apps through GitHub Actions. One brief of notice before any Azure resource: the owner prepares the
subscription budget and the Entra external tenant with the runbook from #20.

---

## Appendix B — `docs/testing/manual-smoke.md` content (write it as numbered steps with expected results)

Mark each step **[auto]** (Claude Code can run it) or **[owner]** (needs a person and a browser).

1. Sign in / sign out; theme System, Light, Dark; reload on a deep link; narrow (mobile) width. [owner]
2. Upload each file in `samples/portfolios/`; expected statuses as in its README. [auto via API + owner in UI]
3. Rejections: empty file, wrong headers, negative amount, `EUR`, description > 150 characters, description starting
   with `=`, file over the size limit, cut-off date before 2000-01-01. Each gives the documented error. [auto]
4. Same file twice → `duplicate: true`; a different file for the same date becomes the current version. [auto]
5. Tenant isolation: user A's submission URL opened as user B → not found. [auto]
6. Boundaries: a class exactly at 100 % of its limit → Warning; at 90 % → alert; above 100 % → Breached. [auto]
7. Shares-heavy: proposal amounts, totals row, and the numbers in the advisory match the table. [owner]
8. Asynchronous recovery: stop the API right after an upload, start it again; the evaluation and advisory arrive;
   dead-letter table empty. [auto]
9. Narrative: with the model off → template; with the model on → AI text or template, never a broken state; a CSV
   description containing instructions changes nothing (descriptions never reach the model). [owner]
10. Logs and traces: search the dashboard (or Application Insights) for amounts, descriptions and cut-off dates →
    none found. [owner]
11. Footer on every page, links work. [owner]
