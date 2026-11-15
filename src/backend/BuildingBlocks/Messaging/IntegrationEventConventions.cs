namespace FinGuard.BuildingBlocks.Messaging;

/// <summary>
/// Integration event conventions (ADR-0009), enforced by <c>MessagingRulesTests</c>:
/// <list type="bullet">
/// <item>Integration events are <c>sealed record</c>s in <c>FinGuard.&lt;Module&gt;.Contracts.Events</c>.</item>
/// <item>Each carries <c>Guid EventId</c>, <c>Guid TenantId</c> and <c>DateTimeOffset OccurredAt</c>.</item>
/// <item>The payload <c>TenantId</c> must equal the envelope tenant; <see cref="TenantPropagationMiddleware"/>
/// rejects a mismatch.</item>
/// <item>Consumers are idempotent by <c>EventId</c>: they store it with a unique index and ignore duplicates.</item>
/// <item>Correlation comes from Wolverine and OpenTelemetry, not from the payload.</item>
/// </list>
/// </summary>
public static class IntegrationEventConventions
{
    public const string EventsNamespaceSuffix = ".Contracts.Events";
}
