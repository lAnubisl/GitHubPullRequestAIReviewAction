namespace PullRequestCopilotReview.Models;

public sealed record CommandSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    string? WorkingDirectory = null,
    CommandOutputLogging StandardOutputLogging = CommandOutputLogging.Stream);
