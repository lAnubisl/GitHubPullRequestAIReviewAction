using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Tests;

internal sealed class FakeCopilotRunner : ICopilotRunner
{
    private readonly ReviewResult _result;

    public FakeCopilotRunner(ReviewResult result)
    {
        _result = result;
    }

    public string? Prompt { get; private set; }

    public Task<ReviewResult> RunReviewAsync(
        string prompt,
        IReadOnlyList<PullRequestFile> changedFiles,
        CancellationToken cancellationToken = default)
    {
        Prompt = prompt;
        return Task.FromResult(_result);
    }
}
