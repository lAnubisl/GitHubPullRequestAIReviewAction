namespace PullRequestCopilotReview.Interfaces;

public interface IReviewApplication
{
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
