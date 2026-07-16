using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface IDiffParser
{
    IReadOnlyList<DiffHunk> Parse(string? patch);
}
