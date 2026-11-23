using Dapper;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Npgsql;

namespace FinGuard.Compliance.Features.GetEvaluation;

/// <summary>Query handler (ADR-0007): Dapper over the shared data source, own schema, tenant from the context.</summary>
public static class GetEvaluationHandler
{
    // Unknown, another tenant's or not yet evaluated all read as not found, so existence is never revealed (D-197).
    // Classes in enum order; the class limit comes from the evaluation's policy snapshot.
    private const string Sql = """
        SELECT id AS "EvaluationId", submission_id AS "SubmissionId", cut_off_date AS "CutOffDate",
               overall_status AS "OverallStatus", policy_version AS "PolicyVersion", evaluated_at AS "EvaluatedAt",
               total_book_value AS "TotalBookValue", alert_threshold AS "AlertThreshold"
        FROM compliance.evaluations
        WHERE submission_id = @SubmissionId AND tenant_id = @TenantId;

        SELECT c.asset_class AS "AssetClass", c.book_value AS "BookValue", c.share AS "Share",
               CASE c.asset_class
                   WHEN 'Cash' THEN e.cash_limit
                   WHEN 'FixedIncome' THEN e.fixed_income_limit
                   WHEN 'Shares' THEN e.shares_limit
                   WHEN 'RealEstate' THEN e.real_estate_limit
               END AS "Limit",
               c.utilization AS "Utilization", c.status AS "Status"
        FROM compliance.evaluation_classes c
        JOIN compliance.evaluations e ON e.id = c.evaluation_id
        WHERE e.submission_id = @SubmissionId AND e.tenant_id = @TenantId AND c.tenant_id = @TenantId
        ORDER BY array_position(ARRAY['Cash', 'FixedIncome', 'Shares', 'RealEstate'], c.asset_class::text);
        """;

    public static async Task<Result<EvaluationResponse>> Handle(
        GetEvaluation query, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Sql, new { query.SubmissionId, TenantId = tenant.RequiredTenantId }, cancellationToken: ct));
        var row = await results.ReadSingleOrDefaultAsync<Row>();
        var classes = (await results.ReadAsync<ClassEvaluationItem>()).ToList();

        if (row is null)
        {
            return Error.NotFound("Compliance.Evaluation.NotFound", "The evaluation does not exist.");
        }

        var alerts = classes
            .Where(c => c.Status != "Compliant")
            .Select(c => new AlertItem(c.AssetClass, c.Status, c.Utilization))
            .ToList();
        return new EvaluationResponse(
            row.EvaluationId, row.SubmissionId, row.CutOffDate, row.OverallStatus, row.PolicyVersion,
            new DateTimeOffset(row.EvaluatedAt), row.TotalBookValue, row.AlertThreshold, classes, alerts);
    }

    // Npgsql hands timestamptz to Dapper as a UTC DateTime, which a DateTimeOffset constructor parameter cannot bind.
    private sealed record Row(
        Guid EvaluationId, Guid SubmissionId, DateOnly CutOffDate, string OverallStatus, int PolicyVersion,
        DateTime EvaluatedAt, decimal TotalBookValue, decimal AlertThreshold);
}
