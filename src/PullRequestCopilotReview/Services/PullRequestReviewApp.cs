using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class PullRequestReviewApp : IReviewApplication
{
    private readonly ILogger _logger;
    private readonly IConfigurationHelper _configuration;
    private readonly IGitHubContextReader _contextReader;
    private readonly IGitHubPullRequestService _pullRequestService;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ICopilotRunner _copilotRunner;
    private readonly IStepSummaryWriter _summaryWriter;
    private readonly IReviewPublisher _reviewPublisher;
    private readonly IPathFilter _pathFilter;
    private readonly ISecretMasker _secretMasker;
    private readonly IReviewFilter _reviewFilter;

    public PullRequestReviewApp(ILogger logger, IConfigurationHelper configuration, IGitHubContextReader contextReader, IGitHubPullRequestService pullRequestService, IPromptBuilder promptBuilder, ICopilotRunner copilotRunner, IStepSummaryWriter summaryWriter, IReviewPublisher reviewPublisher, IPathFilter pathFilter, ISecretMasker secretMasker, IReviewFilter reviewFilter)
        => (_logger, _configuration, _contextReader, _pullRequestService, _promptBuilder, _copilotRunner, _summaryWriter, _reviewPublisher, _pathFilter, _secretMasker, _reviewFilter) = (logger, configuration, contextReader, pullRequestService, promptBuilder, copilotRunner, summaryWriter, reviewPublisher, pathFilter, secretMasker, reviewFilter);

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _secretMasker.Mask(_configuration.GitHubToken, _configuration.CopilotToken);
            var context = _contextReader.Read();
            _logger.Info($"Reviewing {context.Repository} pull request #{context.PullRequestNumber}.");
            var files = await _pullRequestService.GetChangedFilesAsync(context, cancellationToken);
            var filteredFiles = _pathFilter.Apply(files, _configuration.ExcludePaths);
            var review = await _copilotRunner.RunReviewAsync(_promptBuilder.Build(context, filteredFiles), filteredFiles, cancellationToken);
            review = _reviewFilter.Apply(review, _configuration.MinSeverity, _configuration.MaxFindings, filteredFiles.Select(file => file.FileName));
            await _summaryWriter.WriteAsync(context, filteredFiles, review, cancellationToken);
            var published = await _reviewPublisher.PublishAsync(context, filteredFiles, review, cancellationToken);
            if (published.FallbackFindings.Count > 0) _logger.Warning($"{published.FallbackFindings.Count} finding(s) could not be mapped safely to the PR diff and were included in the summary comment.");
            if (_configuration.FailOnFindings && review.Findings.Count > 0) throw new ControlledFailureException(ExitCodes.FindingsFailure, "Review findings met fail_on_findings criteria.");
            return ExitCodes.Success;
        }
        catch (ControlledFailureException ex) { _logger.Error(ex.Message); return ex.ExitCode; }
        catch (Exception ex) { _logger.Error(ex.ToString()); return ExitCodes.UnexpectedFailure; }
    }
}
