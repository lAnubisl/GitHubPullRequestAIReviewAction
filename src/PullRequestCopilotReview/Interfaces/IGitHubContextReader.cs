using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IGitHubContextReader
{
    PullRequestContext Read();
}
