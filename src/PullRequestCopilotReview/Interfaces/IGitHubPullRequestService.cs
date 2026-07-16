using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IGitHubPullRequestService
{
    Task<IReadOnlyList<PullRequestFile>> GetChangedFilesAsync(
        PullRequestContext context,
        CancellationToken cancellationToken = default);
}
