# ADR-0014: Advisory facts, template narrator and narrative validation

- Status: Accepted
- Date: 2026-11-26

## Context
Phase 4 explains each evaluation in words. ADR-0002 says the engine decides and AI only explains: no model computes an
amount or a status, and no free text from the CSV reaches a prompt. ADR-0006 puts every narrator behind the
`IAdvisoryNarrator` port. Brief #19 adds a local model; before that, Advisory needs a narrative that is correct by
construction, a data contract the model will receive, and a check that a narrative says only what the facts say.
`EvaluationCompleted` had no route until now (D-199, R-40), and it lacked the policy ratios the text needs.

## Decision
1. **Event.** `EvaluationCompleted` gains `AlertThreshold` and `RebalanceTargetRatio` from the evaluation's policy
   snapshot, appended last (additive, ADR-0011; no stored messages existed because the event had no route). An
   architecture test proves that the event and every type it contains have no `string` member. `ClassEvaluation`
   gains `Share`, the stored unrounded `book value / total` (D-269), so Advisory never recomputes a value that
   Compliance owns.
2. **Facts.** `AdvisoryFactsBuilder.From(event)` builds `AdvisoryFacts`: cut-off date, overall status, policy version,
   proposal status, the two ratios, and per class the raw decimals plus display texts (`BookValueText`, `ShareText`,
   `LimitText`, `UtilizationText`, `ProposedText`, `ChangeText`). Every value comes from the event, including the
   share. Facts are formatted once; narrators copy texts and never format numbers.
3. **Formatting parity.** `AdvisoryFormat` mirrors the SPA: money `$1,562,500.00` (`UsdPipe`), share and limit
   `25.00 %` (`formatPercent`), utilization with 2 to 8 decimals that never contradicts the status (`formatRatio`,
   D-207). Invariant culture; midpoints round up like JavaScript's `toFixed`. Both test suites use the same vectors.
   C# formats `decimal`, the SPA formats doubles: an exact midpoint could differ in the last digit (R-48).
4. **Template.** `TemplateNarrator` writes fixed English sentences: a headline by overall status, one sentence per
   class in alert (Breached, then Warning by utilization, D-235), the proposal (reducers, receivers by amount, Full or
   Partial ending; nothing for `NotNeeded`), and a closing disclaimer. Receivers are described as moving toward the
   target ratio, never as ending at or below it: a receiver can end a leftover cent above its target (ADR-0013), and
   the Full sentence ("every asset class is within its limit") carries the guarantee, backed by verification. Counts and grammar use words, so every digit in
   the text comes from the facts. Paragraphs are plain text separated by one blank line; never Markdown or HTML.
5. **Validation.** `NarrativeValidator.Validate(text, facts)` extracts numeric tokens with one regex (digits with
   optional thousands separators and decimals) and requires each to appear among the tokens of the facts' texts. It
   returns `Advisory.Narrative.UnknownNumber`, which never contains the offending number. Every template output is
   validated before it is stored; a failure is a bug and the message goes to the error queue.
6. **Pipeline and storage.** One handler: `EvaluationCompleted`, facts, template, validation, store, in one
   transaction and idempotent by `EventId`. `advisory.advisories` holds `facts jsonb`, `narrative`, `narrator`
   (`Template`) and `generated_at`, with unique `evaluation_id`, `submission_id` and `source_event_id`. Plain EF
   entity, no aggregate (ADR-0007). Advisory publishes no event until Audit exists (D-259).
7. **Read.** `GET /api/advisory/submissions/{id}/advisory` returns `{ evaluationId, submissionId, narrator,
   generatedAt, paragraphs }` (Dapper, `tenant_id` filter); unknown, foreign and not-yet-written all answer 404
   `Advisory.Advisory.NotFound`. Facts are not returned. The SPA shows the paragraphs by interpolation only, with the
   same 404 retry loop as the evaluation.
8. **No backfill (R-40 closed).** Evaluations stored before Advisory subscribed have no advisory; the page shows the
   timeout state. They are local development data, and Phase 5 starts with an empty database.

## Path to Brief #19
A later, optional step asks the model through an `AgentFrameworkNarrator` with the same facts, validates the output
with `NarrativeValidator`, and replaces `narrative` and `narrator` only when it passes. The template text is always
stored first, so the page never waits for the model. Wolverine's Eager EF transaction opens before a handler runs, so
a slow model call must not run inside it; #19 decides how.

## Consequences
- The narrative and the evaluation table use the same display rules; a change to one formatter changes both test suites.
- A model in #19 cannot introduce a number that is not in the facts without being rejected.
- Every sentence is true for every proposal: in 9 of 10,000 generated portfolios a receiver ends 0.01 USD above its
  exact target, which "moves toward" allows (R-49, closed by the wording).
