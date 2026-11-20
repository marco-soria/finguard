using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static FinGuard.ArchitectureTests.FinGuardAssemblies;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace FinGuard.ArchitectureTests;

// Type-level rule with ArchUnitNET core (D-51).
public sealed class ComplianceDomainTests
{
    // Allow-list (D-169): the domain sees the base library, itself and Result/Error, nothing else.
    private const string AllowedNamespaces =
        @"^(System|FinGuard\.Compliance\.Domain|FinGuard\.BuildingBlocks\.Results)(\..+)?$";

    // The shared Architecture loads FinGuard.* only, so it cannot see a dependency on EF Core, Wolverine or ASP.NET.
    // This one also loads Compliance's direct references, so every dependency of a domain type is checked.
    private static readonly Architecture ComplianceWithReferences = new ArchLoader()
        .LoadAssembliesIncludingDependencies([Modules["Compliance"]], recursive: false)
        .Build();

    [Fact]
    public void ComplianceDomain_AnyType_DependsOnlyOnSystemItselfAndResults()
    {
        var rule = Types().That().ResideInNamespaceMatching(@"^FinGuard\.Compliance\.Domain(\..+)?$")
            .Should().OnlyDependOnTypesThat().ResideInNamespaceMatching(AllowedNamespaces)
            // Domain/ is still empty; the rule starts guarding with the first domain type (Phase 3).
            .WithoutRequiringPositiveResults();

        var violations = rule.Evaluate(ComplianceWithReferences).Where(r => !r.Passed).Select(r => r.Description).ToList();

        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }
}
