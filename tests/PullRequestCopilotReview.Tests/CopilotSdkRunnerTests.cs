using GitHub.Copilot;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;
using PullRequestCopilotReview.Services;

namespace PullRequestCopilotReview.Tests;

public sealed class CopilotSdkRunnerTests
{
    private static readonly PullRequestFile[] Files = [new("src/App.cs", "modified", 1, 0, "@@ -41 +42 @@\n+code", new DiffParser().Parse("@@ -41 +42 @@\n+code"))];

    [Fact]
    public async Task Builds_an_empty_mode_streaming_sdk_session_with_persistence_disabled()
    {
        var sdkClient = new GitHubCopilotSdkClient();
        var options = new CopilotSdkSessionOptions("token", Path.GetFullPath("workspace"), "gpt-test", ["view"]);
        var clientOptions = sdkClient.BuildClientOptions(options);
        var session = sdkClient.BuildSessionConfig(options);
        Assert.Equal(CopilotClientMode.Empty, clientOptions.Mode);
        Assert.StartsWith(Path.GetTempPath(), clientOptions.BaseDirectory, StringComparison.Ordinal);
        await using var client = new CopilotClient(clientOptions);
        Assert.True(session.Streaming);
        Assert.False(session.EnableFileHooks);
        Assert.False(session.EnableSessionStore);
    }

    [Fact]
    public async Task Uses_the_direct_response_and_streaming_is_logging_only()
    {
        var logger = new FakeTextLogger();
        var sdk = new FakeCopilotSdkClient([Message(ValidJson())], new AssistantMessageEvent { AgentId = "subagent", Data = new AssistantMessageData { MessageId = "subagent-message", Content = "not-json" } });
        var result = await Runner(sdk, new Configuration(), logger).RunReviewAsync("review", Files);
        Assert.Equal("Review complete.", result.Summary);
        Assert.Single(result.Findings);
        Assert.Single(sdk.Prompts);
        Assert.Contains(logger.InfoMessages, message => message == $"Copilot SDK prompt 1:{Environment.NewLine}review");
    }

    [Fact]
    public async Task Repairs_an_invalid_response_in_the_same_session()
    {
        var logger = new FakeTextLogger();
        var sdk = new FakeCopilotSdkClient([Message("```json\n{}\n```"), Message(ValidJson())]);
        var result = await Runner(sdk, new Configuration(), logger).RunReviewAsync("review", Files);
        Assert.Equal("Review complete.", result.Summary);
        Assert.Equal(1, sdk.CreatedSessionCount);
        Assert.Equal(2, sdk.Prompts.Count);
        Assert.Contains("complete replacement JSON document", sdk.Prompts[1]);
        Assert.Contains(logger.InfoMessages, message => message == $"Copilot SDK prompt 2:{Environment.NewLine}{sdk.Prompts[1]}");
    }

    [Fact]
    public async Task Repairs_invalid_paths_and_lines()
    {
        var invalid = """{"summary":"Review complete.","findings":[{"severity":"high","file":"src/Old.cs","line":99,"title":"Bug","body":"Details.","confidence":"high"}]}""";
        var sdk = new FakeCopilotSdkClient([Message(invalid), Message(ValidJson())]);
        await Runner(sdk, new Configuration()).RunReviewAsync("review", Files);
        Assert.Contains("is not a changed file", sdk.Prompts[1]);
    }

    [Fact]
    public async Task Fails_closed_after_three_invalid_attempts()
    {
        var sdk = new FakeCopilotSdkClient([Message("{}"), Message("{}"), Message("{}")]);
        var ex = await Assert.ThrowsAsync<ControlledFailureException>(() => Runner(sdk, new Configuration()).RunReviewAsync("review", Files));
        Assert.Equal(ExitCodes.CopilotFailure, ex.ExitCode);
        Assert.Equal(3, sdk.Prompts.Count);
    }

    [Fact]
    public async Task Null_and_sdk_failures_are_not_repaired()
    {
        var nullSdk = new FakeCopilotSdkClient([null]);
        await Assert.ThrowsAsync<ControlledFailureException>(() => Runner(nullSdk, new Configuration()).RunReviewAsync("review", Files));
        Assert.Single(nullSdk.Prompts);
        var failingSdk = new FakeCopilotSdkClient { ExceptionToThrow = new InvalidOperationException("authentication failed") };
        await Assert.ThrowsAsync<ControlledFailureException>(() => Runner(failingSdk, new Configuration()).RunReviewAsync("review", Files));
        Assert.Empty(failingSdk.Prompts);
    }

    private static string ValidJson() => """{"summary":"Review complete.","findings":[{"severity":"high","file":"src/App.cs","line":42,"title":"Bug","body":"Details.","confidence":"high"}]}""";
    private static AssistantMessageEvent Message(string content) => new() { Data = new AssistantMessageData { MessageId = Guid.NewGuid().ToString("N"), Content = content, ToolRequests = [] } };

    private static CopilotSdkRunner Runner(ICopilotSdkClient sdk, IConfigurationHelper configuration, ILogger? logger = null)
        => new(sdk, configuration, new ReviewResponseValidator(), new CopilotSessionEventLogger(logger));

    private sealed class Configuration : IConfigurationHelper
    {
        public int MaxFindings => 10; public string MinSeverity => "low"; public IReadOnlyList<string> ExcludePaths => []; public string? CopilotModel => null; public string? CopilotExtraInstructions => null; public bool FailOnFindings => false; public string GitHubToken => "github-token"; public string CopilotToken => "copilot-token"; public string Repository => "owner/repo"; public string EventPath => "event.json"; public string Workspace => Environment.CurrentDirectory; public string? StepSummaryPath => null;
    }
}
