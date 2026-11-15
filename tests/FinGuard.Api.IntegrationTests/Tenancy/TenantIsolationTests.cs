using Microsoft.EntityFrameworkCore;

namespace FinGuard.Api.IntegrationTests.Tenancy;

// Query filter and interceptor against real PostgreSQL. Each test uses fresh tenant ids and a unique marker, so tests
// sharing the probe table stay independent.
public sealed class TenantIsolationTests(ApiFixture fixture)
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly string _marker = Guid.NewGuid().ToString();

    [Fact]
    public async Task QueryFilter_TenantA_SeesOnlyOwnRows()
    {
        await SeedOneRowPerTenantAsync();

        var rows = await ReadMarkedRowsAsync(_tenantA);

        var row = Assert.Single(rows);
        Assert.Equal(_tenantA, row.TenantId);
    }

    [Fact]
    public async Task QueryFilter_TenantB_SeesOnlyOwnRows()
    {
        await SeedOneRowPerTenantAsync();

        var rows = await ReadMarkedRowsAsync(_tenantB);

        var row = Assert.Single(rows);
        Assert.Equal(_tenantB, row.TenantId);
    }

    [Fact]
    public async Task QueryFilter_NoTenant_SeesNoRows()
    {
        await SeedOneRowPerTenantAsync();
        await using var db = fixture.CreateProbeContext(tenantId: null);

        var rows = await db.Items.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task QueryFilter_SequentialContextsWithDifferentTenants_DoNotLeak()
    {
        await SeedOneRowPerTenantAsync();

        // Same context type, so both share one cached EF model; each must still use its own tenant.
        var first = await ReadMarkedRowsAsync(_tenantA);
        var second = await ReadMarkedRowsAsync(_tenantB);
        var third = await ReadMarkedRowsAsync(_tenantA);

        Assert.Equal(_tenantA, Assert.Single(first).TenantId);
        Assert.Equal(_tenantB, Assert.Single(second).TenantId);
        Assert.Equal(_tenantA, Assert.Single(third).TenantId);
    }

    [Fact]
    public async Task SaveChanges_AddedWithoutTenantId_StampsCurrentTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var db = fixture.CreateProbeContext(_tenantA))
        {
            db.Items.Add(ProbeItem.Create(_marker));
            await db.SaveChangesAsync(cancellationToken);
        }

        var row = Assert.Single(await ReadMarkedRowsAsync(_tenantA));

        Assert.Equal(_tenantA, row.TenantId);
    }

    [Fact]
    public async Task SaveChanges_NoTenant_Throws()
    {
        await using var db = fixture.CreateProbeContext(tenantId: null);
        db.Items.Add(ProbeItem.Create(_marker));

        // Synchronous SaveChanges: the async path is covered by the other interceptor tests.
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task SaveChanges_AddedWithOtherTenantId_Throws()
    {
        await using var db = fixture.CreateProbeContext(_tenantA);
        db.Items.Add(ProbeItem.Create(_marker, _tenantB));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChanges_TenantIdModified_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedOneRowPerTenantAsync();
        await using var db = fixture.CreateProbeContext(_tenantA);
        var item = await db.Items.SingleAsync(i => i.Marker == _marker, cancellationToken);

        db.Entry(item).Property(i => i.TenantId).CurrentValue = _tenantB;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(cancellationToken));
    }

    private async Task SeedOneRowPerTenantAsync()
    {
        foreach (var tenant in new[] { _tenantA, _tenantB })
        {
            await using var db = fixture.CreateProbeContext(tenant);
            db.Items.Add(ProbeItem.Create(_marker));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<List<ProbeItem>> ReadMarkedRowsAsync(Guid tenantId)
    {
        await using var db = fixture.CreateProbeContext(tenantId);
        return await db.Items.AsNoTracking()
            .Where(i => i.Marker == _marker)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
