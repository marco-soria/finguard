using FinGuard.Tenancy.Development;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace FinGuard.Api.DevAuth;

internal static class AuthenticationSetup
{
    private const string AuthorityKey = "Authentication:Schemes:Bearer:Authority";

    /// <summary>
    /// JWT bearer authentication (D-59, D-60) and a fallback policy that requires an authenticated user (D-61).
    /// Development validates tokens from <see cref="DevTokenIssuer"/>; every other environment binds
    /// <c>Authentication:Schemes:Bearer</c> and fails fast without an <c>Authority</c>.
    /// </summary>
    public static WebApplicationBuilder AddFinGuardAuthentication(this WebApplicationBuilder builder)
    {
        var authentication = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

        if (builder.Environment.IsDevelopment())
        {
            var issuer = new DevTokenIssuer();
            builder.Services.AddSingleton(issuer);
            authentication.AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = DevIdentities.Issuer,
                    ValidAudience = DevIdentities.Audience,
                    IssuerSigningKey = issuer.SigningKey,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(builder.Configuration[AuthorityKey]))
            {
                throw new InvalidOperationException(
                    $"'{AuthorityKey}' is missing. Outside Development the Api needs an external token authority.");
            }

            // Authority and Audience bind from Authentication:Schemes:Bearer.
            authentication.AddJwtBearer(options => options.MapInboundClaims = false);
        }

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }
}
