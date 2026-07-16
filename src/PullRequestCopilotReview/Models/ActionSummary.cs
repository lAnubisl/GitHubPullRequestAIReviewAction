namespace PullRequestCopilotReview.Models;

public sealed record ActionSummary(int InlineCommentsPublished, IReadOnlyList<ReviewFinding> FallbackFindings);
