namespace PullRequestCopilotReview.Interfaces;

public interface IConfigurationHelper
{
    int MaxFindings { get; }
    string MinSeverity { get; }
    IReadOnlyList<string> ExcludePaths { get; }
    string? CopilotModel { get; }
    string? CopilotExtraInstructions { get; }
    bool FailOnFindings { get; }
    string GitHubToken { get; }
    string CopilotToken { get; }
    string Repository { get; }
    string EventPath { get; }
    string Workspace { get; }
    string? StepSummaryPath { get; }
}
