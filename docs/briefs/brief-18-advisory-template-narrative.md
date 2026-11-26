# Brief #18 — Advisory module with a template narrative

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-18-advisory-template-narrative.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-18-advisory-template-narrative.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/advisory-template-narrative` |
| PR title (exact, becomes the squash commit) | `feat: add the Advisory module with a template narrative for each evaluation` |
| Phase | Phase 4, second of three (#17 rebalance engine done, **#18 Advisory with template narrator**, #19 Agent Framework narrator on a local model). Phase 4 exit: proposal always present; narrative asynchronous and degrades gracefully |

---

## Goal
Advisory subscribes to `EvaluationCompleted`, turns the event into a set of **advisory facts** (the event's numbers
plus their display texts, formatted once), writes an English narrative with the deterministic `TemplateNarrator`,
and stores facts and narrative. A query endpoint returns the narrative, and the submission detail page shows it in a
new "Advisory" section with an asynchronous state. A pure `NarrativeValidator` (every number in a narrative must come
from the facts) is built and tested now against the template; #19 wires it to the model.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/backend-vsa.md`,
  `.claude/rules/frontend.md`, `.claude/rules/testing.md`, `docs/decisions.md` (newest delta: session 26, Brief #17
  executed), ADR-0002, ADR-0006, ADR-0007, ADR-0009, ADR-0011, ADR-0013.
- Advisory today is an empty shell: `AdvisoryModule` (DbContext registration, empty `/api/advisory` group),
  `AdvisoryDbContext` (schema `advisory`, `TenantAwareDbContext`), an empty `InitialSchema` migration, an empty
  `FinGuard.Advisory.Contracts`. It has no reference to `FinGuard.Compliance.Contracts` yet.
- `EvaluationCompleted` (Compliance.Contracts) carries `TenantId`, `EvaluationId`, `SubmissionId`, `CutOffDate`,
  `OverallStatus`, `PolicyVersion`, four `ClassEvaluation`s (`BookValue`, `Limit`, `Utilization`, `Status`,
  `ProposedBookValue`) and `ProposalStatus`. It has **no route** today (D-199): the first handler creates it.
  It does not carry `AlertThreshold` or `RebalanceTargetRatio`, which the narrative needs (status-consistent
  percentages, R-39; "85.00 % of its limit").
- Compliance's `SubmissionAcceptedHandler` is the pattern to copy: idempotent by `EventId`, writes through the module
  DbContext, Wolverine's EF middleware commits, logs identifiers only.
