namespace PullRequestCopilotReview.Models;

public sealed record ReviewResponseValidationResult(ReviewResult? Result, IReadOnlyList<string> Errors)
{
    public bool IsValid => Result is not null && Errors.Count == 0;
    public static ReviewResponseValidationResult Valid(ReviewResult result) => new(result, Array.Empty<string>());
}
