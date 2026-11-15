using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinGuard.Compliance.Persistence;

// Used only by dotnet-ef at design time; the connection string is a placeholder and is never opened.
internal sealed class ComplianceDbContextFactory : IDesignTimeDbContextFactory<ComplianceDbContext>
{
    public ComplianceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>();
        options.UseModuleNpgsql(DesignTimeConnection.Placeholder, ComplianceDbContext.Schema);
        return new ComplianceDbContext(options.Options, NoTenantContext.Instance);
    }
}
