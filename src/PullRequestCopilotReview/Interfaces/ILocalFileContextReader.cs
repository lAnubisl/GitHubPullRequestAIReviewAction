using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Interfaces;

public interface ILocalFileContextReader
{
    string? Read(string workspace, string relativePath, IReadOnlyList<DiffHunk> hunks, int contextLines);
}
