namespace PullRequestCopilotReview.Models;

public sealed record ReviewResult(string Summary, IReadOnlyList<ReviewFinding> Findings)
{
    public static ReviewResult Empty => new("No review findings were returned.", Array.Empty<ReviewFinding>());

}
