namespace FinGuard.Tenancy.Persistence;

/// <summary>
/// Maps an external identity (<c>iss</c> + <c>sub</c>) to its tenant. Not tenant-owned: it is the lookup that finds
/// the tenant (ADR-0004).
/// </summary>
internal sealed class User
{
    public const int IssuerMaxLength = 500;
    public const int SubjectMaxLength = 200;

    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public required string Issuer { get; init; }

    public required string Subject { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
