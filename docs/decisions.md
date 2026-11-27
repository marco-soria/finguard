# FinGuard — decisions log

Paste this file into the Claude Project knowledge and update it at the end of every session
("Decisions delta"). Newest delta first.

## Delta 2026-11-27 (session 30: Brief #19 executed)

Branch `feat/model-narrator`. The brief's commits, one bug fix found in the live runs (D-292) and one owner redesign
after the live runs (D-293): the model no longer rewrites the narrative, it adds one context paragraph. ADR-0015
accepted in its D-293 form. Phase 4 complete.

### Owner answers to the plan and the live runs
| ID | Decision | Notes |
| --- | --- | --- |
| D-290 | `CommunityToolkit.Aspire.OllamaSharp` pinned to 13.6.1 (property `OllamaClientVersion` in `Directory.Packages.props`): 17.0.0 needs `Microsoft.Extensions.AI` ≥ 11.0.0, which pulls .NET 11 abstractions into net10.0. Hosting package `CommunityToolkit.Aspire.Hosting.Ollama` stays on 17.0.0. "Pinned packages" table in `docs/dev-environment.md`; every brief's plan reports a NuGet check (AGENTS.md, Stack) | R-51. Evidence below |
| D-293 | The model no longer rewrites the narrative. The template always writes it, every figure and status, and is never replaced (`narrator` stays `Template`). The model adds one paragraph, `ai_context` (text, nullable, migration `AddAiContext`; `AddNarrativeRefinement` kept as applied), on the reasoning behind the proposal. `AiContextValidator` replaces D-283: no digits; no `%`, currency symbol or currency/percent word; no status wording (breach, breached, exceeds, exceeded, above the limit, warning, near the limit, compliant, within the limit, limit used); plain text; one paragraph of 150 to 700 characters; no model disclaimer. Reasons `Digits`, `Symbols`, `StatusWording`, `Format`, `Length`. Prompt v2 (`2026-11-27.2`): input is class names, reduced and receiving classes and the target in words only. API adds `aiContext`; the panel shows a "Context" subsection ("Written by an AI model ({model}). It contains no figures or statuses; all figures above come from the evaluation.") and "Adding AI context…" while pending. API and UI fail closed for any narrator other than `Template`. AI off by default (`FinGuard:Ai:Enabled=false`); `dev-session.ps1 -WithAi` replaces `-NoAi`; `OLLAMA_KEEP_ALIVE=2m` | Supersedes D-283 and the overwrite part of D-281/D-288. Reason: accepted rewrites stated wrong class statuses and passed every rule (kept as regression tests) |

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-291 | Refinement code lives in `Features/Refinement` (a `RefineNarrative` folder clashed with the message type). `IAdvisoryNarrator` gains `ModelId` (null for the template). The Ollama client runs with its health check off. Without a model, `IAdvisoryNarrator` falls back to the template (`TryAdd`) only to satisfy the handler's dependency. Integration tests use `ModelNarratorHost`: a fake `IChatClient`, its own database per test class, so its Wolverine node never takes the shared host's messages. The third manual sample is a Warning portfolio (`invalid-mixed.csv` is rejected, so it has no advisory). The footer's "(opens in a new tab)" uses a local `.visually-hidden` class | |
| D-292 | ServiceDefaults' standard resilience handler (10 s per attempt, retries, 30 s total) also wrapped the Ollama client's `HttpClient` (`narrator_httpClient`): local model calls were cut at 10 s and sent again, so the narrator's own timeout never applied (Ollama logged 500 at 10.00 s; one run ended `ModelError` after about 31 s). That client now has no resilience handler and no `HttpClient` timeout (`RemoveAllResilienceHandlers`, EXTEXP0001 suppressed with a comment). A test inspects the handler chain of the client the integration really uses | Bug fix commit with a failing test first |
| D-295 | AI context is requested only when the proposal status is not `NotNeeded`: with no proposal there is nothing to explain. Otherwise the refinement stays `NotRequested`, no `RefineNarrative` is sent and the panel shows no Context subsection (`EvaluationCompletedHandler.RequestsContext`; unit and integration tests with the balanced sample) | Owner review of the close-out. Removes the balanced run 2 problem by construction |
| D-294 | Local data only: the six rows written by the first design (`narrator = 'Model'`) were deleted from the owner's development database with one approved `DELETE … WHERE narrator <> 'Template'` (6 rows). No migration, no volume removed | Owner approved |

### .NET 11 check (owner condition on D-290)
`dotnet list FinGuard.slnx package --include-transitive` after restore: no `Microsoft.Extensions.*` or
`Microsoft.Extensions.AI*` package at 11.x. Advisory resolves `CommunityToolkit.Aspire.OllamaSharp` 13.6.1,
`Microsoft.Agents.AI` 1.28.0, `Microsoft.Agents.AI.Abstractions` 1.28.0, `Microsoft.Extensions.AI` 10.11.0,
`Microsoft.Extensions.AI.Abstractions` 10.11.0, `Microsoft.Extensions.AI.Evaluation` 10.11.0, `OllamaSharp` 5.4.12.
`Microsoft.Agents.AI` 1.28.0's own net10.0 dependencies are all 10.x (`Microsoft.Extensions.AI` 10.11.0,
`Microsoft.Extensions.Compliance.Abstractions` 10.10.0, `DependencyInjection.Abstractions` 10.0.13,
`Logging.Abstractions` 10.0.13, `VectorData.Abstractions` 10.10.0). The only 11.x package anywhere is
`System.Diagnostics.DiagnosticSource` 11.0.0, which was already there through `OpenTelemetry.Api` 1.21.0 (also in the
untouched Tenancy project).

### Results
- Tests: backend 587 (unit 399, architecture 20, integration 168; baseline 502), Vitest 146 (baseline 135), E2E 12
  (11 plus the footer). E2E passed twice on the default (AI off) after the redesign, and four times with AI off before it.
- Live runs, final design (`-WithAi`, `llama3.2:3b`, CPU): 6/6 accepted (valid-balanced 2/2, valid-shares-heavy 2/2,
  Warning cash portfolio 2/2); Ollama call 4.1 to 9.4 s (first call includes loading the model), 4 to 12 s from upload
  to the stored outcome. Owner check: two texts are generic but plausible; balanced run 2 says Shares "have grown
  significantly" and moves Shares back although balanced has no proposal (no longer possible: D-295 requests no context
  without a proposal), and the Warning runs say reducing Cash brings it "closer to its maximum allocation" (wrong
  direction, kept under R-52). Neither has figures or status words. No further tuning.
- Live runs, first design, after the D-292 fix: 2/6 accepted (both Warning cash runs, both with wrong class statuses),
  4 `StatusMismatch` (balanced and shares-heavy). Before the fix: 2/6 accepted, 3 `StatusMismatch`, 1 `ModelError`.
- Ollama stopped mid-run: refinement `Rejected`/`ModelError` within about 2 s, template unchanged, no context, dead
  letters 0, Api `/health` 200 (first and final design).
- Cost while generating: about 3 GB of RAM and all 16 cores (owner observation, in `docs/dev-environment.md`).

### Risks and flags
- **R-51 Ollama client pinned to 13.6.1 until the move to .NET 11** (D-290). Move when a version needs only 10.x
  abstractions or the solution moves to .NET 11.
- **R-52 AI context may be generic; acceptance re-measured with Azure AI Foundry in Phase 5.** The validator blocks
  figures and statuses, not vague or slightly wrong reasoning (see the live runs above).
- R-50 measured: with the final design the local model is accepted 6/6 at 4 to 12 s; quality is R-52.
- `RemoveAllResilienceHandlers` is experimental (EXTEXP0001): re-check on each `Microsoft.Extensions.Http.Resilience`
  upgrade; `NarratorHttpClientTests` fails if the handler comes back.

### Next step
Owner: review and push `feat/model-narrator`, open the PR. Then sync `decisions.md` to the Project (D-226) and start
Phase 5 with Brief #20.

## Delta 2026-11-27 (session 29: Phase 5 outline and Brief #19 planning)

### Owner answers and plan agreed in the planning chat

| ID    | Decision                                                                                                                                                                                                                                    | Notes                                                  |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------ |
| D-271 | Region East US 2 for every Azure resource; the Entra external tenant stores data in the United States (confirm on creation)                                                                                                                 | SWA backend regions and Foundry models available there |
| D-272 | Azure names follow CAF: `<abbr>-finguard-prod-eus2` (`rg`, `log`, `appi`, `kv`, `id`, `cae`, `ca`, `caj`, `cr`, `psql`, `swa`, `aif`/`proj`); model deployment named `narrator`; tags `workload`, `env`, `owner`, `managedBy`               | Exact list in the Phase 5 briefs                       |
| D-273 | Hostnames: `finguard.marcosoria.dev` (SPA) and `api.finguard.marcosoria.dev` (API), Cloudflare DNS-only records. The owner is new to DNS: the Phase 5 brief that needs them gives step-by-step instructions                                 | Domain bought 2026-11-27 on Cloudflare                 |
| D-274 | Azure infrastructure as code: Aspire-generated Bicep (`aspire publish` for review, deployment from GitHub Actions); a minimal hand-written Bicep or script only for what Aspire does not model. Terraform not used for Azure                |                                                        |
| D-275 | The Entra external tenant is created by hand with a runbook and kept when the resource group is destroyed (reused in Phase 6)                                                                                                               |                                                        |
| D-276 | No portfolio seed: self-service sign-up and downloadable sample CSVs                                                                                                                                                                        |                                                        |
| D-277 | Phase 6: Aspire Docker Compose publisher + Dokploy (Aspirate not used); confirmed in Phase 6                                                                                                                                                |                                                        |
| D-278 | `docs/testing/manual-smoke.md` is the manual checklist for local, Azure and VPS                                                                                                                                                             |                                                        |
| D-279 | Phase 5 plan: deploy with GitHub Actions, record one or two English technical videos (flow, Application Insights), export what is needed, then delete the resource group; Phase 6 keeps the project online on the VPS                       | Owner plan, confirmed                                  |
| D-280 | Azure account: pay-as-you-go since 2026-02, with about 10 months of 12-month free services left (until about 2027-02); Phase 5 must finish before then. Verify the PostgreSQL B1MS free meter in the portal's Free services view before #20 |                                                        |

### Brief #19 decisions

| ID    | Decision                                                                                                                                                                            | Notes                                 |
| ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------- |
| D-281 | Refinement is a separate step: `RefineNarrative` (no DbContext, model call with timeout) → `StoreRefinedNarrative` (short EF write, applies only when `Pending`)                    | Resolves the #18 note on transactions |
| D-282 | Expected model failures set `Rejected` with a reason code; no retry, no error queue                                                                                                 |                                       |
| D-283 | Validator rules for model text: numbers, status phrase, plain-text format, length; disclaimer appended by code                                                                      | ADR-0015                              |
| D-284 | Prompt input = facts' display texts only; fixed versioned instructions; no few-shot numbers                                                                                         |                                       |
| D-285 | One Agent Framework agent, no tools; types only in `Advisory/Infrastructure`                                                                                                        | ADR-0006                              |
| D-286 | Ollama in the AppHost with a model parameter (default `llama3.2:3b`, assumption), referenced without `WaitFor`; `FinGuard:Ai:Enabled` switch and `-NoAi`                            |                                       |
| D-287 | OpenTelemetry on the chat client with sensitive data off; metric `finguard.advisory.refinements` by outcome                                                                         |                                       |
| D-288 | API `model` and `refinement`; UI source line and pending text                                                                                                                       |                                       |
| D-289 | Footer: `© 2026[–year] Marco Soria · Designed and developed by Marco Soria · MIT License · Source on GitHub` + `Demo application. Not investment advice.`; `LICENSE` holder updated | No "All rights reserved" (MIT)        |

### Risks and flags

- **R-50 Local model quality and latency.** A 3B model on CPU may be slow or fail validation often. The template
  always remains; the manual runs measure the acceptance rate. Production uses Azure AI Foundry (Phase 5).
- R-35 also covers `StoreRefinedNarrative` (narrative text with amounts in envelope storage).

### Next step

Sync `decisions.md` to the Project (Phase 4 close, D-226). Then Phase 5 kickoff: Brief #20, the walking skeleton on
Azure Container Apps through GitHub Actions. One brief of notice before any Azure resource: the owner prepares the
subscription budget and the Entra external tenant with the runbook from #20.

## Delta 2026-11-26 (session 28: Brief #18 executed)

Branch `feat/advisory-template-narrative`. The brief's four commits, plus a test commit that reports the R-49
measurement and two commits from the owner's review (D-269, D-270). ADR-0014 accepted. `EvaluationCompleted` now has a
route (Advisory); R-40 closed without backfill (D-258).

### Owner answers to the execution summary
| ID | Decision | Notes |
| --- | --- | --- |
| D-269 | Advisory never recomputes values that Compliance owns. `ClassEvaluation` gains `decimal Share` (stored, unrounded `book value / total`), appended last (additive, ADR-0011) and filled in `ContractMapping`; `AdvisoryFactsBuilder` formats it. The contract-mapping test checks it against the evaluation | Replaces Advisory's own `book value / total` |
| D-270 | The receivers' closing sentence is "Each receiving class moves toward {TargetRatioText} of its limit." (one receiver: "The receiving class moves toward …"). Always true; the Full sentence carries the guarantee, backed by verification | Closes R-49. D-260, D-261 and D-268 accepted as recorded |

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-260 | `.claude/rules/backend-vsa.md` and its Cursor mirror said "change an event by adding a `...V2` type, never by editing the old one", which contradicted ADR-0011 ("additive changes only"). The rule now says: additive changes only (a new field appended last, ADR-0011); a breaking change is a new versioned type | Flagged in the plan; the owner approved the plan. D-242 and D-249 already relied on ADR-0011 |
| D-261 | `EvaluationCompletedHandler` takes `TemplateNarrator` directly, not `IAdvisoryNarrator`: the template is always written first (D-251), whatever narrator #19 adds. Only `TemplateNarrator` is registered (singleton). `TemplateNarrator.Name = "Template"` is the `narrator` column value | `IAdvisoryNarrator` exists as the port (ADR-0006); #19 registers the model narrator behind it |
| D-262 | `ClassFacts.Change` is signed (proposed − current); `ChangeText` is the absolute amount as money. The sentence says "reduces … by" or "(+…)" | No signed money text, so no `-$` vs `−$` question |
| D-263 | Grammar for several classes: "breaches the limits for A and B", "reduces A by … to … and B by … to …, each of which is 85.00 % of its limit", one receiver "The receiving class moves toward …" (D-270). Lists use "A, B and C" (no serial comma). The Warning headline lists the classes in alert in D-235 order; the Breached headline lists the breached classes only | The brief fixed the singular wording only |
| D-264 | Validator error `Advisory.Narrative.UnknownNumber` (validation type), fixed message without the number. Dates tokenize as their parts (`2026`, `06`, `30`), both in the text and in the facts | |
| D-265 | `AdvisoryFormat` uses invariant culture and `MidpointRounding.AwayFromZero` to match `toFixed`/`Intl` on positive values; `Ratio` uses the same 2-to-8-decimal loop as `formatRatio` | R-48 still applies to exact midpoints |
| D-266 | The #17 generator moved from `RebalanceEngineTests` to `PolicyTestData.RandomPortfolio` (same code, same seed 17) so both generated checks share it | |
| D-267 | SPA: the retry loop is `retryWhileNotFound(resource, notFoundCode)` in `shared/retry-while-not-found.ts` (`NOT_FOUND_RETRY_DELAYS_MS`, same delays as D-205). Advisory texts: pending "Writing the advisory…", timeout "The advisory is not ready yet." + Check again, none "No advisory is available for this evaluation.", ready paragraphs + "Written from a template." (shown for `narrator = Template` only) | |
| D-268 | The two existing E2E checks for `Policy version 1` use `exact: true`: the advisory headline also contains "policy version 1" | Test fix only |

### Risks and flags
- **R-49 closed by the wording (D-270):** the brief's "Each of these stays at or below 85.00 % of its limit" was not
  exact. In 9 of the 10,000 generated portfolios a receiver ends above `RebalanceTargetRatio x limit x total` by at most
  0.01 USD: the leftover cent of ADR-0013 (D-240); no fallback receiver was involved. The generated check keeps
  reporting the distance.
- R-48 unchanged (no midpoint difference found in the shared vectors or the generated run).

