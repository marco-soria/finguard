using Dapper;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Npgsql;

namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>Query handler (ADR-0007): Dapper over the shared data source, own schema, tenant from the context.</summary>
public static class ListSubmissionsHandler
{
    /// <summary>Named cap (D-100); paging arrives with the envelope when needed.</summary>
    public const int Max = 100;

    // Never selects portfolio.submission_contents: reads carry metadata only.
    private const string Sql = """
        SELECT id AS "SubmissionId", cut_off_date AS "CutOffDate", file_name AS "FileName",
               size_bytes AS "SizeBytes", status AS "Status", received_at AS "ReceivedAt"
        FROM portfolio.submissions
        WHERE tenant_id = @TenantId
        ORDER BY received_at DESC, id DESC
        LIMIT @Max
        """;

    public static async Task<Result<SubmissionListResponse>> Handle(
        ListSubmissions query, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(Sql, new { TenantId = tenant.RequiredTenantId, Max }, cancellationToken: ct));
        return new SubmissionListResponse(rows
            .Select(r => new SubmissionListItem(
                r.SubmissionId, r.CutOffDate, r.FileName, r.SizeBytes, r.Status, new DateTimeOffset(r.ReceivedAt)))
            .ToList());
    }

    // Npgsql hands timestamptz to Dapper as a UTC DateTime, which a DateTimeOffset constructor parameter cannot bind.
    private sealed record Row(
        Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTime ReceivedAt);
}
