# Sample portfolios

Small CSV files for manual checks, demos and Phase 3 compliance work (D-118). All position ids, names and amounts are
fictional. Upload them from the web app with any cut-off date from 2000-01-01 (the first limit policy) up to today.

Format (see `AGENTS.md`, domain rules): header `position_id,asset_class,book_value,currency,description`, UTF-8,
at most 10,000 rows and 5 MB. Validation errors use the codes `Portfolio.Csv.*`.

`book_value` is the amount the entity carries in its accounts, in USD (`currency` must be `USD`). Compliance limits
are evaluated on book value, not market value (ADR-0010). A row may be 0 (a fully written-down position); the file
total must be greater than 0.

| File | Expected result | What it shows |
| --- | --- | --- |
| `valid-balanced.csv` | Accepted, 9 positions | All four asset classes, total book value 5,000,000.00 USD: cash 32 %, fixed income 25 %, shares 21 %, real estate 22 %. Every class is below 90 % of its limit, so the rebalancing proposal is `NotNeeded`. A quoted description contains a comma, and one fully written-down position has book value 0. |
| `valid-shares-heavy.csv` | Accepted, 5 positions | Shares are 2,200,000.00 of 3,000,000.00 USD (73 %, limit 25 %). Valid as a file; Phase 3 compliance reports it as breached. Rebalancing proposal `Full` (ADR-0013): Shares 2,200,000.00 → 637,500.00; Cash 300,000.00 → 993,374.43; Real estate 100,000.00 → 617,623.26; Fixed income 400,000.00 → 751,502.31; total still 3,000,000.00. |
| `invalid-mixed.csv` | Rejected, 9 errors | One error of each common kind (rows as in a spreadsheet, header = row 1): row 3 `PositionIdRequired`, row 4 `AssetClassUnknown`, row 5 `BookValueNegative`, row 6 `BookValueTooPrecise`, row 7 `BookValueInvalid` (thousands separator), row 8 `BookValueRequired`, row 9 `CurrencyNotSupported`, row 10 `PositionIdDuplicated`, row 11 `DescriptionUnsafe` (a description that a spreadsheet would run as a formula). |

## Expected advisory for `valid-shares-heavy.csv`

With cut-off date 2026-06-30, the Advisory section shows these four paragraphs (ADR-0014; the date is the one chosen
on upload). `TemplateNarratorTests` and `GetAdvisoryTests` keep this text true.

> On 2026-06-30, the portfolio breaches the limit for Shares under policy version 1.
>
> Shares holds 73.33 % of the portfolio against a limit of 25.00 %, so 293.33 % of the limit is used.
>
> The proposal reduces Shares by $1,562,500.00 to $637,500.00, which is 85.00 % of its limit. The amount moves to
> Cash (+$693,374.43), Real estate (+$517,623.26) and Fixed income (+$351,502.31). Each receiving class moves toward
> 85.00 % of its limit. After these moves, every asset class is within its limit.
>
> This text explains a deterministic calculation. It is not investment advice.

`valid-balanced.csv` gets the headline "…the portfolio is within every limit of policy version 1. No rebalancing is
needed." and the same closing sentence.
