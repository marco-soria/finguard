using Microsoft.EntityFrameworkCore;

namespace FinGuard.Portfolio.Persistence;

internal sealed class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : DbContext(options)
{
    public const string Schema = "portfolio";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
