namespace PullRequestCopilotReview.Models;

public sealed record ReviewFinding(
    string Severity,
    string File,
    int Line,
    string Title,
    string Body,
    string Confidence)
{
    public bool IsAtLeast(string minimumSeverity)
        => SeverityRank(Severity) >= SeverityRank(minimumSeverity);

    public static int SeverityRank(string severity)
        => severity.ToLowerInvariant() switch
        {
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0
        };
}
