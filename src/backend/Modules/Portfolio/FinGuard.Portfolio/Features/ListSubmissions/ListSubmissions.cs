namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>Lists the newest submissions of the envelope's tenant (ADR-0009, D-100). Carries nothing.</summary>
public sealed record ListSubmissions
{
    // Identifiers only in logs (R-21, D-77).
    public override string ToString() => $"{nameof(ListSubmissions)} {{ }}";
}
