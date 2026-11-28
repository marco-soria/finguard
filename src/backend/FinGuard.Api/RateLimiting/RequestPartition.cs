using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Api.RateLimiting;

/// <summary>
/// The counter a request is charged to: its tenant when the Tenancy middleware set one, otherwise its client IP (the
/// forwarded one when forwarded headers are on). <see cref="Kind"/> is safe to log; <see cref="Key"/> is not.
/// </summary>
public sealed record RequestPartition(string Kind, string Key)
{
    public const string TenantKind = "tenant";
    public const string IpKind = "ip";

    public static RequestPartition For(HttpContext context)
    {
        var tenant = context.RequestServices.GetService<ITenantContext>();
        if (tenant?.TenantId is { } tenantId)
        {
            return new RequestPartition(TenantKind, $"{TenantKind}:{tenantId}");
        }

        // No address (in-process test server): every such caller shares one partition.
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return new RequestPartition(IpKind, $"{IpKind}:{ip}");
    }
}
