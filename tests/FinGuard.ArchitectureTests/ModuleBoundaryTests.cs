using System.Reflection;
using Microsoft.EntityFrameworkCore;
using static FinGuard.ArchitectureTests.FinGuardAssemblies;

namespace FinGuard.ArchitectureTests;

// Assembly-level rules from AGENTS.md, checked with reflection (D-51).
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Discovery_ByConvention_FindsModulesAndContracts()
    {
        Assert.NotEmpty(Modules);
        Assert.Equal(Modules.Count, Contracts.Count);
    }

    [Fact]
    public void Contracts_AnyAssembly_ReferenceNoFinGuardAssembly()
    {
        var violations = Contracts
            .SelectMany(c => FinGuardReferences(c).Select(r => $"{c.GetName().Name} -> {r}"))
            .ToList();

        Assert.True(violations.Count == 0, $"Contracts must not reference FinGuard assemblies: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Modules_AnyAssembly_ReferenceOnlyBuildingBlocksAndContracts()
    {
        var violations = Modules.Values
            .SelectMany(m => FinGuardReferences(m)
                .Where(r => r != BuildingBlocksName && !IsContracts(r))
                .Select(r => $"{m.GetName().Name} -> {r}"))
            .ToList();

        Assert.True(violations.Count == 0, $"Modules may reference only BuildingBlocks and Contracts: {string.Join(", ", violations)}");
    }

    [Fact]
    public void BuildingBlocks_Always_ReferencesNoFinGuardAssembly()
    {
        var violations = FinGuardReferences(BuildingBlocksAssembly).ToList();

        Assert.True(violations.Count == 0, $"{BuildingBlocksName} must not reference: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Assemblies_Any_DoNotReferenceMediatR()
    {
        var violations = All
            .SelectMany(a => a.GetReferencedAssemblies()
                .Where(r => r.Name!.StartsWith("MediatR", StringComparison.Ordinal))
                .Select(r => $"{a.GetName().Name} -> {r.Name}"))
            .ToList();

        Assert.True(violations.Count == 0, $"MediatR is not allowed: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Types_Any_AreNotGenericRepositories()
    {
        var violations = All
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsGenericTypeDefinition && t.Name.Contains("Repository", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0, $"Generic repositories are not allowed: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Modules_AnyAssembly_HaveOneInternalDbContextWithModuleSchema()
    {
        var violations = new List<string>();
        foreach (var (module, assembly) in Modules)
        {
            var contexts = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(DbContext))).ToList();
            if (contexts.Count != 1)
            {
                violations.Add($"{assembly.GetName().Name} has {contexts.Count} DbContext types: {string.Join(", ", contexts.Select(c => c.FullName))}");
                continue;
            }

            var context = contexts[0];
            if (context.IsPublic || context.IsNestedPublic)
            {
                violations.Add($"{context.FullName} must not be public");
            }

            var schema = context.GetField("Schema", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var expected = module.ToLowerInvariant();
            if (schema is not { IsLiteral: true } || (string?)schema.GetRawConstantValue() != expected)
            {
                violations.Add($"{context.FullName} must declare const Schema = \"{expected}\"");
            }
        }

        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }
}
