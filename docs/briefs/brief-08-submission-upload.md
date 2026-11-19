# Brief #8 — Portfolio submission upload (raw CSV + cut-off date)

How to run it (Windows, PowerShell 7, from the repo root):
1. Start from an up-to-date `main` and save this file as `docs/briefs/brief-08-submission-upload.md`.
2. Start Claude Code: `claude`
3. Prompt: `Read docs/briefs/brief-08-submission-upload.md and execute it. Present your plan first and wait for my approval.`

| Item | Value |
| --- | --- |
| Branch | `feat/portfolio-submission-upload` |
| PR title (exact, becomes the squash commit) | `feat: add portfolio submission upload endpoint` |
| Phase | Phase 1, third brief (backend only; the upload screen is Brief #9) |

---

## Goal
Add the first write slice and the first business tables: `POST /api/portfolio/submissions` accepts a multipart form
with a CSV file and a cut-off date, enforces the upload limits, and stores the submission (metadata + raw bytes) for the
current tenant with status `Received`. No CSV parsing or business validation yet (Phase 2). The slice follows the
reference layout of `GET /api/tenancy/me` (D-81) and the messaging conventions of ADR-0009.

## Context
- Read first and treat as binding: `AGENTS.md`, `CLAUDE.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`,
  `docs/decisions.md` (newest delta first: session 15 executed), ADR-0003, ADR-0004, ADR-0007, ADR-0009.
- Domain facts (AGENTS.md, D-05, D-06, D-10, D-11): the cut-off date is a form field, not a CSV column; files up to
  5 MB, 10,000 rows, UTF-8; several submissions per cut-off date are allowed and immutable; rejected submissions are
  stored with their errors (Phase 2); same SHA-256 + same cut-off date returns the existing submission (Phase 2).
- Current state: Portfolio module has a `TenantAwareDbContext` (schema `portfolio`, only `InitialSchema`), an empty
  `/api/portfolio` group with `TenantMember`, the registration pattern of D-74. Commands go through
  `InvokeForCurrentTenantAsync`; `AutoApplyTransactions` wraps handlers that depend on a DbContext; every message type
  overrides `ToString()` with identifiers only (D-77, enforced by an architecture test).
- Facts verified on 2026-11-19 (re-check at execution):
  - Since .NET 8, Minimal API endpoints that bind `IFormFile` require antiforgery validation; `.DisableAntiforgery()`
    opts out and is appropriate for endpoints authenticated with JWT bearer tokens (not cookies).
    Source: https://learn.microsoft.com/dotnet/core/compatibility/aspnet-core/8.0/antiforgery-checks
  - Two limits apply to uploads: the server request body limit (Kestrel, default about 30 MB; per endpoint through
    `IRequestSizeLimitMetadata`) and `FormOptions.MultipartBodyLengthLimit` (default 128 MB).
    Sources: https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.metadata.irequestsizelimitmetadata ,
    https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.features.formoptions.multipartbodylengthlimit

## Decisions taken for this brief (Appendix A has the full delta)
| Topic | Decision | Rejected |
| --- | --- | --- |
| Raw file storage | PostgreSQL `bytea` in a separate table `portfolio.submission_contents` (1:1 with `submissions`), so metadata queries never load the bytes | Blob storage (not in the locked Azure services, adds a resource); bytes in the `submissions` row (easy to load by accident) |
| Limits | Endpoint request limit 6 MB (5 MB file + multipart overhead); file length checked again in the handler (≤ 5 MB, > 0) | Global Kestrel change |
| Accepted file | Extension `.csv` (case-insensitive); the content type is not trusted (browsers send different ones for CSV); content checks are Phase 2 | MIME sniffing now |
| Cut-off date | Required ISO date (`yyyy-MM-dd`); not later than today in UTC (`TimeProvider`) | Accepting future dates |
| File name | Stored only for display, sanitized (`Path.GetFileName`, control characters removed, max 255); never used as a path; never logged | Not storing it (history UI needs it) |
| SHA-256 | Computed and stored now (column `sha256`, hex), no uniqueness yet; idempotency (D-11) is Phase 2 | Adding the column later with a migration |
| Integration event | None in this brief: Phase 2 validation runs inside Portfolio; the first integration event is `SubmissionAccepted` for Compliance | Publishing `SubmissionReceived` with no consumer |

## Environment rules
- Never run `npm`, `yarn` or `npx`. Never push, never open PRs. Stop after local commits.
- Never invent APIs or versions; stable only; `.NET 10` line for `Microsoft.*` packages.
- Never log file contents, file names, cut-off dates together with tenant data, or the SHA-256. Log the submission id and
  the size in bytes only.
- No file system writes: the upload never touches disk outside what ASP.NET Core buffers internally.

## Preflight
```powershell
git switch main; git pull; git status --short
dotnet build -c Release; dotnet test --no-build -c Release   # all green; report the count
```

## Tasks

### 1. Branch
`git switch -c feat/portfolio-submission-upload`

### 2. Persistence (Portfolio)
- Entities (internal, `ITenantOwned`, private setters, created through a static factory):
  - `Submission`: `Id` (Guid), `TenantId`, `CutOffDate` (`DateOnly`), `FileName` (≤ 255), `SizeBytes` (int),
    `Sha256` (64 lower-case hex chars), `Status` (`SubmissionStatus.Received`, stored as string, max 20),
    `ReceivedAt` (`DateTimeOffset`, from `TimeProvider`).
  - `SubmissionContent`: `SubmissionId` (PK and FK to `submissions.id`, same schema), `TenantId`, `Content` (`byte[]`).
- Index on `(tenant_id, cut_off_date, received_at)` for the history query of Brief #9.
- Migration `AddSubmissions` (`dotnet tool run dotnet-ef migrations add AddSubmissions` with the module as startup
  project, as in earlier briefs). Review the generated SQL: two tables in `portfolio`, snake_case, no cross-schema FK.

### 3. Slice `Features/ReceiveSubmission/`
```
ReceiveSubmission.cs            public sealed record (DateOnly CutOffDate, string FileName, byte[] Content);
                                ToString() => "ReceiveSubmission { CutOffDate = ..., SizeBytes = ... }"
SubmissionReceivedResponse.cs   public sealed record (Guid SubmissionId, string Status, DateTimeOffset ReceivedAt)
ReceiveSubmissionHandler.cs     public; depends on PortfolioDbContext + TimeProvider; validates; creates both entities;
                                returns Result<SubmissionReceivedResponse> (no SaveChanges call: the transactional
                                middleware saves; verify this with Wolverine's EF docs and report)
ReceiveSubmissionEndpoint.cs    internal; MapPost("/submissions") on the /api/portfolio group
```
- Endpoint: binds `IFormFile file` and `string cutOffDate` from the form (`[FromForm]`); `.DisableAntiforgery()` with a
  comment (JWT bearer, no cookies); request size limit 6 MB through endpoint metadata
  (`IRequestSizeLimitMetadata`-based, check the current API) and a matching `FormOptions` limit for this endpoint
  (`WithFormOptions(...)` if available in .NET 10; report what you used). It reads the file into memory (≤ 5 MB),
  builds the command and calls `InvokeForCurrentTenantAsync<Result<SubmissionReceivedResponse>>`. Success → 201 Created
  with `Location: /api/portfolio/submissions/{id}` (the GET arrives in Brief #9) and the response body.
- Validation in the handler, first failure wins, all `Error.Validation` (400 with `code`, D-35):
  `Portfolio.Submission.FileEmpty`, `Portfolio.Submission.FileTooLarge`, `Portfolio.Submission.NotCsv`,
  `Portfolio.Submission.CutOffDateInvalid`, `Portfolio.Submission.CutOffDateInFuture`. A missing `file` form part is a
  400 from the endpoint with code `Portfolio.Submission.FileMissing`.
- Requests above the endpoint limit must end as 413 ProblemDetails with `traceId` (check what the pipeline produces
  today and make it consistent; report).
- Small pure helpers in the slice: `FileNameSanitizer.Sanitize(string)` and `Sha256Hex.Compute(ReadOnlySpan<byte>)`.

### 4. Tests
Unit (`FinGuard.UnitTests`; add `Microsoft.Extensions.TimeProvider.Testing` 10.x to CPM for `FakeTimeProvider` if needed):
| Test | Proves |
| --- | --- |
| `Sanitize_PathTraversalOrControlChars_ReturnsSafeName` (theory) | `../x.csv`, `C:\a\b.csv`, control chars, 300-char names |
| `Compute_KnownInput_ReturnsKnownHex` | SHA-256 helper |
| Handler rules (theory over empty, > 5 MB, `.txt`, `.CSV` accepted, invalid date, tomorrow, today accepted) | validation codes, using `FakeTimeProvider` |

Integration (`FinGuard.Api.IntegrationTests`, multipart requests through `HttpClient`):
| Test | Proves |
| --- | --- |
| `Upload_ValidCsv_Returns201AndStoresSubmissionAndContent` | both rows, `Received`, size, SHA-256, Location header |
| `Upload_ValidCsv_StampsCurrentTenant` | `tenant_id` = Acme for Alice |
| `Upload_TenantIdInFormField_IsIgnored` | AGENTS.md rule 7 (extra `tenantId` form field with Globex's id) |
| `Upload_AsBob_IsInvisibleToAlicesContext` | tenant filter on the new tables |
| `Upload_EmptyFile_Returns400WithCode` / `Upload_NonCsvExtension_...` / `Upload_MissingFile_...` / `Upload_InvalidCutOffDate_...` / `Upload_FutureCutOffDate_...` | validation through HTTP |
| `Upload_BodyOverLimit_Returns413Problem` | limit enforced before buffering the whole body |
| `Upload_NoToken_Returns401` | fallback policy |
| `Upload_Any_DoesNotLogContentFileNameOrHash` | a unique marker in the content and the file name never appears in captured logs (success and validation failure) |
| `Upload_HandlerFailsAfterChanges_WritesNoRows` (optional if a seam exists without production hooks) | atomicity of the two inserts |
Architecture: existing rules must stay green (`ITenantOwned` entities in a `TenantAwareDbContext`, `ToString` override,
one DbContext per handler, `InvokeForCurrentTenantAsync`).

### 5. Docs
- `.claude/rules/backend-vsa.md` + `.cursor/rules/backend-vsa.mdc` (same commit): upload endpoints use
  `.DisableAntiforgery()` only because auth is JWT bearer; never log file names, contents or hashes.
- `docs/decisions.md`: prepend Appendix A verbatim, then your execution delta on top.

## Acceptance criteria
- [ ] Branch `feat/portfolio-submission-upload`, working tree clean, commits follow the commit plan.
- [ ] `dotnet format --verify-no-changes --no-restore`, `dotnet build -c Release` (zero warnings), `dotnet test` pass;
      report counts.
- [ ] `aspire run`: with Alice's token, upload a small CSV with `Invoke-RestMethod -Form` (or `curl.exe -F`): 201;
      `portfolio.submissions` and `portfolio.submission_contents` each hold one row with Acme's `tenant_id`; a 6 MB file
      returns 413. Report the commands and responses (no token in the report).
- [ ] Api logs for those requests contain the submission id and size only.
- [ ] Frontend untouched (`git diff --stat main -- src/frontend` empty).

## Rules to respect
`AGENTS.md`, `.claude/rules/backend-vsa.md`, `.claude/rules/testing.md`, ADR-0004, ADR-0007, ADR-0009,
`docs/git-workflow.md`, `docs/ai-rules.md`. English everywhere.

## Out of scope
CSV parsing, row limits, UTF-8 checks, business validation, rejected submissions, idempotency by hash (Phase 2); GET and
list endpoints, Angular upload form and history (Brief #9); integration events; deleting submissions; virus scanning;
blob storage.

## Commit plan (Conventional Commits, no push)
1. `feat: add submission tables to the portfolio module`
2. `feat: add submission upload endpoint with size and file checks`
3. `test: add submission upload tests`
4. `docs: update backend rules and decisions log`

## Report back (final message)
- Plan versus what you did; every deviation with the reason and the doc you followed.
- The exact APIs used for antiforgery opt-out, request size limit and form options, and the generated migration SQL summary.
- Output of the acceptance checks.
- Your execution delta (also written into `docs/decisions.md`).
- The exact commands for the owner, not run by you:
  ```powershell
  git push -u origin feat/portfolio-submission-upload
  gh pr create --base main --head feat/portfolio-submission-upload `
    --title "feat: add portfolio submission upload endpoint" `
    --body-file .github/pull_request_template.md
  ```

---

## Appendix A — Decisions delta (planning session 16), paste verbatim into `docs/decisions.md`

## Delta 2026-11-19 (session 16: Brief #8 planning)

### Closed decisions
| ID | Decision | Notes |
| --- | --- | --- |
| D-87 | Raw CSV stored as `bytea` in `portfolio.submission_contents` (1:1 with `portfolio.submissions`) | No blob storage; metadata queries never touch the bytes |
| D-88 | Upload limits: endpoint request limit 6 MB, file ≤ 5 MB and > 0 checked in the handler; over-limit requests answer 413 ProblemDetails | Row and encoding limits are Phase 2 |
| D-89 | Only the `.csv` extension is checked at upload; content type is not trusted; content checks in Phase 2 | |
| D-90 | Cut-off date: required `yyyy-MM-dd`, not later than today in UTC | Assumption: no business case for future cut-off dates; reopen if one appears |
| D-91 | Original file name stored sanitized for display only, never used as a path, never logged; SHA-256 computed and stored at upload, uniqueness deferred to Phase 2 (D-11) | |
| D-92 | Upload endpoint opts out of antiforgery: authentication is JWT bearer, no cookies | Revisit if cookie auth ever appears |
| D-93 | No `SubmissionReceived` integration event; the first integration event will be `SubmissionAccepted` (Portfolio → Compliance) | Phase 2 validation stays inside Portfolio |

### Verified on 2026-11-19
| Area | Finding | Source |
| --- | --- | --- |
| ASP.NET Core | `IFormFile` Minimal API endpoints require antiforgery since .NET 8; `.DisableAntiforgery()` opts out | https://learn.microsoft.com/dotnet/core/compatibility/aspnet-core/8.0/antiforgery-checks |
| ASP.NET Core | Request body limit (`IRequestSizeLimitMetadata`) and `FormOptions.MultipartBodyLengthLimit` are separate limits | https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.metadata.irequestsizelimitmetadata |

### Risks and flags
- **R-23 Database growth.** Raw files live in PostgreSQL; with 5 MB per file the demo database grows quickly. Phase 5
  sizes PostgreSQL Flexible Server storage and may add a retention rule.

### Open questions
1. Per-module local queues (session 13): still open, decide with `SubmissionAccepted`.

### Next step
Brief #9 (closes Phase 1): `GET /api/portfolio/submissions` and `GET /api/portfolio/submissions/{id}` with Dapper,
Angular upload form and submission history, UI library decision, browser E2E tool decision and the first E2E flow.
