using System.Reflection;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static FinGuard.ArchitectureTests.FinGuardAssemblies;

namespace FinGuard.ArchitectureTests;

// Tenant isolation rules (ADR-0004, D-62, D-63).
public sealed class TenancyRulesTests
{
    private static readonly Assembly TenancyAssembly = typeof(TenancyModule).Assembly;

    [Fact]
    public void TenantContext_OutsideBuildingBlocksAndTenancy_IsNotUsed()
    {
        var rule = Types().That().DoNotResideInAssembly(BuildingBlocksAssembly, TenancyAssembly)
            .Should().NotDependOnAny(typeof(TenantContext))
            .Because("only the Tenancy module sets the tenant");

        var violations = rule.Evaluate(Architecture).Where(r => !r.Passed).Select(r => r.Description).ToList();

        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Fact]
    public void ModuleDbContexts_ExceptTenancy_DeriveFromTenantAwareDbContext()
    {
        var violations = Modules.Values
            .Where(m => m != TenancyAssembly)
            .SelectMany(m => m.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(DbContext)) && !t.IsSubclassOf(typeof(TenantAwareDbContext)))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Module DbContexts must derive from {nameof(TenantAwareDbContext)}: {string.Join(", ", violations)}");
    }

    [Fact]
    public void TenantOwnedEntities_AnyModuleModel_AreMappedOnlyByTenantAwareContexts()
    {
        var violations = new List<string>();
        foreach (var factoryType in Modules.Values.SelectMany(m => m.GetTypes()).Where(IsDesignTimeFactory))
        {
            // Design-time factories build the model without opening a connection.
            var factory = Activator.CreateInstance(factoryType, nonPublic: true);
            var create = factoryType.GetMethod(nameof(IDesignTimeDbContextFactory<>.CreateDbContext))!;
            using var context = (DbContext)create.Invoke(factory, [Array.Empty<string>()])!;
            if (context is TenantAwareDbContext)
            {
                continue;
            }

            violations.AddRange(context.Model.GetEntityTypes()
                .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType))
                .Select(e => $"{e.ClrType.FullName} in {context.GetType().FullName}"));
        }

        Assert.True(violations.Count == 0,
            $"Tenant-owned entities must be mapped only by a {nameof(TenantAwareDbContext)}: {string.Join(", ", violations)}");
    }

    private static bool IsDesignTimeFactory(Type type) =>
        !type.IsAbstract && type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDesignTimeDbContextFactory<>));
}
