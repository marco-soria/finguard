using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.UnitTests.Tenancy;

public sealed class TenantStampingInterceptorTests
{
    [Fact]
    public async Task SaveChanges_TenantOwnedEntityInPlainDbContext_Throws()
    {
        // The interceptor runs before EF opens a connection, so the placeholder is never used.
        var options = new DbContextOptionsBuilder<PlainContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .AddInterceptors(TenantStampingInterceptor.Instance)
            .Options;
        await using var context = new PlainContext(options);
        context.Add(new Owned { Id = Guid.NewGuid() });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Tenant-owned entities must be saved through a TenantAwareDbContext.", exception.Message);
    }

    private sealed class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Owned>();
    }

    private sealed class Owned : ITenantOwned
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }
    }
}
