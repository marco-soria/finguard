using Microsoft.EntityFrameworkCore;

namespace FinGuard.Tenancy.Persistence;

internal sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options) : DbContext(options)
{
    public const string Schema = "tenancy";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
