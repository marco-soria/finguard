namespace FinGuard.Compliance.Domain.Specifications;

/// <summary>Utilization at or above the alert threshold (unrounded).</summary>
public sealed class IsInAlertSpecification(Ratio threshold) : Specification<ClassUtilization>
{
    public override bool IsSatisfiedBy(ClassUtilization candidate) => candidate.Utilization.Value >= threshold.Value;
}
