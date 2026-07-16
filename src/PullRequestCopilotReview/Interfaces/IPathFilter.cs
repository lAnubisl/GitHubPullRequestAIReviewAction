using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IPathFilter
{
    IReadOnlyList<PullRequestFile> Apply(IEnumerable<PullRequestFile> files, IReadOnlyList<string> excludePaths);
}
