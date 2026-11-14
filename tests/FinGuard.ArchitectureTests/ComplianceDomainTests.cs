using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static FinGuard.ArchitectureTests.FinGuardAssemblies;

namespace FinGuard.ArchitectureTests;

// Type-level rule with ArchUnitNET core (D-51).
public sealed class ComplianceDomainTests
{
    [Fact]
    public void ComplianceDomain_AnyType_DependsOnNoInfrastructure()
    {
        var rule = Types().That().ResideInNamespaceMatching(@"^FinGuard\.Compliance\.Domain(\..+)?$")
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^(Microsoft\.EntityFrameworkCore|Wolverine|Microsoft\.AspNetCore)(\..+)?$")
            // Domain/ is still empty; the rule starts guarding with the first domain type (Phase 3).
            .WithoutRequiringPositiveResults();

        var violations = rule.Evaluate(Architecture).Where(r => !r.Passed).Select(r => r.Description).ToList();

        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }
}
