# ADR-0003: Wolverine for CQRS and messaging with a PostgreSQL transactional outbox

- Status: Accepted
- Date: 2026-11-13

## Context
Modules must communicate without direct references, and an event must never be lost or published for a rolled-back
change. PostgreSQL is the only database. The team wants CQRS handlers without MediatR.

## Decision
Use Wolverine for command/query handlers and messaging. Integration events travel between modules through Wolverine
with durable storage in PostgreSQL (`WolverineFx.Postgresql`) and EF Core transactional integration
(`WolverineFx.EntityFrameworkCore`): a handler's DB changes and its outgoing events commit atomically. Wolverine's own
tables live in a dedicated `wolverine` schema. HTTP endpoints stay plain Minimal APIs that invoke the message bus;
`Wolverine.HTTP` is not adopted by default.

Delivery is at-least-once, so every consumer must be idempotent (key: event id).

## Consequences
- Reliable event flow without a broker; the same code can later use a broker transport.
- Handlers are plain classes; tests can call them directly.
- Risk: using one DbContext per module with a single Wolverine envelope store needs validation. Phase 1 starts with a
  spike that proves the outbox works with two DbContexts before slices depend on it.
- Tracing across handlers needs the correlation id propagated; ServiceDefaults must register it.

## Alternatives considered
- MediatR plus a hand-written outbox: rejected, more code to own.
- A message broker now (Service Bus, RabbitMQ): rejected, extra infrastructure and cost.
- `Wolverine.HTTP`: possible later per slice if it respects Minimal API conventions.
