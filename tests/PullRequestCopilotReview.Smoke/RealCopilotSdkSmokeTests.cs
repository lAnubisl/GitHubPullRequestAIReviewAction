using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Services;

namespace PullRequestCopilotReview.Smoke;

public sealed class RealCopilotSdkSmokeTests
{
    [Fact]
    [Trait("Category", "TokenGatedSmoke")]
    public async Task Real_sdk_session_streams_a_valid_json_review()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_COPILOT_SDK_SMOKE"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var token = Environment.GetEnvironmentVariable("COPILOT_GITHUB_TOKEN");
        Assert.False(string.IsNullOrWhiteSpace(token));
        var workspace = Path.Combine(Path.GetTempPath(), "pr-review-copilot-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var logger = new SmokeLogger();
            var review = await new CopilotSdkRunner(
                    new GitHubCopilotSdkClient(),
                    new SmokeConfiguration(token!, workspace),
                    new ReviewResponseValidator(),
                    new CopilotSessionEventLogger(logger))
                .RunReviewAsync(
                    "Return only this exact JSON object with no Markdown or prose: "
                    + "{\"summary\":\"Smoke test completed.\",\"findings\":[]}",
                    Array.Empty<Models.PullRequestFile>());

            Assert.Equal("Smoke test completed.", review.Summary);
            Assert.Empty(review.Findings);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private sealed class SmokeLogger : ILogger
    {
        public void Info(string message) => Console.WriteLine(message);
        public void Warning(string message) => Console.WriteLine(message);
        public void Error(string message) => Console.Error.WriteLine(message);
    }

    private sealed record SmokeConfiguration(string Token, string WorkspacePath) : IConfigurationHelper
    {
        public int MaxFindings => 1;
        public string MinSeverity => "low";
        public IReadOnlyList<string> ExcludePaths => Array.Empty<string>();
        public string? CopilotModel => null;
        public string? CopilotExtraInstructions => null;
        public bool FailOnFindings => false;
        public string GitHubToken => "unused";
        public string CopilotToken => Token;
        public string Repository => "smoke/test";
        public string EventPath => "unused";
        public string Workspace => WorkspacePath;
        public string? StepSummaryPath => null;
    }
}
