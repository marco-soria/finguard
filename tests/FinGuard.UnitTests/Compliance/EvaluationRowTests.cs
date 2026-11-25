using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Domain.Rebalancing;
using FinGuard.Compliance.Persistence;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Compliance;

public sealed class EvaluationRowTests
{
    [Fact]
    public void From_Evaluation_MapsHeaderAndPolicySnapshot()
    {
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));

        var row = EvaluationRow.From(evaluation);

        Assert.Equal(
            (evaluation.Id, evaluation.SubmissionId, evaluation.CutOffDate, EvaluatedAt, Total, ComplianceStatus.Breached),
            (row.Id, row.SubmissionId, row.CutOffDate, row.EvaluatedAt, row.TotalBookValue, row.OverallStatus));
        Assert.Equal(
            (1, 0.40m, 0.30m, 0.25m, 0.25m, 0.90m, 0.85m),
            (row.PolicyVersion, row.CashLimit, row.FixedIncomeLimit, row.SharesLimit, row.RealEstateLimit,
             row.AlertThreshold, row.RebalanceTargetRatio));
        Assert.Equal(ProposalStatus.Full, row.ProposalStatus);
    }

    [Fact]
    public void From_Evaluation_MapsFourClassRowsWithUnroundedRatios()
    {
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));

        var classes = EvaluationRow.From(evaluation).Classes
            .Select(c => (c.EvaluationId, c.AssetClass, c.BookValue, c.Share, c.Utilization, c.Status, c.ProposedBookValue));

        Assert.Equal(
            [
                (evaluation.Id, AssetClass.Cash, 380_000m, 0.38m, 0.95m, ComplianceStatus.Warning, (decimal?)340_000m),
                // 0.2 / 0.3 keeps every decimal digit: nothing is rounded on the way to the row.
                (evaluation.Id, AssetClass.FixedIncome, 200_000m, 0.20m, 0.2m / 0.3m, ComplianceStatus.Compliant,
                 (decimal?)244_767.45m),
                (evaluation.Id, AssetClass.Shares, 260_000m, 0.26m, 1.04m, ComplianceStatus.Breached, (decimal?)212_500m),
                (evaluation.Id, AssetClass.RealEstate, 160_000m, 0.16m, 0.64m, ComplianceStatus.Compliant,
                 (decimal?)202_732.55m),
            ],
            classes);
    }
}