- Frontend: `submission-detail.ts` already polls the evaluation while it is 404 (`EVALUATION_RETRY_DELAYS_MS`,
  D-205) and shows the proposal (#17). `formatRatio` (D-207, R-39), `formatPercent` and `UsdPipe` (D-208) are the
  display rules the narrative must reproduce exactly.

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Event gap | `EvaluationCompleted` gains `AlertThreshold` and `RebalanceTargetRatio` (decimals from the evaluation's policy snapshot), appended last; additive (ADR-0011) | Advisory hard-coding 0.90 / 0.85 (wrong as soon as policy v2 exists); a query into Compliance (cross-module read for data the event should carry) |
| Pipeline | One handler: `EvaluationCompleted` → build facts → `TemplateNarrator` → store, in one transaction, idempotent by `EventId` | Two steps (store pending, then a `GenerateNarrative` command): no benefit while the narrator is instant. #19 adds the model as a second, optional step that may replace the template text (see "Path to #19") |
| Facts | `AdvisoryFacts`: cut-off date, overall status, policy version, proposal status, ratios, and per class: raw decimals plus display texts (`BookValueText`, `ShareText`, `LimitText`, `UtilizationText`, `ProposedText`, `ChangeText`). Built once by `AdvisoryFactsBuilder` from the event. No free text exists in the event, so none can reach a narrative (ADR-0002) | Formatting inside each narrator (the model and the template could disagree on numbers) |
| Display format | `AdvisoryFormat` in C# mirrors the SPA exactly: money `en-US` currency with 2 decimals (`$1,562,500.00`); share/limit `formatPercent` (`25.00 %`); utilization `formatRatio` (2 to 8 decimals, never contradicts the status, D-207). Shared test vectors in both test suites | Server-formatted texts sent to the SPA (the evaluation table keeps its own formatting; out of scope) |
| Narrative | Plain-text paragraphs (no Markdown, no HTML), English, written by `TemplateNarrator : IAdvisoryNarrator`. Wording fixed in this brief (task 2) | Markdown (rendering untrusted model output as HTML in #19) |
| Validation | `NarrativeValidator.Validate(text, facts)`: every numeric token in the text must appear among the facts' texts. Pure, tested now on every template output; used for the model in #19 | Building it in #19 (riskier; the template is the first, safe consumer) |
| Persistence | `advisory.advisories`: `id`, `tenant_id`, `evaluation_id` (unique), `submission_id` (unique), `source_event_id` (unique), `facts jsonb`, `narrative text`, `narrator varchar(16)` (`Template` now), `generated_at`. Plain EF entity (only Compliance uses aggregates, ADR-0007) | Columns per class (the facts are the narrator input and are only read as a whole) |
| Read API | `GET /api/advisory/submissions/{submissionId}/advisory` (Dapper, own schema, `tenant_id` filter) → `{ evaluationId, submissionId, narrator, generatedAt, paragraphs: string[] }`. 404 `Advisory.Advisory.NotFound` for unknown, other tenant's or not yet written (D-197 pattern). Facts are not returned | Returning facts (the SPA already has the numbers from the evaluation) |
| UI | Separate `AdvisoryPanel` component under the proposal. States: `none` (evaluation has `proposal: null`, legacy: "No advisory is available for this evaluation.", no request), `pending` (404 with the same retry delays), `timeout` ("The advisory is not ready yet." + Check again), `error`, `ready` (paragraphs as text interpolation, source line "Written from a template."). The retry loop is extracted once to `shared/` and used by both the evaluation and the advisory | Growing `submission-detail.ts` further; `innerHTML` (never, also not in #19) |
| R-40 backfill | **No backfill.** Evaluations stored before this brief have no advisory and stay at `timeout`/`none`. All of them are local development data; Phase 5 starts with an empty database. R-40 closed as accepted | A Compliance republish command or a startup backfill (code that runs once, for data nobody keeps) |
| Outbound events | None. `AdvisoryGenerated` / `AdvisoryFailed` in `overview.md` arrive only if the optional Audit module is built (Phase 5) | Publishing events without a consumer (D-199 shows they are dropped) |
| ADR | ADR-0014 "Advisory facts, template narrator and narrative validation" | Only a decisions entry (it sets the contract #19 must keep) |

### Path to #19 (context only, do not build)
#19 adds an optional step after the template is stored: a cascaded command asks the model through
`AgentFrameworkNarrator`, validates the output with `NarrativeValidator`, and replaces `narrative` / `narrator` only
when it passes. The template text is therefore always available immediately; the model is an upgrade. Keep the
handler and the entity shaped so that this is an addition, not a rewrite (for example `narrator` is a column now).

## Environment rules
- AGENTS.md "Closing a work session": `scripts/dev-session.ps1 -Snapshot` first, Aspire only through `-StartAspire`,
  `-Stop` before the final message, Close-out block with "Still running: none".
- pnpm only, from inside `src/frontend/finguard-web`; Git Bash uses `aspire.cmd` (D-135).
- Build the solution as `dotnet build FinGuard.slnx` while the untracked `finguard.sln` exists (R-47); do not delete it.
- Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Logs and `ToString()`: identifiers only (D-162). Never log facts, narrative text, amounts, ratios or dates.
- `docs/decisions.md` is append-only (D-137). Ask before deviating; record approved deviations.

## Preflight
```powershell
git switch main; git pull --ff-only
git switch -c feat/advisory-template-narrative
scripts/dev-session.ps1 -Snapshot
dotnet build FinGuard.slnx -c Release; dotnet test --no-build -c Release      # baseline 463
```
Frontend baseline: Vitest 111, E2E 9.

## Tasks

### 1. Event ratios (Compliance)
- `EvaluationCompleted` gains `decimal AlertThreshold, decimal RebalanceTargetRatio`, appended after
  `ProposalStatus`; XML docs say they come from the policy snapshot the evaluation used. `ContractMapping` fills them;
  `ToString()` unchanged.
- Architecture test (ADR-0002 guard): `EvaluationCompleted` and every type it contains have no `string` member, so no
  free text can reach Advisory. Name it `EvaluationCompleted_CarriesNoFreeText`.

### 2. Advisory facts, formatting, template, validator (`FinGuard.Advisory`)
- Project reference to `FinGuard.Compliance.Contracts` only (module boundary tests must stay green).
- `Features/WriteAdvisory/` (or the VSA folder name the backend rules prefer): `AdvisoryFacts` (records, ids-only
  `ToString()`), `AdvisoryFactsBuilder.From(EvaluationCompleted)`, `AdvisoryFormat`, `IAdvisoryNarrator`
  (`Task<string> NarrateAsync(AdvisoryFacts facts, CancellationToken ct)`), `TemplateNarrator`, `NarrativeValidator`.
  `IAdvisoryNarrator` is the port from ADR-0006; only `TemplateNarrator` is registered now.
- Class names as in the SPA: `Cash`, `Fixed income`, `Shares`, `Real estate`. Class order for sentences = D-235
  (reducers: Breached, then Warning by utilization desc; receivers by amount received desc; ties by enum order).
- Template wording (paragraphs separated by one blank line; adjust grammar for one vs several classes; counts in
  words, so the only digits in the text come from the facts):
  1. Headline by overall status:
     - Compliant: `On {date}, the portfolio is within every limit of policy version {v}. No rebalancing is needed.`
     - Warning: `On {date}, the portfolio is within its limits under policy version {v}, but {list} {is/are} near the limit.`
     - Breached: `On {date}, the portfolio breaches the limit for {list} under policy version {v}.`
  2. One sentence per class in alert:
     `{Class} holds {ShareText} of the portfolio against a limit of {LimitText}, so {UtilizationText} of the limit is used.`
  3. Proposal (not for `NotNeeded`):
     - Reducers: `The proposal reduces {Class} by {abs ChangeText} to {ProposedText}` (joined with "and"), then
       `, which is {TargetRatioText} of its limit.` (`85.00 %`).
     - Receivers: `The amount moves to {Class} (+{amount}), ...` with "and" before the last one, then
       `Each of these stays at or below {TargetRatioText} of its limit.` Middle classes are not mentioned.
     - `Full`: `After these moves, every asset class is within its limit.`
     - `Partial`: `Cent rounding leaves at least one asset class outside its target; review the amounts.`
  4. Always last: `This text explains a deterministic calculation. It is not investment advice.`
- `NarrativeValidator`: numeric tokens are matched with one regex (digits with optional thousands separators and
  decimals, for example `1,562,500.00`, `73.33`, `2026`, `1`); a narrative is valid when every token is contained in
  the token set extracted from the facts' texts (date, policy version, all display texts). Returns a `Result` with an
  error code naming the rule, never the offending number (no amounts in errors or logs).

### 3. Persistence and handler
- `Advisory` entity (internal, `sealed`) + EF configuration for `advisory.advisories` as decided; `facts` is a
  `string` property mapped to `jsonb`, serialized with `System.Text.Json` (decimals as numbers). Migration
  `AddAdvisories`; `has-pending-model-changes` clean. Tenant stamping and the named query filter as in the other
  modules; tenant tests that enumerate tenant-owned tables must cover the new table.
- `EvaluationCompletedHandler` (public sealed, like Compliance's): duplicate `EventId` → nothing; otherwise build
  facts, narrate, validate (a template failing validation is a bug: throw, error queue), store. Log
  `Wrote advisory for evaluation {EvaluationId}` only.
- Tenant: the envelope tenant is checked against the payload tenant by `TenantPropagationMiddleware` (existing).

### 4. Read slice and endpoint
- `GetAdvisory` query + Dapper handler (own schema, mandatory `tenant_id` filter), endpoint in `MapAdvisoryEndpoints`
  through `InvokeForCurrentTenantAsync`, error code `Advisory.Advisory.NotFound`. Paragraphs are split on the blank
  line server-side.

### 5. Frontend
- `core/api/advisory-api.ts` (`httpResource`, types, `ADVISORY_NOT_FOUND`).
- Extract the 404 retry loop from `submission-detail.ts` into one helper in `shared/` (same delays, same
  "Check again" behaviour); the evaluation keeps its current behaviour and tests.
- `features/submissions/advisory-panel.ts` (+ html/css/spec), inputs `submissionId` and `hasProposal`; states as
  decided; `data-advisory-state` for tests; heading "Advisory"; paragraphs rendered with `{{ }}` only.

### 6. Docs
- `docs/adr/0014-advisory-narrative.md` (+ `docs/adr/README.md`): facts, formatting parity, template, validator,
  no backfill (R-40), path to #19.
- New `.claude/rules/advisory.md` + `.cursor/rules/advisory.mdc` (same commit; paths `src/backend/Modules/Advisory/**`,
  `tests/**/*Advisory*/**`), at most ~15 lines: numbers only from `AdvisoryFacts`; narrators never compute amounts or
  statuses; no free text in facts; every narrative passes `NarrativeValidator`; no facts or narrative in logs;
  plain text only. Update the table in `docs/ai-rules.md`.
- `AGENTS.md` (Advisory lines), `docs/architecture/overview.md` (Advisory subscribes; AdvisoryGenerated/Failed only
  with Audit), `README.md` (detail page shows the advisory), `samples/portfolios/README.md` (expected narrative for
  `valid-shares-heavy.csv`).
- `docs/decisions.md`: Appendix A as the planning delta, then the execution delta on top.

## Tests that prove it
Unit:
- `AdvisoryFormat` vectors, identical to new or existing Vitest cases for `formatRatio` / `formatPercent` / `UsdPipe`:
  `0.89999996` Compliant → `89.999996 %`; `0.9` Warning → `90.00 %`; `1` Warning → `100.00 %`;
  `1.00000001` Breached → `100.000001 %`; shares-heavy utilization Breached → `293.33 %`; `0.25` → `25.00 %`;
  `1562500` → `$1,562,500.00`; `0.01` → `$0.01`.
- `AdvisoryFactsBuilder`: four classes in enum order, texts as above, ratios from the event.
- `TemplateNarrator` golden texts for three cases: balanced (Compliant, NotNeeded), shares-heavy (Breached, Full,
  three receivers), a Warning-only portfolio (Full). One `Partial` case built directly from facts. Deterministic:
  same facts, same text.
- `NarrativeValidator`: accepts every golden text; rejects a text with a changed amount, an extra percentage, or a
  number not in the facts; the error never contains the number.
- Generated check: for the 10,000-portfolio generator from #17 (reuse it, same seed), evaluation → event → facts →
  template → validator passes for every case.
- Contract mapping: `AlertThreshold` / `RebalanceTargetRatio` from the snapshot; `ToString()` of the event still ids only.
Architecture: `EvaluationCompleted_CarriesNoFreeText`; Advisory references only Contracts of other modules (existing
rules); Advisory entity internal, DbContext public sealed as the other modules (D-200).
Integration (real PostgreSQL, through HTTP):
- `Upload_SharesHeavy_WritesTemplateAdvisory` (row stored, `narrator = Template`, API paragraphs equal the golden text).
- `EvaluationCompleted_Duplicate_WritesOneAdvisory` (same event twice through the bus).
- `GetAdvisory_OtherTenant_ReturnsNotFound` and `GetAdvisory_Unknown_ReturnsNotFound`.
- `Upload_Valid_PublishesOneEvaluationCompleted` keeps passing (it counts `Sent` or `NoRoutes`, D-199); confirm the
  record is now routed, not `NoRoutes`.
Vitest: retry helper (delays, timeout, check again); `AdvisoryPanel` states none / pending / timeout / error / ready;
a paragraph containing `<b>x</b>` is shown as text; `formatRatio` vectors above.
E2E (extend `evaluation.e2e.ts` or add `advisory.e2e.ts`): shares-heavy shows the Advisory section with the reduction
sentence and "Written from a template."; balanced shows "No rebalancing is needed.". Run twice.

## Acceptance criteria
- `dotnet build FinGuard.slnx -c Release` (0 warnings), `dotnet format --verify-no-changes`, `dotnet test -c Release` green.
- Frontend: `pnpm lint`, `pnpm test --watch=false`, `pnpm build`, `pnpm exec prettier --check src e2e` green.
- With `-StartAspire`: shares-heavy and balanced show their advisory within the retry window; an evaluation from
  before this brief shows the timeout state; `wolverine.wolverine_dead_letters` has 0 new rows; `pnpm e2e` green
  twice; Aspire stopped; Close-out with "Still running: none".

## Rules to respect
- ADR-0002: Advisory never computes amounts or statuses; it formats and explains what the event says.
- ADR-0006: `IAdvisoryNarrator` port; no Agent Framework or `Microsoft.Extensions.AI` package in this brief.
- ADR-0007: EF for the write, Dapper for the read with the `tenant_id` filter; no repository.
- ADR-0011: additive event change only.
- No business rule in the SPA; no `innerHTML`.

## Out of scope
Any model, Ollama, Agent Framework or `IChatClient` (#19); backfill of old evaluations (R-40, closed); outbound
Advisory events and Audit; regenerating narratives; charts; per-position advice; localisation (English only).

## Commit plan
1. `feat: add the policy ratios to EvaluationCompleted` (task 1 with tests)
2. `feat: write a template advisory for each completed evaluation` (tasks 2-3 with tests)
3. `feat: return and show the advisory narrative` (tasks 4-5 with tests)
4. `docs: add ADR-0014 and the advisory rules` (task 6)

## Pull request
- **Title (exact):** `feat: add the Advisory module with a template narrative for each evaluation`
- **Body:** `.git/finguard-pr-body.md` from the template; Summary names ADR-0014, says `EvaluationCompleted` now has a
  route, and that R-40 is closed without backfill.
- Close-out block with:
  `git push -u origin feat/advisory-template-narrative` and
  `gh pr create --base main --head feat/advisory-template-narrative --title "feat: add the Advisory module with a template narrative for each evaluation" --body-file .git/finguard-pr-body.md`.

## Report back
Plan first; wait for approval. At the end: the shares-heavy narrative as generated (sample numbers, may appear in the
report), the generated-check summary (cases, validator failures), test counts, two E2E runs, a screenshot of the
Advisory section (light and dark), the execution delta and the Close-out block.

---

## Appendix A — Decisions delta (session 27: Brief #18 planning)

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
