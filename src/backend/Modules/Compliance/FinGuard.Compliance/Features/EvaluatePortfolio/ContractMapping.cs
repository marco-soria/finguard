using FinGuard.Compliance.Contracts;
using FinGuard.Compliance.Contracts.Events;
using FinGuard.Compliance.Domain;

namespace FinGuard.Compliance.Features.EvaluatePortfolio;

/// <summary>The one place where domain types become contract types (D-172, D-196).</summary>
internal static class ContractMapping
{
    public static EvaluationCompleted ToEvent(Evaluation evaluation, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        return new EvaluationCompleted(
            Guid.CreateVersion7(evaluation.EvaluatedAt), tenantId, evaluation.EvaluatedAt, evaluation.Id,
            evaluation.SubmissionId, evaluation.CutOffDate, ToContract(evaluation.OverallStatus), evaluation.Policy.Version,
            evaluation.Classes
                .Select(c => new ClassEvaluation(
                    ToContract(c.Utilization.Class), c.Utilization.Value.Amount, c.Utilization.Limit.Value,
                    c.Utilization.Utilization.Value, ToContract(c.Status)))
                .ToList());
    }

    public static AssetClassCode ToContract(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Cash => AssetClassCode.Cash,
        AssetClass.FixedIncome => AssetClassCode.FixedIncome,
        AssetClass.Shares => AssetClassCode.Shares,
        AssetClass.RealEstate => AssetClassCode.RealEstate,
        _ => throw new ArgumentOutOfRangeException(nameof(assetClass)),
    };

    public static ComplianceStatusCode ToContract(ComplianceStatus status) => status switch
    {
        ComplianceStatus.Compliant => ComplianceStatusCode.Compliant,
        ComplianceStatus.Warning => ComplianceStatusCode.Warning,
        ComplianceStatus.Breached => ComplianceStatusCode.Breached,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
