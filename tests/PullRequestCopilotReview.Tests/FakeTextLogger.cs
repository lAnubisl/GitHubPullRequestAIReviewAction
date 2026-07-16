using PullRequestCopilotReview.Interfaces;

namespace PullRequestCopilotReview.Tests;

internal sealed class FakeTextLogger : ILogger
{
    public List<string> InfoMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();

    public void Info(string message) => InfoMessages.Add(message);
    public void Warning(string message) => WarningMessages.Add(message);
    public void Error(string message) => ErrorMessages.Add(message);
}
