using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class ReviewPublisher : IReviewPublisher
{
    private readonly ICommandRunner _commandRunner;
    private readonly IConfigurationHelper _configuration;

    public ReviewPublisher(ICommandRunner commandRunner, IConfigurationHelper configuration)
    {
        _commandRunner = commandRunner;
        _configuration = configuration;
    }

    public async Task<ActionSummary> PublishAsync(
        PullRequestContext context,
        IReadOnlyList<PullRequestFile> files,
        ReviewResult review,
        CancellationToken cancellationToken = default)
    {
        var fallback = new List<ReviewFinding>();
        var inlinePublished = 0;

        if (_configuration.ReviewMode.Contains("comments", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var finding in review.Findings)
            {
                if (!files.Any(file => CanMapFindingToFile(finding, file)))
                {
                    fallback.Add(finding);
                    continue;
                }

                var result = await _commandRunner.RunAsync(
                    new CommandSpec(
                        "gh",
                        new[]
                        {
                            "api",
                            $"repos/{context.Repository}/pulls/{context.PullRequestNumber}/comments",
                            "--method",
                            "POST",
                            "-f",
                            $"body={FormatFinding(finding)}",
                            "-f",
                            $"commit_id={context.HeadSha}",
                            "-f",
                            $"path={finding.File}",
                            "-F",
                            $"line={finding.Line}",
                            "-f",
                            "side=RIGHT"
                        },
                        GitHubEnvironment(),
                        WorkingDirectory: _configuration.Workspace),
                    cancellationToken);

                if (result.ExitCode == 0)
                {
                    inlinePublished++;
                }
                else
                {
                    fallback.Add(finding);
                }
            }
        }

        if (_configuration.ReviewMode.Contains("summary", StringComparison.OrdinalIgnoreCase) || fallback.Count > 0)
        {
            var body = FormatSummary(review, fallback);
            var result = await _commandRunner.RunAsync(
                new CommandSpec(
                    "gh",
                    new[]
                    {
                        "api",
                        $"repos/{context.Repository}/issues/{context.PullRequestNumber}/comments",
                        "--method",
                        "POST",
                        "-f",
                        $"body={body}"
                    },
                    GitHubEnvironment(),
                    WorkingDirectory: _configuration.Workspace),
                cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new ControlledFailureException(ExitCodes.GitHubFailure, $"Failed to publish review summary: {result.StandardError.Trim()}");
            }
        }

        return new ActionSummary(inlinePublished, fallback);
    }

    public static bool CanMapFindingToFile(ReviewFinding finding, PullRequestFile file)
        => string.Equals(finding.File.Replace('\\', '/'), file.FileName.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
           && file.Hunks.Any(hunk => hunk.NewLines.Contains(finding.Line));

    private static string FormatSummary(ReviewResult review, IReadOnlyList<ReviewFinding> fallbackFindings)
    {
        var findings = fallbackFindings.Count > 0 ? fallbackFindings : review.Findings;
        var lines = new List<string>
        {
            "## AI Pull Request Review",
            string.Empty,
            review.Summary,
            string.Empty,
            "### Findings"
        };

        if (findings.Count == 0)
        {
            lines.Add("No findings met the configured threshold.");
        }
        else
        {
            lines.AddRange(findings.Select(FormatFinding));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatFinding(ReviewFinding finding)
        => $"**{finding.Severity}** `{finding.File}:{finding.Line}` - {finding.Title}{Environment.NewLine}{finding.Body}";

    private IReadOnlyDictionary<string, string> GitHubEnvironment()
        => new Dictionary<string, string>
        {
            ["GH_TOKEN"] = _configuration.GitHubToken,
            ["GITHUB_TOKEN"] = _configuration.GitHubToken,
            ["NO_COLOR"] = "1"
        };
}
