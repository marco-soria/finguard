using FinGuard.Compliance.Domain;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Compliance.Domain;

public sealed class EvaluationTests
{
    [Fact]
    public void Create_OneBreachedAndOneWarning_OverallIsBreached()
    {
        // cash 0.95 (warning), shares 1.04 (breached), fixed income 0.6667, real estate 0.64.
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));

        Assert.Equal(ComplianceStatus.Breached, evaluation.OverallStatus);
    }

    [Fact]
    public void Create_OnlyWarnings_OverallIsWarning()
    {
        // cash 0.95, shares 0.92, fixed income 0.8333, real estate 0.56.
        var evaluation = Evaluate(Values(380_000m, 250_000m, 230_000m, 140_000m));

        Assert.Equal(ComplianceStatus.Warning, evaluation.OverallStatus);
    }

    [Fact]
    public void Create_NoClassInAlert_OverallIsCompliantWithoutAlerts()
    {
        // cash 0.875, fixed income 0.8333, shares 0.80, real estate 0.80.
        var evaluation = Evaluate(Values(350_000m, 250_000m, 200_000m, 200_000m));

        Assert.Equal(ComplianceStatus.Compliant, evaluation.OverallStatus);
        Assert.Empty(evaluation.Alerts);
    }

    [Fact]
    public void Create_WarningAndBreachedClasses_OneAlertEachWithClassStatusAndUtilization()
    {
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));

        Assert.Equal(
            [
                new Alert(AssetClass.Cash, ComplianceStatus.Warning, new Ratio(0.95m)),
                new Alert(AssetClass.Shares, ComplianceStatus.Breached, new Ratio(1.04m)),
            ],
            evaluation.Alerts);
    }

    [Fact]
    public void Create_UtilizationJustBelowThreshold_StaysCompliantAlthoughRoundingWouldWarn()
    {
        // shares 224,999.99 / 1,000,000 / 0.25 = 0.89999996; rounded to 2 or 4 decimals it would read 0.90.
        var evaluation = Evaluate(Values(300_000m, 250_000.01m, 224_999.99m, 225_000m));

        var shares = evaluation.Classes.Single(c => c.Utilization.Class == AssetClass.Shares);
        Assert.Equal(0.89999996m, shares.Utilization.Utilization.Value);
        Assert.Equal(0.90m, Math.Round(shares.Utilization.Utilization.Value, 4));
        Assert.Equal(ComplianceStatus.Compliant, shares.Status);
    }

    [Fact]
    public void Create_UtilizationJustAboveOne_StaysBreachedAlthoughRoundingWouldNot()
    {
        // shares 250,000.01 / 1,000,000 / 0.25 = 1.00000004; rounded to 2 or 4 decimals it would read 1.00.
        var evaluation = Evaluate(Values(300_000m, 250_000m, 250_000.01m, 199_999.99m));

        var shares = evaluation.Classes.Single(c => c.Utilization.Class == AssetClass.Shares);
        Assert.Equal(1.00000004m, shares.Utilization.Utilization.Value);
        Assert.Equal(1.00m, Math.Round(shares.Utilization.Utilization.Value, 4));
        Assert.Equal(ComplianceStatus.Breached, shares.Status);
    }

    [Fact]
    public void Create_ClassMissing_ThrowsArgumentException()
    {
        var values = Values(1m, 1m, 1m, 1m);
        values.Remove(AssetClass.RealEstate);

        Assert.Throws<ArgumentException>(() => Evaluate(values));
    }

    [Fact]
    public void Create_TotalZero_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Evaluate(Values(0m, 0m, 0m, 0m)));
    }

    [Fact]
    public void Create_AnyPortfolio_SnapshotEqualsThePolicyUsed()
    {
        var policy = Seed();

        var evaluation = Evaluation.Create(
            Guid.NewGuid(), new DateOnly(2026, 6, 30), Values(1m, 1m, 1m, 1m), policy, EvaluatedAt);

        Assert.Equal(
            (policy.Version, policy.AlertThreshold, policy.RebalanceTargetRatio),
            (evaluation.Policy.Version, evaluation.Policy.AlertThreshold, evaluation.Policy.RebalanceTargetRatio));
        Assert.Equal(policy.Limits, evaluation.Policy.Limits);
    }

    [Fact]
    public void Create_AnyPortfolio_IdIsVersion7AndClassesFollowEnumOrder()
    {
        var evaluation = Evaluate(Values(1m, 1m, 1m, 1m));

        Assert.Equal(7, evaluation.Id.Version);
        Assert.Equal(Enum.GetValues<AssetClass>(), evaluation.Classes.Select(c => c.Utilization.Class));
    }

    [Fact]
    public void ToString_DomainTypes_PrintNoAmounts()
    {
        // Marker amounts: none of their digits may reach ToString (D-162).
        var evaluation = Evaluate(Values(123_456.78m, 234_567.89m, 100_000m, 150_000m));
        var cash = evaluation.Classes[0];

        string[] printed =
            [evaluation.ToString(), cash.Utilization.ToString(), cash.ToString(), evaluation.Total.ToString()];

        Assert.All(printed, text =>
        {
            Assert.DoesNotContain("123456", text.Replace(",", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.DoesNotContain("608024", text.Replace(",", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        });
        Assert.Contains(evaluation.SubmissionId.ToString(), evaluation.ToString(), StringComparison.Ordinal);
    }
}
