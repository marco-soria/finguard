using Dapper;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Npgsql;

namespace FinGuard.Advisory.Features.GetAdvisory;

/// <summary>Query handler (ADR-0007): Dapper over the shared data source, own schema, tenant from the context.</summary>
public static class GetAdvisoryHandler
{
    public static readonly Error NotFound = Error.NotFound("Advisory.Advisory.NotFound", "The advisory does not exist.");

    // Unknown, another tenant's or not yet written all read as not found, so existence is never revealed (D-197).
    private const string Sql = """
        SELECT evaluation_id AS "EvaluationId", submission_id AS "SubmissionId", narrator AS "Narrator",
               generated_at AS "GeneratedAt", narrative AS "Narrative"
        FROM advisory.advisories
        WHERE submission_id = @SubmissionId AND tenant_id = @TenantId;
        """;

    public static async Task<Result<AdvisoryResponse>> Handle(
        GetAdvisory query, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            Sql, new { query.SubmissionId, TenantId = tenant.RequiredTenantId }, cancellationToken: ct));

        if (row is null)
        {
            return NotFound;
        }

        // Paragraphs are separated by one blank line (ADR-0014).
        var paragraphs = row.Narrative.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new AdvisoryResponse(
            row.EvaluationId, row.SubmissionId, row.Narrator, new DateTimeOffset(row.GeneratedAt), paragraphs);
    }

    // Npgsql hands timestamptz to Dapper as a UTC DateTime (backend rules).
    private sealed record Row(Guid EvaluationId, Guid SubmissionId, string Narrator, DateTime GeneratedAt, string Narrative);
}
