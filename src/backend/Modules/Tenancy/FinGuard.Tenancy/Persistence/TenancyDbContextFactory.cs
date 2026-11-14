using FinGuard.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinGuard.Tenancy.Persistence;

// Used only by dotnet-ef at design time; the connection string is a placeholder and is never opened.
internal sealed class TenancyDbContextFactory : IDesignTimeDbContextFactory<TenancyDbContext>
{
    public TenancyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenancyDbContext>();
        options.UseModuleNpgsql(DesignTimeConnection.Placeholder, TenancyDbContext.Schema);
        return new TenancyDbContext(options.Options);
    }
}
