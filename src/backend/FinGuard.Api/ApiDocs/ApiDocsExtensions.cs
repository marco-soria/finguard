using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace FinGuard.Api.ApiDocs;

/// <summary>OpenAPI 3.1 document <c>v1</c> rendered with Scalar (D-303). Mapped only when <c>Api:Docs:Enabled</c> is true.</summary>
internal static class ApiDocsExtensions
{
    public const string DocumentName = "v1";
    public const string EnabledKey = "Api:Docs:Enabled";
    public const string BearerScheme = "Bearer";

    public static IHostApplicationBuilder AddFinGuardApiDocs(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(DocumentName, options => options.AddDocumentTransformer(DescribeDocument));
        return builder;
    }

    public static WebApplication MapFinGuardApiDocs(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(EnabledKey))
        {
            return app;
        }

        // Anonymous so the docs open in a browser; the operations themselves still require a token.
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options
                .WithTitle("FinGuard API")
                .AddPreferredSecuritySchemes(BearerScheme))
            .AllowAnonymous();
        return app;
    }

    private static Task DescribeDocument(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = "FinGuard API",
            Version = DocumentName,
            Description = "Upload an investment portfolio at a cut-off date, evaluate it against the allocation limits " +
                "and read the rebalancing advisory. Every call needs a bearer token; the tenant comes from the token.",
        };
        // Generated from the test host's address; the docs are served from the API itself, so no server list is needed.
        document.Servers = [];

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token. In Development, POST /dev/token with {\"user\":\"alice\"} or {\"user\":\"bob\"}.",
        };
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] }];
        return Task.CompletedTask;
    }
}
