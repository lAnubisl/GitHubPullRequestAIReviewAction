using GitHub.Copilot;

namespace PullRequestCopilotReview.Interfaces;

public interface ICopilotSdkSession : IAsyncDisposable
{
    Task<AssistantMessageEvent?> SendAndWaitAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
