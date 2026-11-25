using FinGuard.Compliance.Domain.Specifications;

namespace FinGuard.Compliance.Domain;

public sealed record ClassResult(ClassUtilization Utilization, ComplianceStatus Status)
{
    /// <summary>
    /// The one place where a utilization becomes a status, used by <see cref="Evaluation"/> and by the rebalance
    /// engine's verification step. Unrounded comparisons: breached above 1.00, warning from the alert threshold.
    /// </summary>
    public static ClassResult For(ClassUtilization utilization, Ratio alertThreshold)
    {
        var breached = new IsBreachedSpecification();
        var warning = new IsInAlertSpecification(alertThreshold).And(breached.Not());

        var status = breached.IsSatisfiedBy(utilization)
            ? ComplianceStatus.Breached
            : warning.IsSatisfiedBy(utilization) ? ComplianceStatus.Warning : ComplianceStatus.Compliant;
        return new ClassResult(utilization, status);
    }
}
