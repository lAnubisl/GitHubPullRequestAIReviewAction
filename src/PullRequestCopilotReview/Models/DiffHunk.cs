namespace PullRequestCopilotReview.Models;

public sealed record DiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    IReadOnlySet<int> NewLines);
