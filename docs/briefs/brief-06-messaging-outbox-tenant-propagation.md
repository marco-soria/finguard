# Brief #6 — Wolverine outbox spike: two DbContexts and tenant propagation

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-06-messaging-outbox-tenant-propagation.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-06-messaging-outbox-tenant-propagation.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/messaging-outbox-and-tenant-propagation` |
| PR title (exact, becomes the squash commit) | `feat: enable transactional outbox and tenant propagation for module messaging` |
| Phase | First brief of Phase 1 (closes R-02 and R-16) |

---

## Goal
Prove, with permanent regression tests, that the messaging foundation every later slice depends on works:
1. A command handler that writes through one module DbContext and publishes an integration event commits both
   atomically (transactional outbox), or neither.
2. A second module consumes that event through its own DbContext, durably, retried on transient faults, idempotently.
3. The tenant travels with the message: HTTP request → command handler → cascaded event → consumer, and the consumer's
   EF filter and stamping interceptor see the same tenant. Without a tenant, writes fail closed.

This is a spike with production output: the Wolverine configuration, the tenant propagation middleware and the
conventions become real code; the business-shaped parts (two probe DbContexts, one command, one event) live only in the
test project. No business table, no real slice, no frontend change.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`,
  `docs/decisions.md` (newest delta first: sessions 11 and 12), ADR-0003 (Wolverine and outbox), ADR-0004
  (multi-tenancy), ADR-0007 (persistence), ADR-0008 (development identity), `docs/git-workflow.md`, `docs/ai-rules.md`.