### Execution checks
- Generated check (#17 generator, seed 17, 10,000 portfolios): NotNeeded 1,449, Full 8,551, Partial 0; validator
  failures 0; receivers above target 9 (largest excess 0.01 USD).
- Shares-heavy narrative (cut-off 2026-06-30) as stored and returned: see `samples/portfolios/README.md`.
- Tests: backend 502 (baseline 463), Vitest 135 (baseline 111), E2E 11 (baseline 9), run twice against Aspire.
- Live run: old evaluations show the advisory timeout state; `wolverine.wolverine_dead_letters` 0 rows.

## Delta 2026-11-26 (session 27: Brief #18 planning)

### Owner answers to the Brief #17 execution delta
| ID | Decision | Notes |
| --- | --- | --- |
| D-247 | D-245 accepted: the Partial sentence does not name a class. Partial did not occur at totals of 1.00 USD or more (generated table), so an API change is not worth it | Owner-approved in planning |
| D-248 | R-47: the owner deletes the untracked `finguard.sln` by hand; agents keep building `FinGuard.slnx` explicitly until the next delta confirms it is gone | |

### Brief #18 decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-249 | `EvaluationCompleted` gains `AlertThreshold` and `RebalanceTargetRatio` (additive) | ADR-0011 |
| D-250 | Architecture test: `EvaluationCompleted` carries no `string` member (no free text can reach a narrator) | ADR-0002 guard |
| D-251 | One handler: event → `AdvisoryFacts` → `TemplateNarrator` → store, idempotent by `EventId`. #19 adds the model as a later optional step that replaces the text only after validation | |
| D-252 | `AdvisoryFacts` carry raw values and display texts formatted once by `AdvisoryFormat`, which mirrors `formatRatio`, `formatPercent` and `UsdPipe` with shared test vectors | R-39 consistency |
| D-253 | Narratives are plain-text paragraphs; the SPA renders them by interpolation only | |
| D-254 | `NarrativeValidator`: every numeric token must come from the facts' texts; built in #18, applied to the model in #19 | ADR-0002 |
| D-255 | `advisory.advisories` with unique `evaluation_id`, `submission_id`, `source_event_id`; `facts jsonb`; `narrator` column | ADR-0014 |
| D-256 | `GET /api/advisory/submissions/{submissionId}/advisory` returns paragraphs, not facts; 404 hides existence | D-197 pattern |
| D-257 | `AdvisoryPanel` component; the 404 retry loop is shared with the evaluation | |
| D-258 | No backfill of evaluations stored before Advisory subscribed; **R-40 closed** as accepted (dev data only; Phase 5 starts empty) | |
| D-259 | Advisory publishes no events until Audit exists | |

### Risks and flags
- **R-46 closed**: no `Partial` at realistic totals (#17 table) and the template has a `Partial` sentence.
- **R-48 Last-digit differences between the narrative and the table.** C# formats `decimal`; the SPA formats JSON
  numbers as doubles. On an exact rounding midpoint a percentage could differ in its last digit. The shared vectors
  cover the known cases; revisit if a real case appears.
- **Note for #19:** Wolverine's Eager EF transaction opens before the handler; a model call of several seconds must
  not run inside a database transaction. #19 decides how (for example a handler without the transaction middleware
  that calls the model, then a short write).

### Next step
Brief #19 (closes Phase 4): `AgentFrameworkNarrator` over an `IChatClient` on a local model through Ollama in Aspire,
validation with `NarrativeValidator`, timeout and fallback to the template text, prompt-injection safeguards. Verify
packages and Aspire 17 compatibility first.

## Delta 2026-11-25 (session 26: Brief #17 executed)

Branch `feat/rebalance-proposal`. Four commits, as planned in the brief. ADR-0013 accepted; the relax step of D-03 is
superseded (D-233, confirmed by the owner before execution).

### Owner answers to the execution plan
| ID | Decision | Notes |
| --- | --- | --- |
| D-239 | D-233 confirmed: no relax step; verification gives `NotNeeded` / `Full` / `Partial`. The feasibility proof is in ADR-0013 | Owner-approved |
| D-240 | Rounding edge case, which the brief did not define. Each receiver gets `floor_cents(min(S, H) x h / H)`. The leftover goes to the receiver with the largest headroom, ties by enum order (D-04). With no receiver, the leftover goes to the non-reducer with the largest headroom (`t - v`, may be 0 or negative), ties by enum order, and that class counts as a receiver | Owner-approved. A non-reducer always exists: all four in alert would need shares summing to at least `0.90 x sum(L) >= 0.90 / 0.85 > 1`. Bound: `S > H` leaves `S - H <= 3` cents; `S <= H` leaves rounding only (`<= 2` cents); every receiver ends `<= target + 0.03` (ADR-0013) |

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-241 | The status rule lives once in `ClassResult.For(utilization, alertThreshold)` (the Specifications). `Evaluation.Create` and the engine's verification both call it | Replaces the private `Evaluation.StatusOf`; "no second implementation of the status rule" |
| D-242 | `EvaluationCompleted.ProposalStatus` and `ClassEvaluation.ProposedBookValue` are non-nullable and appended as the last positional parameters | Additive (ADR-0011). No stored messages to break: the event still has no route (D-199) |
| D-243 | API `proposal.moves` leaves out classes with zero change. The order (D-235) is implemented once, in `Features/GetEvaluation/ProposalOrdering`. A stored status with a missing class amount throws (both columns are written in one transaction) | |
| D-244 | UI totals row: Current = `totalBookValue`; Proposed = `totalBookValue` + sum of the API's changes; Change = that sum (`$0.00 (no change)`). Signed change text: `+$x (increase)`, `−$x (decrease)` (U+2212), `$0.00 (no change)`. `data-proposal-status` is `None` for a legacy evaluation | Display arithmetic on API values only; no rule in the SPA |
| D-245 | **Deviation, needs the owner's OK:** the Partial sentence is `Partial: rounding leaves a class outside its target; review the amounts.`, without naming `{Class}` | The API does not say which class stays outside its target, and the SPA must not derive it. Option if wanted: add the class to the API's `proposal` block (additive) |
| D-246 | **Deviation from the brief's wording:** the `NotNeeded` boundary test uses three classes at 0.8999 and none at 0. A Compliant portfolio cannot contain a zero class under the seed policy: the other three cap at `0.8999 x 0.95 < 1` of the total. The zero-value class is covered as a receiver (`Propose_FractionalTargetsAndZeroClass_...`) and in the generated table | |

### Execution checks
- Shares-heavy (T = 3,000,000.00), engine output equals the hand calculation in `RebalanceEngineTests`:
  Shares 2,200,000.00 to 637,500.00 (−1,562,500.00); Cash 300,000.00 to 993,374.43 (+693,374.43, including the 0.01
  leftover); Real estate 100,000.00 to 617,623.26 (+517,623.26); Fixed income 400,000.00 to 751,502.31
  (+351,502.31). Status `Full`; total 3,000,000.00. DB rows, `EvaluationCompleted` and API agree (integration tests).
- Generated table (seed 17, 10,000 portfolios, totals 1.00 to 10^12, half with free weights, half near the limits):
  NotNeeded 1,449, Full 8,551, **Partial 0**; 2,124 cases with a middle class; no-receiver fallback 0 (reachable only
  below 1.00 with the seed policy; covered by the 0.01 unit test, which is `Partial`).
- `has-pending-model-changes`: no changes after `AddRebalanceProposal`.
- Running app (`-StartAspire`): shares-heavy shows `Full` with the amounts above, balanced shows "No rebalancing
  needed.", and a rejected file has no compliance section (no evaluation, D-204).

### Tests
- Backend `dotnet test -c Release`: 463 / 463 (baseline 442; +21).
- Vitest: 111 / 111 (baseline 103). `pnpm e2e` twice: 9 / 9 both times.

### Risks and flags
- **R-46** measured: no `Partial` at totals of 1.00 USD or more in the generated table. Keep it open until Advisory
  shows `Partial` text (#18).
- **R-47 Untracked `finguard.sln`.** An ignored `finguard.sln` (created 2026-11-25 08:15, not by this session) sits next
  to `FinGuard.slnx`. With it, a bare `dotnet build` fails with MSB1011. This session used `dotnet build FinGuard.slnx`.
  The owner decides whether to delete it.

### Next step
Brief #18: Advisory module with `TemplateNarrator` (consumes `EvaluationCompleted` with the proposal) and its UI.

## Delta 2026-11-25 (session 26: Phase 4 plan and Brief #17 planning)

### Session 25 closing decisions (agreed in the planning chat, recorded here)
| ID | Decision | Notes |
| --- | --- | --- |
| D-229 | Phase 4 runs locally at zero cost and needs no account: `TemplateNarrator` first, then a local model through Ollama in a container (Aspire Community Toolkit), verified at Brief #19. GitHub Models is not relied on (a November 2026 source reports it retired; unverified) | Open question 1 of the kickoff (local model) narrowed; final choice in Brief #19 |
| D-230 | A Phase 6 (VPS: Dokploy, Tailscale, Hetzner; personal domain) is planned after Phase 5, not designed yet. Phase 5 is designed to be portable: one standard image configured by environment variables, migrations as a job, OTLP exporter chosen by configuration, Entra External ID kept, SPA as a static build, AI provider behind `IAdvisoryNarrator` | |
| D-231 | The owner buys the personal domain before Phase 5; SWA Free supports custom domains with free certificates, so Phase 5 already uses it | |
| D-232 | The repository stays a monorepo for every phase | |

### Phase 4 plan
| Brief | Scope |
| --- | --- |
| #17 | Rebalance engine in Compliance, stored with the evaluation, in `EvaluationCompleted`, API and UI |
| #18 | Advisory module: consumes `EvaluationCompleted`, `TemplateNarrator`, stores the narrative, API, "Advisory" section with an asynchronous state; R-40 backfill decision |
| #19 | `AgentFrameworkNarrator` over an `IChatClient` on a local model (Ollama via Aspire), output validation (numbers must match), timeout and fallback to the template, prompt-injection safeguards; closes Phase 4 |
Phase 5 then starts at #20.

### Brief #17 decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-233 | The relax-to-alert-threshold step of D-03 is removed: under `R × ΣL ≥ 1` the plan at the target ratio is always feasible in exact arithmetic. A verification step re-evaluates the proposed allocation: `Full` if every class ends Compliant, `Partial` otherwise (rounding only); `NotNeeded` for Compliant evaluations | **Supersedes part of D-03; owner confirmation required.** ADR-0013 |
| D-234 | Reducers = Warning and Breached classes, reduced to `floor_cents(R × L × T)`; receivers = classes below their target, proportional to headroom, leftover cents to the largest headroom (D-04); middle classes untouched | |
| D-235 | Move order: Breached, Warning (utilization desc), then receivers (amount desc), ties by `AssetClass` order; presentation only | |
| D-236 | The proposal is part of `Evaluation` (computed in `Evaluation.Create`), stored in two nullable columns; no backfill | |
| D-237 | `EvaluationCompleted` gains `ProposalStatus` and `ClassEvaluation.ProposedBookValue` (additive) | |
| D-238 | API `proposal` block (null for legacy evaluations) and a "Rebalancing proposal" section with a totals row | |

### Risks and flags
- **R-46 Rounding `Partial` on tiny totals.** Only reachable through cent rounding; the generated table measures it.
  If it shows up for realistic totals, revisit the rounding rule.

### Next step
Brief #18: Advisory module with `TemplateNarrator` (consumes `EvaluationCompleted` with the proposal) and its UI.

## Delta 2026-11-24 (session 25: Brief #16 executed)

Branch `chore/aspire-17`. Three commits, as planned in the brief. Tooling only, no behaviour change. Closes R-08
(no literal SDK version left) and R-31.

### Resolved versions (NuGet, 2026-11-24)
| Package | Version | Notes |
| --- | --- | --- |
| `Aspire.Hosting.AppHost`, `Aspire.Hosting.Dotnet`, `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.JavaScript`, `Aspire.Npgsql` | 17.0.0 (`AspireVersion`) | Latest stable; no 17.0.x patch yet |
| `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | 13.6.5 (`AspireNpgsqlEfVersion`) | Only `17.0.0-preview.1.26567.5` exists; D-227, R-45 |
| `Aspire.Cli` (.NET global tool) | 17.0.0 | Updated with the owner's approval (D-225) |
| PostgreSQL image | `postgres:18.6` | Aspire 17 default; Testcontainers aligned (D-223) |

Restore: no NU1605/NU1608. `dotnet list package --include-transitive`: one `Npgsql.EntityFrameworkCore.PostgreSQL`
(10.0.3). `Microsoft.Extensions.*` is not on one line: `FinGuard.UnitTests` resolves 10.0.14 for
`Configuration.Binder`, `Diagnostics.HealthChecks` and `Hosting.Abstractions` (through the 13.6.5 EF integration);
every other project resolves 10.0.15. The baseline already had such splits (33 packages split before, 25 after);
build and tests are green. Not pinned in this chore.

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-227 | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` stays on 13.6.5 under its own CPM property `AspireNpgsqlEfVersion`; every other Aspire package is on 17.0.0 | Owner-approved deviation from D-222 ("Aspire 17.0.x everywhere"): stable packages only. 13.6.5 depends on no other Aspire package |
| D-228 | `dev-session.ps1` also records and reports `FinGuard.*.exe` processes started from the repo | Aspire 17 runs `AddDotnetProject` resources through their own apphost (`FinGuard.Api.exe`), not `dotnet.exe`. Interface unchanged (D-225) |

### Execution checks
- Build `-c Release` with warnings as errors: 0 warnings. No `Aspire.AppHost.Sdk`, `AddProject<`, `Projects.` or
  ASPIRE0xx/CS0618 suppression left (git grep, outside this log and the briefs). `WithReference`, `WaitFor` and
  `WithHttpHealthCheck` compile on `DotnetProjectResource` (R-44: no API gap).
- `UserSecretsId` kept (the 17 migration guide keeps it). `aspire secret path`:
  `%USERPROFILE%\.aspire\secrets\45a8c4f7-9fe1-40ae-b7b8-2bc5442f10d4\Development.json`.
- CLI: `aspire --version` = `17.0.0+2d325b13709752ba6d9a943635c5e2940a2530c0` in PowerShell 7 and in Git Bash. The
  tool still installs only an `aspire.cmd` shim, so D-135's `aspire.cmd` / alias note still applies. `aspire doctor`:
  6 passed, 1 warning (VS Code extension not installed), 0 failed. `dotnet tool update` also printed
  `Unhandled exception: Access to the path 'bundle' is denied.`: `%USERPROFILE%\.aspire\bundle` is a September symlink
  to a 13.5.3 bundle that the update could not replace. The tool update succeeded and the AppHost ran; left as is.
- Dev password (D-224, R-43): Aspire 17 imported the 13.x user secrets into its store by itself; the
  `Parameters:postgres-password` values in both stores match (compared by hash, never printed). No `aspire secret
  set`, no volume reset.
- Run time (via `-StartAspire`): `postgres` on `postgres:18.6` (server 18.6); existing data kept
  (`portfolio.submissions`: 54 rows); latest applied migration equals the latest in code for all four modules; `api`
  Healthy; `web` Running and Healthy; `web` still gets `API_HTTP` and `API_HTTPS` (and `services__api__http__0`,
  `services__api__https__0`), so `proxy.conf.js` is unchanged; `POST /dev/token` through the `web` URL works;
  `valid-balanced.csv` uploaded through the proxy returns 201 `Accepted` and its evaluation is `Compliant`.
- `aspire ps --format Json` keeps `appHostPath`, `appHostPid` and `cliPid` (new fields: `status`, `sdkVersion`,
  `dashboardUrl`, `logFilePath`); `aspire start --format Json` still gives what the script reads.

### `dev-session.ps1` proof (PIDs and tokens redacted)
```
-Snapshot     Nothing of this repo is running. Baseline: 16 entries
-StartAspire  AppHost PID <pid> recorded in .git\finguard-agent-processes.txt
-Report       agent  AppHost           src\backend\FinGuard.AppHost\FinGuard.AppHost.csproj
              new    Aspire CLI        ...\aspire.cli\17.0.0\...
              new    Aspire container  postgres-<suffix> postgres:18.6
              new    DCP (x8)          ...\aspire.cli\17.0.0\...
              new    Repo process      src\backend\FinGuard.Api\bin\Debug\net10.0\FinGuard.Api.exe
              new    Repo process      node ...\finguard-web\node_modules\... (ng serve)
-Stop         Stopped AppHost (PID <pid>); Stopped Repo process (PID <pid>); dotnet build-server shutdown done
              Nothing of this repo is running.
-Report       Nothing of this repo is running.
```
Before D-228, the first `-Report` did not list the Api process.

### Tests
- Backend `dotnet test -c Release` on `postgres:18.6`: 442 / 442 (baseline 442).
- Vitest: 103 / 103. `pnpm e2e` twice against the Aspire 17 run: 8 / 8 both times.

### Risks and flags
- **R-45 Aspire EF integration behind the 17 line.** `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` has no stable
  17.x. Dependabot ignores majors (D-48), so each phase's majors review checks NuGet by hand; when 17.x stable ships,
  move it to `AspireVersion` and drop `AspireNpgsqlEfVersion`.
- R-08 and R-31 closed. R-43 closed (not triggered). R-44 closed (no gap found).

### Next step
Unchanged from the planning delta below: Phase 4 planning.

## Delta 2026-11-24 (session 25: Brief #16 planning)

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

## Delta 2026-11-23 (session 24: Brief #15 executed)

Branch `feat/evaluation-result-ui`. Five commits, as planned in the brief. The owner approved the answers to the
plan's open points (D-213, D-214, D-215) and accepted the `toFixed` risk under R-42.

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-213 | Alert sentences: Warning "{Class} is near its limit ({x} of the limit used).", Breached "{Class} is over its limit ({x} of the limit used).", `{x}` from `formatRatio` | Owner-approved; replaces the brief's "Shares is breached at 104.20 % of its limit.". Exactly 100 % reads "near its limit (100.00 % of the limit used)", which matches the rule |
| D-214 | The theme toggle shows on every page, `/login` included | Owner-approved: the theme is a browser preference, not an account setting |
| D-215 | Every E2E flow imports `test` from `e2e/support.ts`, whose automatic fixture records requests and fails the test when one leaves the `baseURL` origin (`data:` and `blob:` allowed), naming the URL | Owner-approved; makes "no request to a third-party host" a permanent check instead of a manual one |
| D-216 | `ComplianceApi.evaluation` takes `Signal<string \| undefined>`; `undefined` sends no request | The brief said `Signal<string>`. The detail page passes the submission id only when the status is `Accepted`, which is how a rejected submission never requests it (D-204) |
| D-217 | Polling lives in `SubmissionDetail` (one `setTimeout` at a time, an `effect` that schedules the next retry on each `Compliance.Evaluation.NotFound`, `DestroyRef` clears it); no `core/` utility | Used in one place only. "Check again" resets the counter and reloads |
| D-218 | `formatPercent` (2 decimals) sits next to `formatRatio` in `shared/format-ratio.ts` for shares and limits | The brief's "plain 2 decimals" |
| D-219 | The `--fg-status-*` tokens landed with the evaluation section (commit 2), not with the theme toggle (commit 3) | The pills use them; the commit would otherwise render with undefined variables |
| D-220 | Screenshots come from `e2e/screenshots.e2e.ts`, tagged `@screenshots`; `playwright.config.ts` excludes the tag unless `E2E_SCREENSHOTS` is set: `$env:E2E_SCREENSHOTS = '1'; pnpm e2e --grep '@screenshots'` | Viewport 1280×800, full page, Alice, `valid-shares-heavy.csv` with a random past cut-off date |
| D-221 | `randomPastDate()` and `sample()` moved to `e2e/support.ts`; the duplicate flow uses the shared helper | Same behaviour |

### Status token contrast (WCAG 2.x, text on pill background; values read back from the running app)
| Token pair | Light | Ratio | Dark | Ratio |
| --- | --- | --- | --- | --- |
| compliant fg / bg | `#0d5223` / `#d7f5dd` | 8.00:1 | `#a8f0b8` / `#0f3d1c` | 9.28:1 |
| warning fg / bg | `#6b4300` / `#ffe8b3` | 7.19:1 | `#ffd98a` / `#4a3000` | 9.07:1 |
| breached (`--mat-sys-on-error` / `--mat-sys-error`) | `#ffffff` / `#ba1a1a` | 6.46:1 | `#690005` / `#ffb4ab` | 7.72:1 |

### Tests added
- Integration: `GetEvaluation_Own_Returns200WithClassesAndAlerts` asserts `alertThreshold` 0.90.
- Vitest: `formatRatio` (the brief's cases, plus a threshold other than 0.9; the 8-decimal cap case is
  `0.89999999996` Compliant → `90.00000000 %`, documented in the test), `formatPercent`, `UsdPipe` (`$0.30` for
  0.1 + 0.2; the large value is 90,000,000,000,000.25 because .01 is not representable there, see R-42),
  `ThemeService` (default, set, stored value, unknown value, throwing storage), `SubmissionDetail` (request only for
  accepted, result, rows in order, alert sentences, no alerts, "Evaluating…" in a polite live region then success
  after a retry, timeout and "Check again", other error, no retry after destroy), `App` (toggle with accessible name
  on a signed-out page, toggle changes the theme). Two existing `App` tests now find the sign-out button by its text.
- E2E: `evaluation.e2e.ts` (balanced → Compliant, no alerts, policy version 1, $5,000,000.00; shares-heavy →
  Breached, Shares row breached, one alert "Shares is over its limit (293.33 % of the limit used)."), `theme.e2e.ts`
  (Dark persists across a reload; System under a dark OS gives the same background as Dark).

### Verification
- `dotnet build -c Release` (0 errors), `dotnet format --verify-no-changes`, `dotnet test -c Release` green. Backend
  442 (unit 280, integration 147, architecture 15), unchanged in count.
- Frontend: `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm test --watch=false` (103, was 73), `pnpm build`,
  `pnpm exec prettier --check src e2e` green.
- `-StartAspire`: `pnpm e2e` twice, 8 passed both times (the four existing flows, two evaluation, two theme). The
  same-origin fixture found no third-party request. `invalid-mixed.csv`: detail page `Rejected`, no Compliance
  section, no evaluation request. Screenshots written to `docs/images/`. Aspire stopped with `-Stop`.

### Risks and flags
- **R-39 closed** by D-207.
- **R-42** (amounts as JSON numbers) also covers display: `formatRatio` uses `Number.prototype.toFixed` on the
  IEEE double, so a ratio within about 1e-15 of a decimal boundary can round either way. Owner-accepted; the 8-decimal
  cap bounds it. String decimals are considered in Phase 5.
- `material-theme.scss` still sets `color-scheme: light dark` on `html`; it is the stylesheet default that the
  `ThemeService` inline style overrides.

### Next step
Unchanged from the planning delta: the Aspire 17 chore brief, then Phase 4 planning.

## Delta 2026-11-23 (session 24: Brief #15 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-204 | The compliance result shows on the submission detail page only, for accepted submissions | History column deferred |
| D-205 | Pending evaluation: 404 `Compliance.Evaluation.NotFound` → "Evaluating…" with retries at 1, 2, 4, 8 s, then "not available yet" + "Check again" | |
| D-206 | `EvaluationResponse` gains `alertThreshold` from the policy snapshot (additive) | |
| D-207 | `formatRatio` widens the decimals until the shown percentage agrees with the API status (max 8); it never derives a status | Resolves R-39 |
| D-208 | `UsdPipe` is the only money formatter (en-US, USD, 2 decimals) | |
| D-209 | Status = text + pill with `--fg-status-*` semantic tokens (`light-dark()`, WCAG AA) defined once in `styles.css` | Extends D-106 |
| D-210 | `ThemeService` with system / light / dark in `localStorage` (`finguard.theme`), applied through `color-scheme` at startup; no inline script | Closes the Phase 3 part of D-106 |
| D-211 | README carries Playwright screenshots made from sample data only | |
| D-212 | Phase 3 closes with Brief #15; next is the Aspire 17 chore brief (D-185), then Phase 4 | |

### Risks and flags
- **R-39** expected to close with D-207.
- **R-42 JSON numbers for money.** Amounts reach the SPA as JSON numbers (IEEE double). Cent-exact display holds up to
  about 9×10^13 USD, far above realistic portfolios, but the CSV allows up to 10^16. Revisit in Phase 5 (string
  decimals) if real data could approach it.

### Next step
Chore brief: Aspire 13.6 → 17 (AppHost SDK change, `AddProject` replacement, parameters, secrets path,
`dev-session.ps1`, `docs/dev-environment.md`), then Phase 4 planning (rebalance engine, Advisory, R-40 backfill).

## Delta 2026-11-21 (session 23: Brief #14 executed)

Branch `feat/compliance-evaluation`. Five commits, as planned in the brief. The owner approved the answers to the
plan's open points (D-198, D-199, R-40, R-41).

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-198 | `compliance.evaluations` has no `submission_received_at` column; its index is `(tenant_id, cut_off_date)` | Owner-approved deviation from the brief's table design. "Which evaluation is current for a cut-off date" is a read concern: `compliance.portfolio_snapshots` already holds `received_at` with the index `(tenant_id, cut_off_date, received_at)` in the same schema, so a read that needs it joins `evaluations` to `portfolio_snapshots` on `submission_id` (`tenant_id` filter on both tables). No domain change, no extra repository parameter |
| D-199 | `EvaluationCompleted` has no route until Advisory subscribes | Owner-approved. Observed on Wolverine 6.64.0 (tracked session of an upload): `ExecutionStarted EvaluatePortfolio`, `NoRoutes EvaluationCompleted`, `ExecutionFinished`, `MessageSucceeded`. No `Sent` record and no outbox row. `Upload_Valid_PublishesOneEvaluationCompleted` counts records of type `Sent` or `NoRoutes` matched by `SubmissionId` (D-165), so it keeps passing once a consumer subscribes. See R-40 |
| D-200 | `LimitPolicyRepository` and `EvaluationRepository` are `public sealed` | Found by the first integration run: `InvalidServiceLocationException ... Concrete type FinGuard.Compliance.Persistence.EvaluationRepository is not public, so requires service location` (same for `LimitPolicyRepository`); the message went to the error queue. Public types let Wolverine's generated code build them with the handler's own `ComplianceDbContext`, the same reason the DbContext is public (D-94). Entities and row models stay internal |
| D-201 | `SubmissionAcceptedHandler.Handle` returns `OutgoingMessages` (empty for a duplicate event), not a tuple | The brief said "tuple return, D-164". The tuple exists to answer `InvokeAsync<Result<T>>`; an event handler has no response, so the cascade alone is enough. Same mechanism |
| D-202 | Statuses and asset classes are stored (`varchar(16)`, EF `HasConversion<string>()`) and returned by the API as enum names (`Compliant`, `FixedIncome`) | Same convention as Portfolio's `status`; the Api has no `JsonStringEnumConverter`. The response also carries `evaluationId`, `submissionId` and `cutOffDate` |
| D-203 | The transport-check rule (`AGENTS.md`, `backend-vsa` rules, overview) lists the pre-2000 cut-off date | Follows D-190 |

### Tests added
- Unit: `ReceiveSubmissionHandlerTests` (1999-12-31 gives `CutOffDateTooEarly`, and `NotCsv` still wins first;
  `Validate_CutOffOnFirstPolicyDate_PassesTransportChecks`, so `Validate` is now `internal`),
  `EvaluationCompletedTests`, `ContractMappingTests` (every enum value, undefined values throw, event payload),
  `EvaluationRowTests`, `EvaluatePortfolioTests` (`ToString`).
- Integration: `EvaluatePortfolioTests` (the brief's flow tests, plus
  `EvaluatePortfolio_NoPolicyForCutOffDate_GoesToErrorQueueAndKeepsSnapshot` for D-191 and
  `Evaluation_ForBob_IsInvisibleToAlicesComplianceContext` for both new tables) and `GetEvaluationTests`. The brief's
  "the API returns `Warning`" part of the exactly-at-limit case is its own test,
  `GetEvaluation_ExactlyAtLimit_ReturnsWarningWithUtilizationOne`.

### Verification
- `dotnet build -c Release` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test -c Release` green. Tests:
  backend 442 (unit 280, integration 147, architecture 15), was 413 (unit 268, integration 130, architecture 15).
  Frontend: `pnpm lint`, `pnpm test --watch=false` (73), `pnpm exec prettier --check src e2e` green.
- `has-pending-model-changes`: no changes after `AddEvaluations`.
- `-StartAspire`, as Alice with a fresh cut-off date: `valid-balanced.csv` gives one evaluation, `Compliant`, policy 1,
  4 classes, no alerts; `valid-shares-heavy.csv` gives one evaluation, `Breached`, policy 1, 4 classes, one alert
  (Shares, Breached). `GET .../evaluation` returned both. `wolverine.wolverine_dead_letters`: 0 rows.
- `pnpm e2e` twice against Aspire: 4 passed both times. Aspire stopped with `-Stop`.

### Risks and flags
- **R-38 closed** by D-190.
- **R-39** (display rounding vs status) carried to Brief #15.
- **R-40 Events before Advisory are dropped.** `EvaluationCompleted` published before Advisory subscribes has no
  route (D-199). Phase 4's first brief decides whether to backfill from `compliance.evaluations`.
- **R-41 Simultaneous duplicate `EvaluatePortfolio`.** Two deliveries running at the same moment both pass the
  existence check; the unique `submission_id` makes one fail, and it reaches the error queue. Replay is safe (it
  skips). Owner-accepted; no extra Wolverine policy.

### Next step
Brief #15 (closes Phase 3): evaluation result in the submission detail page (status, classes, alerts, policy version),
"evaluating…" state while the 404 lasts, three-state theme toggle (D-106), E2E for an accepted and a breached upload.
R-39 applies.

## Delta 2026-11-21 (session 23: Brief #14 planning)

### Session 22 closing decisions (agreed in the planning chat, recorded here with repo IDs)
| ID | Decision | Notes |
| --- | --- | --- |
| D-185 | Aspire 17 upgrade in its own chore brief after Brief #15 and before Phase 4 | Closes R-08 and R-31 when done; `dev-session.ps1` and `docs/dev-environment.md` in scope |
| D-186 | pnpm stays on 11 until GitHub's dependency graph reads two-document lockfiles (dependabot-core #15904); re-check at Phase 5 start | Confirms D-42 |
| D-187 | Node 26 arrives with the Phase 5 container images | |
| D-188 | EFCore.NamingConventions stays on 10.x with .NET 10 LTS | |
| D-189 | Minor and patch updates come only through Dependabot's monthly grouped PRs; no manual chore PR | D-48 |
| D-190 | Portfolio rejects cut-off dates before 2000-01-01 (`Portfolio.Submission.CutOffDateTooEarly`, 400, not stored); the constant points to D-175 | Resolves R-38 (option b) |

### Brief #14 decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-191 | A null effective policy in Compliance is a bug after D-190: the handler throws and the message goes to the error queue | |
| D-192 | The snapshot handler cascades an internal `EvaluatePortfolio` command; snapshot and evaluation commit separately | Replayable evaluation without losing the received fact |
| D-193 | One evaluation per submission (unique `submission_id`); re-evaluation post-MVP | |
| D-194 | `IEvaluationRepository` maps the aggregate to row models; no EF constructors in `Domain/` | Same style as `LimitPolicyRow` |
| D-195 | `share` and `utilization` stored as unconstrained `numeric` (exact unrounded values); alerts derived, not stored | |
| D-196 | `EvaluationCompleted` in `FinGuard.Compliance.Contracts.Events`; Compliance.Contracts defines its own `AssetClassCode` and `ComplianceStatusCode`; no contract-to-contract references | |
| D-197 | `GET /api/compliance/submissions/{submissionId}/evaluation`; 404 `Compliance.Evaluation.NotFound` for unknown, other tenant or not yet evaluated; exact decimals, no rounding in the API | |

### Risks and flags
- **R-38** resolved by D-190.
- **R-39 Display rounding vs status.** u = 0.89999996 is Compliant but rounds to 90.00 %. #15 must format so the
  shown number never contradicts the status (for example truncate to 2 decimals, or show the status next to a
  percentage with more precision near thresholds) and must never recompute a status.

### Next step
Brief #14 execution (see the delta above).

## Delta 2026-11-20 (session 22: Brief #13 executed)

Branch `feat/compliance-domain-and-policy`. Four commits, as planned in the brief. Phase 3 started.

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-179 | `ILimitPolicyRepository.GetEffectiveAsync` returns `LimitPolicy?`: null when the cut-off date is before the first version | Owner-approved. Reachable: Portfolio accepts cut-off dates before the 2000-01-01 seed, so **Brief #14 must handle null explicitly**. Integration test: 1999-12-31 returns null |
| D-180 | `compliance.limit_policies` has CHECK constraints mirroring the `LimitPolicy` invariants: each limit `> 0 AND <= 1`, `alert_threshold > 0 AND <= 1`, `rebalance_target_ratio > 0 AND < alert_threshold`, `rebalance_target_ratio * (cash + fixed_income + shares + real_estate) >= 1` (EF `HasCheckConstraint`, names `ck_limit_policies_*`) | Owner's adjustment to the plan. The repository still maps through `LimitPolicy.Create` and throws `InvalidOperationException` (version and error code only) as a second line of defence. `GetEffective_CorruptedRow_Throws` was replaced by `LimitPolicies_InvalidRow_IsRejectedByCheckConstraint` (SqlState 23514, one case per constraint) and a unit test of `LimitPolicyRow.ToDomain` |
| D-181 | Every `Compliance.Policy.*` error is `ErrorType.Validation` | Owner-approved |
| D-182 | The domain allow-list test evaluates an architecture that also loads Compliance's direct references (`LoadAssembliesIncludingDependencies`, not recursive) | The shared `Architecture` loads only `FinGuard.*` assemblies, so ArchUnitNET could not see a dependency on EF Core: the temporary probe (`DbContext` property in `Domain/`) passed the first version of the rule. The old `DependsOnNoInfrastructure` rule had the same blind spot. With the fix the probe failed on both `Microsoft.EntityFrameworkCore.DbContext` and `FinGuard.Portfolio.Contracts.AssetClassCode`; a probe using `Error` passed. Probe reverted |
| D-183 | One status enum `ComplianceStatus` (`Compliant`, `Warning`, `Breached`, ordered by precedence) for class and overall status; overall = max | The brief named the class status `ClassStatus`; one enum keeps one word set (D-173) |
| D-184 | The seed row has a fixed `created_at` of 2026-11-20T00:00Z (`HasData` needs a constant) | |

### Verification
- `dotnet build -c Release` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test -c Release` green. Tests:
  backend 413 (unit 268, integration 130, architecture 15), was 332 (unit 201, integration 116, architecture 15).
- `has-pending-model-changes`: no changes after `AddLimitPolicies`.
- `-StartAspire`, then SQL on the dev database: `compliance.limit_policies` has one row (version 1, effective
  2000-01-01, 0.4000 / 0.3000 / 0.2500 / 0.2500, threshold 0.9000, target 0.8500) and the seven `ck_limit_policies_*`
  constraints. Aspire stopped with `-Stop`.

### Majors review (R-13, report only; checked 2026-11-20)
Sources: `dotnet list package --outdated`, `pnpm outdated`, NuGet registration API, aspire.dev and the Aspire 17.0
change log, the pnpm and dependabot-core issue trackers, nodejs.org.

| Component | Current | Latest major | Finding | Recommendation |
| --- | --- | --- | --- | --- |
| Aspire | 13.6.5 | 17.0.0 (2026-11-18) | Targets .NET 10 (no .NET 11 needed). Breaking: `Aspire.AppHost.Sdk` obsolete (ASPIRE012), `AddProject`/`AddCSharpApp` deprecated, parameters required by default, secrets move to `~/.aspire/secrets`. Sources: https://aspire.dev/whats-new/aspire-17/, https://github.com/microsoft/aspire/wiki/17.0-Change-log | Own chore brief after Phase 3 (after Brief #15), before Phase 4; must also update `dev-session.ps1` if the CLI changes |
| pnpm | 11.28.9 | 12.21.0 | Dependabot version updates read pnpm 12, but the dependency graph (alerts) still reads only the first lockfile document: dependabot-core #15904 and pnpm #13805 are open. R-11 unchanged. Sources: https://github.com/dependabot/dependabot-core/issues/15904, https://github.com/pnpm/pnpm/issues/13805 | Stay on 11 (D-42). Re-check at the start of Phase 5; upgrade only when the graph reads two-document lockfiles |
| Node | 24.x (`^24.15.0`) | 26 (active LTS since 2026-10) | Node 26 is the last release on the old schedule; Angular 22.3 accepts it. Source: https://nodejs.org/en/blog/announcements/evolving-the-nodejs-release-schedule | Upgrade before Phase 5, together with the Container Apps image (unchanged) |
| EFCore.NamingConventions | 10.0.1 | 11.0.0 (2026-11-20, new since the session 18 review) | Follows EF Core 11 / .NET 11 (STS) | Stay on 10 with .NET 10 LTS |
| `Microsoft.*` 11.x, .NET 11 | 10.x | 11.0.0 | Unchanged since session 18 | Stay on 10 LTS |
| Minor/patch | Dapper 2.1.89, Testcontainers 4.16.0, WolverineFx 6.64.0, Angular 22.2.x, ESLint family, Vitest 5.1.0 | 2.1.100, 4.17.0, 6.64.2, 22.3.0, minors, 5.1.1 | No new major | One chore PR when convenient |

### Risks and flags
- **R-37 Decimal division** (from planning): documented in ADR-0012; boundary tests assert exact utilizations.
- **R-38 Null policy.** See D-179: Brief #14 must decide what an evaluation for a cut-off date before 2000-01-01 does
  (for example skip with a logged reason, or reject upstream).
- The README status table had the event-flow paragraph in the middle of the table (rows 4 and 5 rendered as text);
  moved below the table while updating Phase 3.

### Boundary table as run (seed policy, total 1,000,000.00, class value = u x limit x total, exact)
| u | Cash value | Fixed income value | Shares value | Real estate value | Status |
| --- | --- | --- | --- | --- | --- |
| 0.8999 | 359,960.00 | 269,970.00 | 224,975.00 | 224,975.00 | Compliant |
| 0.90 | 360,000.00 | 270,000.00 | 225,000.00 | 225,000.00 | Warning |
| 0.9001 | 360,040.00 | 270,030.00 | 225,025.00 | 225,025.00 | Warning |
| 0.9999 | 399,960.00 | 299,970.00 | 249,975.00 | 249,975.00 | Warning |
| 1.00 | 400,000.00 | 300,000.00 | 250,000.00 | 250,000.00 | Warning |
| 1.0001 | 400,040.00 | 300,030.00 | 250,025.00 | 250,025.00 | Breached |
| 0 | 0.00 | 0.00 | 0.00 | 0.00 | Compliant |
Unrounded cases: shares 224,999.99 gives u = 0.89999996 (Compliant; 0.9000 when rounded to 4 decimals); shares
250,000.01 gives u = 1.00000004 (Breached; 1.0000 when rounded).

### Next step
Brief #14, as in the planning delta below, plus D-179 (null policy).

## Delta 2026-11-20 (session 22: Brief #13 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-169 | Compliance `Domain/` may depend only on `System.*`, itself and `FinGuard.BuildingBlocks.Results` (architecture allow-list) | Closes open question 1 of session 5 |
| D-170 | `LimitPolicy.Create` returns `Result`; `Evaluation.Create` guards throw on impossible inputs | Expected vs unexpected failures (locked decision) |
| D-171 | Compliance stays one project with `Domain/`, `Features/`, `Persistence/`; direction enforced by tests | |
| D-172 | Domain owns `AssetClass`; `AssetClassCode` (Portfolio.Contracts) is mapped in `Features/` | |
| D-173 | Class status Compliant / Warning (threshold ≤ u ≤ 1) / Breached (u > 1); an alert per Warning or Breached class | |
| D-174 | Minimal in-memory `Specification<T>` (`IsSatisfiedBy`, `And`, `Not`) with `IsInAlert` and `IsBreached` | |
| D-175 | Global immutable policy versions in `compliance.limit_policies` (four limit columns, threshold, target ratio, `effective_from` unique); seed v1 effective 2000-01-01 | Not tenant-owned |
| D-176 | An evaluation uses the policy in force at the cut-off date (highest `effective_from` ≤ cut-off) | |
| D-177 | `ILimitPolicyRepository` (EF, write side) maps rows through `LimitPolicy.Create` and throws on corrupted rows | |
| D-178 | ADR-0012: Compliance domain model | |

### Risks and flags
- **R-37 Decimal division.** `decimal` keeps 28-29 significant digits; utilizations are exact for the boundary inputs
  but repeating fractions are truncated. Comparisons stay correct at the boundaries because the inputs are cents and
  the thresholds have ≤ 4 decimals; document it in ADR-0012 and keep the boundary tests exact.

### Next step
Brief #14: evaluate on `SubmissionAccepted` (from the stored snapshot), persist `Evaluation` (repository), publish
`EvaluationCompleted` through the outbox, and a read API for the latest evaluation per submission.

## Delta 2026-11-20 (session 21: Brief #12 executed)

Branch `feat/submission-accepted-event`. Four commits, as planned in the brief.

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-161 | `ComplianceDbContext` is `public sealed`; `PortfolioSnapshot` stays `internal` | Approved deviation, same pattern as D-94: Wolverine generates code only for public handlers, and a public `Handle` cannot take an internal parameter type |
| D-162 | Logs and message `ToString()` carry identifiers, never dates, amounts, file names or hashes. `SubmissionAccepted.ToString()` prints the type name, `EventId` and `SubmissionId`; `AssetClassTotal.ToString()` prints the class; the snapshot log line carries the submission id only | Owner's rule, added to `backend-vsa` (`.md` and `.mdc`). Supersedes the brief's wording that put `CutOffDate` in `ToString()` |
| D-163 | `AssetClassTotal` lives in `FinGuard.Portfolio.Contracts` (next to `AssetClassCode`), not in `.Contracts.Events` | `MessagingRulesTests` treats every type in an Events namespace as an integration event; moving the record keeps the rule unchanged. The brief had it in `Events/SubmissionAccepted.cs` |
| D-164 | `ReceiveSubmissionHandler.Handle` returns `(Result<SubmissionReceivedResponse>, OutgoingMessages)`; `InvokeForCurrentTenantAsync<Result<T>>` receives the first item and Wolverine cascades the second. No fallback needed | Closes R-34. Proven by every existing upload test (response unchanged) and `Upload_ValidCsv_CascadesOneSubmissionAcceptedWithClassTotals`. The handler takes `ITenantContext` for the payload tenant |
| D-165 | Event tests run on the shared Api host in one xUnit collection (`SubmissionEvents`) and match their own `SubmissionId`/`EventId`, never a global count. The duplicate test asserts exactly one `SubmissionAccepted` for the original id across both uploads; the rejected test asserts zero for the rejected id | Approved with the owner's condition that negative assertions still prove something |
| D-166 | The consumer's defensive check also rejects repeated or unknown asset classes (exactly the four `AssetClassCode` values, each once) | Needed to map the totals to four columns without a silent overwrite |
| D-167 | No new architecture rule for "Compliance does not reference `FinGuard.Portfolio`" | `Modules_AnyAssembly_ReferenceOnlyBuildingBlocksAndContracts` already fails on any module-to-module reference |
| D-168 | Tests compare stored timestamps truncated to microseconds | PostgreSQL keeps microseconds, .NET ticks are 100 ns; the first full run failed once on this |

### Verification
- `dotnet build -c Release` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test -c Release` green twice in a
  row. Tests: backend 332 (unit 201, integration 116, architecture 15), was 309.
- `-StartAspire`, Alice, cut-off date 2025-07-15: `valid-balanced.csv` gave `201` and exactly one
  `compliance.portfolio_snapshots` row with Acme's `tenant_id`; its totals equal the sums of its stored positions and
  match the sample README (total and rounded percentages), all compared in SQL; `invalid-mixed.csv` gave `201`
  (Rejected) and no row; the duplicate gave `200` with the original id and no new row; `wolverine_dead_letters` is empty
  (0 after the E2E run too).
- `pnpm e2e`: 4 flows green against the same instance. Aspire stopped with `-Stop`.

### Risks and flags
- **R-24 closed:** the end-to-end test runs through the real module registration and the durable local queue.
- **R-34 closed:** see D-164.
- **R-36 closed** (owner's request, same PR, commit `fix: keep dates out of message ToString overrides`):
  `ReceiveSubmission.ToString()` no longer prints `CutOffDate`; a unit test asserts a marker date and the file name are
  absent. Audit of every `ToString()` override in `src/backend` (type -> fields printed):
  - `ReceiveSubmission` -> type name, `SizeBytes` (was also `CutOffDate`: fixed)
  - `SubmissionReceivedResponse` -> type name, `SubmissionId`, `Status`, `RowCount`, `ErrorCount`, `Duplicate`
  - `GetSubmission` -> type name, `SubmissionId`
  - `ListSubmissions` -> type name only
  - `GetCurrentTenant` -> type name only
  - `SubmissionAccepted` -> type name, `EventId`, `SubmissionId`
  - `AssetClassTotal` -> type name, `AssetClass`
  Records without an override (synthesized `ToString()` prints every property; none is a Wolverine message, so
  `MessageTypes_Always_OverrideToString` does not cover them and nothing logs them today): query responses
  `SubmissionDetailsResponse`, `SubmissionErrorItem`, `SubmissionListResponse`, `SubmissionListItem`,
  `CurrentTenantResponse`; internal `CsvError`, `ParsedPosition`, `PortfolioCsvResult`, the Dapper `Row` records and
  `DevIdentity`. Never log them whole.

### Next step
Brief #13 (Phase 3 start), unchanged from the planning delta below.

## Delta 2026-11-20 (session 21: Brief #12 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-152 | Integration event `SubmissionAccepted` (Portfolio → Compliance), published once per new accepted submission; no event for rejected or duplicate uploads | Closes open question 2 of session 18 (naming). Supersedes `PortfolioSubmitted`/`PortfolioRejected` in `overview.md` |
| D-153 | Event-carried state: four `AssetClassTotal(AssetClassCode, decimal BookValue)` entries, zeros included, plus submission id, cut-off date and `ReceivedAt`; Compliance never calls back into Portfolio | Closes open question 2 of session 20 |
| D-154 | `AssetClassCode` enum lives in `FinGuard.Portfolio.Contracts`; Compliance maps it to its own domain type | |
| D-155 | Published by cascading from `ReceiveSubmissionHandler` inside the EF transaction (tuple with `OutgoingMessages`; fallback `IMessageContext.PublishAsync`) | Atomic with the submission rows |
| D-156 | Local queues: Wolverine default (one durable local queue per message type); no named per-module queues | Closes open question 1 of session 13 |
| D-157 | Compliance stores `portfolio_snapshots` (one row per accepted submission, four class columns, unique `source_event_id`), a persistence entity, not a domain aggregate | Phase 3 builds `Evaluation` from it |
| D-158 | No backfill for submissions accepted before this brief | Development data only |
| D-159 | ADR-0011: Portfolio → Compliance integration contract; additive changes only, breaking changes need a new event type | |
| D-160 | Phase 2 closes with Brief #12; Brief #13 opens Phase 3 with the dependency-majors review (R-13, R-31 Aspire 17) in its preflight | |

### Verified on 2026-11-20
| Area | Finding | Source |
| --- | --- | --- |
| Wolverine local queues | Default: an individual local queue per message type, named after the type | https://wolverine.netlify.app/guide/messaging/transports/local |
| Wolverine return values | Tuple returns: each item handled independently; `OutgoingMessages` cascades its items | https://wolverine.netlify.app/guide/handlers/return-values |

### Risks and flags
- **R-24** expected to close: the end-to-end test runs through the real module registration.
- **R-34 Tuple return with `InvokeAsync<T>`.** Likely supported, not yet proven in this codebase; the first commit with
  the change proves it or switches to the fallback.
- **R-35 Amounts in envelope storage.** Wolverine persists event bodies (with totals) in `wolverine` tables. That is
  storage, not logging, and stays inside the database; Phase 5 must keep database access and backups as protected as
  the module tables.

### Next step
Brief #13 (Phase 3 start): majors review, Compliance domain (`LimitPolicy` versioned with seed, `Evaluation`,
`Money`/`Ratio`/`AssetClass`, specifications), boundary tests, and the decision on `Result` in the Compliance domain
(open question 1 of session 5).

## Delta 2026-11-20 (session 20: Brief #11 executed)

Branch `feat/submission-idempotency-and-current-version`. Six commits, as planned in the brief.

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-145 | Handler tests that reach the duplicate pre-check run against real PostgreSQL in `FinGuard.Api.IntegrationTests` (`ReceiveSubmissionHandlerTests`, `FakeTimeProvider`, fresh tenant ids). The unit project keeps only the transport-check cases, which return before the query | Approved deviation: the unit tests used a placeholder Npgsql connection that cannot run a query. Rejected: EF InMemory/SQLite (new dependency, different filter and `char(64)` behaviour), an injectable lookup seam (abstraction only for tests) |
| D-146 | `ReceiveSubmissionHandler.Handle` is async (`Task<Result<T>>`, `CancellationToken`) for the pre-check; the duplicate path uses `AsNoTracking` reads only | No entity is added for a duplicate, so Wolverine's transaction has nothing to save |
| D-147 | `200 OK` for a duplicate sets `Location` through `HttpResponse.Headers` and returns `TypedResults.Ok` | ASP.NET Core has no typed "OK with Location" result |
| D-148 | List `isCurrent` uses a `DISTINCT ON (cut_off_date)` CTE over the tenant's accepted rows, LEFT JOINed to the page; details use `NOT EXISTS` on a newer accepted row with a row comparison `(received_at, id) >` | Same rule as the brief's `ROW_NUMBER` example, shorter SQL. Both sub-selects filter `tenant_id` |
| D-149 | `errorMessage` returns one line, `"{title}: {detail}"`, for 4xx with a detail; title alone without one; the generic text for 5xx, non-ProblemDetails and non-`ApiError` values. `ApiError` order is `(status, title, detail?, code?, traceId?)`. `Login` uses the helper too | Replaces `errorTitle`; no component reads `ApiError.title` directly any more |
| D-150 | E2E helpers live in `e2e/support.ts`; every upload has unique content. The duplicate flow uses a random past cut-off date, so the "current" rows of the parallel flows (today) never interfere; the future-date check sits in `submissions.e2e.ts` | Playwright runs files in parallel workers against one shared database |
| D-151 | The session 6 line edited in place by PR #18 ("(Session 19: still true; …)") stays as it is on `main` | Reverting it would itself change a line and fail the new guard; `git diff 57d5ad5 -- docs/decisions.md` therefore shows that one changed line besides the additions |

### Verification
- Restored deltas: lines 6-156 of `57d5ad5:docs/decisions.md` inserted below the session-hygiene delta.
  `git diff 57d5ad5 --numstat -- docs/decisions.md` before this delta: `75 1` (the session-hygiene delta plus the
  D-151 line).
- CI guard, proven locally with the job's script against `main`: the branch passes (0 deleted lines); a throwaway
  commit that edited an old line (line 600) failed with `docs/decisions.md is append-only: …` (exit 1, numstat
  `152 1`); the commit was dropped with `git reset --hard HEAD~1`. A first attempt edited line 200, which lies inside the
  restored block and so counts as an addition against `main`; correct behaviour, but no proof.
- `EXPLAIN (ANALYZE)` on the dev database (22 submissions, 1 tenant): both queries seq-scan, as expected at this size
  (list 0.09 ms, details 0.02 ms). With `enable_seqscan = off` the list query uses the `tenant_id` prefix of
  `ix_submissions_tenant_id_cut_off_date_sha256` and an incremental sort presorted on `cut_off_date`. No new index for
  `isCurrent`; revisit if a tenant reaches thousands of submissions.
- Tests: backend 309 (unit 187, integration 107, architecture 15), was 293; Vitest 73, was 64; E2E 4 flows, two
  runs in a row green against one `-StartAspire` instance. `dotnet format --verify-no-changes`, `pnpm lint`,
  `pnpm exec prettier --check src e2e`, `pnpm build` green.

### Next step
Brief #12 (closes Phase 2), unchanged from the planning delta below.

## Delta 2026-11-20 (session 20: Brief #11 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-137 | `docs/decisions.md` is append-only; corrections supersede through a new delta; CI `hygiene` fails PRs that delete or change existing lines | PR #18 silently dropped the Brief #10 deltas while resolving a conflict |
| D-138 | Two branches in parallel only if at most one adds a decisions delta | Root cause of the loss |
| D-139 | Duplicate = same tenant + cut-off date + SHA-256 of raw bytes; returns the earliest match whatever its status; `200 OK` + `duplicate: true` + `Location`; nothing stored | Implements D-11 |
| D-140 | Enforced by a pre-check and a non-unique index `(tenant_id, cut_off_date, sha256)`; a simultaneous identical upload may be stored twice (accepted race) | Unique constraint rejected: existing duplicate rows in dev/test data; one user per tenant |
| D-141 | `isCurrent` is derived at read time: latest accepted by `received_at` (tie: `id`) per tenant and cut-off date; rejected never current | Not stored |
| D-142 | SPA shows `title` + `detail` for 4xx through one helper; 5xx stays generic; codes only as `data-code` | Closes R-28; supersedes D-80's "only title" |
| D-143 | README is the reviewer's landing page and is updated when a phase or a visible feature changes (Definition of done) | |
| D-144 | Phase 5 hosting (planned, ADR in Phase 5): Angular SPA on **Azure Static Web Apps Free**, Api on **Azure Container Apps** (Consumption); SPA calls the Api cross-origin with bearer tokens; the Api gets a CORS policy for the SWA origin only; `staticwebapp.config.json` carries the SPA fallback and security headers (CSP, R-22) | Closes open question 1 of session 7. Rejected: second container app with nginx (cold start for static files, an image to maintain); SPA served by the Api container (couples deploys, blank page while the Api scales from zero); SWA Standard linked backend (~monthly fee; not zero-cost) |

### Verified on 2026-11-20
| Area | Finding | Source |
| --- | --- | --- |
| Static Web Apps | Free plan has no SLA; Standard is for production with SLA | https://azure.microsoft.com/pricing/details/app-service/static/ |
| Static Web Apps | Quotas: 100 GB bandwidth per month, 10 apps per subscription (Free), 250 MB per app | https://learn.microsoft.com/azure/static-web-apps/quotas |
| Static Web Apps | "Bring your own API" (linked Container Apps backend) needs the Standard plan; not supported on pull-request environments | https://learn.microsoft.com/azure/static-web-apps/apis-overview , https://learn.microsoft.com/azure/static-web-apps/apis-container-apps |
| Container Apps | Consumption: first 180,000 vCPU-s, 360,000 GiB-s and 2 million requests per subscription per month are free | https://learn.microsoft.com/azure/container-apps/billing |

### Risks and flags
- **R-32 Cross-origin in Phase 5.** SWA Free + ACA means CORS and two origins; Entra redirect URIs must list the SWA
  production URL (and each preview URL used for testing). Mitigation: one CORS origin from configuration, a CORS
  integration test, preview environments without real login unless registered.
- **R-33 Cold start.** The Api scales to zero; the first request after idle is slow. The SPA (static, always on) must
  show a "waking up" state instead of an error. Design in Phase 5.

### Open questions
1. Per-module local queues and event naming (`SubmissionAccepted` vs `PortfolioSubmitted`): Brief #12.
2. What Compliance receives (class totals in the event, or a Contracts query): Brief #12.

### Next step
Brief #12 (closes Phase 2): integration event from Portfolio to Compliance for the current accepted submission,
idempotent consumer stub in Compliance, local-queue and naming decisions.

## Delta 2026-11-19 (session 19: session hygiene)

Brief #10a, Part B. Branch `chore/agent-session-hygiene` from `origin/main`.

### Incident that motivated the rules
At the start of session 19 an `aspire run` was still running from the Brief #10 session (CLI PID 46896, AppHost
47896, DCP, Api, `ng serve`, container `postgres-dqcdbukq`). The agent had started it in session 18 and never stopped
it. The first baseline listed it, so it looked like the owner's; the owner corrected this. It was stopped with
`aspire stop --apphost src/backend/FinGuard.AppHost/FinGuard.AppHost.csproj` (graceful: DCP removed the container,
no manual removal needed; the data volume was kept and later reset with approval for the regenerated migration, D-130).

### Decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-132 | `scripts/dev-session.ps1` (PowerShell 7, no dependencies): `-Snapshot`, `-StartAspire`, `-Report` (default), `-Stop`, `-WhatIf`. Baseline in `.git/finguard-session-baseline.txt`, agent-started AppHosts in `.git/finguard-agent-processes.txt` (never committed) | Items keyed by PID + process start time (PID reuse) or container id |
| D-133 | Agents start Aspire only through `-StartAspire` (`aspire start` in the background). `-Stop` runs `aspire stop` for every recorded AppHost, including earlier sessions', waits up to 60 s, then stops the remaining new items: DCP (found by its `--monitor` chain from the AppHost), `dotnet`/`node` processes whose command line holds the repo path (editor helpers excluded), Playwright browsers, Testcontainers (`org.testcontainers=true`) and Aspire containers (`com.microsoft.developer.usvc-dev.*` labels); then `dotnet build-server shutdown`. Never volumes, never baseline items | In every proof run the Api `dotnet run` (and sometimes `ng serve`) outlived `aspire stop` by a few seconds; `-Stop` catches it |
| D-134 | An AppHost of this repo that is neither recorded nor in the previous baseline is "unknown owner": `-Snapshot` and `-Report` warn, the agent asks the owner once, and `-Stop` leaves it and (shortcut) every DCP/repo process/Aspire container alone while it runs | Children are not traced to their AppHost; trace parent PIDs if this blocks a real cleanup |
| D-135 | Aspire CLI: one install, the .NET global tool `aspire.cli` 13.6.5 (= `AspireVersion`). The session 6 note was right: `Aspire.Cli` is a RID-specific native tool (`Runner="executable"`, package `Aspire.Cli.win-x64`), so SDK 10.0.403 writes `%USERPROFILE%\.dotnet\tools\aspire.cmd`, not `aspire.exe`. Git Bash resolves `.exe` without the extension but not `.cmd`; `aspire.cmd --version` works there. Workaround: `aspire.cmd` or `alias aspire=aspire.cmd` | The brief assumed the tool installs `aspire.exe`; it does not. `docs/dev-environment.md` records it |
| D-136 | `AGENTS.md` gains "Closing a work session" (four rules) and a Commands link to `docs/dev-environment.md`; `CLAUDE.md` gains one line | Applies to Claude Code and Cursor |

### Aspire CLI diagnosis (report only; nothing changed outside the repo)
| Check | Result |
| --- | --- |
| PowerShell `Get-Command aspire -All` / `where.exe aspire` | `%USERPROFILE%\.dotnet\tools\aspire.cmd` only |
| `dotnet tool list -g` | `aspire.cli 13.6.5` |
| `aspire --version` (PowerShell) | `13.6.5+b33d222049f1ecc3bc7257ec07b622fe550b2580` |
| Git Bash `which -a aspire` / `type -a aspire` | not found; `aspire.cmd --version` → 13.6.5 |
| npm global shims (`%APPDATA%\npm\aspire*`) | none |
| Install script (`%USERPROFILE%\.aspire\bin`) | none |
| WinGet (`%LOCALAPPDATA%\Microsoft\WinGet\Links`, `Packages`) | none |
| Leftovers of the old npm install (13.5.3), not on PATH | `%USERPROFILE%\.aspire\npm\13.5.3` (144 MB, contains `aspire.exe`) and `%USERPROFILE%\.aspire\versions\13.5.3_b5f1…` (291 MB) |

Owner (optional, frees about 435 MB; keep the rest of `%USERPROFILE%\.aspire`, which the 13.6.5 CLI uses):
`Remove-Item -Recurse -Force "$env:USERPROFILE\.aspire\npm\13.5.3", "$env:USERPROFILE\.aspire\versions\13.5.3_*"`

### Proof (`-Snapshot`, `-StartAspire`, `-Report`, `-Stop`, `-Report`)
```text
PS> scripts/dev-session.ps1 -Snapshot
Nothing of this repo is running.
Baseline: 6 entries -> D:\dev\projects\finguard\.git\finguard-session-baseline.txt
PS> scripts/dev-session.ps1 -StartAspire
AppHost PID 680 recorded in D:\dev\projects\finguard\.git\finguard-agent-processes.txt
Dashboard: https://localhost:17182/login?t=<token>
PS> scripts/dev-session.ps1 -Report

Status Kind                       Id Started              Detail
------ ----                       -- -------              ------
agent  AppHost                   680 2026-11-19T21:26:00Z D:\dev\projects\finguard\src\backend\FinGuard.AppHost\FinGua
new    Aspire CLI              23760 2026-11-19T21:25:58Z "%USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspir
new    Aspire container c34a95b91845                      postgres-kxnxhyrc postgres:18.3
new    DCP                     45608 2026-11-19T21:26:01Z %USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspire
new    DCP                     45996 2026-11-19T21:26:01Z %USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspire
new    DCP                     44392 2026-11-19T21:26:02Z %USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspire
new    DCP                     46456 2026-11-19T21:26:02Z %USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspire
new    DCP                     44776 2026-11-19T21:26:02Z %USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspire
new    DCP                     40808 2026-11-19T21:26:03Z %USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspire
new    DCP                     44464 2026-11-19T21:26:04Z %USERPROFILE%\.dotnet\tools\.store\aspire.cli\13.6.5\aspire
new    Repo process            47776 2026-11-19T21:26:04Z dotnet run --project D:\dev\projects\finguard\src\backend\Fi

PS> scripts/dev-session.ps1 -Stop
Stopped AppHost D:\dev\projects\finguard\src\backend\FinGuard.AppHost\FinGuard.AppHost.csproj (PID 680)
Stopped Repo process (PID 47776)
Stopped Repo process (PID 13492)
dotnet build-server shutdown done
Nothing of this repo is running.
PS> scripts/dev-session.ps1 -Report
Nothing of this repo is running.
```
Unknown owner (AppHost started with plain `aspire start`): `-Report` listed it as `unknown owner` with the warning;
`-Stop` printed "Unknown-owner AppHost running: its processes and containers were not stopped." and left the AppHost,
Api, `ng serve` and `postgres` container running. `-WhatIf` listed every planned stop and changed nothing. Two script
bugs found in the first runs were fixed before commit (`ForEach-Object <member>` is a ShouldProcess operation under
`-WhatIf`; strict mode rejects `.Key` on an empty array).

## Delta 2026-11-19 (session 19: Brief #10 amendments)

The owner changed the CSV contract before Brief #10 was pushed (Brief #10a, Part A). Same branch and PR as Brief #10.

### Decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-126 | Limits are evaluated on book value (ADR-0010). CSV column `book_value` (C# `BookValue`, DB `book_value numeric(18,2)`, JSON `bookValue`); codes `Portfolio.Csv.BookValue{Required,Invalid,Negative,TooPrecise,OutOfRange}` | **Supersedes D-06's column name** (`market_value_usd`). Other D-06 limits unchanged |
| D-127 | `description` optional, ≤ 150 characters, still not stored | **Supersedes D-114's description limit** (500). D-114's "0 allowed per row" and "total > 0" still hold |
| D-128 | Currency required, case-insensitive, normalised to `USD`; anything else → `Portfolio.Csv.CurrencyNotSupported`, "Only USD positions are supported in this version." | Replaces `CurrencyNotUsd`. Column kept for future multi-currency |
| D-129 | `position_id` and `description` are rejected when the raw cell's leading whitespace contains tab, CR or LF, or its first character after leading whitespace (the same set `Trim()` removes) is `=`, `+`, `-`, `@` or a full-width form (`＝＋－＠`): `Portfolio.Csv.PositionIdUnsafe`, `Portfolio.Csv.DescriptionUnsafe`. One pure helper, `FormulaGuard.StartsWithFormulaTrigger` | OWASP CSV Injection. Defence in depth: any future export still escapes cells. One error per cell: `PositionIdRequired`/`TooLong` and `DescriptionTooLong` win over `Unsafe`. A security review caught a first version that skipped only spaces (`" \t=x"`, NBSP + `=` passed the guard but were stored as `=x`) |
| D-130 | `AddCsvValidation` regenerated (new id `20261119211247`) instead of a rename migration; its hand-written D-117 SQL (Phase 1 rows → `Rejected` with `NotValidated`) was carried over unchanged | Branch was unpushed. The local dev volume had the old id applied, so it was reset with the owner's approval |
| D-131 | Samples: `valid-balanced.csv` gains a written-down row with book value 0 (9 positions); `invalid-mixed.csv` gains a formula description on row 11 (9 errors). `invalid-mixed.csv` row 9 now reports `CurrencyNotSupported` | Updates the D-118 outcomes ("8 positions", "8 errors") |

### Results
- Unit tests: 192 passed. Integration tests: 86 passed. Architecture tests: 15 passed. Vitest: 64 passed.
  `dotnet format --verify-no-changes` clean; `has-pending-model-changes` clean.
- E2E (`pnpm e2e`, Chromium) against a fresh `aspire` instance: 2 passed, twice. The rejected flow asserts the
  `DescriptionUnsafe` line on the detail page. `pnpm lint`, `pnpm build` clean.

## Delta 2026-11-19 (session 18: Brief #10 executed)

### Resolved versions
| Package | Version | Notes |
| --- | --- | --- |
| CsvHelper | 34.1.0 | Latest stable 34.x, published 2026-11-19 02:24 UTC (34.0.0 was 2026-10-28). Same license (MS-PL OR Apache-2.0). No fallback to 33.1.0 needed. Referenced by `FinGuard.Portfolio` only |

### APIs checked against the installed XML docs (`CsvHelper.xml` 34.1.0)
| Concern | Finding |
| --- | --- |
| Reader | 34.x is a new span-based API: `CsvReader.Create(ReadOnlyMemory<byte>, CsvReaderOptions)`, `Headers`, `Read()`, `GetString(int)`, `RawRecord` (UTF-8 bytes). The 33.x `CsvConfiguration`/`GetField` samples do not apply |
| Options used | `Separator = ','` (otherwise it is detected), `HasHeader`, `Mode = Rfc4180`, `AllowLineBreaksInQuotes`, `SkipEmptyLines`, `DetectSeparatorHint = false`, `TrimWhitespace = false` (FinGuard trims), `TrackLineNumbers`, `MalformedData = Throw`, `FieldCountMismatch = Throw`, `IncludeValuesInExceptions = false` |
| Errors | `CsvFormatException.LineNumber` (with `TrackLineNumbers`) gives the input line where the bad record starts; `RecordNumber` skips empty lines, so it is not used |
| Row numbers | `CsvReader.LineNumber` counts every line, including lines inside quoted fields. Spreadsheet row = `LineNumber` minus the extra lines that earlier multi-line records took |
| Encoding | A UTF-16/32 BOM makes CsvHelper transcode; FinGuard checks strict UTF-8 first (`UTF8Encoding(false, true)`), so such files are `InvalidEncoding`. The UTF-8 BOM is stripped before reading |

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-119 | CsvHelper 34.1.0 (see above) | Brief named 34.0.0; 34.1.0 is the latest stable 34.x on the day |
| D-120 | `row` is the spreadsheet row: header = 1, blank lines keep their number but are skipped, a quoted multi-line field is one row | Approved in the plan |
| D-121 | A file error after some rows (`MalformedRow`, `TooManyRows`) keeps the row errors found so far and adds the file error. `RowCount` is the data rows read (10,001 for `TooManyRows`). Positions are stored only when there is no error at all | Approved in the plan |
| D-122 | Header errors are file errors (`row` null). `ColumnMissing` and `ColumnDuplicated` set `column` to the canonical name; `ColumnUnknown` leaves `column` null because the name comes from the file (D-115) | |
| D-123 | Only completely empty lines are blank. A whitespace-only line is a one-field record, so it is `MalformedRow`. Position ids are unique per file with ordinal (case-sensitive) comparison after trimming | |
| D-124 | Each slice owns its error DTO: `CsvError` (upload response), `SubmissionErrorItem` (details). Same JSON shape `{ row, column, code, message }` | |
| D-125 | `SamplePortfoliosTests` (unit) reads the three sample files and checks the outcomes promised in `samples/portfolios/README.md` | Keeps the README true |

### Bugs found and fixed
- **`position_id` came out nullable in the first migration draft.** EF's `<Type>Id` key convention picked up
  `Position.PositionId`. Fixed with `.IsRequired()` and the migration regenerated before any commit.

### Deviations
- `AddCsvValidation.Down` also maps `NotValidated` rows back to `Received` before dropping the tables.
- Extra integration test `Upload_AsBob_PositionsAndErrorsAreInvisibleToAlicesContext` (EF query filter on the new
  tables). Extra unit tests: `SamplePortfoliosTests` (D-125) and ordering tests.
- `Migration_PhaseOneReceivedRows_BecomeRejectedWithNotValidated` runs the real migrations on a fresh database in the
  shared container (`IMigrator` up to `AddSubmissions`, SQL seed, then up to the latest). No SQL-only fallback needed.
- `SubmissionReceivedResponse` overrides `ToString()` (ids and counts) although it is a reply, not a handled message (D-77).
- The brief file itself is committed with the docs commit.

### Findings
- `dotnet format --verify-no-changes` crashed once with `0xC0000005` (segfault) and passed on the next two runs. Tool
  flake, not code; rerun if CI shows it.
- An `aspire run` left by an earlier session kept `FinGuard.Api` alive and locked `bin/Debug`; builds and tests ran in
  Release until it was stopped (with the user's approval) and restarted on this branch.
- Running `pnpm --dir src/frontend/finguard-web ...` from the repo root fails with `ERR_PNPM_BAD_PM_VERSION` (global
  pnpm 12.18.0 via Corepack vs the pinned 11.28.9). From inside the app folder pnpm switches versions and works.
- The live dev database shows the Phase 1 rows as `Rejected` with 1 error after the migration (D-117 verified live).

### Majors review (R-13, report only; checked 2026-11-19)
Sources: NuGet registration API (`api.nuget.org/v3/registration5-gz-semver2/<id>/index.json`), .NET
`releases-index.json` (dotnetcli.blob.core.windows.net), `nodejs.org/dist/index.json`, `pnpm view <pkg>`, GitHub
releases API (`gh api repos/<action>/releases/latest`).

| Component | Current pinned | Latest stable major | Release date | Support | Blocker / dependency | Recommendation |
| --- | --- | --- | --- | --- | --- | --- |
| .NET SDK / runtime | SDK 10.0.403 (`global.json`, `latestFeature`), runtime 10.0.x | 11.0.0 (SDK 11.0.100); 10.0.15 / SDK 10.0.404 latest 10.x | 2026-11-10 | 10 LTS (EOL 2028-11-14); 11 STS | Every `Microsoft.*` 11.x package follows; ADR-level change | Stay on 10 LTS; take 10.0.404 patch in a chore |
| `Microsoft.*` 10.x (EF Core, JwtBearer, Mvc.Testing) | 10.0.15 | 11.0.0 | 2026-11-10 | follows .NET | Targets .NET 11 | Stay |
| `Microsoft.Extensions.*` (Resilience, ServiceDiscovery, AI, Testing) | 10.11.0 | 11.0.0 | 2026-11-10 | follows .NET | 11.x pulls .NET 11 abstractions into net10.0 (already noted in CPM) | Stay |
| Aspire | 13.6.5 | 17.0.0 (targets net10.0) | 2026-11-18 | — | One day old; check its AppHost SDK and CLI changes | Plan a brief (before Phase 5) |
| WolverineFx | 6.64.0 | 6.64.2 (6.x) | 2026-11-16 | — | Patch only | Stay (patch in a chore) |
| Npgsql EF provider | 10.0.3 | 10.0.3 | 2026-07-10 | follows EF | 11.x not out yet | Stay |
| EFCore.NamingConventions | 10.0.1 | 10.0.1 | 2026-01-22 | community (R-10) | Must follow EF major | Stay |
| Dapper | 2.1.89 | 2.1.89 | 2026-09-23 | — | — | Stay |
| xUnit v3 | 4.0.3 | 4.0.3 | 2026-11-09 | — | — | Stay |
| Testcontainers | 4.16.0 | 4.17.0 (4.x) | 2026-11-16 | — | Minor only | Stay (minor in a chore) |
| ArchUnitNET | 0.13.5 | 0.13.5 | 2026-10-26 | 0.x | — | Stay |
| Angular (+ CLI) | core 22.2.6, CLI 22.2.7 | 22.3.0 | 2026-11-18 / 2026-11-19 | 22 active | Minor; Material 22.3.0 already in | Stay (minor in a chore) |
| Angular Material / CDK | 22.3.0 | 22.3.0 | 2026-11-18 | follows Angular | Follows Angular major | Stay |
| TypeScript | 6.0.3 | 7.0.2 | 2026-07-08 | — | **Blocked:** `@angular/compiler-cli@22.3.0` peer `typescript >=6.0 <6.1` | Stay until Angular supports 7 |
| Vitest | 5.1.0 | 5.1.1 | 2026-11-18 | — | — | Stay |
| ESLint / angular-eslint / typescript-eslint | 10.14.0 / 22.6.0 / 8.71.0 | 10.15.0 / 22.8.0 / 8.73.0 | 2026-11-13 / 11-18 / 11-16 | — | angular-eslint follows Angular | Stay (minors in a chore) |
| Playwright | 1.65.1 | 1.65.1 | 2026-11-13 | — | Browser build tied to version | Stay |
| pnpm | 11.28.9 (`packageManager`) | 12.21.0 (`latest` tag) | 2026 | — | Global Corepack pnpm is already 12.18.0; lockfile format check | Plan a brief |
| Node | 24.16.0 local, `^24.15.0` | 26.13.1 (LTS "Lithium"); 24.22.3 latest 24.x | 2026-11-10 | 24 maintenance LTS; 26 active LTS | Angular 22.3 engines accept `>=26.0.0` | Upgrade before Phase 5 (with Container Apps image) |
| GitHub Actions | checkout@v7, setup-dotnet@v6, setup-node@v7, pnpm/action-setup@v6 | v7.0.1, v6.1.0, v7.1.0, v6.1.1 | 2026-07-20 / 11-03 / 10-08 / 11-03 | — | Major tags already current | Stay |

### Test counts
Backend: unit 137 (was 54; +83), integration 84 (was 71; +13), architecture 15. Frontend (Vitest): 64 in 13 files
(was 60). E2E (Playwright): 2, passed twice in a row against the same database (2.8 s cold, 1.3 s warm).
R-30: `Upload_TenThousandRows_IsAccepted` took 2,062 ms through HTTP on a cold host (run alone), well under "a few
seconds"; no batching needed now.

### Manual check (`aspire run`, 2026-11-19)
Playwright script against the `web` resource (`http://localhost:52295`), light and dark (`colorScheme` emulation):
- `valid-balanced.csv` → "Accepted valid-balanced.csv: 8 positions."; `valid-shares-heavy.csv` → "Accepted …: 5
  positions."; `invalid-mixed.csv` → "Rejected invalid-mixed.csv: 8 errors." with the 8 rows listed in the README.
- History shows the Errors column (empty for accepted); the detail page lists the errors.
- Request hosts: `localhost:52295` only.

### Risks and flags
- **R-28 (narrowed).** Rejected content now explains itself (201 + errors). Transport 400s (future date, wrong
  extension) still show only the API title; Brief #11 handles the rest.
- **R-31 Aspire 17 is one day old.** Re-check its release notes before planning the upgrade brief.

## Delta 2026-11-19 (session 18: Brief #10 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-110 | Every brief ends with a "Pull request" section (exact title + body skeleton); Claude Code writes the filled body to `.git/finguard-pr-body.md`; `.github/pull_request_template.md` gains Changes, How it was tested, Deviations and risks, Screenshots and a longer checklist | PR #16 squash title differed from the brief's title |
| D-111 | CSV validation runs synchronously in `ReceiveSubmission`; one transaction stores submission, bytes and positions or errors | |
| D-112 | Transport failures → 400/413, not stored; content failures → stored `Rejected` submission, 201 with `status` and errors; valid → 201 `Accepted` | Supersedes the 422/202 sketch in `overview.md` |
| D-113 | CSV reader: CsvHelper (latest stable 34.x, fallback 33.1.0) as a field reader only; strict UTF-8, BOM allowed; header of exactly the five columns, case-insensitive, any order | Rejected: Sep (0.x), hand-written parser |
| D-114 | Value rules: trimmed fields; asset class and currency case-insensitive; amounts invariant, ≤ 2 decimals, no sign/thousands/exponent, 0 allowed per row, max `numeric(18,2)`; position id required, ≤ 64, unique per file; description optional ≤ 500 and not stored; total > 0 | Assumptions on top of AGENTS.md; reopen if a real file format disagrees |
| D-115 | Errors carry row (header = 1), column, code `Portfolio.Csv.*`, fixed message without cell values; first 100 stored and returned, `error_count` holds the total | |
| D-116 | Accepted rows stored in `portfolio.positions` (PK submission + row); errors in `portfolio.submission_errors`; both tenant-owned | Class totals designed with the Compliance input in Brief #12 |
| D-117 | `Received` status removed; Phase 1 rows migrated to `Rejected` with `Portfolio.Csv.NotValidated` | Dev data only |
| D-118 | Sample portfolios in `samples/portfolios/` (fictional data) for manual checks, demos and Phase 3 | |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| CsvHelper | 34.0.0 latest stable (2026-10-28), new major with a reworked reader; 33.1.0 previous stable | https://www.nuget.org/packages/CsvHelper |
| Sep | Latest is 0.x | https://www.nuget.org/packages/Sep |

### Risks and flags
- **R-29 CsvHelper 34 is a fresh major.** Few samples match its API. Mitigation: read the package XML docs, keep the
  reader behind `PortfolioCsvReader`, fall back to 33.1.0 if needed.
- **R-30 Validation latency.** 10,000 rows plus 10,000 EF inserts run inside the request. Measure in
  `Upload_TenThousandRows_IsAccepted` (report the time); if it exceeds a few seconds, consider batching or `COPY` in
  Phase 5.

### Open questions
1. Per-module local queues: decide with the Compliance event (Brief #12).
2. Event naming: `overview.md` says `PortfolioSubmitted`/`PortfolioRejected`, D-93 says `SubmissionAccepted`; settle in
   Brief #12.

### Next step
Brief #11: SHA-256 idempotency (D-11), current version per cut-off date (latest accepted), errors UI and R-28.

## Delta 2026-11-19 (session 17: Brief #9 executed)

### Resolved versions
| Package | Version | Notes |
| --- | --- | --- |
| `@angular/material`, `@angular/cdk` | 22.3.0 | Latest; pnpm's `minimumReleaseAge` did not hold it back. Angular core stays 22.2.6 |
| `@playwright/test` | 1.65.1 (pinned exact) | Browser builds are tied to the Playwright version; Chromium build 1255 installed with `pnpm exec playwright install chromium` |
| Dapper | 2.1.89 (now also referenced by `FinGuard.Portfolio`) | `DateOnly` maps natively, as verified in planning |

No new `allowBuilds` approvals. The `pnpm peers check` warning (`@emnapi/*` under `@napi-rs/wasm-runtime`) was there
before this brief.

### APIs checked against installed `.d.ts` / sources
| Concern | Finding |
| --- | --- |
| `matButton` | Selector `matButton` with `MatButtonAppearance = 'text' \| 'filled' \| 'elevated' \| 'outlined' \| 'tonal'` (`@angular/material/types/button.d.ts`) |
| `httpResource` | `@publicApi 22.0`; uses `HttpClient`, so interceptors apply. Needs an injection context |
| Resource errors | `resource` keeps an error only if it is error-like (`instanceof Error` or `name` + `message`); anything else becomes `ResourceWrappedError` with `cause` (`@angular/core` `_resource-chunk.mjs`). `ApiError` was a plain class, so `ApiError` now extends `Error` (one place). Existing `toEqual(new ApiError(...))` tests stay green |
| Dapper + `timestamptz` | Npgsql gives Dapper a UTC `DateTime`; a positional record with a `DateTimeOffset` parameter fails with "A parameterless default constructor or one matching signature ... is required". Handlers read into a private `Row` record and map to the response |

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-107 | `PortfolioApi.submissions()` / `submission(id)` are factories called in the component's injection context, not resources held by the root service | A root-held resource would keep the previous user's list after sign-out |
| D-108 | Read-slice integration tests that need an exact list provision their own tenant + user (SQL into `tenancy.tenants` / `tenancy.users`, token from the dev issuer with a new `DevIdentity`) | The test database is shared per assembly, so Alice's list is never empty. Isolation tests still use Alice and Bob |
| D-109 | Router uses `withComponentInputBinding()`; `submission-detail` reads `:id` as `input.required<string>()` | |

### Bugs found and fixed
- **`ng add` ran the schematic twice** (Angular CLI 22.2.7 + Material 22.3.0): `material-theme.scss` and the Google
  Fonts `<link>` block were written twice. The theme file was rewritten and the font links removed (they had to go anyway).
- **Field errors after a successful upload:** `form.reset()` left `FormGroupDirective.submitted` true, so Material showed
  "Enter the cut-off date." on the fresh form. Found in the manual screenshot; fixed with `resetForm()`, regression
  assertion in `UploadForm` spec (fails without the fix). Separate commit `fix: clear field errors after a successful upload`.

### Deviations
- `DevelopmentSeedTests.SeedDevelopmentTenants_RunTwice_KeepsTwoTenants` now counts only the seeded identities' tenants
  and users; provisioned test tenants broke its global count (D-108).
- `GetSubmission_OwnSubmission_Returns200WithFields` compares `receivedAt` within 1 µs: the upload response carries .NET
  ticks (100 ns), PostgreSQL stores microseconds (see R-27).
- `ReadSlices_Always_DoNotLogFileNames` has no positive log assertion: Wolverine does not log inline `InvokeAsync` of
  queries, so there is no read-slice record to anchor on. It asserts the reads returned the marker and no log contains it.
- Commit plan: six commits instead of five (the fix above).
- `@playwright/test` pinned exact (`1.65.1`) instead of a caret range.
- Lint covers `e2e/**/*.ts` and `playwright.config.ts` through `lintFilePatterns`. No tsconfig for `e2e/`: Playwright
  transpiles without type-checking; editors show `process`/`Buffer` as unknown without `@types/node` (not added).
- Time zone: `TZ=America/Lima pnpm test --watch=false` passes (60/60) and Node honors `TZ` on Windows; the spec itself
  asserts the exact API string. Not wired into a script.

### Test counts
Backend: unit 54, architecture 15, integration 71 (was 59; +12). Frontend (Vitest): 60 in 13 files (was 36 in 9).
E2E (Playwright): 1, passed twice in a row against the same database (4.9 s cold, 0.7 s warm).

### Manual check (`aspire run`, 2026-11-19)
Playwright script against the `web` resource (`http://localhost:53495`), light and dark (`colorScheme` emulation):
- Alice uploads; the new file is the top history row; the detail page opens from the row and from the `Location` id.
- Future cut-off date: alert "Validation failed" (API title). Unknown id: "Resource not found".
- Request hosts: `localhost:53495` only (no third-party hosts).
- Dark: body `rgb(18, 19, 22)` on `rgb(227, 226, 230)`; light: `rgb(250, 249, 253)` on `rgb(26, 27, 31)`.

### Risks and flags
- **R-27 `receivedAt` precision.** The upload response returns the in-memory `DateTimeOffset` (100 ns); reads return
  PostgreSQL microseconds. Harmless for display; truncate to microseconds in the handler if a client ever compares them.
- **R-28 Only the API title is shown.** An invalid upload shows "Validation failed" without the reason (D-80 shows only
  `title`). Revisit with rejected submissions (Phase 2), e.g. show `detail` for 400s.

## Delta 2026-11-19 (session 17: Brief #9 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-98 | UI library: Angular Material 22 + CDK, theme `azure-blue` via the `ng add` Sass theme file; system font stack; no third-party font or icon CDNs | Closes the open question of sessions 7 and 15. Rejected: PrimeNG, CDK only, Tailwind (accessible table, form fields and errors would be hand-built; the project's showcase is architecture and domain, not visual design); Material + Tailwind (two styling systems) |
| D-99 | E2E: Playwright Test (TypeScript) in `src/frontend/finguard-web/e2e`, Chromium, run locally against `aspire run` via `E2E_BASE_URL`; CI E2E decided in Phase 5 | Closes the E2E tool question. Rejected: Cypress, Playwright .NET + `Aspire.Hosting.Testing` |
| D-100 | `GET /api/portfolio/submissions`: newest 100 for the tenant, `received_at DESC, id DESC`, envelope `{ items }`, no paging yet | Read queries never select `submission_contents` |
| D-101 | `GET /api/portfolio/submissions/{id}`: unknown or other tenant's id → 404 `Portfolio.Submission.NotFound` | Existence is not revealed |
| D-102 | Angular reads use `httpResource` (stable since 22.0); writes use `HttpClient` | |
| D-103 | Calendar dates (`cutOffDate`) travel and render as `yyyy-MM-dd` strings, never through `Date`/`DatePipe`; timestamps (`receivedAt`) render in local time | Prevents the one-day shift west of UTC |
| D-104 | Client-side file checks (`.csv`, > 0, ≤ 5 MB) are UX only; the API is authoritative | |
| D-106 | Dark mode follows the operating system (`color-scheme: light dark`); component CSS uses `--mat-sys-*` tokens only; a three-state toggle (system/light/dark) arrives in Phase 3 with the status colors | Tailwind evaluated and rejected for this project (see D-98) |
| D-105 | Phase 1 ends with Brief #9; Brief #10 opens Phase 2 with the dependency-majors review (R-13) in its preflight | |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| Angular Material | 22.3.0 latest; `ng add` option `theme`; CSS projects get a generated `material-theme.scss` and Google Fonts links | npm registry; https://material.angular.dev/guide/getting-started |
| Angular | `httpResource` is `@publicApi 22.0` | `@angular/common` 22.2.6 `types/http.d.ts` |
| Playwright | `@playwright/test` 1.65.1, Node ≥ 20, browsers installed explicitly | npm registry; https://playwright.dev/docs/intro |
| Dapper / Npgsql | Dapper 2.1.86 re-enabled `DateOnly`; Npgsql 10 reads `date` as `DateOnly` | https://www.npgsql.org/doc/types/datetime.html |

### Risks and flags
- **R-25 E2E only local.** The browser flow is not in CI until Phase 5; regressions show up only when someone runs
  `pnpm e2e`. Mitigation: run it before every PR that touches the frontend or the Portfolio slices; Phase 5 adds a CI job.
- **R-26 Material ties to Angular majors.** Angular 23 must wait for Material 23 (Material already accepts core ^23 in
  peers). Covered by the R-13 majors review.

### Open questions
1. Per-module local queues: decide with `SubmissionAccepted` (Phase 2).
2. E2E in CI (Aspire in the runner, or against the deployed environment) and Entra test users for E2E: Phase 5.

### Next step
Brief #10 (Phase 2 start): dependency-majors review, CSV parsing and validation (D-06 columns, rows, encoding,
USD only, no negatives, total > 0), rejected submissions stored with their errors (D-10), and SHA-256 idempotency (D-11).

## Delta 2026-11-19 (session 16: Brief #8 executed)

### Resolved versions
`Microsoft.Extensions.TimeProvider.Testing` 10.11.0 added to CPM (unit tests, `FakeTimeProvider`); same release train
as `Microsoft.Extensions.Diagnostics.Testing` 10.11.0. No other package changes.

### APIs used (checked against the ASP.NET Core 10.0.14 shared framework and Mvc.Testing 10.0 XML docs)
| Concern | API | Notes |
| --- | --- | --- |
| Antiforgery opt-out | `RouteHandlerBuilder.DisableAntiforgery()` | JWT bearer only (D-92) |
| Request body limit | `.WithMetadata(new RequestSizeLimitAttribute(6 MB))` | `RequestSizeLimitAttribute` implements `IRequestSizeLimitMetadata`; `EndpointRoutingMiddleware` applies it. There is no `WithRequestSizeLimit` extension in .NET 10 |
| Form limit | `.WithFormOptions(multipartBodyLengthLimit: 6 MB)` | `IFormOptionsMetadata` |
| No `SaveChanges` in the handler | Wolverine's generated code for `ReceiveSubmission` calls `portfolioDbContext.SaveChangesAsync` ("Added by EF Core Transaction Middleware"), then commits | Read from the generated source in a temporary test |
| 413 test on Kestrel | `WebApplicationFactory<T>.UseKestrel(int port)` + `StartServer()` (new in .NET 10) | TestServer has no `IHttpMaxRequestBodySizeFeature` |

### Migration `AddSubmissions`
`portfolio.submissions` (`id` uuid PK, `tenant_id`, `cut_off_date` date, `file_name` varchar(255), `size_bytes` int,
`sha256` char(64), `status` varchar(20), `received_at` timestamptz; index
`ix_submissions_tenant_id_cut_off_date_received_at`) and `portfolio.submission_contents` (`submission_id` uuid PK and FK
to `portfolio.submissions(id)` ON DELETE CASCADE, `tenant_id`, `content` bytea). No cross-schema FK.

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-94 | A module DbContext may be `public` (must be `sealed`); its entities stay `internal` and are used through `Set<T>()` | Wolverine refused an internal constructor: "does not have a suitable, public constructor for Wolverine". Other modules still cannot reach it: modules reference only BuildingBlocks and Contracts (architecture test). `Modules_AnyAssembly_HaveOneInternalDbContextWithModuleSchema` became `..._HaveOneSealedDbContextWithModuleSchema` |
| D-95 | Wolverine resolves `ITenantContext` from the message scope: `opts.CodeGeneration.AlwaysUseServiceLocationFor<ITenantContext>()` | Bug found by this brief (see below) |
| D-96 | `GlobalExceptionHandler` keeps the status of `BadHttpRequestException` (e.g. 413) with the generic reason phrase as title, logged at Warning with status and trace id; everything else stays a generic 500 | In Development, Minimal APIs throw on bad requests (`ThrowOnBadRequest`); the handler turned the 413 into a 500 |
| D-97 | An unparsable or missing cut-off date and a missing file part are rejected by the endpoint (`CutOffDateInvalid`, `FileMissing`); the handler checks empty, size, extension and future date | The command carries a `DateOnly`, so an invalid date cannot reach the handler; parsing is binding |

### Bug found and fixed (D-95)
In the real Api, module DbContexts are registered through Aspire's `EnrichNpgsqlDbContext`, and Wolverine resolves them
from the message scope (generated code: "marked as requiring service location"). `TenantPropagationMiddleware` got an
inlined `new TenantContext()` instead, so it set the tenant on an object the DbContext never saw. The first save failed
with `No tenant is set for the current scope.` It failed closed (no leak). The messaging probes never showed it because
`MessagingHost` registers its DbContexts without `EnrichNpgsqlDbContext`, so their code is fully inlined. The upload
integration tests are the regression tests; after the fix the generated code reads
`var tenantContext = GetRequiredService<ITenantContext>(serviceScope.ServiceProvider)`.

### Deviations
- `PortfolioDbContext` is public (D-94); the slice template's "Wolverine discovers only public types" now also covers
  handler dependencies.
- `CutOffDateInvalid` comes from the endpoint, not the handler (D-97); the handler unit-test theory covers the other
  four codes plus accepted cases, and the integration tests cover all six codes through HTTP.
- `InternalsVisibleTo` from `FinGuard.Portfolio` to `FinGuard.UnitTests` and `FinGuard.Api.IntegrationTests` (entities,
  helpers and the isolation test's DbContext reads).
- 413 test: TestServer ignores `IRequestSizeLimitMetadata` (log: "This server does not support the
  IHttpMaxRequestBodySizeFeature") and the form limit then gives 400. The test boots a second Api on Kestrel
  (`ApiFixture.ApiFactory` is now internal) and sends `Expect: 100-continue`, as curl does for large bodies: Kestrel
  answers 413 from `Content-Length` without reading the body. Without it Kestrel closes the connection while the
  client is still writing.
- `Upload_HandlerFailsAfterChanges_WritesNoRows` skipped: no seam without a production hook. The generated code wraps
  `Handle` + `SaveChangesAsync` + commit in one transaction, and `OutboxAndTenantPropagationTests` already proves the
  rollback with `FailAfterSave`.
- Extra tests: `Upload_FileOverFiveMegabytes_Returns400WithCode` (between the 5 MB file limit and the 6 MB request limit).
- `FileNameSanitizer` splits on both `/` and `\` instead of `Path.GetFileName`, which only splits on `\` on Windows.
- Submission ids are `Guid.CreateVersion7(receivedAt)` (time-ordered, good for the index).
- Form files above 64 KB are buffered by ASP.NET Core to a temporary file (default `MemoryBufferThreshold`); allowed by
  the brief ("outside what ASP.NET Core buffers internally"). The slice itself never writes to disk.

### Manual check (`aspire run`, 2026-11-19)
- `curl.exe -F "file=@sample.csv;type=text/csv" -F "cutOffDate=2026-11-19"` with Alice's token: `201 Created`,
  `Location: /api/portfolio/submissions/01a1f4b3-...`, body `{"status":"Received",...}`.
- `portfolio.submissions` and `portfolio.submission_contents`: one row each, `tenant_id` = Acme
  (`6f1d2c3b-...-5e01`), 98 bytes.
- A 6 MB file: `413 Payload Too Large`, `application/problem+json`, title "Content Too Large", with `traceId`.
- Api log for the upload: `Received submission 01a1f4b3-... (98 bytes)`. EF command logs show parameters as `'?'`.

### Risks and flags
- **R-24 Probe hosts differ from module registration.** `MessagingHost` does not use `EnrichNpgsqlDbContext`, which hid
  the D-95 bug. Real module slices with integration tests now cover the real registration; consider aligning the probe
  registration when the first integration event lands.

## Delta 2026-11-19 (session 16: Brief #8 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-87 | Raw CSV stored as `bytea` in `portfolio.submission_contents` (1:1 with `portfolio.submissions`) | No blob storage; metadata queries never touch the bytes |
| D-88 | Upload limits: endpoint request limit 6 MB, file ≤ 5 MB and > 0 checked in the handler; over-limit requests answer 413 ProblemDetails | Row and encoding limits are Phase 2 |
| D-89 | Only the `.csv` extension is checked at upload; content type is not trusted; content checks in Phase 2 | |
| D-90 | Cut-off date: required `yyyy-MM-dd`, not later than today in UTC | Assumption: no business case for future cut-off dates; reopen if one appears |
| D-91 | Original file name stored sanitized for display only, never used as a path, never logged; SHA-256 computed and stored at upload, uniqueness deferred to Phase 2 (D-11) | |
| D-92 | Upload endpoint opts out of antiforgery: authentication is JWT bearer, no cookies | Revisit if cookie auth ever appears |
| D-93 | No `SubmissionReceived` integration event; the first integration event will be `SubmissionAccepted` (Portfolio → Compliance) | Phase 2 validation stays inside Portfolio |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| ASP.NET Core | `IFormFile` Minimal API endpoints require antiforgery since .NET 8; `.DisableAntiforgery()` opts out | https://learn.microsoft.com/dotnet/core/compatibility/aspnet-core/8.0/antiforgery-checks |
| ASP.NET Core | Request body limit (`IRequestSizeLimitMetadata`) and `FormOptions.MultipartBodyLengthLimit` are separate limits | https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.metadata.irequestsizelimitmetadata |

### Risks and flags
- **R-23 Database growth.** Raw files live in PostgreSQL; with 5 MB per file the demo database grows quickly. Phase 5
  sizes PostgreSQL Flexible Server storage and may add a retention rule.

### Open questions
1. Per-module local queues (session 13): still open, decide with `SubmissionAccepted`.

### Next step
Brief #9 (closes Phase 1): `GET /api/portfolio/submissions` and `GET /api/portfolio/submissions/{id}` with Dapper,
Angular upload form and submission history, UI library decision, browser E2E tool decision and the first E2E flow.

## Delta 2026-11-19 (session 15: Brief #7 executed)

### Resolved versions
No upgrades and no new frontend dependency. WolverineFx stays 6.64.0, Angular 22.2.x.

### Wolverine 6.64 API names checked (XML docs in the NuGet package)
`IPolicies.MessageSuccessLogLevel(LogLevel)` (default Information) and `IPolicies.MessageExecutionLogLevel(LogLevel)`
(default Debug) match the brief. The package also has `IPolicies.MessageFailureLogLevel(LogLevel)` (GH-5043, default
Error), so failure logging is configurable, not fixed. It stays at Error: hiding failures is not the fix (D-77).

### R-21 evidence
Before the overrides, `FailedMessage_WithFreeText_DoesNotLogPayload` failed with this Error record from Wolverine:
`Invocation of PlaceProbeOrder { OrderId = ..., Reference = secret-reference-..., FailAfterSave = True } failed!`.
After the overrides it passes, and it also asserts that a record names `PlaceProbeOrder`, so it cannot pass by logging
nothing. `MessageTypes_Always_OverrideToString` failed with a temporary `TempProbeMessage` record in Tenancy, passed
once that record had an override, and the record was then removed. **R-21 is closed.**

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-83 | The architecture test treats a record's synthesized `ToString()` as missing: it requires `DeclaringType == type` and no `[CompilerGenerated]` | The compiler declares a synthesized `ToString()` on every record, so `DeclaringType == type` alone passes for all records |
| D-84 | The dev proxy (`proxy.conf.js`) forwards `/dev` as well as `/api` | `POST /dev/token` stays same-origin; no CORS |
| D-85 | `AuthProvider` exposes `users` (the sign-in choices) and `AuthService.users` forwards it | The login page must not import a provider (D-78) |
| D-86 | Test classes that use `MessagingHost` share the xUnit collection `MessagingHost.Collection` | `TrackActivity` sees all host activity; parallel classes saw each other's messages and timed out |

### Deviations
- The architecture test only checks message types declared in FinGuard assemblies. Without that filter it flagged
  `RequestDelegate`, the first parameter of ASP.NET's `IAuthorizationMiddlewareResultHandler.HandleAsync` on
  `TenantAuthorizationResultHandler`.
- Probe messages live in the test assembly, which the architecture test does not scan. Their overrides were added by
  hand, as the brief asked.
- The `/api/tenancy` group had no `RequireAuthorization(TenantMember)` call (the other modules had one); it has now.
- The slice's query, response and handler are `public`, because Wolverine discovers only public handler and message
  types (same rule as the probe modules). The endpoint class is `internal`.
- `MessagingHost` now registers fake logging and exposes `Logs`, so the R-21 test can read the probe host's logs.
- `ApiError` is a class, so `authInterceptor` can match it with `instanceof`. Interceptor order: `authInterceptor` runs
  outermost, so on the way back it sees the `ApiError` mapped by `problemDetailsInterceptor` and signs out on status
  401.
- Non-ProblemDetails errors get the title `Something went wrong. Please try again.`; the brief asked for "status only",
  but the login page needs some title to show.
- `frontend.md` / `.mdc` said "One interceptor handles auth headers and maps ProblemDetails". They now describe the two
  interceptors of D-80.
- Specs use a shared test double, `core/auth/fake-auth-provider.ts`. The app does not register it, so the build tree-shakes it
  out of the bundle.
- `withFetch()` is removed from `provideHttpClient`: Angular 22 deprecates it ("`FetchBackend` is the default
  `HttpBackend`", `@angular/common/types/http.d.ts`), so requests still use fetch.
- Prettier: `index.html` and `main.ts`, unformatted on `main`, are now formatted; `prettier --check src` is clean.

### Manual check (`aspire run`, 2026-11-19)
- Opening `/` without a session redirected to `/login` ("Sign in (development)", buttons for Alice and Bob).
- Alice: header shows "Acme Pension Fund"; a reload keeps the session (sessionStorage).
- Sign out: back on `/login`. Bob: header shows "Globex Insurance".
- `aspire resource api restart`, then reload: `GET /api/tenancy/me` returned 401 and the app went to `/login` (R-18).
- Network list: `POST /dev/token` and `GET /api/tenancy/me` through the web origin; no URL contains a token. The
  browser tool redacts headers, so the bearer header was proven by behaviour: through the proxy, the same call returns
  200 with the token and 401 without it.

### Risks and flags
- **R-21** closed (see above).
- **R-22** unchanged (token in `sessionStorage`; Phase 5 review).

## Delta 2026-11-19 (session 15: Brief #7 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-77 | R-21 mitigation: every Wolverine message type in `src/backend` overrides `ToString()` with type name and identifiers only; enforced by an architecture test; success logging at Debug | Wolverine always logs failures at Error with the message; a category filter would hide real failures |
| D-78 | SPA authentication: `AuthService` over an abstract `AuthProvider`; `DevAuthProvider` now, MSAL provider for Entra External ID in Phase 5; components never import a provider | Closes open question 1 of session 11 |
| D-79 | Development session stored in `sessionStorage`, cleared on sign-out and on any 401 from `/api/` | R-18: dev tokens die on Api restart |
| D-80 | Interceptors: `authInterceptor` (bearer on same-origin `/api/` only, 401 → sign out) then `problemDetailsInterceptor` (typed `ApiError` from RFC 9457) | Only `title` reaches the user |
| D-81 | `GET /api/tenancy/me` is the reference slice layout: `Features/<UseCase>/` with query record, response record, Wolverine handler (Dapper, tenant from `ITenantContext`) and Minimal API endpoint using `InvokeForCurrentTenantAsync<Result<T>>` | Documented in `backend-vsa` rules |
| D-82 | UI library decision moves to Brief #9 (first table and form) | Login and header use plain semantic HTML |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| Wolverine logging | Execution/success log levels configurable; failures logged at Error with the exception | https://wolverine.netlify.app/guide/logging |
| Message logging pattern | Message loggers write `ToString()`; override it to control content | https://docs.particular.net/nservicebus/logging/message-contents |
| Angular HTTP | Functional interceptors via `provideHttpClient(withInterceptors([...]))` | https://angular.dev/api/common/http/withInterceptors |

### Risks and flags
- **R-21** expected to close with this brief.
- **R-22 Token in `sessionStorage`.** Readable by any script on the page (XSS). Acceptable for the development provider;
  Phase 5 reviews MSAL's cache location and the Content Security Policy.

### Open questions
1. Browser end-to-end test tool (Playwright or other): Brief #9.
2. UI library: Brief #9.

### Next step
Brief #8: minimal upload slice (`POST /api/portfolio/submissions` storing the raw CSV and cut-off date, size limit,
`SubmissionReceived` event), without validation rules yet.

## Delta 2026-11-15 (session 14: Brief #6 executed)

### Resolved versions
No upgrades. WolverineFx 6.64.0 is still the newest stable 6.x on NuGet (checked 2026-11-15). New references to pinned
packages: `WolverineFx` (BuildingBlocks, D-72) and `WolverineFx.EntityFrameworkCore` (Api).

### Spike verdict
| Row | Result | Evidence |
| --- | --- | --- |
| Envelope storage | Baseline kept: one main store in `wolverine`, shared through the DbContext connection and transaction | `PlaceOrder_ForTenant_WritesOrderAndLedgerEntry`, `PlaceOrder_HandlerThrowsAfterChanges_WritesNothingAndPublishesNothing`; `aspire run`: `wolverine` holds 9 tables, no module schema has a `wolverine*` table |
| Transaction mode | Baseline kept: Eager, Aspire EF retries off, Wolverine `RetryWithCooldown(50ms, 250ms, 1s).Then.MoveToErrorQueue()` on transient `NpgsqlException` | `OrderPlaced_ConsumerFailsTransientlyTwice_IsRetriedAndWritesOnce` (3 attempts, all after the command finished) |
| Tenant propagation | Baseline kept: envelope tenant id, `InvokeForCurrentTenantAsync`, `TenantPropagationMiddleware` | `PlaceOrder_ViaHttpWithAliceToken_StampsAcmeOnBothRows`, `PlaceOrder_AliceThenBob_EachLedgerSeesOnlyOwnEntries`, `OrderPlaced_WithoutTenant_FailsClosedAndWritesNothing`, `OrderPlaced_PayloadTenantDiffersFromEnvelope_IsRejected` |
| Idempotent consumers | Baseline kept: unique index on `SourceEventId`, duplicate ignored | `OrderPlaced_DeliveredTwiceWithSameEventId_WritesOneLedgerEntry` |
| Local delivery | Baseline kept: `UseDurableLocalQueues()` | Handled `ProbeOrderPlaced` rows in `wolverine_incoming_envelopes` (status `Handled`) |

R-02 and R-16 are closed.

### Decisions taken during execution
| ID | Decision | Notes |
| --- | --- | --- |
| D-73 | Test-only: the messaging probe host runs on its own database `finguard_messaging` in the shared Testcontainers PostgreSQL container | Two Wolverine hosts in Solo mode on one envelope store would both claim the durability agents and recover each other's envelopes. Approved by the owner before execution |
| D-74 | No service location (R-19): `ITenantContext` is the type-based registration (`AddScoped<ITenantContext, TenantContext>()`, `TenantContext` forwards to it); module `DbContextOptions` are singletons; `TenantStampingInterceptor` is a stateless singleton (`Instance`) that reads the tenant from the intercepted `TenantAwareDbContext` | Generated handler code creates one `TenantContext` per message and shares it with the middleware, the handler and every DbContext. `AlwaysUseServiceLocationFor` is not used |
| D-75 | `TenantPropagationMiddleware.Before(Envelope, ITenantContext)` takes the interface and requires the instance to be a `TenantContext` | Taking `TenantContext` (a forwarding factory) would have needed service location |
| D-76 | `DurabilityMode.Solo` is Development-only; multi-replica environments (Phase 5) need `Balanced` | Comment in `WolverineSetup.cs` and ADR-0009 |

### Deviations
- **R-19 restructure instead of an escape hatch.** At the first handler, code generation refused two opaque scoped
  factories: `ITenantContext` (Tenancy's lambda) and `DbContextOptions<T>` (EF's `CreateDbContextOptions`, whose lambda
  resolved the scoped interceptor). Error: `InvalidServiceLocationException : Found service locations while generating
  code for Message Handler for ...PlaceProbeOrder, but ServiceLocationPolicy.NotAllowed is in effect`. As the brief
  asked, I restructured first (D-74, D-75). Owner condition 2 is met without the hatch: the HTTP test proves the same
  instance end to end. With `SetTenant` turned into a no-op, 6 of 8 messaging tests failed, including
  `PlaceOrder_ViaHttpWithAliceToken_StampsAcmeOnBothRows`.
- The Brief #5 interceptor changed shape (no constructor, `Instance`). A tenant-owned entity saved through a plain
  `DbContext` now throws `Tenant-owned entities must be saved through a TenantAwareDbContext.` New unit test:
  `TenantStampingInterceptorTests`.
- Wolverine configuration moved to `src/backend/FinGuard.Api/Messaging/WolverineSetup.cs` (`AddFinGuardMessaging`).
- Test extension point: no production code. Wolverine's `services.ConfigureWolverine(...)` (6.64 XML docs:
  `HostBuilderExtensions.ConfigureWolverine(IServiceCollection, Action<WolverineOptions>)`) adds the test assembly to
  discovery from the probe `WebApplicationFactory`.
- `ApiFixture` starts the `MessagingHost` instead of a separate xUnit fixture; the brief's "second
  `WebApplicationFactory` subclass" is `MessagingHost.MessagingFactory`.
- `PlaceProbeOrderHandler` calls `SaveChangesAsync` before throwing, so the atomicity test proves a rollback of rows
  already sent to the database, not just a skipped save.
- The optional `Handler_WithTwoDbContexts_FailsAtStartupUnlessDesignated` was not written (a third host boot); the rule
  is documented in ADR-0009 and enforced by `ModuleHandlers_Always_DependOnAtMostOneDbContext`.
- Extra unit tests: `Before_PayloadTenantMatchesEnvelope_SetsTenant`, `Before_ContextIsNotTenantContext_Throws`.
- Rules: `backend-vsa` also documents the module DbContext registration pattern, and that `SetTenant` is allowed in the
  BuildingBlocks middleware.

### Wolverine 6.64 API names checked (XML docs in the NuGet package)
All names used match the brief: `UseEntityFrameworkCoreTransactions()`, `Policies.AutoApplyTransactions()`,
`Policies.UseDurableLocalQueues()`, `Policies.AddMiddleware(Type, ...)`, `OnException<T>(Func<T,bool>)`,
`RetryWithCooldown(params TimeSpan[])`, `IAdditionalActions.Then`, `MoveToErrorQueue()`,
`IMessageBus.InvokeForTenantAsync`, `Envelope.TenantId`, `IServiceProvider.TrackActivity()`,
`DoNotAssertOnExceptionsDetected()`, `ExecuteAndWaitAsync(Func<IMessageContext, Task>)`. Differences:
- `AlwaysUseServiceLocationFor<T>()` lives on JasperFx `GenerationRules` (`opts.CodeGeneration`); not used.
- `InvokeAsync` is declared on `ICommandBus`, not `IMessageBus`. The architecture rule bans any `Wolverine.*`
  `InvokeAsync` / `InvokeForTenantAsync` call.
- `ExecuteAndWaitAsync` has `Task` and `ValueTask` overloads; async lambdas need an explicit `Func<IMessageContext, Task>`.
- Inline retries show in tracked sessions as repeated `ExecutionStarted` records, not as `MessageFailed`.

### Findings
- Architecture probes (temporary, reverted): `TempBadEvent` record → `Integration events must follow ADR-0009:
  FinGuard.Portfolio.Contracts.Events.TempBadEvent is not a sealed record; ... lacks Guid TenantId; ... lacks
  DateTimeOffset OccurredAt`; a handler with two contexts → `A handler owns one DbContext; cross-module effects go
  through events: FinGuard.Portfolio.TempTwoContextsHandler -> PortfolioDbContext, TempOtherDbContext`; a direct call →
  `Send commands with InvokeForCurrentTenantAsync: FinGuard.Portfolio.TempDirectInvokeEndpoint calls
  Wolverine.ICommandBus.InvokeAsync(System.Object,System.Threading.CancellationToken,System.Nullable`1<System.TimeSpan>)`.
  Rule 3 uses ArchUnitNET's `GetCalledMethods()`; rules 1 and 2 use reflection (D-51 style).
- `aspire run`: `postgres`, `finguard`, `api`, `web` healthy; the api log has no `fail`/`crit` lines and no
  code-generation or service-location errors; module schemas hold only their own tables.
- Tests: 90 (unit 37, integration 39, architecture 14), all green; the 8 messaging tests take about 10 s including
  container and host boot.

### Risks and flags
- **R-02, R-16** closed (see Spike verdict).
- **R-19** closed without `AlwaysUseServiceLocationFor`. New modules must copy the registration pattern, or codegen fails
  at the first handler.
- **R-21 Wolverine logs message payloads.** On a failed invocation, Wolverine logs `Invocation of {message} failed!` at
  Error under the message-type category, and records print every property. This conflicts with "never log payloads".
  Until it is fixed, messages carry no amounts or free text. Options for Brief #7/#8: override `ToString` by
  convention (architecture test), a logging filter for message categories, or a Wolverine log-level setting.

## Delta 2026-11-15 (session 13: Brief #6 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-65 | Brief #6 is a spike with production output: Wolverine config, tenant middleware and conventions are real; the two DbContexts, the command and the event are test-only probes that stay as regression tests | No business tables before Brief #8 |
| D-66 | Baseline envelope storage: one main store in schema `wolverine`, shared by all module DbContexts through their connection and transaction; no `MapWolverineEnvelopeStorage` in module models | Fallback: ancillary store per DbContext |
| D-67 | Eager transactional middleware with `AutoApplyTransactions`; EF/Aspire retries disabled for module DbContexts; transient database faults retried by a Wolverine error policy around the whole handler | Eager + `EnableRetryOnFailure` fails at startup |
| D-68 | Tenant propagation through Wolverine's envelope tenant id: endpoints use `InvokeForCurrentTenantAsync`; cascaded messages inherit the tenant; a BuildingBlocks middleware sets `TenantContext` before every handler; no tenant means fail closed | Fallback: explicit `TenantId` on every message |
| D-69 | Integration events: sealed records in `FinGuard.<Module>.Contracts.Events` with `EventId`, `TenantId`, `OccurredAt`; payload tenant must match the envelope; consumers are idempotent by `EventId` (unique index) | `CorrelationId` leaves the payload; Wolverine and OpenTelemetry carry correlation |
| D-70 | One DbContext per handler; cross-module effects only through events | Wolverine refuses to guess between two DbContexts |
| D-71 | Durable local queues for all local messages | Events survive a crash between commit and handling |
| D-72 | BuildingBlocks may reference `WolverineFx` for messaging primitives | Compliance `Domain/` still cannot |

### Verified on 2026-11-15
| Area | Finding | Source |
| --- | --- | --- |
| Wolverine EF Core | `UseEntityFrameworkCoreTransactions()` for separately registered DbContexts; multiple DbContexts supported; ancillary store per DbContext available | https://wolverine.netlify.app/guide/durability/efcore/ |
| Wolverine EF Core | Several DbContexts in one handler fail at startup unless designated; `AutoApplyTransactions`; Eager vs Lightweight; Eager + `EnableRetryOnFailure` fails at startup | https://wolverine.netlify.app/guide/durability/efcore/transactional-middleware |
| Wolverine tenancy | `InvokeForTenantAsync`; cascaded messages keep the tenant id; `TenantId` injectable in handlers | https://wolverine.netlify.app/guide/handlers/multi-tenancy |

### Risks and flags
- **R-02 and R-16** are expected to close with this brief; the execution delta confirms or reopens them.
- **R-19 Wolverine service location.** Factory-lambda registrations (`ITenantContext`, DbContext options) may require
  `AlwaysUseServiceLocationFor`; keep that list short and documented.
- **R-20 Retry ownership.** With EF retries off, only Wolverine retries transient faults; Dapper queries (outside
  Wolverine transactions) get no automatic retry. Revisit if queries show transient failures.

### Open questions
1. Per-module local queues and parallelism: decide when the first real event exists (Brief #8 or #10).

### Next step
Brief #7: Angular login against the development issuer (`AuthService`, interceptor, ProblemDetails mapping) and the
first real slice `GET /api/tenancy/me` through Wolverine.

## Delta 2026-11-15 (session 12: Brief #5 executed)

### Resolved versions
No new package versions. New references to packages already pinned in `Directory.Packages.props`:
`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15 (Api) and `Dapper` 2.1.89 (Tenancy).

### Deviations
- Design-time factories pass `NoTenantContext.Instance` (new, BuildingBlocks) instead of `new TenantContext()`: the new
  architecture rule forbids depending on the concrete `TenantContext` outside BuildingBlocks and Tenancy. Approved by
  the owner before execution.
- `TenantContext`, `ITenantContext` and `TenantStampingInterceptor` are registered by `AddTenancyModule` from commit 1,
  so every commit boots. The `TenantMember` policy, its handler and the 403 result handler are registered by Tenancy;
  the Api registers authentication and the fallback policy (`DevAuth/AuthenticationSetup.cs`).
- Existing tests touched outside commit 4, so every commit stays green: `StartupTests` now checks that the first
  migration per schema is `InitialSchema` (Tenancy has two); `ErrorHandlingTests` send Alice's token (commit 3).
- Extra tests beyond the brief: `UnknownRoute_NoToken_Returns401` (confirms D-61) and
  `DevToken_SeededUser_ReturnsUsableToken` (the only test of the issuer's success path through HTTP).
- `TenantContext.SetTenant(Guid.Empty)` throws `ArgumentException`; the interceptor also refuses a `Modified` entry
  whose original tenant differs from the current one (defence in depth; not reachable through the filter).
- `DevTokenIssuer.CreateToken(identity, issuedAt)` is public so tests can issue expired and unprovisioned tokens; the
  endpoint always passes `TimeProvider.GetUtcNow()`. Unknown user error code: `DevAuth.User.Unknown`.
- `Health_NoToken_Returns200` calls `/alive`; the existing `Health_DatabaseUp_Returns200` keeps covering `/health`.

### Findings
- The filter must compare `(Guid?)e.TenantId == this.CurrentTenantId`. A first version used `CurrentTenantId.Value`;
  EF evaluates that member client-side as a parameter and threw `Nullable object must have a value` without a tenant.
  `QueryFilter_NoTenant_SeesNoRows` caught it. `QueryFilter_SequentialContextsWithDifferentTenants_DoNotLeak` proves
  the filter reads the context instance, not a value cached in the model.
- `has-pending-model-changes` reports no changes for Portfolio, Compliance and Advisory (filters are not part of
  migrations; no entities yet).
- With the fallback policy, unknown routes answer 401 without a token and 404 ProblemDetails with one. In `Production`,
  `POST /dev/token` answers 401 (no endpoint, fallback policy) and never returns a token.
- Test-only routes added by an `IStartupFilter` after `next(app)` get the Api's authentication and tenant resolution;
  adding `UseRouting` → `UseAuthorization` → `UseEndpoints` there applies endpoint policies (`TenantMember`, 403 handler).
- `aspire run`: `api` healthy; `/dev/token` issued Alice's token; `/api/portfolio` answered 401 without a token and 404
  ProblemDetails with Alice's token (the group has no endpoints yet); `tenancy.tenants` holds Acme Pension Fund and
  Globex Insurance; `web` reached ready.
- Architecture probes (temporary, reverted): Portfolio registering `TenantContext` →
  `FinGuard.Portfolio.PortfolioModule does depend on "FinGuard.BuildingBlocks.Tenancy.TenantContext"`; Advisory context
  on plain `DbContext` → `Module DbContexts must derive from TenantAwareDbContext: FinGuard.Advisory.Persistence.AdvisoryDbContext`;
  an `ITenantOwned` entity in Tenancy → `Tenant-owned entities must be mapped only by a TenantAwareDbContext:
  FinGuard.Tenancy.Persistence.ProbeOwned in FinGuard.Tenancy.Persistence.TenancyDbContext`.
- RS0030 probe: both overloads in Portfolio failed with `The symbol 'EntityFrameworkQueryableExtensions.IgnoreQueryFilters<TEntity>(...)'
  is banned in this project: Tenant filter must not be bypassed (ADR-0004)`.
- An IDE-generated `finguard.sln` next to `FinGuard.slnx` makes bare `dotnet build` fail with MSB1011; the owner
  deleted it; `*.sln` is now in `.gitignore`.
- Tests: 72 (unit 30, integration 31, architecture 11), all green.

### Risks and flags
- **R-17** mitigated as planned (Development-only mapping, Production test, fail-fast `Authority`).
- **R-18 Dev tokens die on restart.** The signing key is per process, so the SPA (Brief #7) must request a new token
  after a 401.

## Delta 2026-11-14 (session 11: roadmap, Entra timing and Brief #5 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-56 | The repository is the source of truth for `docs/decisions.md`, `AGENTS.md`, rules and briefs. The planning assistant reads the repo at the start of each session and syncs the Claude Project copies itself; the owner no longer re-uploads them | Project copies of decisions.md, AGENTS.md, testing.md and compliance-domain.md synced on 2026-11-14 |
| D-57 | Microsoft Entra External ID tenant is created in Phase 5 (planned Brief #21), linked to an Azure subscription at creation. No trial tenant now | A trial tenant without subscription lasts 30 days; one active trial per account |
| D-58 | Phase 0 ends with Brief #5. The former roadmap item 6 (CI) was delivered by Briefs #1, #3 and #4 | Tentative map: Phase 1 = #6-#9, Phase 2 = #10-#12, Phase 3 = #13-#15, Phase 4 = #16-#19, Phase 5 = #20-#24 |
| D-59 | Development identity: in-process token issuer in the Api (`POST /dev/token`), Development only, random signing key per process, issuer `finguard-dev`, audience `finguard-api` | ADR-0008. Rejected: `dotnet user-jwts` (no SPA flow), Keycloak (`Aspire.Hosting.Keycloak` preview only) |
| D-60 | Non-Development environments bind `Authentication:Schemes:Bearer` (`Authority`, `Audience`) and fail fast without `Authority` | Entra External ID becomes configuration in Phase 5 |
| D-61 | Authorization: fallback policy requires an authenticated user; module endpoint groups require `TenantMember`; health endpoints anonymous | Unknown routes now answer 401 without a token |
| D-62 | Tenant filter: EF Core 10 named filter `"Tenant"` in `TenantAwareDbContext`, reading the context instance's `CurrentTenantId`; fail closed without a tenant; stamping and guards in `TenantStampingInterceptor` | `IgnoreQueryFilters` banned in `src/backend` (RS0030) |
| D-63 | Tenant resolution: Tenancy maps (`iss`, `sub`) to a tenant through `tenancy.users`; that Dapper lookup is the only query without a `tenant_id` filter; unknown identities get 403 `Tenancy.User.NotProvisioned` | `User` is not `ITenantOwned` |
| D-64 | Two development tenants seeded in Development only: `alice` → Acme Pension Fund, `bob` → Globex Insurance | Real onboarding arrives with Entra in Phase 5 |

### Verified on 2026-11-14
| Area | Finding | Source |
| --- | --- | --- |
| Entra External ID | Core offer free for the first 50,000 MAU | https://learn.microsoft.com/entra/external-id/external-identities-pricing |
| Entra External ID | Trial external tenant without subscription lasts 30 days; one active trial per account | https://learn.microsoft.com/entra/external-id/customers/quickstart-trial-setup |
| EF Core 10 | Named query filters; `IgnoreQueryFilters` can disable filters by name | https://learn.microsoft.com/ef/core/querying/filters |
| ASP.NET Core | JWT bearer options bind from `Authentication:Schemes:Bearer` | https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/security |
| Aspire Keycloak | `Aspire.Hosting.Keycloak` is preview | https://aspire.dev/integrations/security/keycloak |

### Risks and flags
- **R-16 Wolverine scope and the tenant.** `IMessageBus.InvokeAsync` may resolve handler services in a new scope, so the
  HTTP request's `ITenantContext` might not reach handlers. Brief #6 proves propagation (tenant id carried by the message,
  set by Wolverine middleware) before any slice depends on it.
- **R-17 Dev issuer leaking to production.** Mitigated by mapping only in Development, a Production test, and fail-fast
  configuration outside Development.

### Open questions
1. Whether the SPA keeps a generic OIDC client for both the dev issuer and Entra, or uses MSAL only in Phase 5 (frontend
   rule says MSAL behind `AuthService`). Decide in Brief #7.

### Next step
Brief #6 (Phase 1): Wolverine outbox spike with two DbContexts and tenant propagation into handlers.

## Delta 2026-11-14 (session 10: Brief #4 executed)

### Resolved versions (2026-11-14, `scripts/Get-LatestNuGetVersion.ps1`, stable only)
| Package | Version | Notes |
| --- | --- | --- |
| xunit.v3 | 4.0.3 | MTP v2 (`xunit.v3.mtp-v2`) |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.15 | `$(MicrosoftNet10Version)`; 11.0.0 is the .NET 11 line |
| Microsoft.Extensions.Diagnostics.Testing | 10.11.0 | Same 10.x line as Http.Resilience; 11.0.0 skipped |
| Testcontainers.PostgreSql | 4.16.0 | Image `postgres:18.3` |
| TngTech.ArchUnitNET | 0.13.5 | Core only, no framework extension |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | Not 3.3.4, see findings |

### Deviations
- BannedApiAnalyzers 5.6.0 instead of the 3.3.4 the brief named: the resolver returned 5.6.0 (stable 4.14.0, 5.0.0,
  5.3.0 and 5.6.0 exist). 5.6.0 needs Roslyn 5.6+; SDK 10.0.403 ships Roslyn 5.9. Approved by the owner before execution.
- `CloudSeamsStartupLogger` still receives two bools; `Extensions.cs` computes them once through `CloudSeams`, so the
  check runs once per seam and the log lines are byte-identical.
- The generic-repository and one-DbContext rules use reflection, not ArchUnitNET: they inspect generic type definitions
  and a `const` value, which reflection reads directly. ArchUnitNET is used where it adds value: type dependencies
  (Compliance `Domain/`).
- `tests/Directory.Build.props` also adds a global `using Xunit` (xunit.v3 does not add one).
- `.editorconfig` `[tests/**.cs]` relaxes only CA1707. CA2007 never fired, so it stays on.
- The integration fixture blanks `KeyVault:Uri` and `APPLICATIONINSIGHTS_CONNECTION_STRING` so local user-secrets or
  environment variables cannot switch a seam on during tests.

### Findings
- `dotnet test --no-build -c Release` works unchanged in MTP mode from the repo root; CI needs no edit.
- `UseSetting("ConnectionStrings:finguard", ...)` reaches the module registration that reads the connection string while
  the builder runs; no other hook was needed.
- EFCore.NamingConventions also snake-cases the migrations history columns (`migration_id`, `product_version`).
- With an `IExceptionHandler` that handles the exception, .NET 10 does not log the exception from the middleware;
  the only record is `GlobalExceptionHandler`'s (type + trace id). Proven by `UnhandledException_AnyRequest_DoesNotLogExceptionMessage`.
- The test-only throwing route is added after the Api pipeline (`next(app)` first in the `IStartupFilter`) so the Api's
  exception handler wraps it.
- RS0030 probe: a temporary `DateTime.UtcNow` in BuildingBlocks failed the build with
  `error RS0030: The symbol 'DateTime.UtcNow' is banned in this project: Use TimeProvider (AGENTS.md)`; a temporary
  `EnableSensitiveDataLogging()` in Compliance failed with `... Never log query parameters (D-35)`. Both removed.
- Architecture probes (temporary, reverted) failed all seven rules with messages naming the offender, identically in
  Debug and Release (R-14: no difference seen). Note that an unused `ProjectReference` is dropped by the compiler in both
  configurations, so assembly rules see only references that code actually uses.
- Tests: 43 (unit 25, integration 10, architecture 8), all green; `dotnet test` about 6 s locally with the image cached.

### Risks and flags
- **R-15 updated.** BannedApiAnalyzers has stable 5.x releases; the risk becomes "keep the analyzer at or below the
  SDK's Roslyn version", not "the analyzer is stuck at 3.3.4".

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

## Delta 2026-11-14 (session 8: Brief #3 executed)

### Resolved versions (2026-11-14, `pnpm view`, stable only)
| Package | Version | Notes |
| --- | --- | --- |
| pnpm (`packageManager`) | 11.28.9 | `latest-11`; global pnpm 12.18.0 switches to it automatically |
| Node | 24.16.0 (local) | `engines.node` = `^24.15.0` |
| @angular/cli, @angular/build | 22.2.7 | |
| @angular/core and framework packages | 22.2.6 | Caret ranges from the CLI; 22.2.7 is newer than pnpm 11's `minimumReleaseAge` window |
| TypeScript | 6.0.3 | `~6.0.2`, inside Angular's `>=6.0.0 <6.1.0` |
| Vitest | 5.1.0 | jsdom 30.1.3 |
| angular-eslint, @angular-eslint/builder | 22.6.0 | |
| ESLint | 10.14.0 | Chosen by the schematic (`^10.12.0`); typescript-eslint 8.71.0, @eslint/js 10.0.1 |

### Build scripts
None approved. The Angular CLI writes `allowBuilds` with `@parcel/watcher`, `esbuild`, `lmdb` and `msgpackr-extract`
set to `false`; install, build, test and `ng serve` work with their prebuilt binaries, so the denials stay (with a
comment) in `pnpm-workspace.yaml`. pnpm reports no ignored build scripts.

### Deviations from the brief
- `ng new` ran with `--skip-install --interactive false --zoneless --test-runner vitest --ai-config none` so the first
  install used the pinned pnpm 11 (the CLI wrote `packageManager: pnpm@12.18.0` from the global pnpm).
- The generated `pnpm-workspace.yaml` had `packages: ['.']`; replaced by the brief's file (no `packages:` key) plus the
  CLI's `allowBuilds` denials. The generated `watch` and `ng` scripts are kept.
- `AppHost.cs` had no Api variable; `var api =` was added to the existing chain. The `web` resource matches the brief
  (the 13.6.5 API is unchanged).
- Dependabot `cooldown.default-days: 3`: Dependabot's own default is 3 days, stated explicitly (brief asks for at least 2).
- `.gitignore` entries from the CLI were merged into the root file; root-anchored ones are prefixed with
  `src/frontend/finguard-web/`. `index.html` title is `FinGuard`.

### Findings
- `pnpm start` outside Aspire: without `PORT`, the pnpm shell emulator stops at once with `Unbound variable "PORT"`.
  With `PORT` set, `ng serve` prints the proxy error (`API endpoint is not configured. Run the app through Aspire
  (aspire run).`), does not listen, but keeps its watch process alive until stopped. No silent fallback URL.
- Peer warning on install: `@napi-rs/wasm-runtime@1.2.7` wants `@emnapi/core`/`@emnapi/runtime` `^1.7.1`, gets
  `2.0.0-alpha.6` (transitive WASM fallback, unused on supported platforms).
- `aspire run` shows `web-installer` (the pnpm install step) as `Finished` next to `postgres`, `finguard`, `api`, `web`.
- Proxy check: `GET http://localhost:<web port>/api/does-not-exist` returned the Api's `404 application/problem+json`
  with `traceId`.
- `pnpm-lock.yaml` is a single document (`lockfileVersion: '9.0'`). Proxy config stays CommonJS (`proxy.conf.js`).

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

## Delta 2026-11-14 (session 6: Brief #2 executed)

### Resolved versions (2026-11-14, `scripts/Get-LatestNuGetVersion.ps1`, stable only)
| Package | Version | Notes |
| --- | --- | --- |
| Aspire.* (`AspireVersion`), `Aspire.AppHost.Sdk` | 13.6.5 | Unchanged; Aspire CLI 13.6.5 (now a .NET global tool `Aspire.Cli`) |
| WolverineFx, .Postgresql, .EntityFrameworkCore, .RuntimeCompilation (`WolverineVersion`) | 6.64.0 | Unchanged; all four equal |
| EFCore.NamingConventions | 10.0.1 | |
| dotnet-ef (local tool) | 10.0.15 | 11.0.0 is latest but targets EF 11; matches `MicrosoftNet10Version` |
| Microsoft.Extensions.Http.Resilience / ServiceDiscovery | 10.11.0 | Template uses 10.10.0; 11.0.0 skipped (pulls .NET 11 abstractions, same reason as D-20) |
| Microsoft.EntityFrameworkCore.Relational (transitive pin) | 10.0.15 | New entry; otherwise 10.0.14 and 10.0.15 both resolve (MSB3277) |
| PostgreSQL container image (Aspire default) | `postgres:18.3` | |

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-37 | No `PackageVersion` for `Aspire.Hosting.AppHost`: the SDK references it implicitly and CPM fails restore with NU1009 | Version comes only from the `Aspire.AppHost.Sdk/13.6.5` literal (R-08) |
| D-38 | Modules register `AddScoped<DbContext>(sp => sp.GetRequiredService<XxxDbContext>())` so the Api applies migrations without seeing the internal DbContext types | Never inject the base `DbContext` elsewhere |
| D-39 | Connection name and the "fail fast when missing" lookup live in BuildingBlocks (`ModuleDbContextOptions.ConnectionName`, `GetRequiredFinGuardConnectionString`); design-time factories use the placeholder `DesignTimeConnection.Placeholder` (no credentials) | One place for the four modules |
| D-40 | `Npgsql.Command` logs at Warning in `appsettings.json` | Health checks otherwise log every `SELECT 1` at Information |
| D-41 | `Error` keeps its name; CA1716 suppressed on the type with a justification | Brief/D-35 name; C#-only codebase |

### Deviations from the brief
- Tool manifest: .NET 10 `dotnet new tool-manifest` writes `dotnet-tools.json` at the root; moved to `.config/dotnet-tools.json` as the brief asks (both locations are honored by `dotnet tool`).
- Wolverine runtime compilation: `WolverineFx.RuntimeCompilation` auto-registers when referenced (`RuntimeCompilationModule`); `opts.UseRuntimeCompilation()` is also called explicitly (idempotent) to make the opt-in visible.
- Wolverine storage auto-create: no setting needed. `AutoBuildMessageStorageOnStartup` defaults to `CreateOrDefault` (Wolverine 6.64 XML docs).
- Wolverine telemetry: tracing source `Wolverine`, meter wildcard `Wolverine*`; the meter is named `Wolverine:{ApplicationName}`, a bare `AddMeter("Wolverine")` matches nothing (https://wolverine.netlify.app/guide/logging.html).
- Aspire 13.6 AppHost template entry file is `AppHost.cs` (not `Program.cs`) and adds `<AspireUseCliBundle>true</AspireUseCliBundle>`; both kept.
- Migration logger uses its own category: `ILogger<WebApplication>` falls under `Microsoft.AspNetCore` (Warning) and was silent.
- Module libraries work as their own dotnet-ef startup project; the Api was not needed.

### Findings
- `__ef_migrations_history` columns are snake-cased by the naming convention: `migration_id`, `product_version`.
- On a fresh database EF logs one `fail` (`Microsoft.EntityFrameworkCore.Database.Command[20102]`) per module for the first `SELECT ... FROM <schema>.__ef_migrations_history`, then creates the table and applies the migration. It does not recur on later runs. Expected noise, not an error.
- `wolverine` schema tables created at startup: `wolverine_agent_restrictions`, `wolverine_control_queue`, `wolverine_dead_letter_history`, `wolverine_dead_letters`, `wolverine_incoming_envelopes`, `wolverine_node_assignments`, `wolverine_node_records`, `wolverine_nodes`, `wolverine_outgoing_envelopes`.
- Wolverine logs "found no handlers" as a warning; expected until the first slice.
- Probe: unhandled exception returns 500 `application/problem+json` with `title`, `status`, `traceId` only; the exception message did not appear in the response or the logs. Unknown route returns 404 ProblemDetails with `traceId`.
- Aspire CLI installed from npm cannot self-update and conflicts with the no-npm rule; replaced with the `Aspire.Cli` .NET global tool. In Git Bash the tool shim is `aspire.cmd`; use PowerShell. (Session 19: still true; cause and Git Bash workaround in D-135.)

### Next step
Brief #3: Angular 22 app with pnpm, added to the AppHost with `AddJavaScriptApp(...).WithPnpm()`.

## Delta 2026-11-14 (session 5: Brief #2 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-28 | Every brief states its branch and exact PR title (Conventional Commits). Squash merge uses "Pull request title". Brief numbers and GitHub PR numbers differ (Brief #2 = PR #3); refer to work by brief number | PR #1 squash title "Chore/repo hygiene (#1)" stays; `main` is not rewritten |
| D-29 | Wolverine in Brief #2 = durable message storage only, schema `wolverine`, via `PersistMessagesWithPostgresqlUsingDataSourceFromContainer` on the Aspire `NpgsqlDataSource`; plus `WolverineFx.RuntimeCompilation` | EF transactional integration stays in the Phase 1 spike (R-02) |
| D-30 | Wolverine/Weasel-managed EF schema (`UseEntityFrameworkCoreWolverineManagedMigrations`) is not used; EF Core migrations own module schemas | ADR-0007; Wolverine docs say not to mix both |
| D-31 | One empty `InitialSchema` migration per module; history table `__ef_migrations_history` in the module schema; applied at Api startup in Development only | Production strategy decided in Phase 5 |
| D-32 | snake_case via `EFCore.NamingConventions` (10.x); Dapper `MatchNamesWithUnderscores = true` | Closes the choice left open in ADR-0007 |
| D-33 | DbContexts registered with `AddDbContext` + shared `UseModuleNpgsql` + `EnrichNpgsqlDbContext` (DbContext health checks off); the `NpgsqlDataSource` health check covers the database | Same options for runtime and design time |
| D-34 | Cloud seams (Key Vault source, Azure Monitor toggle) move into Brief #2 (ServiceDefaults); Brief #5 keeps `ITenantContext` and the dev JWT | Roadmap item 5 narrowed |
| D-35 | `ErrorType` to HTTP: Validation 400, NotFound 404, Conflict 409, BusinessRule 422; ProblemDetails carry `code` and `traceId`; unexpected exceptions are a generic 500 in every environment | Correlation id = W3C trace id |
| D-36 | Solution file is `FinGuard.slnx` at the repo root; AppHost uses `Aspire.AppHost.Sdk` as its only SDK | |

### Closed open questions
- `main` is unborn: closed. `main` has the initial commit and the PR #1 squash commit.

### Verified on 2026-11-14
| Area | Finding | Source |
| --- | --- | --- |
| Aspire | 13.6 released 2026-09-29; 13.6.5 is the latest patch on NuGet (`Aspire.Npgsql`) | https://devblogs.microsoft.com/aspire/whats-new-aspire-13-6 , https://nuget.org/packages/Aspire.Npgsql/13.6.5 |
| Aspire AppHost | Since 13.0 `Aspire.AppHost.Sdk` can be the sole SDK and implicitly references `Aspire.Hosting.AppHost` | https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview |
| Aspire EF | `EnrichNpgsqlDbContext<T>` adds retries, health check, logging, telemetry to a self-registered DbContext | https://aspire.dev/integrations/databases/efcore/postgres/postgresql-client |
| Wolverine 6 | Requires .NET 9/10; Roslyn moved to `WolverineFx.RuntimeCompilation`; `ServiceLocationPolicy` default `NotAllowed` | https://wolverine.netlify.app/guide/migration |
| Wolverine PG | `PersistMessagesWithPostgresqlUsingDataSourceFromContainer(schemaName:)` uses a DI-registered `NpgsqlDataSource` | https://wolverine.netlify.app/guide/durability/postgresql |
| Wolverine EF | Weasel-managed EF migrations are opt-in; docs advise choosing one approach | https://wolverine.netlify.app/guide/durability/efcore/migrations |
| EF naming | `EFCore.NamingConventions` 10.0.1 stable, community-maintained | https://www.nuget.org/packages/EFCore.NamingConventions/ |
| .slnx | .NET 10 `dotnet new sln` creates `.slnx` | https://learn.microsoft.com/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default |

### Risks and flags
- **R-08 AppHost SDK version outside CPM.** The `Sdk` attribute holds a literal version; keep it equal to `AspireVersion`
  on every Aspire upgrade (Dependabot may not bump it).
- **R-09 Wolverine 6 cadence and defaults.** Very frequent 6.x releases; Roslyn opt-in and `ServiceLocationPolicy.NotAllowed`
  change behaviour from older samples. Pin, read the changelog, never copy 5.x snippets.
- **R-10 Community naming package.** `EFCore.NamingConventions` is not Microsoft-supported and may lag EF majors.

### Open questions
1. Compliance `Domain/` "depends on nothing", but `Result`/`Error` live in BuildingBlocks next to ASP.NET-specific
   mapping. Decide in Phase 3: allow BuildingBlocks `Results/` in the domain, or move HTTP mapping out.
2. Production migration strategy (migration bundle, job or startup with lock): Phase 5.

### Next step
Brief #3: Angular 22 app with pnpm, added to the AppHost with `AddJavaScriptApp(...).WithPnpm()`.

## Delta 2026-11-14 (session 4: AI rules guide)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-27 | Path-scoped AI rules policy documented in `docs/ai-rules.md`: no always-loaded rules, Claude and Cursor rule files mirrored and edited in the same commit | Cursor `compliance-domain.mdc` globs fixed to `src/backend/Modules/Compliance/**,tests/**/Compliance*/**` to match `.claude/rules/compliance-domain.md` |

## Delta 2026-11-13 (session 3: Brief #1 executed)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-19 | .NET 10 package line: EF Core, EF Core Design, JwtBearer pinned to latest 10.x (10.0.15) via `MicrosoftNet10Version`; 11.x skipped | 11.0.0 targets .NET 11 |
| D-20 | `Microsoft.Extensions.AI` pinned to 10.11.0 to match `Microsoft.Agents.AI` 1.28.0 | 11.0.0 pulls .NET 11 abstractions into net10.0 |
| D-21 | GitHub Actions majors: checkout@v7, pnpm/action-setup@v6, setup-dotnet@v6, setup-node@v7 | Resolved via gh api 2026-11-13 |
| D-22 | `ci-gate` is the single required status check | |
| D-23 | EF Core for commands, Dapper for queries (ADR-0007) | Dapper 2.1.89 and `Aspire.Npgsql` added to `Directory.Packages.props` |
| D-24 | Add `dotnet format --verify-no-changes` to the CI `backend` job in PR #2 | `dotnet build` does not report IDE0003 or using order |
| D-25 | PascalCase for private `const` / `static readonly` fields | Naming rule placed before the `_camelCase` rule in `.editorconfig` |
| D-26 | Repository visibility: public (portfolio; enables branch protection on GitHub Free). LICENSE: MIT | |

### Verified on 2026-11-13
| Area | Finding | Source |
| --- | --- | --- |
| Angular 22 | Node `^22.22.3 \|\| ^24.15.0 \|\| ^26.0.0`, TypeScript `>=6.0.0 <6.1.0`; no package-manager requirement | https://angular.dev/reference/versions |
| pnpm | 12.18.0 installed locally; engines `node >=18` | `pnpm view pnpm@12.18.0 engines` |
| Code style | `dotnet build` enforces IDE0161, IDE0065, IDE0011, IDE1006; IDE0003 and using order only via `dotnet format --verify-no-changes` | Probe in Brief #1 |

### Open questions
- `main` is unborn locally; create an initial commit before the first push.

### Next step
PR #2: .NET solution, AppHost, ServiceDefaults, Api, module projects.

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
