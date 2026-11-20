namespace FinGuard.Compliance.Domain;

/// <summary>An unrounded decimal ratio (a share, a limit, a utilization or a threshold). Round only for display.</summary>
public readonly record struct Ratio(decimal Value);
