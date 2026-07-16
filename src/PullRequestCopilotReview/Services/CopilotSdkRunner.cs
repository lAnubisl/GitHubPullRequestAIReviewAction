using GitHub.Copilot;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class CopilotSdkRunner : ICopilotRunner
{
    private static readonly string[] ReviewOnlyTools = ["view", "grep", "glob", "web_fetch"];
    private readonly ICopilotSdkClient _sdkClient;
    private readonly IConfigurationHelper _configuration;
    private readonly ICopilotSessionEventLogger _eventLogger;
    private readonly IReviewResponseValidator _reviewResponseValidator;
    private const int MaximumAttempts = 3;

    public CopilotSdkRunner(
        ICopilotSdkClient sdkClient,
        IConfigurationHelper configuration,
        IReviewResponseValidator reviewResponseValidator,
        ICopilotSessionEventLogger eventLogger)
    {
        _sdkClient = sdkClient;
        _configuration = configuration;
        _reviewResponseValidator = reviewResponseValidator;
        _eventLogger = eventLogger;
    }

    public async Task<ReviewResult> RunReviewAsync(
        string prompt,
        IReadOnlyList<PullRequestFile> changedFiles,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunReviewAttemptsAsync(prompt, changedFiles, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ControlledFailureException)
        {
            throw new ControlledFailureException(
                ExitCodes.CopilotFailure,
                $"GitHub Copilot SDK review failed: {ex.Message}",
                ex);
        }
    }

    public async Task<ReviewResult> RunReviewAttemptsAsync(
        string prompt,
        IReadOnlyList<PullRequestFile> changedFiles,
        CancellationToken cancellationToken = default)
    {
        await using var session = await _sdkClient.CreateSessionAsync(
            BuildSessionOptions(), _eventLogger.Handle, cancellationToken);

        var attemptPrompt = prompt;
        IReadOnlyList<string> errors = Array.Empty<string>();
        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            var response = await session.SendAndWaitAsync(attemptPrompt, cancellationToken);
            if (response is null)
            {
                throw new ControlledFailureException(
                    ExitCodes.CopilotFailure,
                    "GitHub Copilot SDK completed without an assistant response.");
            }

            var validation = _reviewResponseValidator.Validate(response.Data.Content, changedFiles);
            if (validation.IsValid) return validation.Result!;
            errors = validation.Errors;
            if (attempt < MaximumAttempts)
            {
                attemptPrompt = BuildCorrectionPrompt(errors);
            }
        }

        throw new ControlledFailureException(
            ExitCodes.CopilotFailure,
            $"Copilot review response remained invalid after {MaximumAttempts} attempts: {string.Join("; ", errors)}");
    }

    private static string BuildCorrectionPrompt(IReadOnlyList<string> errors)
        => "Your previous response is invalid. Return a complete replacement JSON document only: no Markdown fences, patches, explanation, or surrounding prose. Correct every error below while preserving otherwise valid findings.\nValidation errors:\n"
            + string.Join("\n", errors.Select(error => $"- {error}"));

    public CopilotSdkSessionOptions BuildSessionOptions()
        => new(
            _configuration.CopilotToken,
            _configuration.Workspace,
            _configuration.CopilotModel,
            ReviewOnlyTools);
}
