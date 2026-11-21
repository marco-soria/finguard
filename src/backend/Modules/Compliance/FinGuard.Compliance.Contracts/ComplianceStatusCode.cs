namespace FinGuard.Compliance.Contracts;

/// <summary>
/// Status of a class or of a whole evaluation, ordered by precedence: a higher value wins (breached &gt; warning &gt;
/// compliant). Consumers show it and never recompute it. Additive changes only.
/// </summary>
public enum ComplianceStatusCode
{
    Compliant,
    Warning,
    Breached,
}
