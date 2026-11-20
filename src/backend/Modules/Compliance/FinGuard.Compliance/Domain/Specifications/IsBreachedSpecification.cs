namespace FinGuard.Compliance.Domain.Specifications;

/// <summary>Utilization above 1.00 (unrounded). Exactly 1.00 is still within the limit.</summary>
public sealed class IsBreachedSpecification : Specification<ClassUtilization>
{
    public override bool IsSatisfiedBy(ClassUtilization candidate) => candidate.Utilization.Value > 1m;
}
