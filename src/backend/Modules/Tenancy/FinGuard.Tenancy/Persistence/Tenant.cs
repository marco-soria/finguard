namespace FinGuard.Tenancy.Persistence;

internal sealed class Tenant
{
    public const int NameMaxLength = 200;

    public Guid Id { get; init; }

    public required string Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
