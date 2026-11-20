using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Persistence;

namespace FinGuard.UnitTests.Compliance;

/// <summary>The repository's second line of defence behind the CHECK constraints (D-177).</summary>
public sealed class LimitPolicyRowTests
{
    private static readonly LimitPolicyRow Seed = new()
    {
        Version = 1,
        EffectiveFrom = new DateOnly(2000, 1, 1),
        Cash = 0.40m,
        FixedIncome = 0.30m,
        Shares = 0.25m,
        RealEstate = 0.25m,
        AlertThreshold = 0.90m,
        RebalanceTargetRatio = 0.85m,
    };

    [Fact]
    public void ToDomain_ValidRow_MapsEveryValue()
    {
        var policy = Seed.ToDomain();

        Assert.Equal((1, new DateOnly(2000, 1, 1)), (policy.Version, policy.EffectiveFrom));
        Assert.Equal(
            [0.40m, 0.30m, 0.25m, 0.25m],
            Enum.GetValues<AssetClass>().Select(c => policy.Limits[c].Value));
        Assert.Equal((0.90m, 0.85m), (policy.AlertThreshold.Value, policy.RebalanceTargetRatio.Value));
    }

    [Fact]
    public void ToDomain_RowBreakingAnInvariant_ThrowsWithVersionAndCodeOnly()
    {
        var corrupted = new LimitPolicyRow
        {
            Version = 7,
            EffectiveFrom = Seed.EffectiveFrom,
            Cash = Seed.Cash,
            FixedIncome = Seed.FixedIncome,
            Shares = Seed.Shares,
            RealEstate = Seed.RealEstate,
            AlertThreshold = 0.80m,
            RebalanceTargetRatio = 0.85m,
        };

        var error = Assert.Throws<InvalidOperationException>(corrupted.ToDomain);

        Assert.Equal("Limit policy version 7 is invalid: Compliance.Policy.TargetNotBelowAlert.", error.Message);
    }
}
