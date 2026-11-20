namespace FinGuard.Compliance.Domain;

/// <summary>The policy values an evaluation used, kept with it so it stays reproducible.</summary>
public sealed record PolicySnapshot(
    int Version, IReadOnlyDictionary<AssetClass, Ratio> Limits, Ratio AlertThreshold, Ratio RebalanceTargetRatio);
