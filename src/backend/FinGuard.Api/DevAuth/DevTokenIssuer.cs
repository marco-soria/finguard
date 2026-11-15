using System.Security.Claims;
using System.Security.Cryptography;
using FinGuard.Tenancy.Development;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinGuard.Api.DevAuth;

/// <summary>
/// Development-only token issuer (ADR-0008). The HMAC key is random per process and never written anywhere, so tokens
/// stop working when the Api restarts.
/// </summary>
public sealed class DevTokenIssuer
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly JsonWebTokenHandler _handler = new();

    public SecurityKey SigningKey { get; } = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));

    public DevToken CreateToken(DevIdentity identity, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var expiresAt = issuedAt + Lifetime;
        var accessToken = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = DevIdentities.Issuer,
            Audience = DevIdentities.Audience,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, identity.Subject),
                new Claim(JwtRegisteredClaimNames.Name, identity.Name),
            ]),
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        });

        return new DevToken(accessToken, expiresAt);
    }
}

public sealed record DevToken(string AccessToken, DateTimeOffset ExpiresAt);
