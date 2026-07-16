using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IReviewResponseValidator
{
    ReviewResponseValidationResult Validate(string content, IReadOnlyList<PullRequestFile> changedFiles);
}
