# Brief #10a — CSV amendments (book value, currency, description hardening) and agent session hygiene

How to run it (Windows, PowerShell 7, from the repo root, in the same Claude Code session or a new one):
1. Stay on `feat/portfolio-csv-validation` (Brief #10, not pushed yet). Save this file as
   `docs/briefs/brief-10a-csv-amendments-and-session-hygiene.md`.
2. Prompt: `Read docs/briefs/brief-10a-csv-amendments-and-session-hygiene.md and execute Part A, then Part B. Present
   your plan first and wait for my approval.`

| Part | Branch | PR title (exact) |
| --- | --- | --- |
| A — CSV amendments | `feat/portfolio-csv-validation` (extra commits; same PR as Brief #10) | `feat: validate portfolio CSV and store positions or rejection errors` |
| B — Session hygiene and Aspire CLI | `chore/agent-session-hygiene`, created from `main` (not from Part A) | `chore: add agent session close-out and dev environment setup` |

Both parts follow the session rules in section 0 from the first command of this run.

---

## 0. Session rules for this run (and every future brief, once Part B lands)
1. **Baseline first.** Before starting anything, record what is already running so you never stop the owner's
   processes or containers: `docker ps --format '{{.ID}} {{.Names}} {{.Image}}'` and the list of `dotnet`, `node`,
   `aspire`, `dcp`, `chrome-headless-shell`/`chromium` processes with their command lines. Keep it in
   `.git/finguard-session-baseline.txt` (never committed).
2. **Close what you open.** Before your final message, stop everything you started in this session and that is not in
   the baseline:
   - `aspire run`: stop it gracefully (Ctrl+C in its terminal, or the CLI's stop command if `aspire --help` lists one) so
     Aspire removes its session containers (`postgres`, its pgAdmin if any). Never kill DCP first: a hard kill leaves
     containers behind.
   - Remaining Aspire leftovers (AppHost, `dcp`, Api, `ng serve`/node of `finguard-web`): stop only processes whose
     command line contains this repository's path.
   - Testcontainers: containers labelled `org.testcontainers=true` that are not in the baseline (Ryuk normally removes
     them; check anyway).
   - Playwright browsers left running; `dotnet build-server shutdown`.
   - **Never** remove Docker volumes (the `postgres` data volume holds the dev database) and never stop containers that
     were in the baseline. If something cannot be stopped, say what and why.
3. **Final message always ends with a "Close-out" block:**
   - what was stopped, and what is still running (should be nothing new);
   - `git log --oneline main..HEAD` and `git diff --stat main...HEAD`;
   - the PR body file path (`.git/finguard-pr-body.md`), written from `.github/pull_request_template.md` with real numbers;
   - the exact commands for the owner:
     ```powershell
     git push -u origin <branch>
     gh pr create --base main --head <branch> --title "<exact title>" --body-file .git/finguard-pr-body.md
     # after merge:
     git switch main; git pull --ff-only; git branch -d <branch>
     ```
   (Several parts or branches in one run: one block per branch, same format.)

---

## Part A — CSV amendments (branch `feat/portfolio-csv-validation`)

### Goal
The owner changed the CSV contract before Brief #10 is pushed: the amount column becomes `book_value` (regulatory
limits are evaluated on book value, not market value), `currency` stays but is strictly USD, and `description` is
shorter and protected against CSV/formula injection. Update parser, entities, DTOs, frontend, samples, tests and docs.

### Decisions for Part A
| Topic | Decision | Notes |
| --- | --- | --- |
| Valuation basis | Limits are evaluated on **book value**. CSV column `book_value` (replaces `market_value_usd`); C# `decimal BookValue`; DB `book_value numeric(18,2)`; JSON `bookValue` | New ADR-0010 (short). The amount is in USD because `currency` must be USD |
| Amount sign | `book_value` must be **greater than or equal to 0** on every row (no negatives; a fully written-down position with book value 0 is valid). Code `Portfolio.Csv.BookValueNegative` replaces `MarketValueNegative`. The file-level rule stays: `Portfolio.Csv.TotalNotPositive` when the sum of `book_value` is 0 | Same as D-114 and AGENTS.md ("no negatives, total > 0"), only renamed |
| Other amount rules | Unchanged (invariant, ≤ 2 decimals, no sign/thousands/exponent, max `numeric(18,2)`), codes renamed `BookValueRequired`, `BookValueInvalid`, `BookValueTooPrecise`, `BookValueOutOfRange` | |
| Currency | Required; case-insensitive, normalised to `USD`; anything else → `Portfolio.Csv.CurrencyNotSupported` with the message "Only USD positions are supported in this version." (replaces `CurrencyNotUsd`) | Column kept for future multi-currency (out of scope) |
| Description | Optional, max **150** characters (was 500), still not stored | |
| Formula injection | Reject a row when `description` **or `position_id`** starts with a formula trigger: after trimming spaces, first character `=`, `+`, `-`, `@` or their full-width forms (`＝`, `＋`, `－`, `＠`); or the raw value starts with tab, CR or LF. Codes `Portfolio.Csv.DescriptionUnsafe`, `Portfolio.Csv.PositionIdUnsafe` | OWASP lists `=`, `+`, `-`, `@`, tab, CR, LF and full-width variants. `position_id` is stored and displayed, so it needs the same guard. Input rejection is defence in depth: any future CSV/Excel export must still escape cells on output |
| Migration | The `AddCsvValidation` migration only exists on this unpushed branch: **regenerate it** with the new column name instead of adding a rename migration. The local dev database already applied the old version, so **ask the owner before** removing the `postgres` data volume (or offer `aspire` with a fresh volume); never remove it without approval | Keeps `main`'s migration history clean. Test databases are fresh per run |

### Tasks
1. `PortfolioCsvReader`: canonical header `position_id, asset_class, book_value, currency, description`; the rules
   and codes above; `MaxDescriptionLength = 150`; one shared `FormulaGuard.StartsWithFormulaTrigger(string raw)` helper
   (pure, unit tested) used for both columns. Messages stay fixed sentences without cell values.
2. Entities/DTOs: `Position.MarketValueUsd` → `BookValue`; `ParsedPosition` likewise; any response or TS interface that
   exposes the amount uses `bookValue`. Regenerate `AddCsvValidation`; `has-pending-model-changes` clean.
3. Frontend: interfaces and any label say "Book value"; no other UI change.
4. Samples: update the three files in `samples/portfolios/` and their README (header, no zero values, one
   formula-trigger description, one negative `book_value` and one non-USD row in `invalid-mixed.csv`;
   `valid-balanced.csv` keeps one position with `book_value` 0).
5. Docs:
   - `docs/adr/0010-valuation-basis-book-value.md` (context, decision, consequences: Compliance utilization uses book
     value; the narrator says "book value"; market data feeds stay out of scope).
   - `AGENTS.md` domain rules: CSV columns, "limits are evaluated on book value", `book_value >= 0` per row and total > 0, description
     ≤ 150 with formula guard, currency USD only.
   - `samples/portfolios/README.md`: one paragraph on book value.
   - `.claude/rules/backend-vsa.md` + `.cursor/rules/backend-vsa.mdc` (same commit): "Free-text and identifier columns
     from uploaded files are rejected when they start with a formula trigger; any export escapes them again (OWASP CSV
     Injection)."
   - `docs/decisions.md`: a new delta "session 19: Brief #10 amendments" with the next free D-numbers; mark D-06's
     column name and D-114's description limit (500 → 150) as superseded; D-114's "0 allowed per row" still holds.

### Tests
Unit (table-driven):
- Header with `book_value` accepted; old header `market_value_usd` → `ColumnUnknown` + `ColumnMissing`.
- `book_value`: `0`, `0.00` and `0.01` accepted on a row; `-1` and `-0.01` → `BookValueNegative` (a leading `-` is
  reported as negative, not as an invalid format; test it); previous precision and range cases with the new codes.
- Total: a file whose rows are all `0` → `TotalNotPositive`; one row `0` plus one row `10` → accepted.
- Currency: `USD`, `usd`, ` Usd ` accepted; `EUR`, `PEN`, empty → the right codes; message text asserted.
- Description: 150 accepted, 151 → `DescriptionTooLong`; `=SUM(A1)`, `+1`, `-x`, `@cmd`, `  =x` (leading spaces),
  `\t=x`, `＝x` → `DescriptionUnsafe`; `Net - of fees` (dash not first) accepted; empty accepted.
- `position_id`: `=HYPERLINK(...)`, `-123`, `@x` → `PositionIdUnsafe`.
- `FormulaGuard` unit tests for every trigger and for safe values.
Integration: rename existing tests to book value; add `Upload_FormulaInDescription_IsRejectedWithRowError` and
`Upload_NonUsdCurrency_IsRejectedWithClearMessage`; log test markers move to `book_value` and `description`.
Vitest: interfaces/labels. E2E: in-memory CSVs use the new header; the rejected flow includes one formula-trigger
description and asserts its error line on the detail page. Run `pnpm e2e` twice with `aspire run`, then stop Aspire
(section 0).

### Commits (on `feat/portfolio-csv-validation`)
1. `feat: evaluate portfolios on book value instead of market value`
2. `feat: reject formula-like descriptions and position ids in portfolio CSV`
3. `docs: add ADR-0010 on book value and update domain rules`

Then regenerate `.git/finguard-pr-body.md` for the whole Brief #10 PR (Brief #10 + #10a: mention ADR-0010, the
renamed column and the hardening in Summary and Changes).

---

## Part B — Session hygiene and Aspire CLI (branch `chore/agent-session-hygiene` from `main`)

### Goal
Make section 0 permanent and verifiable, and fix the Aspire CLI setup so PowerShell and Git Bash run the same CLI.

### Tasks
1. **`scripts/dev-session.ps1`** (PowerShell 7, no dependencies):
   - `-Snapshot` writes the baseline described in 0.1 to `.git/finguard-session-baseline.txt`.
   - `-Report` (default) lists processes whose command line contains the repo path (AppHost, Api, `dcp`, `ng serve`),
     Playwright browsers, Testcontainers containers, Aspire session containers, and marks which are new versus the
     baseline.
   - `-Stop` stops only the new items from `-Report`, AppHost first and gracefully where possible, then runs
     `dotnet build-server shutdown`. Never touches volumes or baseline items. `-WhatIf` supported.
   - A Pester test is not required; prove it by running `-Snapshot`, starting `aspire run`, `-Report` (shows the new
     items), `-Stop`, `-Report` (empty). Paste the outputs in the delta.
2. **`AGENTS.md`**: new section "Closing a work session" with the three rules of section 0, short, referring to
   `scripts/dev-session.ps1`. Applies to Claude Code and Cursor.
3. **`CLAUDE.md`**: one line: "Start every brief with `scripts/dev-session.ps1 -Snapshot`; end with `-Stop` and the
   Close-out block (AGENTS.md)". Keep the file under 40 lines.
4. **Aspire CLI diagnosis** (report first, change nothing outside the repo without the owner):
   - PowerShell: `Get-Command aspire -All | Format-List Source,Version`, `where.exe aspire`, `dotnet tool list -g`,
     `aspire --version`.
   - Git Bash: `which -a aspire`, `type -a aspire`, `aspire --version`.
   - Look for every install: .NET global tool (`%USERPROFILE%\.dotnet\tools`), npm global shims
     (`%APPDATA%\npm\aspire`, `aspire.cmd`, `aspire.ps1`), install script (`%USERPROFILE%\.aspire\bin`), WinGet.
   - Target state: **one** install, the .NET global tool `Aspire.Cli` at the AppHost's Aspire version (13.6.5 today),
     working in both shells. If other installs exist, list the exact removal command for the owner (you must not run
     `npm`; the owner removes an npm install with `npm uninstall -g <package>` and an install-script copy by deleting
     `%USERPROFILE%\.aspire\bin` and its PATH entry).
   - Correct the earlier note "In Git Bash the tool shim is `aspire.cmd`; use PowerShell" (session 6) wherever docs repeat
     it: the .NET tool installs `aspire.exe`, which Git Bash runs. Record the real cause found.
5. **`docs/dev-environment.md`** (new, short): prerequisites (Docker, .NET SDK from `global.json`, Node 24, pnpm via
   `packageManager`, Aspire CLI as .NET global tool with the pinned version and the update command), shells (PowerShell
   7 recommended; Git Bash works), how to run, how to close a session (`scripts/dev-session.ps1`), how to reset the dev
   database volume (only when asked). Link it from `AGENTS.md` Commands and `README.md`.
6. `docs/decisions.md`: delta "session 19: session hygiene" with the next free D-numbers (after Part A's).

### Commits (on `chore/agent-session-hygiene`)
1. `chore: add dev session snapshot, report and stop script`
2. `docs: add closing-a-session rules and dev environment setup`

Write `.git/finguard-pr-body-hygiene.md` (template format) and give its Close-out block. Part B touches `AGENTS.md` in a
different section from Part A, so the two PRs merge in either order; if Git reports a conflict, resolve it on the second
branch after the first merges.

---

## Out of scope
SHA-256 idempotency, current version per cut-off date, the errors UI (Brief #11); Compliance; any dependency upgrade;
Claude Code hooks that enforce the close-out automatically (possible later if the rule is ignored); CSV export.

## Report back
Plan first; wait for approval. At the end: Part A results (tests by project, E2E twice), the Aspire diagnosis with the
removal commands for the owner, the `dev-session.ps1` proof, deltas written, and one Close-out block per branch.
