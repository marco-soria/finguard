using FinGuard.BuildingBlocks.Http;

namespace FinGuard.UnitTests.Http;

public sealed class ApiRoutesTests
{
    [Fact]
    public void V1_Always_IsTheVersionOneSegment() => Assert.Equal("/api/v1", ApiRoutes.V1);
}
