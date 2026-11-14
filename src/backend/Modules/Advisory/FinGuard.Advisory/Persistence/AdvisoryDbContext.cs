using Microsoft.EntityFrameworkCore;

namespace FinGuard.Advisory.Persistence;

internal sealed class AdvisoryDbContext(DbContextOptions<AdvisoryDbContext> options) : DbContext(options)
{
    public const string Schema = "advisory";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
