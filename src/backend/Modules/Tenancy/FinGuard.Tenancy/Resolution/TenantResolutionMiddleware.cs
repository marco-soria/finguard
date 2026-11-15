using Dapper;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FinGuard.Tenancy.Resolution;

/// <summary>
/// Maps the authenticated identity (<c>iss</c> + <c>sub</c>) to its tenant. The tenant never comes from the request
/// body, route, query string or headers (AGENTS.md rule 7). Unknown identities leave the context empty.
/// </summary>
internal sealed partial class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    // The only query without a tenant_id filter: it is how the tenant is found (ADR-0004, D-63).
    private const string FindTenantSql =
        "SELECT tenant_id FROM tenancy.users WHERE issuer = @Issuer AND subject = @Subject";

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, NpgsqlDataSource dataSource)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && user.FindFirst("iss")?.Value is { Length: > 0 } issuer
            && user.FindFirst("sub")?.Value is { Length: > 0 } subject)
        {
            await using var connection = await dataSource.OpenConnectionAsync(context.RequestAborted);
            var tenantId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                FindTenantSql, new { Issuer = issuer, Subject = subject }, cancellationToken: context.RequestAborted));

            if (tenantId is { } id)
            {
                tenantContext.SetTenant(id);
                LogResolved(logger, id);
            }
            else
            {
                LogNotProvisioned(logger);
            }
        }

        await next(context);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved tenant {TenantId}")]
    private static partial void LogResolved(ILogger logger, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Authenticated identity is not provisioned to a tenant")]
    private static partial void LogNotProvisioned(ILogger logger);
}
