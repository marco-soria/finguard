using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.UnitTests.Tenancy;

public sealed class TenantContextTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public void SetTenant_FirstCall_SetsTenant()
    {
        var context = new TenantContext();

        context.SetTenant(TenantA);

        Assert.True(context.HasTenant);
        Assert.Equal(TenantA, context.TenantId);
        Assert.Equal(TenantA, context.RequiredTenantId);
    }

    [Fact]
    public void SetTenant_SameIdTwice_IsNoOp()
    {
        var context = new TenantContext();
        context.SetTenant(TenantA);

        context.SetTenant(TenantA);

        Assert.Equal(TenantA, context.TenantId);
    }

    [Fact]
    public void SetTenant_DifferentId_Throws()
    {
        var context = new TenantContext();
        context.SetTenant(TenantA);

        Assert.Throws<InvalidOperationException>(() => context.SetTenant(TenantB));
        Assert.Equal(TenantA, context.TenantId);
    }

    [Fact]
    public void RequiredTenantId_NoTenant_Throws()
    {
        var context = new TenantContext();

        Assert.False(context.HasTenant);
        Assert.Throws<InvalidOperationException>(() => context.RequiredTenantId);
    }
}
