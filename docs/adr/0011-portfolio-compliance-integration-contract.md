# ADR-0011: Portfolio to Compliance integration contract

- Status: Accepted
- Date: 2026-11-20

## Context
Compliance (Phase 3) evaluates a tenant's portfolio at a cut-off date against the maximum-allocation limits. The
portfolio lives in Portfolio's schema, and modules talk only through integration events on the Wolverine outbox
(ADR-0001, ADR-0003, ADR-0009). The planned events in the overview (`PortfolioSubmitted`, `PortfolioRejected`) predate
the upload rules of Phase 2: rejected uploads are stored too, duplicates store nothing, and the current submission per
cut-off date is the latest accepted one (D-141). Phase 2 needs one long-lived contract before Compliance builds on it.

## Decision
- **Name.** `FinGuard.Portfolio.Contracts.Events.SubmissionAccepted`, in Portfolio's language: what happened.
- **When.** Published once per new accepted submission, cascaded from `ReceiveSubmissionHandler` in the same
  transaction as the submission rows (atomic outbox). Rejected and duplicate uploads publish nothing.
- **Payload (event-carried state).** `EventId`, `TenantId`, `OccurredAt`, `SubmissionId`, `CutOffDate`, `ReceivedAt`
  and `Totals`: four `AssetClassTotal(AssetClassCode AssetClass, decimal BookValue)` entries, one per class in enum
  order, zeros included. Amounts are book value in USD (ADR-0010). Positions and their ids stay in Portfolio.
- **Asset classes.** `FinGuard.Portfolio.Contracts.AssetClassCode`. Compliance maps it to its own domain type.
- **Current version.** An accepted submission is the current one for its date when it is accepted, so the event has no
  `isCurrent` flag. Compliance applies D-141 (latest `ReceivedAt`) when it reads its snapshots.
- **Consumer.** Compliance stores its own `compliance.portfolio_snapshots` row per event, idempotent by `EventId`.
  It never calls back into Portfolio.
- **Versioning.** Additive changes only (a new optional field, a new event). A breaking change (renaming, removing or
  changing the meaning of a field, a new asset class consumers cannot handle) is a new event type.
- **Logging.** `ToString()` prints the type name, `EventId` and `SubmissionId` only: never dates or amounts.

## Consequences
- Compliance evaluates from data it owns; Portfolio can be down or change its schema without breaking evaluation.
- Amounts travel in the envelope body and are stored in the `wolverine` tables (R-35): database access and backups in
  Phase 5 must be as protected as the module tables.
- Submissions accepted before this contract have no snapshot (development data only, no backfill).
- A consumer for rejected uploads (for example Audit) will need its own event; none is published until one exists.
- Wolverine's default local routing applies: one durable local queue per message type.

## Alternatives
- `PortfolioSubmitted` / `PortfolioRejected`: rejected, a rejected upload was also "submitted", and no consumer needs
  the rejected event yet.
- A Contracts query that Compliance calls back: rejected, temporal coupling; the event already is the snapshot.
- Positions in the event: rejected, Compliance needs class totals only.
- Asset classes as strings: rejected, typos would compile.
- A separate `CurrentSubmissionChanged` event: rejected, every accepted submission is current when accepted.
- Publishing after the HTTP response: rejected, not atomic with the stored rows.
