using FinGuard.BuildingBlocks.Tenancy;
using Wolverine;

namespace FinGuard.BuildingBlocks.Messaging;

/// <summary>
/// Wolverine middleware applied to every handler (ADR-0009): sets the scoped <see cref="TenantContext"/> from the
/// envelope tenant id before the handler runs. Without a tenant id the context stays empty, so query filters match
/// nothing and the stamping interceptor refuses writes (fail closed).
/// </summary>
public static class TenantPropagationMiddleware
{
    private const string TenantIdPropertyName = "TenantId";

    // Takes ITenantContext, the type-based registration, so the generated handler shares this exact instance with the
    // handler and its DbContext; TenantContext itself is a forwarding factory registration (R-19).
    public static void Before(Envelope envelope, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var settable = tenantContext as TenantContext
            ?? throw new InvalidOperationException("The scoped ITenantContext must be a TenantContext.");

        if (string.IsNullOrEmpty(envelope.TenantId))
        {
            return;
        }

        if (!Guid.TryParse(envelope.TenantId, out var tenantId))
        {
            throw new InvalidOperationException("The envelope tenant id is not a GUID.");
        }

        // Defence in depth: an integration event carries its tenant (IntegrationEventConventions) and it must match.
        // Read by convention because Contracts projects cannot reference BuildingBlocks.
        if (envelope.Message?.GetType().GetProperty(TenantIdPropertyName) is { } property
            && property.PropertyType == typeof(Guid)
            && (Guid)property.GetValue(envelope.Message)! != tenantId)
        {
            throw new InvalidOperationException("The message tenant id differs from the envelope tenant id.");
        }

        settable.SetTenant(tenantId);
    }
}
