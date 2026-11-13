# ADR-0007: EF Core for commands, Dapper for queries

- Status: Accepted
- Date: 2026-11-13

## Context
FinGuard uses CQRS with Wolverine. Writes need change tracking, aggregates (Compliance), migrations and the
transactional outbox. Reads are mostly projections for the UI (submission history, evaluation results, advice) where
hand-written SQL is simpler and faster than LINQ over the write model.

## Decision
- **Commands** use EF Core through the module's own DbContext. Migrations, the outbox integration and tenant stamping
  stay in EF Core. EF Core remains the only owner of the schema.
- **Queries** use Dapper over an `NpgsqlDataSource` (registered by the Aspire Npgsql integration), returning read DTOs
  directly. Queries never load aggregates and never write.
- A query reads only its own module's schema. Cross-module data comes from that module's Contracts, never from SQL joins.
- **Tenant isolation** does not come for free: EF global query filters do not apply to Dapper. Every query that touches
  tenant-owned tables must include `tenant_id = @TenantId`, with the value taken from `ITenantContext`. A small
  `ITenantQueryContext` helper exposes the open connection plus the tenant id, so handlers never build the filter from
  request data.
- SQL lives next to the slice (a `const string` in the query handler). Always parameterized; no string concatenation.
- Column names are snake_case; DTO mapping uses explicit aliases (`AS "PropertyName"`) or Dapper's
  `MatchNamesWithUnderscores`, chosen once in PR #2.
- Simple reads inside a command handler (for example, loading an aggregate) stay in EF Core.

## Consequences
- Clear separation of read and write paths, good for the portfolio story.
- Two data-access libraries to learn and keep consistent.
- Schema changes can break SQL silently. Mitigation: every Dapper query has an integration test against real PostgreSQL
  (Testcontainers), including a tenant-isolation case.
- A missed `tenant_id` filter would leak data. Mitigations: the helper above, the mandatory isolation test, and an
  optional PostgreSQL Row-Level Security policy evaluated in Phase 5 as defense in depth.

## Alternatives considered
- EF Core for everything (`AsNoTracking` projections): less code, but the owner wants the explicit read model.
- Dapper for everything: loses migrations, change tracking and Wolverine's EF Core outbox integration.
