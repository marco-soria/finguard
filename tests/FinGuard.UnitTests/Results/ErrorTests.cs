using FinGuard.BuildingBlocks.Results;

namespace FinGuard.UnitTests.Results;

public sealed class ErrorTests
{
    public static TheoryData<Func<string, string, Error>, ErrorType> Factories => new()
    {
        { Error.Validation, ErrorType.Validation },
        { Error.NotFound, ErrorType.NotFound },
        { Error.Conflict, ErrorType.Conflict },
        { Error.BusinessRule, ErrorType.BusinessRule },
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factory_AnyType_SetsTypeCodeAndDescription(Func<string, string, Error> factory, ErrorType expectedType)
    {
        var error = factory("Test.Code", "Test description.");

        Assert.Equal(expectedType, error.Type);
        Assert.Equal("Test.Code", error.Code);
        Assert.Equal("Test description.", error.Description);
    }
}
