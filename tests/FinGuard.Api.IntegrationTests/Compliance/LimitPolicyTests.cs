using System.Globalization;
using Dapper;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Persistence;
using FinGuard.Tenancy.Development;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Compliance;

/// <summary>
/// Brief #13 (D-175 to D-177): the versioned global limit policy, its seed, CHECK constraints and repository.
/// Extra versions live in a transaction that is rolled back: the database is shared by the whole assembly.
/// </summary>
public sealed class LimitPolicyTests(ApiFixture fixture)
{
    private const string InsertPolicy =
        """
        INSERT INTO compliance.limit_policies
            (version, effective_from, cash, fixed_income, shares, real_estate, alert_threshold, rebalance_target_ratio,
             created_at)
        VALUES (@Version, @EffectiveFrom, @Cash, @FixedIncome, @Shares, @RealEstate, @AlertThreshold,
                @RebalanceTargetRatio, now())
        """;

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_Apply_SeedPolicyVersionOneExists()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var row = await connection.QuerySingleAsync<SeedRow>(new CommandDefinition(
            """
            SELECT version AS Version, effective_from AS EffectiveFrom, cash AS Cash, fixed_income AS FixedIncome,
                   shares AS Shares, real_estate AS RealEstate, alert_threshold AS AlertThreshold,
                   rebalance_target_ratio AS RebalanceTargetRatio
            FROM compliance.limit_policies WHERE version = 1
            """,
            cancellationToken: _ct));

        Assert.Equal(new SeedRow(1, new DateOnly(2000, 1, 1), 0.40m, 0.30m, 0.25m, 0.25m, 0.90m, 0.85m), row);
    }

    [Fact]
    public async Task GetEffective_CutOffBeforeFirstPolicy_ReturnsNull()
    {
        await using var db = CreateContext(DevIdentities.Alice.TenantId);

        var policy = await new LimitPolicyRepository(db).GetEffectiveAsync(new DateOnly(1999, 12, 31), _ct);

        Assert.Null(policy);
    }

    [Theory]
    [InlineData("2025-12-31", 1)]
    [InlineData("2026-01-01", 2)]
    [InlineData("2026-06-30", 2)]
    public async Task GetEffective_SecondVersionFrom2026_ReturnsVersionInForceAtCutOff(string cutOff, int expected)
    {
        await using var db = CreateContext(DevIdentities.Alice.TenantId);
        await using var transaction = await db.Database.BeginTransactionAsync(_ct);
        await InsertAsync(db, Valid with { Version = 2, EffectiveFrom = new DateOnly(2026, 1, 1) });

        var policy = await new LimitPolicyRepository(db).GetEffectiveAsync(DateOnly.Parse(cutOff, CultureInfo.InvariantCulture), _ct);

        Assert.Equal(expected, policy?.Version);
    }

    public static TheoryData<string, PolicyValues> InvalidRows() => new()
    {
        { "ck_limit_policies_cash", Valid with { Cash = 1.5m } },
        { "ck_limit_policies_fixed_income", Valid with { FixedIncome = 1.5m } },
        { "ck_limit_policies_shares", Valid with { Shares = 0m, Cash = 1m } },
        { "ck_limit_policies_real_estate", Valid with { RealEstate = 1.5m } },
        { "ck_limit_policies_alert_threshold", Valid with { AlertThreshold = 1.5m } },
        { "ck_limit_policies_rebalance_target_ratio", Valid with { RebalanceTargetRatio = 0.90m } },
        // 0.85 x 1.17 < 1.
        { "ck_limit_policies_limits_cover_target", Valid with { FixedIncome = 0.27m } },
    };

    [Theory]
    [MemberData(nameof(InvalidRows))]
    public async Task LimitPolicies_InvalidRow_IsRejectedByCheckConstraint(string constraint, PolicyValues row)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(_ct);
        await using var transaction = await connection.BeginTransactionAsync(_ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.ExecuteAsync(new CommandDefinition(InsertPolicy, row, transaction, cancellationToken: _ct)));

        Assert.Equal((PostgresErrorCodes.CheckViolation, constraint), (error.SqlState, error.ConstraintName));
    }

    [Fact]
    public async Task LimitPolicies_AreGlobal_SameForAliceAndBob()
    {
        var cutOff = new DateOnly(2026, 6, 30);
        await using var alice = CreateContext(DevIdentities.Alice.TenantId);
        await using var bob = CreateContext(DevIdentities.Bob.TenantId);

        var forAlice = await new LimitPolicyRepository(alice).GetEffectiveAsync(cutOff, _ct);
        var forBob = await new LimitPolicyRepository(bob).GetEffectiveAsync(cutOff, _ct);

        Assert.NotNull(forAlice);
        Assert.NotNull(forBob);
        Assert.Equal((1, 1), (forAlice.Version, forBob.Version));
        Assert.Equal(forAlice.Limits, forBob.Limits);
    }

    [Fact]
    public void ComplianceModule_Registers_LimitPolicyRepository()
    {
        using var scope = fixture.Factory.Services.CreateScope();

        Assert.IsType<LimitPolicyRepository>(scope.ServiceProvider.GetRequiredService<ILimitPolicyRepository>());
    }

    private static PolicyValues Valid => new(99, new DateOnly(2030, 1, 1), 0.40m, 0.30m, 0.25m, 0.25m, 0.90m, 0.85m);

    private ComplianceDbContext CreateContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<ComplianceDbContext>();
        options.UseModuleNpgsql(fixture.ConnectionString, ComplianceDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        return new ComplianceDbContext(options.Options, tenantContext);
    }

    // On the context's own connection and transaction, so the repository sees the row and the rollback removes it.
    private async Task InsertAsync(ComplianceDbContext db, PolicyValues row) =>
        await db.Database.GetDbConnection().ExecuteAsync(new CommandDefinition(
            InsertPolicy, row, db.Database.CurrentTransaction!.GetDbTransaction(), cancellationToken: _ct));

    public sealed record PolicyValues(
        int Version, DateOnly EffectiveFrom, decimal Cash, decimal FixedIncome, decimal Shares, decimal RealEstate,
        decimal AlertThreshold, decimal RebalanceTargetRatio);

    private sealed record SeedRow(
        int Version, DateOnly EffectiveFrom, decimal Cash, decimal FixedIncome, decimal Shares, decimal RealEstate,
        decimal AlertThreshold, decimal RebalanceTargetRatio);
}
