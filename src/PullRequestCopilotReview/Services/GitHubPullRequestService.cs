using System.Text.Json;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class GitHubPullRequestService : IGitHubPullRequestService
{
    private readonly ICommandRunner _commandRunner;
    private readonly IConfigurationHelper _configuration;
    private readonly IDiffParser _diffParser;
    private readonly ILocalFileContextReader _localFileContextReader;
    private readonly ILogger _logger;

    public GitHubPullRequestService(
        ICommandRunner commandRunner,
        IConfigurationHelper configuration,
        IDiffParser diffParser,
        ILocalFileContextReader localFileContextReader,
        ILogger logger)
    {
        _commandRunner = commandRunner;
        _configuration = configuration;
        _diffParser = diffParser;
        _localFileContextReader = localFileContextReader;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PullRequestFile>> GetChangedFilesAsync(
        PullRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await _commandRunner.RunAsync(
            new CommandSpec(
                "gh",
                new[]
                {
                    "api",
                    $"repos/{context.Repository}/pulls/{context.PullRequestNumber}/files?per_page=100",
                    "--paginate",
                    "--slurp"
                },
                GitHubEnvironment(),
                WorkingDirectory: _configuration.Workspace,
                StandardOutputLogging: CommandOutputLogging.CaptureOnly),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new ControlledFailureException(ExitCodes.GitHubFailure, $"Failed to fetch changed files: {result.StandardError.Trim()}");
        }

        var dto = ParseFiles(result.StandardOutput);
        _logger.Info($"Received {dto.Count} changed pull request file(s):");
        foreach (var file in dto)
        {
            _logger.Info($"- {file.FileName ?? string.Empty}: {file.Additions + file.Deletions} change(s)");
        }

        return dto.Select(file =>
        {
            var patch = Truncate(file.Patch, 120_000);
            var hunks = _diffParser.Parse(patch).ToArray();
            var localContext = _configuration.IncludeFileContext
                ? _localFileContextReader.Read(_configuration.Workspace, file.FileName ?? string.Empty, hunks, _configuration.FileContextLines)
                : null;

            return new PullRequestFile(
                file.FileName ?? string.Empty,
                file.Status ?? "modified",
                file.Additions,
                file.Deletions,
                patch,
                hunks,
                localContext);
        }).ToArray();
    }

    public IReadOnlyDictionary<string, string> GitHubEnvironment()
        => new Dictionary<string, string>
        {
            ["GH_TOKEN"] = _configuration.GitHubToken,
            ["GITHUB_TOKEN"] = _configuration.GitHubToken,
            ["NO_COLOR"] = "1"
        };

    private static IReadOnlyList<PullRequestFileDto> ParseFiles(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (root.ValueKind == JsonValueKind.Array
            && root.GetArrayLength() > 0
            && root[0].ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray()
                .SelectMany(page => page.Deserialize<PullRequestFileDto[]>(options) ?? Array.Empty<PullRequestFileDto>())
                .ToArray();
        }

        return JsonSerializer.Deserialize<PullRequestFileDto[]>(json, options) ?? Array.Empty<PullRequestFileDto>();
    }

    private static string? Truncate(string? value, int maxCharacters)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
        {
            return value;
        }

        return value[..maxCharacters] + "\n[diff truncated]";
    }
}
