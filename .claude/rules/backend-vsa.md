---
paths:
  - "src/backend/**/*.cs"
---

# Backend rules (VSA modular monolith)

- One folder per use case: `Features/<UseCase>/` with endpoint, command or query record, Wolverine handler,
  validator and response DTO. A slice owns its whole path down to the database.
- Endpoints are Minimal APIs and contain no logic: bind the request, call
  `bus.InvokeForCurrentTenantAsync<Result<T>>(tenant, command, ct)`, map the result with `ToHttpResult()`. Never call
  `InvokeAsync` or `InvokeForTenantAsync` directly (architecture test).
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
- Module DbContexts derive from `TenantAwareDbContext` (named filter `"Tenant"` + `TenantStampingInterceptor`),
  except Tenancy's, whose tables are global. `IgnoreQueryFilters` is banned (RS0030).
- Module endpoint groups call `.RequireAuthorization(AuthorizationPolicies.TenantMember)`. Every other endpoint needs
  an authenticated user (fallback policy); only health checks and `/dev/token` allow anonymous callers.
- Upload endpoints (`IFormFile`) call `.DisableAntiforgery()` only because authentication is JWT bearer, no cookies
  (D-92). They set a per-endpoint body limit (`RequestSizeLimitAttribute` metadata) and a matching `WithFormOptions`.
  Never log file names, file contents or file hashes; log the submission id and the size only (D-91).
- Never call `TenantContext.SetTenant` outside the Tenancy module and the BuildingBlocks `TenantPropagationMiddleware`;
  everything else reads `ITenantContext`. Handlers get the tenant from the envelope (ADR-0009).
- One DbContext per handler. Cross-module effects go only through integration events.
- Integration events (ADR-0009) are `sealed record`s in `FinGuard.<Module>.Contracts.Events` carrying `EventId`,
  `TenantId`, `OccurredAt`. Change one by adding a new versioned type (`...V2`), never by editing the old one.
- Consumers are idempotent by `EventId`: store it with a unique index and ignore duplicates.
- Module DbContext registration: singleton options (`optionsLifetime: ServiceLifetime.Singleton`),
  `TenantStampingInterceptor.Instance`, and `DisableRetry = true` in `EnrichNpgsqlDbContext`. Otherwise Wolverine code
  generation or Eager transactions fail.
- Module DbContexts are `sealed`. A module whose command handlers inject it makes it `public` (Wolverine's generated
  code needs public types, D-94); other modules still cannot reach it (they reference only Contracts). Its entities
  stay `internal` and are reached through `Set<T>()`.
- Wolverine resolves `ITenantContext` from the message scope (`AlwaysUseServiceLocationFor`, D-95). Never remove it:
  module DbContexts are service-located, and an inlined tenant context would leave them without a tenant.
- Every message type (handled message, integration event) overrides `ToString()` to return its type name and
  identifiers only, because Wolverine logs failed messages with `ToString()` (D-77, architecture test).
- Use `TimeProvider`, `CancellationToken` everywhere, nullable enabled, warnings as errors.
- Logging: message templates, never string interpolation; log ids and counts, never monetary values or CSV rows.
- Do not add generic repositories or MediatR. Aggregates with repositories exist only in Compliance.
- Persistence (ADR-0007): command handlers write with EF Core through the module DbContext. Query handlers read with
  Dapper over `NpgsqlDataSource` and return read DTOs; they never load aggregates and never write.
- Query SQL is a parameterized `const string` in the query handler. No string concatenation.
- Every query on tenant-owned tables filters `tenant_id = @TenantId`, with the value from `ITenantContext`
  (EF global query filters do not apply to Dapper).
- A query reads only its own module schema. No cross-schema SQL; cross-module data comes from Contracts.
- Read queries never select `portfolio.submission_contents` (file bytes) or `portfolio.positions` unless the slice is
  about positions; they return metadata, counts and stored errors. List queries have a named cap constant and a
  deterministic order (a unique tiebreaker such as `id`) (D-100).
- Upload validation has two layers (D-112). Transport problems (no file, empty, over 5 MB, not `.csv`, bad or future
  cut-off date) return 400/413 and store nothing. Content problems store a `Rejected` submission with its errors and
  answer 201; a valid file stores an `Accepted` submission with its positions and answers 201.
- Validation messages are fixed sentences per code and never echo a cell value or a column name from the file (D-115).
- Npgsql hands `timestamptz` to Dapper as a UTC `DateTime`; a positional record with a `DateTimeOffset` parameter fails
  to materialize. Read into a private row record and map to the response DTO.

## Slice template (D-81)
Copy `src/backend/Modules/Tenancy/FinGuard.Tenancy/Features/GetCurrentTenant/` for every new slice:
- `<UseCase>.cs`: public sealed record command or query with a `ToString()` override (identifiers only).
- `<Response>.cs`: public sealed record read DTO.
- `<UseCase>Handler.cs`: public Wolverine handler returning `Result<T>`; queries use Dapper over `NpgsqlDataSource`
  with a `const` SQL string and the tenant from `ITenantContext`; commands use the module DbContext, add entities and
  never call `SaveChangesAsync` (Wolverine's EF transaction middleware saves and commits).
- `<UseCase>Endpoint.cs`: internal `Map<UseCase>(this RouteGroupBuilder)` extension, called from the module's
  `Map<Module>Endpoints`; it sends with `InvokeForCurrentTenantAsync<Result<T>>` and maps with `ToHttpResult`.
- Wolverine discovers only public handler and message types.
