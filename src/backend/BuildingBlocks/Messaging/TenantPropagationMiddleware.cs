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

    public static void Before(Envelope envelope, TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(tenantContext);

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

        tenantContext.SetTenant(tenantId);
    }
}
