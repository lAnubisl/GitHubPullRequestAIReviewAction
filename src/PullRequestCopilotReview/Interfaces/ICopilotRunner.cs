using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface ICopilotRunner
{
    Task<ReviewResult> RunReviewAsync(
        string prompt,
        IReadOnlyList<PullRequestFile> changedFiles,
        CancellationToken cancellationToken = default);
}
