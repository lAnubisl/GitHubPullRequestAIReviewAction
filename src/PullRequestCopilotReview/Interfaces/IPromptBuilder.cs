using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IPromptBuilder
{
    string Build(PullRequestContext context, IReadOnlyList<PullRequestFile> files);
}
