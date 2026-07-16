using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IStepSummaryWriter
{
    Task WriteAsync(
        PullRequestContext context,
        IReadOnlyList<PullRequestFile> files,
        ReviewResult review,
        CancellationToken cancellationToken = default);
}
