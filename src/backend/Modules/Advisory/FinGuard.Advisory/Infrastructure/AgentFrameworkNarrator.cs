using FinGuard.Advisory.Features.WriteAdvisory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace FinGuard.Advisory.Infrastructure;

/// <summary>
/// The model narrator (ADR-0006, ADR-0015): one Agent Framework agent over the injected <see cref="IChatClient"/>, with
/// fixed instructions and no tools, no memory and no MCP. It writes the context paragraph shown beside the template
/// (D-293), never the narrative itself. It returns the model's raw text; the caller normalizes and validates it. A call longer than <see cref="AdvisoryOptions.TimeoutSeconds"/> throws <see cref="TimeoutException"/>.
/// </summary>
public sealed class AgentFrameworkNarrator : IAdvisoryNarrator
{
    private readonly ChatClientAgent _agent;
    private readonly IChatClient _chatClient;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _time;

    public AgentFrameworkNarrator(IChatClient chatClient, IOptions<AdvisoryOptions> options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(options);
        _chatClient = chatClient;
        _timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
        _time = time;
        _agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "advisory-narrator",
            ChatOptions = new ChatOptions { Instructions = NarratorPrompt.Instructions, Temperature = 0.2f },
        });
    }

    public string? ModelId => _chatClient.GetService<ChatClientMetadata>()?.DefaultModelId;

    public async Task<string> NarrateAsync(AdvisoryFacts facts, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(_timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            var response = await _agent.RunAsync(NarratorPrompt.Input(facts), cancellationToken: linked.Token);
            return response.Text;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("The narrator model did not answer in time.");
        }
    }
}
