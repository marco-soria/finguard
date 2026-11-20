namespace FinGuard.Compliance.Domain;

/// <summary>Status of a class or of a whole evaluation. Ordered by precedence: a higher value wins (D-173).</summary>
public enum ComplianceStatus
{
    Compliant,
    Warning,
    Breached,
}
