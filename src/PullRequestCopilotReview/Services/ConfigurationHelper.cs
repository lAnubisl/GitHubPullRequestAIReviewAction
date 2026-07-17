using PullRequestCopilotReview.Interfaces;

namespace PullRequestCopilotReview.Services;

public sealed class ConfigurationHelper : IConfigurationHelper
{
    private static readonly HashSet<string> Severities = new(StringComparer.OrdinalIgnoreCase)
    {
        "low",
        "medium",
        "high"
    };

    public ConfigurationHelper()
    {
        MaxFindings = ParseIntActionInput("max_findings", 10, min: 0, max: 100);
        MinSeverity = ParseMinSeverity();
        IncludeFileContext = ParseBoolActionInput("include_file_context", true);
        FileContextLines = ParseIntActionInput("file_context_lines", 4, min: 0, max: 50);
        ExcludePaths = SplitCsv(GetActionInput("exclude_paths"));
        CopilotModel = EmptyToNull(GetActionInput("copilot_model"));
        CopilotExtraInstructions = EmptyToNull(GetActionInput("copilot_extra_instructions"));
        FailOnFindings = ParseBoolActionInput("fail_on_findings", false);
        GitHubToken = RequiredEnvironmentVariable("GH_CLI_TOKEN");
        CopilotToken = RequiredCopilotToken();
        Repository = ParseRepository();
        EventPath = RequiredFileEnvironmentVariable("GITHUB_EVENT_PATH");
        Workspace = RequiredDirectoryEnvironmentVariable("GITHUB_WORKSPACE");
        StepSummaryPath = OptionalFileEnvironmentVariable("GITHUB_STEP_SUMMARY");
    }

    public int MaxFindings { get; }
    public string MinSeverity { get; }
    public bool IncludeFileContext { get; }
    public int FileContextLines { get; }
    public IReadOnlyList<string> ExcludePaths { get; }
    public string? CopilotModel { get; }
    public string? CopilotExtraInstructions { get; }
    public bool FailOnFindings { get; }
    public string GitHubToken { get; }
    public string CopilotToken { get; }
    public string Repository { get; }
    public string EventPath { get; }
    public string Workspace { get; }
    public string? StepSummaryPath { get; }

    private string GetEnvironmentVariable(string name, string defaultValue = "")
        => Environment.GetEnvironmentVariable(name) ?? defaultValue;

    private string GetActionInput(string name, string defaultValue = "")
    {
        var key = $"INPUT_{name.Replace('-', '_').ToUpperInvariant()}";
        return GetEnvironmentVariable(key, defaultValue);
    }

    private string ParseMinSeverity()
    {
        var minSeverity = GetActionInput("min_severity", "low").Trim().ToLowerInvariant();
        if (!Severities.Contains(minSeverity))
        {
            throw new ControlledFailureException(
                ExitCodes.ConfigurationFailure,
                "Invalid min_severity. Expected one of: low, medium, high.");
        }

        return minSeverity;
    }

    private string ParseRepository()
    {
        var repository = RequiredEnvironmentVariable("GITHUB_REPOSITORY").Trim();
        var parts = repository.Split('/');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ControlledFailureException(
                ExitCodes.ConfigurationFailure,
                "GITHUB_REPOSITORY must use the 'owner/repository' format.");
        }

        return repository;
    }

    private string RequiredEnvironmentVariable(string name)
    {
        var value = GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ControlledFailureException(ExitCodes.ConfigurationFailure, $"{name} is required.");
        }

        return value;
    }

    private string RequiredFileEnvironmentVariable(string name)
    {
        var path = RequiredEnvironmentVariable(name);
        if (!File.Exists(path))
        {
            throw new ControlledFailureException(ExitCodes.ConfigurationFailure, $"{name} must point to an existing file.");
        }

        return Path.GetFullPath(path);
    }

    private string RequiredDirectoryEnvironmentVariable(string name)
    {
        var path = RequiredEnvironmentVariable(name);
        if (!Directory.Exists(path))
        {
            throw new ControlledFailureException(ExitCodes.ConfigurationFailure, $"{name} must point to an existing directory.");
        }

        return Path.GetFullPath(path);
    }

    private string? OptionalFileEnvironmentVariable(string name)
    {
        var value = EmptyToNull(GetEnvironmentVariable(name));
        if (value is null)
        {
            return null;
        }

        var path = Path.GetFullPath(value);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new ControlledFailureException(ExitCodes.ConfigurationFailure, $"{name} must point to a file in an existing directory.");
        }

        return path;
    }

    private string RequiredCopilotToken()
    {
        var token = GetEnvironmentVariable("COPILOT_GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            token = GetEnvironmentVariable("COPILOT_CLI_TOKEN");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ControlledFailureException(
                ExitCodes.ConfigurationFailure,
                "COPILOT_GITHUB_TOKEN is required (COPILOT_CLI_TOKEN is accepted as a legacy alias).");
        }

        return token;
    }

    private int ParseIntActionInput(string name, int defaultValue, int min, int max)
    {
        var raw = GetActionInput(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, out var value) || value < min || value > max)
        {
            throw new ControlledFailureException(ExitCodes.ConfigurationFailure, $"{name} must be an integer from {min} to {max}.");
        }

        return value;
    }

    private bool ParseBoolActionInput(string name, bool defaultValue)
    {
        var raw = GetActionInput(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (bool.TryParse(raw, out var value))
        {
            return value;
        }

        throw new ControlledFailureException(ExitCodes.ConfigurationFailure, $"{name} must be true or false.");
    }

    private static string[] SplitCsv(string raw)
        => raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? EmptyToNull(string raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw;
}
