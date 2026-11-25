using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Domain.Rebalancing;

namespace FinGuard.Compliance.Persistence;

/// <summary>
/// An <see cref="Evaluation"/> in <c>compliance.evaluations</c>, with the policy snapshot it used (D-194). Write-only
/// row model: the domain gets no EF constructor, and reads go through Dapper. Alerts are not stored: they are the
/// classes with status Warning or Breached (D-195).
/// </summary>
internal sealed class EvaluationRow : ITenantOwned
{
    public Guid Id { get; init; }

    // Set by TenantStampingInterceptor from ITenantContext on save (ADR-0004).
    public Guid TenantId { get; private set; }

    public Guid SubmissionId { get; init; }

    public DateOnly CutOffDate { get; init; }

    public DateTimeOffset EvaluatedAt { get; init; }

    public decimal TotalBookValue { get; init; }

    public ComplianceStatus OverallStatus { get; init; }

    public int PolicyVersion { get; init; }

    public decimal CashLimit { get; init; }

    public decimal FixedIncomeLimit { get; init; }

    public decimal SharesLimit { get; init; }

    public decimal RealEstateLimit { get; init; }

    public decimal AlertThreshold { get; init; }

    public decimal RebalanceTargetRatio { get; init; }

    // Null only for evaluations stored before the rebalance engine (D-236); no backfill.
    public ProposalStatus? ProposalStatus { get; init; }

    public List<EvaluationClassRow> Classes { get; init; } = [];

    public static EvaluationRow From(Evaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var limits = evaluation.Policy.Limits;
        return new EvaluationRow
        {
            Id = evaluation.Id,
            SubmissionId = evaluation.SubmissionId,
            CutOffDate = evaluation.CutOffDate,
            EvaluatedAt = evaluation.EvaluatedAt,
            TotalBookValue = evaluation.Total.Amount,
            OverallStatus = evaluation.OverallStatus,
            PolicyVersion = evaluation.Policy.Version,
            CashLimit = limits[AssetClass.Cash].Value,
            FixedIncomeLimit = limits[AssetClass.FixedIncome].Value,
            SharesLimit = limits[AssetClass.Shares].Value,
            RealEstateLimit = limits[AssetClass.RealEstate].Value,
            AlertThreshold = evaluation.Policy.AlertThreshold.Value,
            RebalanceTargetRatio = evaluation.Policy.RebalanceTargetRatio.Value,
            ProposalStatus = evaluation.Proposal.Status,
            Classes = evaluation.Classes
                .Select(c => new EvaluationClassRow
                {
                    EvaluationId = evaluation.Id,
                    AssetClass = c.Utilization.Class,
                    BookValue = c.Utilization.Value.Amount,
                    Share = c.Utilization.Share.Value,
                    Utilization = c.Utilization.Utilization.Value,
                    Status = c.Status,
                    ProposedBookValue = evaluation.Proposal.Moves
                        .Single(m => m.Class == c.Utilization.Class).Proposed.Amount,
                })
                .ToList(),
        };
    }
}

/// <summary>One class of an evaluation in <c>compliance.evaluation_classes</c>. Ratios are unrounded (D-195).</summary>
internal sealed class EvaluationClassRow : ITenantOwned
{
    public Guid EvaluationId { get; init; }

    public Guid TenantId { get; private set; }

    public AssetClass AssetClass { get; init; }

    public decimal BookValue { get; init; }

    public decimal Share { get; init; }

    public decimal Utilization { get; init; }

    public ComplianceStatus Status { get; init; }

    // Null only for evaluations stored before the rebalance engine (D-236).
    public decimal? ProposedBookValue { get; init; }
}
