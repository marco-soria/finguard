# ADR-0005: Local-first delivery, cloud-ready code

- Status: Accepted
- Date: 2026-11-13

## Context
FinGuard is a portfolio project, not a commercial SaaS. Always-on Azure resources (especially PostgreSQL Flexible
Server and a container registry) would cost money for months of development.

## Decision
Phases 0 to 4 run 100% locally with Aspire. All Azure infrastructure moves to Phase 5 and is created from
infrastructure as code so it can be deployed for demos and torn down.

The code is cloud-ready from Phase 0 through these seams:
- **Configuration:** only `IConfiguration`. Key Vault is added as a configuration source when `KeyVault:Uri` is set,
  using `DefaultAzureCredential`. No secrets in the repo; locally use user-secrets.
- **Telemetry:** OpenTelemetry in ServiceDefaults. Locally it exports to the Aspire dashboard (OTLP). The Azure Monitor
  exporter activates when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present.
- **Identity:** JWT bearer with configurable authority. Local mode uses a development-only token issuer; Phase 5 swaps
  in Microsoft Entra External ID. Both resolve the tenant through `ITenantContext` (ADR-0004).
- **AI:** `IAdvisoryNarrator` port with `TemplateNarrator` (default, tests, CI) and an Agent Framework adapter. Local
  development uses a local or free model endpoint; Phase 5 points it to Azure AI Foundry.
- **Tenant isolation** is active from day one.

Phase 5 cost controls: deploy on demand and tear down, scale Container Apps to zero, stop the PostgreSQL server
when idle, set a budget alert, and compare GitHub Container Registry with Azure Container Registry on price before
choosing.

## Consequences
- Near-zero cost while building; Azure-specific problems (identity, secrets, containers) surface late.
  Mitigation: keep the seams above tested and run the Docker images locally during Phase 0 to 4.
- The final deployment is a demonstration, not a permanent environment.

## Alternatives considered
- Deploy to Azure from Phase 1: rejected, recurring cost.
- Never deploy: rejected, deployment is part of the portfolio value.
