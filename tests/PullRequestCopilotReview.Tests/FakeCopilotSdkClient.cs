using GitHub.Copilot;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Tests;

internal sealed class FakeCopilotSdkClient : ICopilotSdkClient
{
    private readonly Queue<AssistantMessageEvent?> _responses;
    private readonly IReadOnlyList<SessionEvent> _events;

    public FakeCopilotSdkClient(IEnumerable<AssistantMessageEvent?>? responses = null, params SessionEvent[] events)
    {
        _responses = new Queue<AssistantMessageEvent?>(responses ?? Array.Empty<AssistantMessageEvent?>());
        _events = events;
    }

    public CopilotSdkSessionOptions? Options { get; private set; }
    public List<string> Prompts { get; } = [];
    public int CreatedSessionCount { get; private set; }
    public Exception? ExceptionToThrow { get; init; }

    public Task<ICopilotSdkSession> CreateSessionAsync(CopilotSdkSessionOptions options, Action<SessionEvent> onEvent, CancellationToken cancellationToken = default)
    {
        Options = options;
        CreatedSessionCount++;
        if (ExceptionToThrow is not null) throw ExceptionToThrow;
        foreach (var evt in _events) onEvent(evt);
        return Task.FromResult<ICopilotSdkSession>(new Session(this));
    }

    private sealed class Session(FakeCopilotSdkClient client) : ICopilotSdkSession
    {
        public Task<AssistantMessageEvent?> SendAndWaitAsync(string prompt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (client.ExceptionToThrow is not null) throw client.ExceptionToThrow;
            client.Prompts.Add(prompt);
            return Task.FromResult(client._responses.Count > 0 ? client._responses.Dequeue() : null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
