namespace PullRequestCopilotReview.Models;

public sealed record PullRequestContext(
    string Repository,
    int PullRequestNumber,
    string BaseRef,
    string BaseSha,
    string HeadRef,
    string HeadSha);
