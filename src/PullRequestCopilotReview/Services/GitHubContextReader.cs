using System.Text.Json;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class GitHubContextReader : IGitHubContextReader
{
    private readonly IConfigurationHelper _configuration;

    public GitHubContextReader(IConfigurationHelper configuration)
    {
        _configuration = configuration;
    }

    public PullRequestContext Read()
    {
        using var stream = File.OpenRead(_configuration.EventPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        if (!root.TryGetProperty("pull_request", out var pullRequest))
        {
            throw new ControlledFailureException(ExitCodes.ConfigurationFailure, "This action requires a pull request event context. Configure the consumer workflow with a pull_request trigger.");
        }

        var number = GetInt(root, "number");
        if (number <= 0)
        {
            number = GetInt(pullRequest, "number");
        }

        if (number <= 0)
        {
            throw new ControlledFailureException(ExitCodes.ConfigurationFailure, "Could not infer the pull request number from GITHUB_EVENT_PATH.");
        }

        var baseElement = pullRequest.GetProperty("base");
        var headElement = pullRequest.GetProperty("head");
        return new PullRequestContext(
            _configuration.Repository,
            number,
            GetString(baseElement, "ref"),
            GetString(baseElement, "sha"),
            GetString(headElement, "ref"),
            GetString(headElement, "sha"));
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return 0;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : 0;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }
}
