namespace PullRequestCopilotReview;

public static class ExitCodes
{
    public const int Success = 0;
    public const int ConfigurationFailure = 2;
    public const int GitHubFailure = 3;
    public const int CopilotFailure = 4;
    public const int FindingsFailure = 5;
    public const int UnexpectedFailure = 10;
}
