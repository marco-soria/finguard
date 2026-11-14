using FinGuard.BuildingBlocks.Results;

namespace FinGuard.UnitTests.Results;

public sealed class GenericResultTests
{
    private static readonly Error SampleError = Error.NotFound("Test.Missing", "The item was not found.");

    [Fact]
    public void Value_OnSuccess_ReturnsValue()
    {
        var result = Result<int>.Success(42);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        var result = Result<int>.Failure(SampleError);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_Succeeds()
    {
        Result<string> result = "value";

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_Fails()
    {
        Result<string> result = SampleError;

        Assert.True(result.IsFailure);
        Assert.Same(SampleError, result.Error);
    }
}
