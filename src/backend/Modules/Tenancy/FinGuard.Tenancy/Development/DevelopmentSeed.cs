using FinGuard.Tenancy.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinGuard.Tenancy.Development;

public static class DevelopmentSeed
{
    /// <summary>Inserts the development tenants and users that are missing. Development only; safe to run again.</summary>
    public static async Task SeedDevelopmentTenantsAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        foreach (var identity in DevIdentities.All)
        {
            if (!await db.Tenants.AnyAsync(t => t.Id == identity.TenantId, cancellationToken))
            {
                db.Tenants.Add(new Tenant { Id = identity.TenantId, Name = identity.TenantName, CreatedAt = now });
            }

            if (!await db.Users.AnyAsync(
                    u => u.Issuer == DevIdentities.Issuer && u.Subject == identity.Subject, cancellationToken))
            {
                db.Users.Add(new User
                {
                    Id = Guid.CreateVersion7(now),
                    TenantId = identity.TenantId,
                    Issuer = DevIdentities.Issuer,
                    Subject = identity.Subject,
                    CreatedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
