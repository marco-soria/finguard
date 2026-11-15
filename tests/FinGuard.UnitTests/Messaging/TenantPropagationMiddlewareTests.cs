using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Tenancy;
using Wolverine;

namespace FinGuard.UnitTests.Messaging;

public sealed class TenantPropagationMiddlewareTests
{
    private static readonly Guid Tenant = Guid.Parse("8c1a3f0e-2f4b-4d6e-9a1b-0c2d3e4f5a6b");

    [Fact]
    public void Before_GuidTenantId_SetsTenant()
    {
        var context = new TenantContext();

        TenantPropagationMiddleware.Before(new Envelope(new Command()) { TenantId = Tenant.ToString() }, context);

        Assert.Equal(Tenant, context.TenantId);
    }

    [Fact]
    public void Before_NoTenantId_LeavesContextEmpty()
    {
        var context = new TenantContext();

        TenantPropagationMiddleware.Before(new Envelope(new Command()), context);

        Assert.False(context.HasTenant);
    }

    [Fact]
    public void Before_NonGuidTenantId_Throws()
    {
        var context = new TenantContext();

        Assert.Throws<InvalidOperationException>(() =>
            TenantPropagationMiddleware.Before(new Envelope(new Command()) { TenantId = "acme" }, context));
        Assert.False(context.HasTenant);
    }

    [Fact]
    public void Before_PayloadTenantDiffersFromEnvelope_Throws()
    {
        var context = new TenantContext();
        var envelope = new Envelope(new Event(Guid.NewGuid())) { TenantId = Tenant.ToString() };

        Assert.Throws<InvalidOperationException>(() => TenantPropagationMiddleware.Before(envelope, context));
        Assert.False(context.HasTenant);
    }

    [Fact]
    public void Before_PayloadTenantMatchesEnvelope_SetsTenant()
    {
        var context = new TenantContext();

        TenantPropagationMiddleware.Before(new Envelope(new Event(Tenant)) { TenantId = Tenant.ToString() }, context);

        Assert.Equal(Tenant, context.TenantId);
    }

    [Fact]
    public void Before_ContextIsNotTenantContext_Throws() =>
        Assert.Throws<InvalidOperationException>(() => TenantPropagationMiddleware.Before(
            new Envelope(new Command()) { TenantId = Tenant.ToString() }, NoTenantContext.Instance));

    private sealed record Command;

    private sealed record Event(Guid TenantId);
}
