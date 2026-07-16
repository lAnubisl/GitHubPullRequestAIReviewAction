using System.Collections.Concurrent;
using PullRequestCopilotReview.Models;
using PullRequestCopilotReview.Services;

namespace PullRequestCopilotReview.Tests;

public sealed class CommandRunnerTests
{
    [Fact]
    public async Task Streams_standard_output_and_error_while_retaining_captured_result()
    {
        var streamedOutput = new ConcurrentQueue<string>();
        var streamedError = new ConcurrentQueue<string>();
        var logger = new FakeTextLogger();
        var runner = new CommandRunner(logger, streamedOutput.Enqueue, streamedError.Enqueue);
        var spec = CreateOutputCommand();

        var result = await runner.RunAsync(spec);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(streamedOutput, line => line.TrimEnd() == "standard output");
        Assert.Contains(streamedError, line => line.TrimEnd() == "standard error");
        Assert.Contains("standard output", result.StandardOutput);
        Assert.Contains("standard error", result.StandardError);
        Assert.Contains(logger.InfoMessages, message => message.StartsWith("Starting command '", StringComparison.Ordinal));
        Assert.Contains(logger.InfoMessages, message => message.Contains("exited with code 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Capture_only_suppresses_streamed_stdout_but_retains_the_result()
    {
        var streamedOutput = new ConcurrentQueue<string>();
        var logger = new FakeTextLogger();
        var runner = new CommandRunner(logger, streamedOutput.Enqueue);
        var command = CreateOutputCommand() with
        {
            StandardOutputLogging = CommandOutputLogging.CaptureOnly
        };

        var result = await runner.RunAsync(command);

        Assert.Empty(streamedOutput);
        Assert.Contains("standard output", result.StandardOutput);
    }

    [Fact]
    public async Task Redacts_the_argument_following_prompt_from_command_logging()
    {
        const string secretPrompt = "source code from a private repository";
        var logger = new FakeTextLogger();
        var runner = new CommandRunner(logger);
        var command = CreatePromptCommand(secretPrompt);

        await runner.RunAsync(command);

        var start = Assert.Single(logger.InfoMessages.Where(message => message.StartsWith("Starting command", StringComparison.Ordinal)));
        Assert.DoesNotContain(secretPrompt, start);
        Assert.Contains("[REDACTED]", start);
    }

    private static CommandSpec CreateOutputCommand()
    {
        if (OperatingSystem.IsWindows())
        {
            return new CommandSpec(
                "cmd.exe",
                new[] { "/d", "/s", "/c", "echo standard output & echo standard error 1>&2" },
                new Dictionary<string, string>());
        }

        return new CommandSpec(
            "/bin/sh",
            new[] { "-c", "echo 'standard output'; echo 'standard error' >&2" },
            new Dictionary<string, string>());
    }

    private static CommandSpec CreatePromptCommand(string prompt)
    {
        if (OperatingSystem.IsWindows())
        {
            return new CommandSpec(
                "cmd.exe",
                new[] { "/d", "/s", "/c", "echo ok", "--prompt", prompt },
                new Dictionary<string, string>());
        }

        return new CommandSpec(
            "/bin/sh",
            new[] { "-c", "echo ok", "--prompt", prompt },
            new Dictionary<string, string>());
    }
}
