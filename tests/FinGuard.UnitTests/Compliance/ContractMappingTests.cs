using FinGuard.Compliance.Contracts;
using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Features.EvaluatePortfolio;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Compliance;

public sealed class ContractMappingTests
{
    [Fact]
    public void ToContract_EveryAssetClass_MapsToTheCodeWithTheSameName()
    {
        var mapped = Enum.GetValues<AssetClass>().Select(c => ContractMapping.ToContract(c).ToString());

        Assert.Equal(Enum.GetNames<AssetClassCode>(), mapped);
    }

    [Fact]
    public void ToContract_EveryStatus_MapsToTheCodeWithTheSameNameAndPrecedence()
    {
        var mapped = Enum.GetValues<ComplianceStatus>().Select(ContractMapping.ToContract).ToList();

        Assert.Equal(Enum.GetValues<ComplianceStatusCode>(), mapped);
        Assert.Equal(Enum.GetNames<ComplianceStatusCode>(), mapped.Select(s => s.ToString()));
    }

    [Fact]
    public void ToContract_UndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractMapping.ToContract((AssetClass)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractMapping.ToContract((ComplianceStatus)99));
    }

    [Fact]
    public void ToEvent_BreachedEvaluation_CarriesIdsStatusPolicyAndFourClassesInOrder()
    {
        // cash 0.95 (warning), fixed income 0.6667, shares 1.04 (breached), real estate 0.64.
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));
        var tenantId = Guid.NewGuid();

        var @event = ContractMapping.ToEvent(evaluation, tenantId);

        Assert.Equal(
            (tenantId, evaluation.EvaluatedAt, evaluation.Id, evaluation.SubmissionId, evaluation.CutOffDate,
             ComplianceStatusCode.Breached, 1),
            (@event.TenantId, @event.OccurredAt, @event.EvaluationId, @event.SubmissionId, @event.CutOffDate,
             @event.OverallStatus, @event.PolicyVersion));
        Assert.NotEqual(Guid.Empty, @event.EventId);
        Assert.Equal(
            [
                new ClassEvaluation(AssetClassCode.Cash, 380_000m, 0.40m, 0.95m, ComplianceStatusCode.Warning),
                new ClassEvaluation(
                    AssetClassCode.FixedIncome, 200_000m, 0.30m, evaluation.Classes[1].Utilization.Utilization.Value,
                    ComplianceStatusCode.Compliant),
                new ClassEvaluation(AssetClassCode.Shares, 260_000m, 0.25m, 1.04m, ComplianceStatusCode.Breached),
                new ClassEvaluation(AssetClassCode.RealEstate, 160_000m, 0.25m, 0.64m, ComplianceStatusCode.Compliant),
            ],
            @event.Classes);
    }
}
