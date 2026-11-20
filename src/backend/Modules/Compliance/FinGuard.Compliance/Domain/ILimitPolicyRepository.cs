namespace FinGuard.Compliance.Domain;

public interface ILimitPolicyRepository
{
    /// <summary>
    /// The version in force at the cut-off date: the highest <c>EffectiveFrom</c> on or before it (D-176).
    /// Null when the date is before the first version; Portfolio accepts such dates, so callers must handle it.
    /// </summary>
    Task<LimitPolicy?> GetEffectiveAsync(DateOnly cutOffDate, CancellationToken cancellationToken);
}
