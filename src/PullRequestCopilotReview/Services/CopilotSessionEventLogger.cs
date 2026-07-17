using System.Collections.Concurrent;
using System.Text.Json;
using GitHub.Copilot;
using PullRequestCopilotReview.Interfaces;

namespace PullRequestCopilotReview.Services;

public sealed class CopilotSessionEventLogger : ICopilotSessionEventLogger
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, string> _toolActivities = new();
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
                var activity = DescribeToolActivity(started.Data.ToolName, started.Data.Arguments);
                _toolActivities[started.Data.ToolCallId] = activity;
                _logger?.Info($"Copilot: {activity} (tool: {started.Data.ToolName}; call: {started.Data.ToolCallId}).");
                break;
            case ToolExecutionCompleteEvent completed:
                var completedActivity = _toolActivities.TryRemove(completed.Data.ToolCallId, out var description)
                    ? description
                    : "Running a repository inspection tool";
                if (completed.Data.Success)
                {
                    _logger?.Info($"Copilot: Completed {LowercaseFirst(completedActivity)} (call: {completed.Data.ToolCallId}).");
                }
                else
                {
                    var error = DescribeError(completed.Data.Error);
                    _logger?.Warning(
                        $"Copilot: Failed while {LowercaseFirst(completedActivity)} "
                        + $"(call: {completed.Data.ToolCallId}; error: {error ?? "not reported by SDK"}).");
                }
                break;
            case AssistantUsageEvent usage:
                _logger?.Info($"Copilot SDK usage (model: {usage.Data.Model}; input tokens: {usage.Data.InputTokens?.ToString() ?? "unknown"}; output tokens: {usage.Data.OutputTokens?.ToString() ?? "unknown"}).");
                break;
            case AssistantMessageEvent message when message.AgentId is null:
                _messageCount++;
                var toolRequestCount = message.Data.ToolRequests?.Length ?? 0;
                var messageKind = toolRequestCount > 0 ? "intermediate tool-request message" : "assistant response";
                _logger?.Info($"Copilot SDK {messageKind} {_messageCount} (model: {message.Data.Model ?? "unknown"}; tool requests: {toolRequestCount}):{Environment.NewLine}{message.Data.Content}");
                break;
            case SessionErrorEvent error:
                _logger?.Error($"Copilot SDK session error ({error.Data.ErrorType}): {error.Data.Message}");
                break;
            case SessionIdleEvent:
                _logger?.Info($"Copilot SDK streaming completed after {_streamedCharacters} assistant response characters.");
                break;
        }
    }

    private static string DescribeToolActivity(string toolName, object? arguments)
    {
        var properties = ReadArguments(arguments);
        var path = First(properties, "path", "file_path", "file", "directory", "search_path") ?? ".";

        return toolName switch
        {
            "view" => $"Reading `{Bounded(path, 300)}`",
            "grep" => DescribeSearch(properties, path),
            "glob" => $"Finding files matching `{Bounded(First(properties, "pattern", "glob") ?? "*", 200)}` in `{Bounded(path, 300)}`",
            "web_fetch" => $"Fetching `{SanitizeUrl(First(properties, "url") ?? "a public URL")}`",
            _ => $"Running `{toolName}`"
        };
    }

    private static string DescribeSearch(IReadOnlyDictionary<string, string> properties, string path)
    {
        var query = First(properties, "pattern", "query", "search_term") ?? "a text pattern";
        var include = First(properties, "include", "glob");
        return include is null
            ? $"Searching for `{Bounded(query, 200)}` in `{Bounded(path, 300)}`"
            : $"Searching for `{Bounded(query, 200)}` in `{Bounded(path, 300)}` (files: `{Bounded(include, 200)}`)";
    }

    private static IReadOnlyDictionary<string, string> ReadArguments(object? arguments)
    {
        if (arguments is null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var element = arguments is JsonElement json
                ? json
                : JsonSerializer.SerializeToElement(arguments);
            if (element.ValueKind == JsonValueKind.String)
            {
                using var parsed = JsonDocument.Parse(element.GetString() ?? "{}");
                return ReadObject(parsed.RootElement);
            }

            return ReadObject(element);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (NotSupportedException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyDictionary<string, string> ReadObject(JsonElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object) return result;
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
        }

        return result;
    }

    private static string? First(IReadOnlyDictionary<string, string> properties, params string[] names)
        => names.Select(name => properties.TryGetValue(name, out var value) ? value : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string SanitizeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return Bounded(value, 300) ?? string.Empty;
        return Bounded(uri.GetLeftPart(UriPartial.Path), 300) ?? string.Empty;
    }

    private static string LowercaseFirst(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static string? DescribeError(ToolExecutionCompleteError? error)
    {
        if (error is null) return null;
        var code = string.IsNullOrWhiteSpace(error.Code) ? null : error.Code;
        var message = string.IsNullOrWhiteSpace(error.Message) ? null : error.Message;
        return Bounded(code is null ? message : message is null ? code : $"{code}: {message}", 1_000);
    }

    private static string? Bounded(string? value, int maximumLength)
    {
        if (value is null) return null;
        var singleLine = value.Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= maximumLength ? singleLine : singleLine[..maximumLength] + "…";
    }
}
