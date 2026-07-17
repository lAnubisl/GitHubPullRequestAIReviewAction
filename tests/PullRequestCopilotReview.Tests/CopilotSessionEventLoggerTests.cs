using System.Text.Json;
using GitHub.Copilot;
using PullRequestCopilotReview.Services;

namespace PullRequestCopilotReview.Tests;

public sealed class CopilotSessionEventLoggerTests
{
    [Fact]
    public void Describes_repository_tool_activity_and_completion()
    {
        var logger = new FakeTextLogger();
        var eventLogger = new CopilotSessionEventLogger(logger);

        eventLogger.Handle(Start("view-call", "view", """{"path":"src/abc.py","line_start":10,"line_end":30}"""));
        eventLogger.Handle(Complete("view-call", success: true));
        eventLogger.Handle(Start("grep-call", "grep", """{"pattern":"unsafe_load","path":"src","include":"*.py"}"""));

        Assert.Contains(logger.InfoMessages, message => message.Contains("Reading `src/abc.py`"));
        Assert.Contains(logger.InfoMessages, message => message.Contains("Completed reading `src/abc.py`"));
        Assert.Contains(logger.InfoMessages, message => message.Contains("Searching for `unsafe_load` in `src` (files: `*.py`)"));
    }

    [Fact]
    public void Logs_tool_failures_as_warnings()
    {
        var logger = new FakeTextLogger();
        var eventLogger = new CopilotSessionEventLogger(logger);
        eventLogger.Handle(Start("glob-call", "glob", """{"pattern":"**/*.cs","path":"src"}"""));
        eventLogger.Handle(Complete("glob-call", success: false, error: "Path is outside the allowed workspace."));

        var warning = Assert.Single(logger.WarningMessages);
        Assert.Contains("Failed while finding files matching `**/*.cs` in `src`", warning);
        Assert.Contains("Path is outside the allowed workspace.", warning);
    }

    private static ToolExecutionStartEvent Start(string callId, string toolName, string arguments)
        => new()
        {
            Data = new ToolExecutionStartData
            {
                ToolCallId = callId,
                ToolName = toolName,
                Arguments = JsonDocument.Parse(arguments).RootElement.Clone()
            }
        };

    private static ToolExecutionCompleteEvent Complete(string callId, bool success, string? error = null)
        => new()
        {
            Data = new ToolExecutionCompleteData
            {
                ToolCallId = callId,
                Success = success,
                Error = error is null
                    ? null
                    : new ToolExecutionCompleteError { Code = "tool_failed", Message = error }
            }
        };
}
