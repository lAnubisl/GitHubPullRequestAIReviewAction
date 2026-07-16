using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IReviewFilter
{
    ReviewResult Apply(ReviewResult review, string minSeverity, int maxFindings, IEnumerable<string>? changedFiles = null);
}
