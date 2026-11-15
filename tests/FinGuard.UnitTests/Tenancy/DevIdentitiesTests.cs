using FinGuard.Tenancy.Development;

namespace FinGuard.UnitTests.Tenancy;

public sealed class DevIdentitiesTests
{
    [Fact]
    public void DevIdentities_All_HaveUniqueSubjectsAndTenants()
    {
        var all = DevIdentities.All;

        Assert.Equal(2, all.Count);
        Assert.Equal(all.Count, all.Select(i => i.User).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(all.Count, all.Select(i => i.Subject).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(all.Count, all.Select(i => i.TenantId).Distinct().Count());
        Assert.DoesNotContain(all, i => i.TenantId == Guid.Empty);
    }
}
