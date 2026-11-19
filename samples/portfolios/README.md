# Sample portfolios

Small CSV files for manual checks, demos and Phase 3 compliance work (D-118). All position ids, names and amounts are
fictional. Upload them from the web app with any cut-off date up to today.

Format (see `AGENTS.md`, domain rules): header `position_id,asset_class,book_value,currency,description`, UTF-8,
at most 10,000 rows and 5 MB. Validation errors use the codes `Portfolio.Csv.*`.

| File | Expected result | What it shows |
| --- | --- | --- |
| `valid-balanced.csv` | Accepted, 9 positions | All four asset classes, total book value 5,000,000.00 USD: cash 32 %, fixed income 25 %, shares 21 %, real estate 22 %. Every class is below 90 % of its limit. A quoted description contains a comma, and one fully written-down position has book value 0. |
| `valid-shares-heavy.csv` | Accepted, 5 positions | Shares are 2,200,000.00 of 3,000,000.00 USD (73 %, limit 25 %). Valid as a file; Phase 3 compliance reports it as breached. |
| `invalid-mixed.csv` | Rejected, 9 errors | One error of each common kind (rows as in a spreadsheet, header = row 1): row 3 `PositionIdRequired`, row 4 `AssetClassUnknown`, row 5 `BookValueNegative`, row 6 `BookValueTooPrecise`, row 7 `BookValueInvalid` (thousands separator), row 8 `BookValueRequired`, row 9 `CurrencyNotSupported`, row 10 `PositionIdDuplicated`, row 11 `DescriptionUnsafe` (a description that a spreadsheet would run as a formula). |
