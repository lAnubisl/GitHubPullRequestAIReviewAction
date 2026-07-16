using GitHub.Copilot;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface ICopilotSdkClient
{
    Task<ICopilotSdkSession> CreateSessionAsync(
        CopilotSdkSessionOptions options,
        Action<SessionEvent> onEvent,
        CancellationToken cancellationToken = default);
}
