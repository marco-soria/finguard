using FinGuard.Tenancy.Development;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Tracking;

namespace FinGuard.Api.IntegrationTests.Messaging;

// R-21, D-77: Wolverine logs a failed message with its ToString(); message types override it to keep free text out.
[Collection(MessagingHost.Collection)]
public sealed class MessageLoggingTests(ApiFixture fixture)
{
    private readonly MessagingHost _host = fixture.Messaging;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task FailedMessage_WithFreeText_DoesNotLogPayload()
    {
        var reference = $"secret-reference-{Guid.NewGuid():N}";
        var message = new PlaceProbeOrder(Guid.NewGuid(), reference, FailAfterSave: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _host.Services.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync(_ => InvokeAsync(message)));

        var records = _host.Logs.GetSnapshot();
        Assert.Contains(records, r => r.Message.Contains(nameof(PlaceProbeOrder), StringComparison.Ordinal));
        var leaks = records
            .Where(r => r.Message.Contains(reference, StringComparison.Ordinal)
                        || r.StructuredState?.Any(kv => kv.Value?.Contains(reference, StringComparison.Ordinal) == true) == true
                        || r.Exception?.ToString().Contains(reference, StringComparison.Ordinal) == true)
            .Select(r => $"[{r.Level}] {r.Category}: {r.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak the message payload: {string.Join(" | ", leaks)}");
    }

    private async Task InvokeAsync(object message)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeForTenantAsync(DevIdentities.Alice.TenantId.ToString(), message, _ct);
    }
}
