---
paths:
  - "src/backend/**/*.cs"
---

# Backend rules (VSA modular monolith)

- One folder per use case: `Features/<UseCase>/` with endpoint, command or query record, Wolverine handler,
  validator and response DTO. A slice owns its whole path down to the database.
- Endpoints are Minimal APIs and contain no logic: bind the request, call
  `bus.InvokeAsync<Result<T>>(command, ct)`, map the result with `ToHttpResult()`.
- Handlers are plain classes with a `Handle` method (Wolverine convention) and return `Result<T>`.
- Expected failures (validation, business rule, not found, conflict) are `Error` values from BuildingBlocks.
  Throw only for unexpected faults. Never use exceptions for control flow.
- The global `IExceptionHandler` maps unexpected exceptions to RFC 9457 ProblemDetails with a correlation id.
  Never leak stack traces, SQL or CSV content.
- Reference another module only through its `*.Contracts` project. Communicate through integration events
  published via the Wolverine outbox. No cross-schema joins, foreign keys or shared DbContext.
- Each module owns one DbContext, a schema named after the module (lowercase) and its own migrations history table.
- Tenant-owned entities implement `ITenantOwned`: global query filter plus a SaveChanges interceptor that sets
  `tenant_id`. The tenant always comes from `ITenantContext`, never from the request.
- Integration events are immutable records carrying `TenantId`, `CorrelationId`, `OccurredAt`. Change one by adding a
  new versioned type (`...V2`), never by editing the old one.
- Use `TimeProvider`, `CancellationToken` everywhere, nullable enabled, warnings as errors.
- Logging: message templates, never string interpolation; log ids and counts, never monetary values or CSV rows.
- Do not add generic repositories or MediatR. Slices use their DbContext directly; aggregates with repositories
  exist only in Compliance.
