using Microsoft.EntityFrameworkCore;

namespace FinGuard.Tenancy.Persistence;

// Plain DbContext on purpose: tenants and users are global tables, not tenant-owned (ADR-0004).
internal sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options) : DbContext(options)
{
    public const string Schema = "tenancy";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Tenant>(tenant =>
        {
            tenant.ToTable("tenants");
            tenant.Property(t => t.Name).HasMaxLength(Tenant.NameMaxLength);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Issuer).HasMaxLength(User.IssuerMaxLength);
            user.Property(u => u.Subject).HasMaxLength(User.SubjectMaxLength);
            user.HasIndex(u => new { u.Issuer, u.Subject }).IsUnique();
            user.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId);
        });
    }
}
