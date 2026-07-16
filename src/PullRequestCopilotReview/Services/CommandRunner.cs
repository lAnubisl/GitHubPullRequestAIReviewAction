using System.Diagnostics;
using System.Text;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class CommandRunner : ICommandRunner
{
    private readonly ILogger _logger;
    private readonly Action<string>? _standardOutputReceived;
    private readonly Action<string>? _standardErrorReceived;

    public CommandRunner(
        ILogger logger,
        Action<string>? standardOutputReceived = null,
        Action<string>? standardErrorReceived = null)
    {
        _logger = logger;
        _standardOutputReceived = standardOutputReceived;
        _standardErrorReceived = standardErrorReceived;
    }

    public async Task<CommandResult> RunAsync(CommandSpec spec, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(spec.FileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = spec.WorkingDirectory ?? Environment.CurrentDirectory
        };

        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Clear();
        foreach (var pair in spec.Environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        var output = new StringBuilder();
        var error = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };
        var outputCallback = spec.StandardOutputLogging == CommandOutputLogging.Stream
            ? _standardOutputReceived
            : null;
        process.OutputDataReceived += (_, e) => HandleData(e, output, outputCallback);
        process.ErrorDataReceived += (_, e) => HandleData(e, error, _standardErrorReceived);

        LogStartingCommand(startInfo);
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {spec.FileName}.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        LogCompletedCommand(startInfo, process.ExitCode);
        return new CommandResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private static void HandleData(
        DataReceivedEventArgs args,
        StringBuilder destination,
        Action<string>? dataReceived)
    {
        if (args.Data is null)
        {
            return;
        }

        destination.AppendLine(args.Data);
        dataReceived?.Invoke(args.Data);
    }

    private void LogStartingCommand(ProcessStartInfo startInfo)
    {
        _logger.Info($"Starting command '{FormatCommand(startInfo)}'.");
    }

    private void LogCompletedCommand(ProcessStartInfo startInfo, int exitCode)
    {
        _logger.Info($"Command '{startInfo.FileName}' exited with code {exitCode}.");
    }

    private static string FormatCommand(ProcessStartInfo startInfo)
    {
        var arguments = startInfo.ArgumentList.ToArray();
        for (var index = 0; index < arguments.Length; index++)
        {
            if (string.Equals(arguments[index], "--prompt", StringComparison.Ordinal)
                && index + 1 < arguments.Length)
            {
                arguments[index + 1] = "[REDACTED]";
                index++;
            }
            else if (arguments[index].StartsWith("--prompt=", StringComparison.Ordinal))
            {
                arguments[index] = "--prompt=[REDACTED]";
            }
        }

        return string.Join(
            " ",
            new[] { QuoteArgument(startInfo.FileName) }.Concat(arguments.Select(QuoteArgument)));
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0
            && argument.All(character => !char.IsWhiteSpace(character) && character is not '"' and not '\''))
        {
            return argument;
        }

        return $"'{argument.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }
}
