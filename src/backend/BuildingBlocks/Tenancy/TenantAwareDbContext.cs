using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.BuildingBlocks.Tenancy;

/// <summary>
/// Base DbContext for modules with tenant-owned data. Every <see cref="ITenantOwned"/> entity gets the named query
/// filter <see cref="TenantQueryFilter.Name"/>; without a tenant the filter matches nothing (fail closed).
/// </summary>
public abstract class TenantAwareDbContext(DbContextOptions options, ITenantContext tenantContext) : DbContext(options)
{
    private static readonly System.Reflection.PropertyInfo CurrentTenantIdProperty =
        typeof(TenantAwareDbContext).GetProperty(nameof(CurrentTenantId),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    protected Guid? CurrentTenantId => tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => t.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            entityType.SetQueryFilter(TenantQueryFilter.Name, BuildFilter(entityType.ClrType));
        }
    }

    // e => this.CurrentTenantId != null && (Guid?)e.TenantId == this.CurrentTenantId
    // The filter reads this instance's property, so EF parameterizes it per context instead of baking a value
    // into the cached model. No `.Value`: EF evaluates parameters client-side and it would throw without a tenant.
    private LambdaExpression BuildFilter(Type entityClrType)
    {
        var entity = Expression.Parameter(entityClrType, "e");
        var current = Expression.Property(Expression.Constant(this), CurrentTenantIdProperty);
        var entityTenantId = Expression.Convert(
            Expression.Property(entity, nameof(ITenantOwned.TenantId)), typeof(Guid?));

        var body = Expression.AndAlso(
            Expression.NotEqual(current, Expression.Constant(null, typeof(Guid?))),
            Expression.Equal(entityTenantId, current));

        return Expression.Lambda(body, entity);
    }
}
