using System.Globalization;
using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Domain.Specifications;

namespace FinGuard.UnitTests.Compliance.Domain;

public sealed class SpecificationTests
{
    [Theory]
    [InlineData("0.8999", false)]
    [InlineData("0.90", true)]
    [InlineData("1.00", true)]
    [InlineData("1.5", true)]
    public void IsInAlert_Utilization_TrueAtOrAboveThreshold(string utilization, bool expected)
    {
        var spec = new IsInAlertSpecification(new Ratio(0.90m));

        Assert.Equal(expected, spec.IsSatisfiedBy(At(decimal.Parse(utilization, CultureInfo.InvariantCulture))));
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1.00", false)]
    [InlineData("1.0001", true)]
    public void IsBreached_Utilization_TrueOnlyAboveOne(string utilization, bool expected)
    {
        Assert.Equal(
            expected, new IsBreachedSpecification().IsSatisfiedBy(At(decimal.Parse(utilization, CultureInfo.InvariantCulture))));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void And_TwoSpecifications_TrueOnlyWhenBoth(bool left, bool right, bool expected)
    {
        Assert.Equal(expected, new Constant(left).And(new Constant(right)).IsSatisfiedBy(At(0m)));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Not_Specification_Negates(bool inner, bool expected)
    {
        Assert.Equal(expected, new Constant(inner).Not().IsSatisfiedBy(At(0m)));
    }

    private static ClassUtilization At(decimal utilization) =>
        new(AssetClass.Cash, new Money(0m), new Ratio(0m), new Ratio(0.40m), new Ratio(utilization));

    private sealed class Constant(bool value) : Specification<ClassUtilization>
    {
        public override bool IsSatisfiedBy(ClassUtilization candidate) => value;
    }
}
