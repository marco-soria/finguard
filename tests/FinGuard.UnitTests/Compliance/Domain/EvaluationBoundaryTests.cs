using FinGuard.Compliance.Domain;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Compliance.Domain;

/// <summary>Mandatory boundary table per class (compliance-domain rules), on the seed policy.</summary>
public sealed class EvaluationBoundaryTests
{
    private static readonly (decimal Utilization, ComplianceStatus Status)[] Boundaries =
    [
        (0.8999m, ComplianceStatus.Compliant),
        (0.90m, ComplianceStatus.Warning),
        (0.9001m, ComplianceStatus.Warning),
        (0.9999m, ComplianceStatus.Warning),
        (1.00m, ComplianceStatus.Warning),
        (1.0001m, ComplianceStatus.Breached),
        (0m, ComplianceStatus.Compliant),
    ];

    public static TheoryData<AssetClass, decimal, ComplianceStatus> Table()
    {
        var data = new TheoryData<AssetClass, decimal, ComplianceStatus>();
        foreach (var assetClass in Enum.GetValues<AssetClass>())
        {
            foreach (var (utilization, status) in Boundaries)
            {
                data.Add(assetClass, utilization, status);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void Create_ClassAtBoundary_HasExactUtilizationAndStatus(
        AssetClass assetClass, decimal utilization, ComplianceStatus expected)
    {
        // Class value = u x limit x total, exact in cents (e.g. shares 0.90 -> 225,000.00). The rest of the total goes
        // to the next class so the total stays 1,000,000.00.
        var value = utilization * SeedLimits()[assetClass].Value * Total;
        var values = Enum.GetValues<AssetClass>().ToDictionary(c => c, _ => new Money(0m));
        values[assetClass] = new Money(value);
        values[Next(assetClass)] = new Money(Total - value);

        var result = Evaluate(values).Classes.Single(c => c.Utilization.Class == assetClass);

        Assert.Equal(utilization, result.Utilization.Utilization.Value);
        Assert.Equal(expected, result.Status);
    }

    private static AssetClass Next(AssetClass assetClass)
    {
        var classes = Enum.GetValues<AssetClass>();
        return classes[(Array.IndexOf(classes, assetClass) + 1) % classes.Length];
    }
}
