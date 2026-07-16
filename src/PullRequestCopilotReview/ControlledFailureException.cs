namespace PullRequestCopilotReview;

public sealed class ControlledFailureException : Exception
{
    public ControlledFailureException(int exitCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
