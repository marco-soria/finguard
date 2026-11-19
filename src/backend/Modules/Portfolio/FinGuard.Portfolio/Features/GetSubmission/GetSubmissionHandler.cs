using Dapper;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Npgsql;

namespace FinGuard.Portfolio.Features.GetSubmission;

/// <summary>Query handler (ADR-0007): Dapper over the shared data source, own schema, tenant from the context.</summary>
public static class GetSubmissionHandler
{
    // Another tenant's id reads as not found, so existence is never revealed (D-101).
    // Never selects portfolio.submission_contents or portfolio.positions: this slice returns metadata and errors.
    private const string Sql = """
        SELECT id AS "SubmissionId", cut_off_date AS "CutOffDate", file_name AS "FileName",
               size_bytes AS "SizeBytes", status AS "Status", received_at AS "ReceivedAt",
               row_count AS "RowCount", error_count AS "ErrorCount"
        FROM portfolio.submissions
        WHERE id = @SubmissionId AND tenant_id = @TenantId;

        SELECT row_number AS "Row", "column" AS "Column", code AS "Code", message AS "Message"
        FROM portfolio.submission_errors
        WHERE submission_id = @SubmissionId AND tenant_id = @TenantId
        ORDER BY ordinal;
        """;

    public static async Task<Result<SubmissionDetailsResponse>> Handle(
        GetSubmission query, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Sql, new { query.SubmissionId, TenantId = tenant.RequiredTenantId }, cancellationToken: ct));
        var row = await results.ReadSingleOrDefaultAsync<Row>();
        var errors = (await results.ReadAsync<SubmissionErrorItem>()).ToList();

        return row is null
            ? Error.NotFound("Portfolio.Submission.NotFound", "The submission does not exist.")
            : new SubmissionDetailsResponse(
                row.SubmissionId, row.CutOffDate, row.FileName, row.SizeBytes, row.Status, new DateTimeOffset(row.ReceivedAt),
                row.RowCount, row.ErrorCount, errors);
    }

    // Npgsql hands timestamptz to Dapper as a UTC DateTime, which a DateTimeOffset constructor parameter cannot bind.
    private sealed record Row(
        Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTime ReceivedAt,
        int RowCount, int ErrorCount);
}
