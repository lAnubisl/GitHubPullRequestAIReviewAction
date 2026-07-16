namespace PullRequestCopilotReview.Models;

public sealed record PullRequestFile(
    string FileName,
    string Status,
    int Additions,
    int Deletions,
    string? Patch,
    IReadOnlyList<DiffHunk> Hunks,
    string? LocalContext);
