using System.Net;
using System.Text.Json;

namespace FinGuard.Api.IntegrationTests;

public sealed class ErrorHandlingTests(ApiFixture fixture)
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task UnhandledException_AnyRequest_Returns500ProblemWithoutDetails()
    {
        var secret = Guid.NewGuid().ToString();
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(ThrowUri(secret), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.False(problem.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnhandledException_AnyRequest_DoesNotLogExceptionMessage()
    {
        var secret = Guid.NewGuid().ToString();
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(ThrowUri(secret), TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var traceId = problem.RootElement.GetProperty("traceId").GetString()!;
        var records = fixture.Logs.GetSnapshot();

        Assert.Contains(records, r =>
            r.Message.Contains(typeof(InvalidOperationException).FullName!, StringComparison.Ordinal)
            && r.Message.Contains(traceId, StringComparison.Ordinal));
        Assert.DoesNotContain(records, r =>
            r.Message.Contains(secret, StringComparison.Ordinal)
            || (r.Exception?.ToString().Contains(secret, StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public async Task UnknownRoute_AnyRequest_Returns404Problem()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/no-such-route", UriKind.Relative), TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    private static Uri ThrowUri(string message) =>
        new($"{ThrowingEndpointStartupFilter.Path}?message={Uri.EscapeDataString(message)}", UriKind.Relative);
}
