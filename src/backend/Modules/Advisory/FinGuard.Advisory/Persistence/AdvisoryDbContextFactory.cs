using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinGuard.Advisory.Persistence;

// Used only by dotnet-ef at design time; the connection string is a placeholder and is never opened.
internal sealed class AdvisoryDbContextFactory : IDesignTimeDbContextFactory<AdvisoryDbContext>
{
    public AdvisoryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdvisoryDbContext>();
        options.UseModuleNpgsql(DesignTimeConnection.Placeholder, AdvisoryDbContext.Schema);
        return new AdvisoryDbContext(options.Options, NoTenantContext.Instance);
    }
}
