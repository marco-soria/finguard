using Microsoft.Extensions.AI;

namespace FinGuard.UnitTests.Advisory;

/// <summary>A deterministic <see cref="IChatClient"/>: tests never call a real model (testing rules).</summary>
internal sealed class FakeChatClient(Func<CancellationToken, Task<string>> respond) : IChatClient
{
    public const string ModelId = "fake-model";

    public List<ChatMessage> LastMessages { get; } = [];

    public ChatOptions? LastOptions { get; private set; }

    public static FakeChatClient Answering(string text) => new(_ => Task.FromResult(text));

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        LastMessages.Clear();
        LastMessages.AddRange(messages);
        LastOptions = options;
        var text = await respond(cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text)) { ModelId = ModelId };
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The narrator does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("fake", null, ModelId)
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    public void Dispose()
    {
    }
}
