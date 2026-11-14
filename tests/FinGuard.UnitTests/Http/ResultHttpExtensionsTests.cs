using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinGuard.UnitTests.Http;

public sealed class ResultHttpExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.BusinessRule, StatusCodes.Status422UnprocessableEntity)]
    public void ToHttpResult_Failure_ReturnsProblemWithStatusDetailAndCode(ErrorType type, int expectedStatus)
    {
        var result = Result.Failure(new Error("Test.Code", "Test description.", type));

        var problem = Assert.IsType<ProblemHttpResult>(result.ToHttpResult());

        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal("Test description.", problem.ProblemDetails.Detail);
        Assert.Equal("Test.Code", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void ToHttpResult_SuccessWithoutValue_ReturnsNoContent()
    {
        var httpResult = Result.Success().ToHttpResult();

        Assert.IsType<NoContent>(httpResult);
    }

    [Fact]
    public void ToHttpResult_SuccessWithValue_ReturnsOnSuccessResult()
    {
        Result<int> result = 7;

        var httpResult = result.ToHttpResult(TypedResults.Ok);

        var ok = Assert.IsType<Ok<int>>(httpResult);
        Assert.Equal(7, ok.Value);
    }
}
