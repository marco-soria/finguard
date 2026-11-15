# ADR-0009: Module messaging conventions (outbox, retries, tenant propagation, events)

- Status: Accepted
- Date: 2026-11-15

## Context
ADR-0003 chose Wolverine with a PostgreSQL transactional outbox and left one risk open (R-02): one DbContext per module
with a single Wolverine envelope store. ADR-0004 requires every handler to run with the right tenant, and R-16 asked
whether the HTTP request's tenant reaches Wolverine handlers, which run in their own scope. Brief #6 proved both with
two probe DbContexts and permanent regression tests (`tests/FinGuard.Api.IntegrationTests/Messaging/`).

## Decision
- **Envelope storage.** One main Wolverine store in schema `wolverine`
  (`PersistMessagesWithPostgresqlUsingDataSourceFromContainer`). Every module DbContext shares the database, so Wolverine
  writes outbox rows on the DbContext's connection and transaction. No `MapWolverineEnvelopeStorage` in module models
  and no ancillary stores: envelope tables never appear in module migrations.
- **Transactions.** `UseEntityFrameworkCoreTransactions()` in Eager mode (default) with
  `Policies.AutoApplyTransactions()`. A handler's changes and its cascaded messages commit together or not at all.
- **Retries.** Eager mode refuses EF's `EnableRetryOnFailure`, so every module disables Aspire's EF retries
  (`DisableRetry = true` in `EnrichNpgsqlDbContext`). A Wolverine error policy retries transient `NpgsqlException`s
  around the whole handler (cooldowns 50 ms, 250 ms, 1 s), then moves the message to the dead-letter table.
- **Local delivery.** `Policies.UseDurableLocalQueues()`: cascaded events are stored in the inbox and survive a crash
  between commit and handling.
- **Tenant propagation.** The tenant travels as Wolverine's envelope tenant id. Endpoints send commands only with
  `bus.InvokeForCurrentTenantAsync(tenant, command, ct)` (BuildingBlocks); cascaded messages inherit the tenant id.
  `TenantPropagationMiddleware` (BuildingBlocks, applied to every handler) sets the scoped `TenantContext` from it.
  No tenant id: the context stays empty, so filters match nothing and the stamping interceptor refuses writes (fail
  closed). A non-GUID tenant id, or a message whose `TenantId` property differs from the envelope tenant, throws.
- **No service location.** Wolverine 6 forbids service location by default. Registrations are shaped so generated code
  builds everything itself: `ITenantContext` is registered as `AddScoped<ITenantContext, TenantContext>()` (the
  concrete `TenantContext` forwards to it), module `DbContextOptions` are singletons, and `TenantStampingInterceptor`
  is a stateless singleton that reads the tenant from the `TenantAwareDbContext` it intercepts. Generated handler code
  then creates one `TenantContext` per message and passes that same instance to the middleware, the handler and every
  DbContext. No `AlwaysUseServiceLocationFor` is needed.
- **Integration events.** `sealed record`s in `FinGuard.<Module>.Contracts.Events` with `Guid EventId`,
  `Guid TenantId` and `DateTimeOffset OccurredAt`. Correlation comes from Wolverine and OpenTelemetry, not the payload.
  The payload tenant must equal the envelope tenant.
- **Idempotent consumers.** Delivery is at least once. A consumer stores the source `EventId` with a unique index and
  ignores duplicates.
- **One DbContext per handler.** Cross-module effects go only through events. Wolverine refuses to guess between two
  DbContexts anyway.

## Consequences
- R-02 and R-16 are closed by tests: atomic outbox across two DbContexts, retry, idempotency, tenant through HTTP →
  command → cascaded event → consumer, isolation between tenants, fail-closed without a tenant.
- Architecture tests enforce the event shape, one DbContext per handler, and no direct `InvokeAsync` /
  `InvokeForTenantAsync` in module code.
- Dapper queries run outside Wolverine transactions and get no automatic retry (R-20).
- Wolverine logs the failed message with its `ToString()` when an invocation fails, and records print every
  property (R-21). Messages must not carry amounts or free text until this is resolved.
- `DurabilityMode.Solo` is Development-only; multi-replica environments (Phase 5) need `Balanced`.
- New modules must follow the registration pattern (singleton options, `TenantStampingInterceptor.Instance`,
  `DisableRetry`), or Wolverine code generation fails at the first handler.

## Alternatives considered
- Ancillary envelope store per DbContext (`MessageStoreRole.Ancillary` + `Enroll<T>()`): not needed, because all
  contexts share one database. It would put envelope tables into every module schema.
- Lightweight transaction mode per handler, keeping Aspire's EF retries: rejected. EF retries would retry
  `SaveChanges` without re-running the handler, and the Eager transaction is the clearer atomicity boundary.
- Explicit `TenantId` on every message plus middleware matching a marker interface: rejected. The envelope already
  carries the tenant and cascades it, and commands stay free of tenant fields that callers could forge.
- `AlwaysUseServiceLocationFor` for `ITenantContext` and every DbContext: rejected. The list would grow with every
  module, and the restructured registrations need none.
- Wolverine's conjoined or database-per-tenant multi-tenancy: out of scope; the explicit filter stays (ADR-0004).
