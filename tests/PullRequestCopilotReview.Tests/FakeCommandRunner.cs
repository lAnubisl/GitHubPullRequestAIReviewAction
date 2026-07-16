using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Tests;

internal sealed class FakeCommandRunner : ICommandRunner
{
    private readonly Queue<CommandResult> _results;

    public FakeCommandRunner(params CommandResult[] results)
    {
        _results = new Queue<CommandResult>(results);
    }

    public List<CommandSpec> Calls { get; } = new();

    public Action<CommandSpec, CommandResult>? OnResult { get; init; }

    public Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
    {
        Calls.Add(spec);
        var result = _results.Count == 0
            ? new CommandResult(0, string.Empty, string.Empty)
            : _results.Dequeue();
        OnResult?.Invoke(spec, result);
        return Task.FromResult(result);
    }
}
