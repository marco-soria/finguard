# ADR-0006: Microsoft Agent Framework behind a narrator port

- Status: Accepted (supersedes the earlier choice of Semantic Kernel, same day)
- Date: 2026-11-13

## Context
The first draft of this ADR kept Semantic Kernel because it was a stated goal of the portfolio, and flagged the risk that
Microsoft presents Microsoft Agent Framework (1.0 GA on 2026-04-03) as the successor to Semantic Kernel and AutoGen,
built on `Microsoft.Extensions.AI`. The project owner then decided to migrate to Agent Framework. Sources:
- https://devblogs.microsoft.com/agent-framework/migrate-your-semantic-kernel-and-autogen-projects-to-microsoft-agent-framework-release-candidate/
- https://visualstudiomagazine.com/articles/2026/04/06/microsoft-ships-production-ready-agent-framework-1-0-for-net-and-python.aspx

## Decision
Use Microsoft Agent Framework 1.x (`Microsoft.Agents.AI`) on top of `Microsoft.Extensions.AI` (`IChatClient`) for the
advisory narrative. Keep the `IAdvisoryNarrator` port in the Advisory module so the product never depends on a model:
- `AgentFrameworkNarrator` builds one agent over an `IChatClient`, with fixed instructions and **no tools**
  (no function calling, no MCP, no retrieval). The agent receives a JSON DTO of numbers and returns English text.
- `TemplateNarrator` is the default for tests, CI and fallback (ADR-0002).
- No Agent Framework type appears outside `Advisory/Infrastructure`.
- Only GA (stable) `1.x` packages go into `Directory.Packages.props`. Packages marked preview, alpha or rc are not used.
- Local development (Phases 0 to 4) uses any local or free `IChatClient` endpoint; Phase 5 uses Azure AI Foundry with
  managed identity. Provider package choice is verified at the start of Phase 4.

## Consequences
- The stack matches Microsoft's forward path for .NET AI work and shares abstractions with `Microsoft.Extensions.AI`.
- The framework ships frequent 1.x minor releases and many satellite packages are still preview, so pin versions and read
  the changelog on each upgrade. Sample code on the internet mixes Semantic Kernel and Agent Framework APIs; check any
  snippet against the current documentation before using it.
- A single-shot narrative does not need multi-agent features. If it ever grows (tools, memory, workflows), that is a new ADR.
- Replacing the library later, or using a plain `IChatClient`, remains a one-adapter change.

## Alternatives considered
- Semantic Kernel: the first choice of this ADR; superseded because it is positioned as the predecessor.
- Direct `IChatClient` without Agent Framework: smallest dependency set, not chosen because the portfolio targets
  Agent Framework.
