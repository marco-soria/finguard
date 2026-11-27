# ADR-0015: Model narrator: an AI context paragraph beside the template

- Status: Accepted
- Date: 2026-11-27

## Context
ADR-0014 stores a template narrative for every evaluation, correct by construction and validated against its facts.
ADR-0006 chose Microsoft Agent Framework behind the `IAdvisoryNarrator` port for an AI narrative, and ADR-0002 says the
model only explains: it never computes an amount or decides a status. Phase 4 ends with a narrative that is
"asynchronous and degrades gracefully". Two constraints shape the design. Wolverine's Eager EF transaction opens before
any handler that uses a DbContext (D-67), so a model call of several seconds must not run in such a handler. And the
app must work the same with no model at all: CI, the tests and a fresh clone have no Ollama.

## Decision
The first design let a validated model text replace the template narrative. Live runs with `llama3.2:3b` rejected it
(D-293): accepted rewrites stated wrong class statuses ("The Cash asset class is Breached" for a Warning class, "above
the limit" for a class using 84.00 % of it) and still passed every number, status-phrase, format and length rule. The
template now always writes the narrative, every figure and every status; the model only adds one context paragraph
that may contain none of them.

1. **Refinement is a separate step (D-281).** `EvaluationCompletedHandler` is unchanged except that, when a model is
   configured and the proposal is not `NotNeeded` (D-295: no proposal, nothing to explain), it stores the template advisory with `refinement_status = Pending` and cascades
   `RefineNarrative(AdvisoryId)` through the outbox in the same transaction. `RefineNarrativeHandler` has **no
   DbContext dependency**, so Wolverine opens no transaction: it reads the facts with Dapper (own schema, `tenant_id`
   filter, pending rows only), calls the narrator with a timeout, validates, and cascades
   `StoreRefinedNarrative(AdvisoryId, Outcome, AiContext?, ModelId?)`. `StoreRefinedNarrativeHandler` writes with EF in a
   short transaction. An architecture test pins the missing DbContext.
2. **One queue, one call at a time.** `RefineNarrative` goes to the durable local queue `advisory-refinement` with
   `MaximumParallelMessages(1)`. A local model on CPU answers one request at a time anyway; a dedicated queue keeps
   slow model calls from holding up evaluations and template advisories on the other queues, and serializing them
   avoids piling requests on the model.
3. **The template is never replaced (D-293).** `narrative` and `narrator = Template` never change.
   `StoreRefinedNarrative` applies only while the advisory is `Pending`: accepted sets `ai_context` (text, nullable,
   migration `AddAiContext`), `model_id` and status `Accepted`; otherwise only status `Rejected` and
   `refinement_reason`. A redelivered message changes nothing.
4. **Failure policy (D-282).** A timeout (`Advisory:Narrator:TimeoutSeconds`, default 90), any exception from the model
   call, or a validation failure becomes `Rejected` with a reason code: `Timeout`, `ModelError`, `Digits`, `Symbols`,
   `StatusWording`, `Format`, `Length`. These never reach the error queue and are not retried: the text is cosmetic
   and the template is already a complete answer. Host shutdown still cancels the call, and failures outside the model
   call (the database read, for example) follow the normal error policy.
5. **Validation (D-293, replaces D-283).** `AiContextValidator` runs after whitespace normalization (never a word) and
   checks, in order: no digit; no `%`, currency symbol, or currency or percent word ("percent", "dollars", "USD");
   no status wording (breach, breached, exceeds, exceeded, above the limit, warning, near the limit, compliant, within
   the limit, limit used); plain text (no `<`, `>`, backtick, `#`, `*`, `|`, URLs); exactly one paragraph of 150 to
   700 characters. Errors are fixed messages. The model writes no disclaimer: the template's stays the last line of
   the narrative. The live rewrites that passed the first design are kept as regression tests.
6. **Prompt v2 (D-284, D-293).** Fixed English instructions in code (`NarratorPrompt.Instructions`, version
   `2026-11-27.2`, pinned by a hash test; no digits, no few-shot examples): one paragraph on the reasoning behind the
   proposal (why concentration close to a maximum allocation matters, what moving toward the target achieves), class
   names allowed, no numbers, no status words, no buy or sell timing or advice, plain text. The input is JSON of the
   class names, the classes the proposal reduces and those that receive money, and the target in words: no amounts,
   no ratios, no statuses, no ids, no tenant data, no free text (D-250). One revision only; no further tuning before
   Phase 5.
7. **Agent (D-285).** `AgentFrameworkNarrator` builds one `ChatClientAgent` (Microsoft.Agents.AI 1.28.0) over the
   injected `IChatClient`, with instructions and a low temperature only: no tools, no memory, no MCP. Agent Framework,
   `Microsoft.Extensions.AI` and Ollama types live only in `FinGuard.Advisory.Infrastructure` (architecture tests).
8. **Configuration (D-286).** The AppHost adds an Ollama container with a data volume and the model `narrator`
   (`Parameters:narrator-model`, default `llama3.2:3b`), referenced by the Api **without `WaitFor`**.
   It is **off by default** (D-293): `FinGuard:Ai:Enabled=true` (`dev-session.ps1 -StartAspire -WithAi`) opts in;
   CI and the E2E runs stay on the default. `OLLAMA_KEEP_ALIVE=2m` unloads the model (about 3 GB of RAM) after two
   idle minutes. Advisory registers the Ollama
   chat client (`CommunityToolkit.Aspire.OllamaSharp` 13.6.1, R-51) and the narrator only when the `narrator`
   connection string exists, with its health check off so the Api stays healthy while the model downloads or is
   stopped. Its `HttpClient` (`narrator_httpClient`) runs without the standard resilience handler that ServiceDefaults
   adds to every client (10 s per attempt, retries): the narrator's timeout governs the call and there is no retry
   (D-292). Without the connection string nothing is requested and every advisory stays `NotRequested`.
9. **Telemetry (D-287).** The chat client's OpenTelemetry wrapper runs with sensitive data off: traces keep the model,
   timings and token counts, never prompts or completions (they contain amounts). The meter `FinGuard.Advisory`
   counts `finguard.advisory.refinements` by `outcome` (`accepted`, `timeout`, `model_error`, `rejected_<rule>`). Logs
   carry the advisory id and the outcome only.
10. **API and UI (D-288, D-293).** `GET .../advisory` adds `aiContext`, `model` and `refinement` (`NotRequested`,
    `Pending`, `Accepted`, `Rejected`). The panel always shows the template paragraphs and "Written from a template.";
    with an accepted `aiContext` it adds a "Context" subsection labelled "Written by an AI model ({model}). It contains
    no figures or statuses; all figures above come from the evaluation." While `Pending` it shows "Adding AI context…"
    and reloads with the shared retry delays, then stops silently. `Rejected` and `NotRequested` show no subsection.
    API and UI fail closed: a narrator other than `Template` gets no context.

## Consequences
- The page never waits for the model, and a model that is slow, stopped or wrong leaves the template alone.
- Every figure and status a reader sees comes from the template. The model cannot state one without being rejected;
  generic or off-tone wording still passes (owner check in `docs/testing/manual-smoke.md`, R-52).
- The model's paragraph carries no amounts; its envelope sits in Wolverine's tables like other payloads (R-35).
- A small local model may be slow or rejected often (R-50); production uses Azure AI Foundry in Phase 5, behind the
  same port, prompt and validator, and acceptance is measured again there (R-52).
- Old advisories are not regenerated. Local rows written by the first design (`narrator = Model`) were deleted from
  the owner's development database; the API and UI ignore any narrator other than `Template`.

## Alternatives considered
- Calling the model inside the template handler: holds the transaction and a connection for seconds.
- Writing the outcome with Dapper: commands write with EF (ADR-0007).
- A full model rewrite checked for numbers, status phrase, format and length (the first design): rejected after live
  runs showed wrong per-class status claims that passed every rule.
- A per-class status rule on top of the rewrite: wording varies too much to match reliably; taking figures and
  statuses out of the model's job removes the risk instead.
- Retrying the model: cost and latency for a cosmetic step.
- Asking the model to check itself, or trusting its wording: replaced by deterministic validation.
- Calling `IChatClient` directly: ADR-0006 chose Agent Framework; the adapter is one class either way.
