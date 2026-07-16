namespace PullRequestCopilotReview.Models;

public sealed record CopilotSdkSessionOptions(
    string GitHubToken,
    string WorkingDirectory,
    string? Model,
    IReadOnlyList<string> AvailableTools);
