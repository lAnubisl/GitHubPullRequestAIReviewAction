using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IReviewPublisher
{
    Task<ActionSummary> PublishAsync(
        PullRequestContext context,
        IReadOnlyList<PullRequestFile> files,
        ReviewResult review,
        CancellationToken cancellationToken = default);
}
