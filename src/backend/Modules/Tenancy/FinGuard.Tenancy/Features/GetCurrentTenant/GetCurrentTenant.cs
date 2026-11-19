namespace FinGuard.Tenancy.Features.GetCurrentTenant;

/// <summary>Reads the caller's tenant. Carries nothing: the tenant comes from the envelope (ADR-0009).</summary>
public sealed record GetCurrentTenant
{
    // Identifiers only in logs (R-21, D-77).
    public override string ToString() => $"{nameof(GetCurrentTenant)} {{ }}";
}
