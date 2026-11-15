using FinGuard.BuildingBlocks.Tenancy;
using Wolverine;

namespace FinGuard.BuildingBlocks.Messaging;

/// <summary>
/// The only way endpoints send commands (ADR-0009): the current tenant travels as the envelope tenant id, cascaded
/// messages inherit it and <see cref="TenantPropagationMiddleware"/> restores it before every handler.
/// </summary>
public static class MessageBusTenantExtensions
{
    public static Task InvokeForCurrentTenantAsync(
        this IMessageBus bus, ITenantContext tenant, object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(tenant);
        return bus.InvokeForTenantAsync(tenant.RequiredTenantId.ToString(), message, cancellationToken);
    }

    public static Task<T> InvokeForCurrentTenantAsync<T>(
        this IMessageBus bus, ITenantContext tenant, object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(tenant);
        return bus.InvokeForTenantAsync<T>(tenant.RequiredTenantId.ToString(), message, cancellationToken);
    }
}
