# ADR-0002: Deterministic compliance engine, AI only explains

- Status: Accepted
- Date: 2026-11-13

## Context
The product tells an entity whether its portfolio complies with limits and how to rebalance. Wrong amounts or a wrong
status are product failures. LLMs are non-deterministic and can be manipulated by text inside inputs.

## Decision
Compliance owns a deterministic evaluation and rebalance engine (pure domain code, fully unit tested). The Advisory
module receives the finished result and asks an LLM only to explain it in English. The LLM:
- never computes amounts and never decides status;
- receives only structured numbers (class names, percentages, amounts, status), never free-text CSV fields;
- has its output validated: every number in the narrative must match the supplied data, otherwise the narrative is
  discarded.
If the model fails or its output is rejected, the deterministic proposal is still shown and a template narrative is used.

## Consequences
- The product works without any AI; AI is an enhancement with graceful degradation.
- Reproducible results; tests do not depend on a model.
- Prompt-injection surface is small because user text never reaches the prompt.
- Cost: extra output validation code and a `TemplateNarrator`.

## Alternatives considered
- LLM computes the rebalance: rejected, accuracy and auditability.
- No AI: rejected, AI advisory is part of the portfolio goal.
