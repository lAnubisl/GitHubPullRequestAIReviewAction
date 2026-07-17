using GitHub.Copilot;
using PullRequestCopilotReview.Interfaces;

namespace PullRequestCopilotReview.Services;

public sealed class CopilotSessionEventLogger : ICopilotSessionEventLogger
{
    private readonly ILogger? _logger;
    private int _streamedCharacters;
    private int _messageCount;

    public CopilotSessionEventLogger(ILogger? logger) => _logger = logger;

    public void LogPrompt(int attempt, string prompt)
        => _logger?.Info($"Copilot SDK prompt {attempt}:{Environment.NewLine}{prompt}");

    public void Handle(SessionEvent evt)
    {
        switch (evt)
        {
            case AssistantMessageDeltaEvent delta when delta.AgentId is null:
                _streamedCharacters += delta.Data.DeltaContent.Length;
                break;
            case AssistantIntentEvent intent when intent.AgentId is null:
                _logger?.Info($"Copilot SDK intent: {intent.Data.Intent}");
                break;
            case ToolExecutionStartEvent started:
                _logger?.Info($"Copilot SDK tool started: {started.Data.ToolName}.");
                break;
            case ToolExecutionCompleteEvent completed:
                _logger?.Info($"Copilot SDK tool completed: {completed.Data.ToolCallId} (success: {completed.Data.Success}).");
                break;
            case AssistantUsageEvent usage:
                _logger?.Info($"Copilot SDK usage (model: {usage.Data.Model}; input tokens: {usage.Data.InputTokens?.ToString() ?? "unknown"}; output tokens: {usage.Data.OutputTokens?.ToString() ?? "unknown"}).");
                break;
            case AssistantMessageEvent message when message.AgentId is null:
                _messageCount++;
                _logger?.Info($"Copilot SDK assistant response {_messageCount} (model: {message.Data.Model ?? "unknown"}; tool requests: {message.Data.ToolRequests?.Length ?? 0}):{Environment.NewLine}{message.Data.Content}");
                break;
            case SessionErrorEvent error:
                _logger?.Error($"Copilot SDK session error ({error.Data.ErrorType}): {error.Data.Message}");
                break;
            case SessionIdleEvent:
                _logger?.Info($"Copilot SDK streaming completed after {_streamedCharacters} assistant response characters.");
                break;
        }
    }
}