- Current code: `Program.cs` registers Wolverine with durable storage only (`PersistMessagesWithPostgresqlUsingDataSourceFromContainer("wolverine")`,
  `UseRuntimeCompilation`, module assemblies in discovery, `DurabilityMode.Solo` in Development). Module DbContexts derive
  from `TenantAwareDbContext` and are registered with `AddDbContext((sp, o) => o.UseModuleNpgsql(...).AddInterceptors(...))`
  + `EnrichNpgsqlDbContext` (Aspire retries on by default). `TenantContext` is scoped; `ITenantContext` forwards to it.
  `WolverineFx.EntityFrameworkCore` 6.64.0 is pinned but unused. The integration-test fixture already creates a probe
  schema from a generated create script (`ProbeDbContext`, Brief #5).
- Facts verified on 2026-11-15 (re-check at execution; Wolverine moves fast, R-09):
  - EF Core transactional middleware: `opts.UseEntityFrameworkCoreTransactions()` works with DbContexts registered
    separately; `opts.Policies.AutoApplyTransactions()` wraps any handler that depends on a DbContext.
    Source: https://wolverine.netlify.app/guide/durability/efcore/ and .../efcore/transactional-middleware
  - With several DbContext dependencies Wolverine does not guess and fails at startup unless the handler designates one
    (`[Transactional(typeof(X))]`) or opts out (`[NonTransactional]`). Source: .../efcore/transactional-middleware
  - **Eager mode (default) combined with `EnableRetryOnFailure()` fails at startup.** Aspire's
    `EnrichNpgsqlDbContext` enables retries unless disabled. Source: .../efcore/transactional-middleware
  - Multiple DbContext types in one Wolverine app are supported; each can enroll an ancillary message store
    (`MessageStoreRole.Ancillary` + `.Enroll<TDbContext>()`) for modular monoliths. Source: https://wolverine.netlify.app/guide/durability/efcore/
  - Tenant id is envelope metadata: `bus.InvokeForTenantAsync(tenantId, message)`; cascaded messages are tagged with the
    same tenant id; handlers can take a `JasperFx.MultiTenancy.TenantId` parameter. No multi-database setup is implied.
    Source: https://wolverine.netlify.app/guide/handlers/multi-tenancy
  - Wolverine 6 defaults `ServiceLocationPolicy` to `NotAllowed`; the escape hatch is
    `opts.CodeGeneration.AlwaysUseServiceLocationFor<T>()`. Source: https://wolverine.netlify.app/guide/migration
- The user works on Windows. Use PowerShell 7 syntax. Docker must run for integration tests.

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Baseline to implement | Fallback if the baseline fails (report, do not improvise further) |
| --- | --- | --- |
| Envelope storage | One main store in schema `wolverine`; every module DbContext shares the database, so Wolverine writes outbox rows on the DbContext's connection and transaction. No `MapWolverineEnvelopeStorage` in module models (it would leak envelope tables into module migrations) | Ancillary store per DbContext (`MessageStoreRole.Ancillary` + `Enroll<T>()`), envelope tables in each module schema |
| Transaction mode | Eager (default). Disable Aspire's EF retries (`s.DisableRetry = true` in `EnrichNpgsqlDbContext`) and retry transient database faults with a Wolverine error policy around the whole handler | Lightweight mode per handler, keeping Aspire retries |
| Tenant propagation | Wolverine envelope tenant id: endpoints call `InvokeForTenantAsync(RequiredTenantId.ToString(), command)`; cascaded events inherit it; a Wolverine middleware in BuildingBlocks sets `TenantContext` from the `TenantId` before every handler | Explicit `TenantId` on every message + middleware matching a marker interface |
| Idempotent consumers | Natural idempotency: a consumer stores the source `EventId` with a unique index and ignores duplicates | — |
| Local delivery | `opts.Policies.UseDurableLocalQueues()` so cascaded events survive a crash between commit and handling | — |

## Environment rules
- Never run `npm`, `yarn` or `npx`. Never push, never open PRs, never change GitHub settings. Stop after local commits.
- Never invent APIs. Every Wolverine call in this brief must be checked against the 6.64 docs or XML docs before use;
  if a name differs, use the documented one and report it.
- Do not enable `UseEntityFrameworkCoreWolverineManagedMigrations` (D-30) or `MapWolverineEnvelopeStorage`.
- Do not add business tables, slices or endpoints to the modules.
- Timebox: if the baseline and the fallback of the same row both fail, stop, revert that row's changes, and report with
  the error output and the doc pages you read.

## Preflight (run first, report results, stop if something is missing)
```powershell
git switch main; git pull; git status --short
dotnet build -c Release            # zero warnings
dotnet test --no-build -c Release  # 72 tests green (Docker running)
```
Check whether a stable Wolverine 6.x newer than 6.64.0 exists; if so, report it but do **not** upgrade in this brief.

## Tasks

### 1. Branch
`git switch -c feat/messaging-outbox-and-tenant-propagation`

### 2. BuildingBlocks: messaging primitives (`src/backend/BuildingBlocks/Messaging/`)
BuildingBlocks may now reference `WolverineFx` (no business rules; the Compliance `Domain/` rule still forbids Wolverine there).
- `TenantPropagationMiddleware`: a Wolverine `Before` middleware applied to every message handler that reads the
  envelope tenant id and calls `TenantContext.SetTenant(Guid.Parse(...))`. No tenant id: leave the context empty (filters
  fail closed, stamping throws). A tenant id that is not a GUID: throw (bug). It lives in BuildingBlocks because only
  BuildingBlocks and Tenancy may depend on `TenantContext` (architecture rule from Brief #5).
- `MessageBusTenantExtensions.InvokeForCurrentTenantAsync<T>(this IMessageBus bus, ITenantContext tenant, object message, CancellationToken ct)`
  (and a non-generic overload): the only way endpoints send commands. Uses `RequiredTenantId`.
- `IntegrationEventConventions` (doc comment only, enforced by the architecture test in task 6): integration events are
  `sealed record`s in `FinGuard.<Module>.Contracts.Events` with `Guid EventId`, `Guid TenantId`, `DateTimeOffset OccurredAt`.
  The payload `TenantId` must equal the envelope tenant; the middleware throws when an incoming message exposes a
  `TenantId` property that differs (defence in depth; read it by convention, Contracts cannot reference BuildingBlocks).

### 3. Api: Wolverine configuration (`Program.cs`, or `Messaging/WolverineSetup.cs` if `Program.cs` grows)
- Keep the existing storage, runtime compilation, discovery and Solo mode.
- Add `opts.UseEntityFrameworkCoreTransactions()`, `opts.Policies.AutoApplyTransactions()`,
  `opts.Policies.UseDurableLocalQueues()`, the tenant propagation middleware for all handlers, and a transient-fault
  error policy (for example `NpgsqlException` with `IsTransient` → retry with cooldown 3 times, then dead letter).
  Check the exact policy API in the docs.
- Module registrations: `EnrichNpgsqlDbContext<X>(s => { s.DisableHealthChecks = true; s.DisableRetry = true; })`
  for all four modules (Eager mode + EF retries fail at startup).
- Service location (Wolverine 6 `NotAllowed`): if code generation refuses `ITenantContext`, a module DbContext or its
  options because they are registered with factory lambdas, first try to restructure the registration; otherwise add
  `opts.CodeGeneration.AlwaysUseServiceLocationFor<T>()` for exactly those types and list them in the report.
- A hosted extension point for tests: register Wolverine options extensions from DI (`IWolverineExtension` or the
  documented equivalent) so the test project can add its probe assembly to discovery without touching `Program.cs`.

### 4. Probe harness (test project only: `tests/FinGuard.Api.IntegrationTests/Messaging/`)
- Two probe contexts deriving from `TenantAwareDbContext`, in schemas `probe_orders` and `probe_ledger`, created from
  their generated create scripts by a messaging fixture (reuse the shared PostgreSQL container; a second
  `WebApplicationFactory` subclass registers the probes so other tests stay unaffected).
  - `ProbeOrder : ITenantOwned` (`Id`, `TenantId`, `Reference`).
  - `ProbeLedgerEntry : ITenantOwned` (`Id`, `TenantId`, `SourceEventId` with a unique index, `Reference`).
- Messages: `PlaceProbeOrder(Guid OrderId, string Reference, bool FailAfterSave)` command; `ProbeOrderPlaced`
  integration-event-shaped record (`EventId`, `TenantId`, `OccurredAt`, `OrderId`, `Reference`).
- Handlers:
  - `PlaceProbeOrderHandler` depends only on the orders context, adds the order, returns `ProbeOrderPlaced` as a
    cascading message; throws after the change when `FailAfterSave` is true.
  - `ProbeOrderPlacedHandler` depends only on the ledger context; if `SourceEventId` already exists, does nothing;
    otherwise adds a ledger entry. A static switch lets one test make it fail transiently N times.
- A test-only HTTP route (existing `IStartupFilter` pattern, `TenantMember` policy) that calls
  `InvokeForCurrentTenantAsync(new PlaceProbeOrder(...))`.
- Use Wolverine's tracked sessions (`Wolverine.Tracking`) to wait for cascaded work; never `Task.Delay`.

### 5. Spike tests (they stay as regression tests)
| Test | Proves |
| --- | --- |
| `PlaceOrder_ForTenant_WritesOrderAndLedgerEntry` | outbox + durable local delivery across two DbContexts |
| `PlaceOrder_HandlerThrowsAfterChanges_WritesNothingAndPublishesNothing` | atomicity: no order row, no outgoing or incoming envelope, no ledger entry |
| `OrderPlaced_ConsumerFailsTransientlyTwice_IsRetriedAndWritesOnce` | error policy and decoupling (order row exists before the consumer succeeds) |
| `OrderPlaced_DeliveredTwiceWithSameEventId_WritesOneLedgerEntry` | idempotent consumer (invoke the consumer twice with the same event) |
| `PlaceOrder_ViaHttpWithAliceToken_StampsAcmeOnBothRows` | HTTP → handler → cascaded event keep the tenant (closes R-16) |
| `PlaceOrder_AliceThenBob_EachLedgerSeesOnlyOwnEntries` | tenant isolation holds in handlers |
| `OrderPlaced_WithoutTenant_FailsClosedAndWritesNothing` | no envelope tenant → stamping throws → message dead-lettered or errored, nothing written |
| `OrderPlaced_PayloadTenantDiffersFromEnvelope_IsRejected` | middleware guard |
| `Handler_WithTwoDbContexts_FailsAtStartupUnlessDesignated` (optional, document only if expensive) | Wolverine's "does not guess" rule is understood |
Unit (`FinGuard.UnitTests`): `TenantPropagationMiddleware` sets the tenant from a GUID string, leaves it empty without a
tenant, throws on a non-GUID value and on a payload mismatch.

### 6. Architecture tests
| Rule | Test |
| --- | --- |
| Integration events follow the convention | every type in a `*.Contracts.Events` namespace is a sealed record with `EventId` (Guid), `TenantId` (Guid), `OccurredAt` (DateTimeOffset). Passes on an empty set today |
| Module handlers own one DbContext | no type in a module assembly whose name ends with `Handler` depends on two module DbContext types |
| Endpoints do not bypass the tenant | no type in a module assembly calls `IMessageBus.InvokeAsync` / `InvokeForTenantAsync` directly (only `InvokeForCurrentTenantAsync`); check the ArchUnitNET method-call API first and report if it cannot express it |
Prove each one fails once with a temporary change, then revert; report the messages.

### 7. Docs and rules
- `docs/adr/0009-module-messaging-conventions.md` (Accepted): envelope storage choice, Eager mode and retries,
  durable local queues, tenant propagation through the envelope, idempotent consumers, event shape, one DbContext per
  handler. Alternatives: ancillary stores, Lightweight mode, explicit tenant on messages.
- ADR-0003: add "R-02 resolved by ADR-0009" under Consequences. ADR-0004: tenant propagation now via envelope tenant id.
- `.claude/rules/backend-vsa.md` + `.cursor/rules/backend-vsa.mdc` (same commit): endpoints send commands with
  `InvokeForCurrentTenantAsync`; one DbContext per handler; integration events follow ADR-0009; consumers are idempotent
  by `EventId`; remove the Brief #5 note that handlers must not assume the tenant. Integration events carry `EventId`,
  `TenantId`, `OccurredAt` (correlation comes from Wolverine and OpenTelemetry, so `CorrelationId` leaves the payload rule).
- `docs/decisions.md`: prepend Appendix A verbatim, then your execution delta on top, including a short "spike verdict"
  table (row, baseline or fallback, evidence).

## Acceptance criteria
- [ ] Branch `feat/messaging-outbox-and-tenant-propagation`, working tree clean, commits follow the commit plan.
- [ ] `dotnet format --verify-no-changes --no-restore`, `dotnet build -c Release` (zero warnings) and
      `dotnet test --no-build -c Release` pass; report counts per project and the duration of the messaging tests.
- [ ] Every row of the decision table is reported as baseline kept or fallback used, with the evidence (test name or error).
- [ ] `aspire run` still starts `postgres`, `finguard`, `api`, `web`; the Api logs no Wolverine code-generation or
      service-location errors; the `wolverine` schema still holds the envelope tables and no module schema gained any.
- [ ] `git grep` shows no `MapWolverineEnvelopeStorage`, `UseEntityFrameworkCoreWolverineManagedMigrations`,
      `EnableRetryOnFailure` or direct `InvokeAsync` in `src/backend/Modules`.
- [ ] Each new architecture rule seen failing once; messages reported.

## Rules to respect
`AGENTS.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`, ADR-0003, ADR-0004, ADR-0007, ADR-0008, the new
ADR-0009, `docs/git-workflow.md`, `docs/ai-rules.md`. English everywhere. Never log message payloads, tokens or amounts.

## Out of scope
Business tables, real slices and endpoints (Brief #7 onwards); the Angular login (Brief #7); a message broker; sagas;
scheduled messages; Wolverine.HTTP; Wolverine's database-per-tenant features; upgrading Wolverine; production migration
strategy (Phase 5).

## Commit plan (Conventional Commits, no push)
1. `feat: add tenant propagation middleware and tenant-aware bus extension`
2. `feat: enable ef core transactional outbox and durable local queues`
3. `test: add outbox and tenant propagation spike tests with probe modules`
4. `test: add messaging architecture rules`
5. `docs: add adr-0009 and update messaging rules and decisions log`

## Report back (final message)
- Plan versus what you did; every deviation with the reason and the doc you followed.
- The spike verdict table, the types that needed `AlwaysUseServiceLocationFor` (if any) and why.
- Output of the acceptance checks and the architecture-rule failure messages.
- Your execution delta (also written into `docs/decisions.md`).
- The exact commands for the owner, not run by you:
  ```powershell
  git push -u origin feat/messaging-outbox-and-tenant-propagation
  gh pr create --base main --head feat/messaging-outbox-and-tenant-propagation `
    --title "feat: enable transactional outbox and tenant propagation for module messaging" `
    --body-file .github/pull_request_template.md
  ```

---

## Appendix A — Decisions delta (planning session 13), paste verbatim into `docs/decisions.md`

## Delta 2026-11-15 (session 13: Brief #6 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-65 | Brief #6 is a spike with production output: Wolverine config, tenant middleware and conventions are real; the two DbContexts, the command and the event are test-only probes that stay as regression tests | No business tables before Brief #8 |
| D-66 | Baseline envelope storage: one main store in schema `wolverine`, shared by all module DbContexts through their connection and transaction; no `MapWolverineEnvelopeStorage` in module models | Fallback: ancillary store per DbContext |
| D-67 | Eager transactional middleware with `AutoApplyTransactions`; EF/Aspire retries disabled for module DbContexts; transient database faults retried by a Wolverine error policy around the whole handler | Eager + `EnableRetryOnFailure` fails at startup |
| D-68 | Tenant propagation through Wolverine's envelope tenant id: endpoints use `InvokeForCurrentTenantAsync`; cascaded messages inherit the tenant; a BuildingBlocks middleware sets `TenantContext` before every handler; no tenant means fail closed | Fallback: explicit `TenantId` on every message |
| D-69 | Integration events: sealed records in `FinGuard.<Module>.Contracts.Events` with `EventId`, `TenantId`, `OccurredAt`; payload tenant must match the envelope; consumers are idempotent by `EventId` (unique index) | `CorrelationId` leaves the payload; Wolverine and OpenTelemetry carry correlation |
| D-70 | One DbContext per handler; cross-module effects only through events | Wolverine refuses to guess between two DbContexts |
| D-71 | Durable local queues for all local messages | Events survive a crash between commit and handling |
| D-72 | BuildingBlocks may reference `WolverineFx` for messaging primitives | Compliance `Domain/` still cannot |

### Verified on 2026-11-15
| Area | Finding | Source |
| --- | --- | --- |
| Wolverine EF Core | `UseEntityFrameworkCoreTransactions()` for separately registered DbContexts; multiple DbContexts supported; ancillary store per DbContext available | https://wolverine.netlify.app/guide/durability/efcore/ |
| Wolverine EF Core | Several DbContexts in one handler fail at startup unless designated; `AutoApplyTransactions`; Eager vs Lightweight; Eager + `EnableRetryOnFailure` fails at startup | https://wolverine.netlify.app/guide/durability/efcore/transactional-middleware |
| Wolverine tenancy | `InvokeForTenantAsync`; cascaded messages keep the tenant id; `TenantId` injectable in handlers | https://wolverine.netlify.app/guide/handlers/multi-tenancy |

### Risks and flags
- **R-02 and R-16** are expected to close with this brief; the execution delta confirms or reopens them.
- **R-19 Wolverine service location.** Factory-lambda registrations (`ITenantContext`, DbContext options) may require
  `AlwaysUseServiceLocationFor`; keep that list short and documented.
- **R-20 Retry ownership.** With EF retries off, only Wolverine retries transient faults; Dapper queries (outside
  Wolverine transactions) get no automatic retry. Revisit if queries show transient failures.

### Open questions
1. Per-module local queues and parallelism: decide when the first real event exists (Brief #8 or #10).

### Next step
Brief #7: Angular login against the development issuer (`AuthService`, interceptor, ProblemDetails mapping) and the
first real slice `GET /api/tenancy/me` through Wolverine.
