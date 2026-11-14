using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FinGuard.Api.IntegrationTests;

/// <summary>Test-only route that throws. Added after the Api pipeline so the Api's exception handler wraps it.</summary>
internal sealed class ThrowingEndpointStartupFilter : IStartupFilter
{
    public const string Path = "/__tests/throw";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
            context.Request.Path == Path
                ? throw new InvalidOperationException(context.Request.Query["message"])
                : nextMiddleware(context));
    };
}
