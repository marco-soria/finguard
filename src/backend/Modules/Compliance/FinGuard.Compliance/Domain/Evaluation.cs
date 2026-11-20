using FinGuard.Compliance.Domain.Specifications;

namespace FinGuard.Compliance.Domain;

/// <summary>
/// The result of checking one portfolio against one policy version. Immutable after creation; it keeps a snapshot of
/// the policy so it stays reproducible.
/// </summary>
public sealed class Evaluation
{
    private Evaluation(
        Guid submissionId, DateOnly cutOffDate, DateTimeOffset evaluatedAt, Money total,
        IReadOnlyList<ClassResult> classes, PolicySnapshot policy)
    {
        Id = Guid.CreateVersion7(evaluatedAt);
        SubmissionId = submissionId;
        CutOffDate = cutOffDate;
        EvaluatedAt = evaluatedAt;
        Total = total;
        Classes = classes;
        Policy = policy;
        // ComplianceStatus is ordered by precedence: breached > warning > compliant.
        OverallStatus = classes.Max(c => c.Status);
        Alerts = classes
            .Where(c => c.Status != ComplianceStatus.Compliant)
            .Select(c => new Alert(c.Utilization.Class, c.Status, c.Utilization.Utilization))
            .ToList();
    }

    public Guid Id { get; }

    public Guid SubmissionId { get; }

    public DateOnly CutOffDate { get; }

    public DateTimeOffset EvaluatedAt { get; }

    public Money Total { get; }

    /// <summary>One result per <see cref="AssetClass"/>, in enum order.</summary>
    public IReadOnlyList<ClassResult> Classes { get; }

    public ComplianceStatus OverallStatus { get; }

    public IReadOnlyList<Alert> Alerts { get; }

    public PolicySnapshot Policy { get; }

    /// <summary>
    /// Inputs come from an accepted submission, so a missing class or a zero total is a bug upstream, not a business
    /// outcome: both throw <see cref="ArgumentException"/> (D-170).
    /// </summary>
    public static Evaluation Create(
        Guid submissionId, DateOnly cutOffDate, IReadOnlyDictionary<AssetClass, Money> classValues, LimitPolicy policy,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(classValues);
        ArgumentNullException.ThrowIfNull(policy);

        var classes = Enum.GetValues<AssetClass>();
        if (classValues.Count != classes.Length || classes.Any(c => !classValues.ContainsKey(c)))
        {
            throw new ArgumentException("Every asset class needs exactly one value.", nameof(classValues));
        }

        var total = new Money(classValues.Values.Sum(v => v.Amount));
        if (total.Amount == 0m)
        {
            throw new ArgumentException("The portfolio total must be greater than zero.", nameof(classValues));
        }

        var breached = new IsBreachedSpecification();
        var warning = new IsInAlertSpecification(policy.AlertThreshold).And(breached.Not());
        var results = classes
            .Select(c => ClassUtilization.Calculate(c, classValues[c], total, policy.Limits[c]))
            .Select(u => new ClassResult(u, StatusOf(u, breached, warning)))
            .ToList();

        return new Evaluation(submissionId, cutOffDate, evaluatedAt, total, results, policy.ToSnapshot());
    }

    // D-162: identifiers only.
    public override string ToString() =>
        $"{nameof(Evaluation)} {{ Id = {Id}, SubmissionId = {SubmissionId}, PolicyVersion = {Policy.Version} }}";

    private static ComplianceStatus StatusOf(
        ClassUtilization utilization, Specification<ClassUtilization> breached, Specification<ClassUtilization> warning)
    {
        if (breached.IsSatisfiedBy(utilization))
        {
            return ComplianceStatus.Breached;
        }

        return warning.IsSatisfiedBy(utilization) ? ComplianceStatus.Warning : ComplianceStatus.Compliant;
    }
}
