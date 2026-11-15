using Dapper;
using FinGuard.Tenancy.Development;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Tenancy;

public sealed class DevelopmentSeedTests(ApiFixture fixture)
{
    [Fact]
    public async Task SeedDevelopmentTenants_RunTwice_KeepsTwoTenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Startup already seeded once; two more runs must change nothing.
        await fixture.Factory.Services.SeedDevelopmentTenantsAsync(cancellationToken);
        await fixture.Factory.Services.SeedDevelopmentTenantsAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var tenants = (await connection.QueryAsync<string>("SELECT name FROM tenancy.tenants ORDER BY name")).ToList();
        var users = await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM tenancy.users");

        Assert.Equal(["Acme Pension Fund", "Globex Insurance"], tenants);
        Assert.Equal(2, users);
    }
}
