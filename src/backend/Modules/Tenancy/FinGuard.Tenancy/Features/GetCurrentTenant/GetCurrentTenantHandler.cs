using Dapper;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Npgsql;

namespace FinGuard.Tenancy.Features.GetCurrentTenant;

/// <summary>Query handler (ADR-0007): Dapper over the shared data source, own schema, tenant from the context.</summary>
public static class GetCurrentTenantHandler
{
    // tenancy.tenants is global, but filtering by the context's tenant means nobody can read another tenant's name.
    private const string Sql = """
        SELECT id AS "TenantId", name AS "TenantName"
        FROM tenancy.tenants
        WHERE id = @TenantId
        """;

    public static async Task<Result<CurrentTenantResponse>> Handle(
        GetCurrentTenant query, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var response = await connection.QuerySingleOrDefaultAsync<CurrentTenantResponse>(
            new CommandDefinition(Sql, new { TenantId = tenant.RequiredTenantId }, cancellationToken: ct));

        return response is null
            ? Error.NotFound("Tenancy.Tenant.NotFound", "The current tenant does not exist.")
            : response;
    }
}
