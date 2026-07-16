using System.Text;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class StepSummaryWriter : IStepSummaryWriter
{
    private readonly IConfigurationHelper _configuration;

    public StepSummaryWriter(IConfigurationHelper configuration)
    {
        _configuration = configuration;
    }

    public async Task WriteAsync(
        PullRequestContext context,
        IReadOnlyList<PullRequestFile> files,
        ReviewResult review,
        CancellationToken cancellationToken = default)
    {
        if (_configuration.StepSummaryPath is null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("# AI Pull Request Review");
        builder.AppendLine();
        builder.AppendLine($"Repository: `{context.Repository}`");
        builder.AppendLine($"Pull request: `#{context.PullRequestNumber}`");
        builder.AppendLine($"Files reviewed: `{files.Count}`");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine(review.Summary);
        builder.AppendLine();
        builder.AppendLine("## Findings");

        if (review.Findings.Count == 0)
        {
            builder.AppendLine("No findings met the configured threshold.");
        }
        else
        {
            foreach (var finding in review.Findings)
            {
                builder.AppendLine($"- **{finding.Severity}** `{finding.File}:{finding.Line}` {finding.Title}");
            }
        }

        await File.AppendAllTextAsync(_configuration.StepSummaryPath, builder.ToString(), cancellationToken);
    }
}
