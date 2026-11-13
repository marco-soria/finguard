---
paths:
  - "tests/**"
  - "**/*.spec.ts"
---

# Testing rules

- Pyramid: domain unit tests (fast, no I/O) > integration tests (real PostgreSQL via Testcontainers) > architecture
  tests > a few end-to-end flows.
- Name tests `Method_Scenario_ExpectedResult`. Arrange-Act-Assert. One behaviour per test.
- Every slice has at least one integration test through its HTTP endpoint, including a tenant-isolation case
  (tenant A cannot read or affect tenant B).
- Never mock the DbContext. Use PostgreSQL in a container.
- Every Dapper query has an integration test against real PostgreSQL (Testcontainers), including a
  tenant-isolation case.
- Tests never call a real model. Use the deterministic `TemplateNarrator` or a fake `IChatClient`.
  Include a prompt-safety test proving no free-text CSV field reaches a prompt.
- Architecture tests in `tests/FinGuard.ArchitectureTests` encode the module boundaries and must stay green.
- Compliance boundary tables are mandatory (see `compliance-domain.md`).
- Frontend: test component logic and services; do not snapshot-test.
- A bug fix starts with a failing test that reproduces it.
