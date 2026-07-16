using GitHub.Copilot;
using PullRequestCopilotReview.Interfaces;

namespace PullRequestCopilotReview.Services;

internal sealed class GitHubCopilotSdkSession : ICopilotSdkSession
{
    private readonly CopilotClient _client;
    private readonly CopilotSession _session;
    private readonly IDisposable _subscription;

    public GitHubCopilotSdkSession(CopilotClient client, CopilotSession session, IDisposable subscription)
        => (_client, _session, _subscription) = (client, session, subscription);

    public Task<AssistantMessageEvent?> SendAndWaitAsync(string prompt, CancellationToken cancellationToken = default)
        => _session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(15),
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        await _session.DisposeAsync();
        await _client.DisposeAsync();
    }
}
