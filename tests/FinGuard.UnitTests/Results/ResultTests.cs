using FinGuard.BuildingBlocks.Results;

namespace FinGuard.UnitTests.Results;

public sealed class ResultTests
{
    private static readonly Error SampleError = Error.Validation("Test.Invalid", "The input is invalid.");

    [Fact]
    public void Success_NoError_IsSuccessTrue()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_WithError_IsFailureAndKeepsError()
    {
        var result = Result.Failure(SampleError);

        Assert.True(result.IsFailure);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Failure_NullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }
}
