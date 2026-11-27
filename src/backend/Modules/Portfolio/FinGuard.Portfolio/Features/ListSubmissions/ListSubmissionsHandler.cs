using Dapper;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Npgsql;

namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>Query handler (ADR-0007): Dapper over the shared data source, own schema, tenant from the context.</summary>
public static class ListSubmissionsHandler
{
    /// <summary>Largest page: the D-100 cap is now the maximum <c>limit</c> (D-299).</summary>
    public const int Max = 100;

    // Never selects portfolio.submission_contents, positions or errors: reads carry metadata and counts only.
    // Current = latest accepted per cut-off date by received_at, tie by id (D-141); derived over all of the tenant's
    // submissions, never over the filtered page. Keyset paging on (received_at, id), served by the index
    // (tenant_id, received_at DESC, id DESC); without a cursor the bound is +infinity. One extra row tells whether a
    // next page exists.
    private const string Sql = """
        WITH current_accepted AS (
            SELECT DISTINCT ON (cut_off_date) id
            FROM portfolio.submissions
            WHERE tenant_id = @TenantId AND status = 'Accepted'
            ORDER BY cut_off_date, received_at DESC, id DESC
        )
        SELECT s.id AS "SubmissionId", s.cut_off_date AS "CutOffDate", s.file_name AS "FileName",
               s.size_bytes AS "SizeBytes", s.status AS "Status", s.received_at AS "ReceivedAt",
               s.row_count AS "RowCount", s.error_count AS "ErrorCount", c.id IS NOT NULL AS "IsCurrent"
        FROM portfolio.submissions s
        LEFT JOIN current_accepted c ON c.id = s.id
        WHERE s.tenant_id = @TenantId
          AND (s.received_at, s.id) < (COALESCE(@CursorReceivedAt::timestamptz, 'infinity'),
                                       COALESCE(@CursorId::uuid, 'ffffffff-ffff-ffff-ffff-ffffffffffff'))
          AND (@Status::text IS NULL OR s.status = @Status::text)
          AND (@CutOffFrom::date IS NULL OR s.cut_off_date >= @CutOffFrom::date)
          AND (@CutOffTo::date IS NULL OR s.cut_off_date <= @CutOffTo::date)
          AND (NOT @CurrentOnly OR c.id IS NOT NULL)
        ORDER BY s.received_at DESC, s.id DESC
        LIMIT @Take
        """;

    public static async Task<Result<SubmissionListResponse>> Handle(
        ListSubmissions query, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        var validated = SubmissionListFilter.From(query);
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        var filter = validated.Value;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = (await connection.QueryAsync<Row>(new CommandDefinition(Sql, new
        {
            TenantId = tenant.RequiredTenantId,
            CursorReceivedAt = filter.Cursor?.ReceivedAt,
            CursorId = filter.Cursor?.Id,
            filter.Status,
            filter.CutOffFrom,
            filter.CutOffTo,
            filter.CurrentOnly,
            Take = filter.Limit + 1,
        }, cancellationToken: ct))).ToList();

        var page = rows.Take(filter.Limit)
            .Select(r => new SubmissionListItem(
                r.SubmissionId, r.CutOffDate, r.FileName, r.SizeBytes, r.Status, new DateTimeOffset(r.ReceivedAt),
                r.RowCount, r.ErrorCount, r.IsCurrent))
            .ToList();
        var nextCursor = rows.Count > filter.Limit
            ? new SubmissionCursor(page[^1].ReceivedAt, page[^1].SubmissionId).Encode()
            : null;
        return new SubmissionListResponse(page, nextCursor);
    }

    // Npgsql hands timestamptz to Dapper as a UTC DateTime, which a DateTimeOffset constructor parameter cannot bind.
    private sealed record Row(
        Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTime ReceivedAt,
        int RowCount, int ErrorCount, bool IsCurrent);
}
