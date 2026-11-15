namespace FinGuard.Tenancy.Development;

/// <summary>A seeded development identity and its tenant (D-64).</summary>
public sealed record DevIdentity(string User, string Subject, string Name, Guid TenantId, string TenantName);

/// <summary>Development-only identities served by the in-process token issuer (ADR-0008).</summary>
public static class DevIdentities
{
    public const string Issuer = "finguard-dev";
    public const string Audience = "finguard-api";

    public static DevIdentity Alice { get; } = new(
        "alice", "0b9f6a52-3d1e-4c7a-9a43-2f1c5e8d7b10", "Alice",
        new Guid("6f1d2c3b-8a47-4e59-b0c1-1a2b3c4d5e01"), "Acme Pension Fund");

    public static DevIdentity Bob { get; } = new(
        "bob", "5c2e8d41-7f3a-4b6e-8d19-9e0a1b2c3d20", "Bob",
        new Guid("6f1d2c3b-8a47-4e59-b0c1-1a2b3c4d5e02"), "Globex Insurance");

    public static IReadOnlyList<DevIdentity> All { get; } = [Alice, Bob];

    public static DevIdentity? Find(string? user) =>
        All.FirstOrDefault(i => string.Equals(i.User, user, StringComparison.Ordinal));
}
