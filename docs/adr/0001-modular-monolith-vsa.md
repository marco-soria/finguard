# ADR-0001: Modular monolith with vertical slices

- Status: Accepted
- Date: 2026-11-13

## Context
FinGuard has four cohesive areas (tenancy, portfolio ingestion, compliance evaluation, advisory) built and operated by
one developer, deployed as one unit to Azure Container Apps. It must show good boundaries without the operational
cost of distributed systems.

## Decision
Build a modular monolith. Each module is organized as Vertical Slice Architecture (`Features/<UseCase>/`). Compliance,
the only module with real domain complexity, adds a Clean Architecture `Domain/` with a rich DDD model and the
Specification pattern. Modules are separate projects with a public `*.Contracts` project; one PostgreSQL database with
one schema and one DbContext per module. Boundaries are enforced by architecture tests.

## Consequences
- One deployable, simple local run with Aspire, in-process calls where safe.
- Boundaries stay real because of the Contracts projects and tests, so a module could be extracted later.
- Simple modules (Tenancy, Portfolio, Advisory) stay light; only Compliance pays for DDD.
- Risk: slices drift into shared code. Mitigation: BuildingBlocks contains no business rules.

## Alternatives considered
- Microservices: rejected, operational cost with no benefit at this scale.
- Layered monolith: rejected, features scatter across layers and boundaries erode.
- Clean Architecture everywhere: rejected, ceremony without domain value outside Compliance.
