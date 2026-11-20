namespace FinGuard.Compliance.Domain;

/// <summary>Raised for every class whose status is <see cref="ComplianceStatus.Warning"/> or <see cref="ComplianceStatus.Breached"/>.</summary>
public sealed record Alert(AssetClass Class, ComplianceStatus Status, Ratio Utilization);
