# ADR-0010: Limits are evaluated on book value

- Status: Accepted
- Date: 2026-11-19

## Context
The first CSV contract (D-06) carried `market_value_usd`. The owner confirmed that the regulatory maximum-allocation
limits FinGuard checks are evaluated on **book value**, the amount the entity carries in its accounts, not on market
value. The entity's file is the only source of amounts: FinGuard has no market data feed.

## Decision
- The CSV amount column is `book_value` (C# `decimal BookValue`, database `book_value numeric(18,2)`, JSON
  `bookValue`). It replaces `market_value_usd`; there is no transition period because Brief #10 was not released.
- `book_value` is `>= 0` on every row (a fully written-down position has book value 0) and the file total is `> 0`.
- `currency` stays a required column but must be `USD` (case-insensitive). The amount is therefore in USD. Other
  currencies are rejected with `Portfolio.Csv.CurrencyNotSupported`; the column is kept so multi-currency can be added
  later without changing the header.

## Consequences
- Compliance utilization (Phase 3) is computed on book value: class book value / total book value / limit.
- The advisory narrator says "book value", never "market value".
- Market data feeds, revaluation and FX conversion stay out of scope.
- Error codes, messages, samples and tests use the book value vocabulary (`Portfolio.Csv.BookValue*`).

## Alternatives
- Keep market value: rejected, it is not the regulatory basis.
- Accept both columns: rejected, two valuation bases would make every utilization ambiguous.
