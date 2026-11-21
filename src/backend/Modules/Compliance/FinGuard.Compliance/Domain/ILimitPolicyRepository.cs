namespace FinGuard.Compliance.Domain;

public interface ILimitPolicyRepository
{
    /// <summary>
    /// The version in force at the cut-off date: the highest <c>EffectiveFrom</c> on or before it (D-176).
    /// Null when the date is before the first version. Portfolio rejects such dates (D-190), so for an accepted
    /// submission null means old data or a bug (D-191).
    /// </summary>
    Task<LimitPolicy?> GetEffectiveAsync(DateOnly cutOffDate, CancellationToken cancellationToken);
}
