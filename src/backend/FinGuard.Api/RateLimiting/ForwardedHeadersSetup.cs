using Microsoft.AspNetCore.HttpOverrides;

namespace FinGuard.Api.RateLimiting;

/// <summary>
/// Reads the client IP and scheme from <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> when
/// <c>ForwardedHeaders:Enabled</c> is true (off in Development, where no proxy exists; ADR-0016, D-316).
/// </summary>
internal static class ForwardedHeadersSetup
{
    public const string EnabledKey = "ForwardedHeaders:Enabled";

    public static IHostApplicationBuilder AddFinGuardForwardedHeaders(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Only the entry the platform proxy appended: anything further left was written by the client.
            options.ForwardLimit = 1;
            // Trust assumption: the Api is reachable ONLY through the platform proxy (Container Apps ingress in Phase 5,
            // Traefik in Phase 6), whose addresses are not fixed. Clearing the lists trusts any peer, which Microsoft
            // warns is unsafe otherwise: if the Api ever gets a directly reachable address, list the proxy here instead.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return builder;
    }

    /// <summary>First in the pipeline, so every later middleware (rate limiting included) sees the client IP.</summary>
    public static WebApplication UseFinGuardForwardedHeaders(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(EnabledKey))
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
