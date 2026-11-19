using Dapper;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Npgsql;

namespace FinGuard.Portfolio.Features.GetSubmission;

/// <summary>Query handler (ADR-0007): Dapper over the shared data source, own schema, tenant from the context.</summary>
public static class GetSubmissionHandler
{
    // Another tenant's id reads as not found, so existence is never revealed (D-101).
    private const string Sql = """
        SELECT id AS "SubmissionId", cut_off_date AS "CutOffDate", file_name AS "FileName",
               size_bytes AS "SizeBytes", status AS "Status", received_at AS "ReceivedAt"
        FROM portfolio.submissions
        WHERE id = @SubmissionId AND tenant_id = @TenantId
        """;

    public static async Task<Result<SubmissionDetailsResponse>> Handle(
        GetSubmission query, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            Sql, new { query.SubmissionId, TenantId = tenant.RequiredTenantId }, cancellationToken: ct));

        return row is null
            ? Error.NotFound("Portfolio.Submission.NotFound", "The submission does not exist.")
            : new SubmissionDetailsResponse(
                row.SubmissionId, row.CutOffDate, row.FileName, row.SizeBytes, row.Status, new DateTimeOffset(row.ReceivedAt));
    }

    // Npgsql hands timestamptz to Dapper as a UTC DateTime, which a DateTimeOffset constructor parameter cannot bind.
    private sealed record Row(
        Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTime ReceivedAt);
}
