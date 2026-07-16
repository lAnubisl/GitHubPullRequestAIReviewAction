using PullRequestCopilotReview.Interfaces;

namespace PullRequestCopilotReview.Services;

public sealed class TextLogger : ILogger
{
    public void Info(string message) => Console.WriteLine(message);
    public void Warning(string message) => Console.WriteLine($"::warning::{message}");
    public void Error(string message) => Console.Error.WriteLine($"::error::{message}");
}
