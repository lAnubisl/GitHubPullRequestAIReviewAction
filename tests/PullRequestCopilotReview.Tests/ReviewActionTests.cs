using PullRequestCopilotReview;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;
using PullRequestCopilotReview.Services;

namespace PullRequestCopilotReview.Tests;

public sealed class ReviewActionTests
{
    private static readonly object EnvironmentLock = new();
    private static readonly string[] ConfigurationVariableNames =
    {
        "GH_CLI_TOKEN",
        "COPILOT_GITHUB_TOKEN",
        "COPILOT_CLI_TOKEN",
        "INPUT_MAX_FINDINGS",
        "INPUT_MIN_SEVERITY",
        "INPUT_EXCLUDE_PATHS",
        "INPUT_COPILOT_MODEL",
        "INPUT_COPILOT_EXTRA_INSTRUCTIONS",
        "INPUT_FAIL_ON_FINDINGS",
        "GITHUB_REPOSITORY",
        "GITHUB_EVENT_PATH",
        "GITHUB_WORKSPACE",
        "GITHUB_STEP_SUMMARY"
    };

    [Theory]
    [InlineData("GH_CLI_TOKEN")]
    [InlineData("COPILOT_GITHUB_TOKEN")]
    [InlineData("GITHUB_REPOSITORY")]
    [InlineData("GITHUB_EVENT_PATH")]
    [InlineData("GITHUB_WORKSPACE")]
    public void Validates_required_environment_values_during_creation(string missingValue)
    {
        var values = ValidConfigurationValues();
        values.Remove(missingValue);

        var exception = Assert.Throws<ControlledFailureException>(() => CreateConfiguration(values));

        Assert.Equal(ExitCodes.ConfigurationFailure, exception.ExitCode);
        Assert.Contains(missingValue, exception.Message);
    }

    [Theory]
    [InlineData("INPUT_MAX_FINDINGS", "not-a-number", "max_findings")]
    [InlineData("INPUT_MIN_SEVERITY", "critical", "min_severity")]
    [InlineData("INPUT_FAIL_ON_FINDINGS", "yes", "fail_on_findings")]
    public void Validates_action_inputs_during_creation(string name, string value, string expectedMessage)
    {
        var values = ValidConfigurationValues();
        values[name] = value;

        var exception = Assert.Throws<ControlledFailureException>(() => CreateConfiguration(values));

        Assert.Equal(ExitCodes.ConfigurationFailure, exception.ExitCode);
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void Validates_repository_format_during_creation()
    {
        var values = ValidConfigurationValues();
        values["GITHUB_REPOSITORY"] = "missing-owner-or-repository";

        var exception = Assert.Throws<ControlledFailureException>(() => CreateConfiguration(values));

        Assert.Equal(ExitCodes.ConfigurationFailure, exception.ExitCode);
        Assert.Contains("owner/repository", exception.Message);
    }

    [Fact]
    public void Accepts_the_legacy_copilot_cli_token_alias()
    {
        var values = ValidConfigurationValues();
        values.Remove("COPILOT_GITHUB_TOKEN");
        values["COPILOT_CLI_TOKEN"] = "legacy-token";

        var configuration = CreateConfiguration(values);

        Assert.Equal("legacy-token", configuration.CopilotToken);
    }

    [Fact]
    public void Infers_pull_request_context_from_event_path()
    {
        using var temp = new TempWorkspace();
        var eventPath = temp.WriteEvent("""
            {
              "number": 42,
              "pull_request": {
                "base": {"ref": "main", "sha": "base-sha"},
                "head": {"ref": "feature", "sha": "head-sha"}
              }
            }
            """);

        var configuration = TestConfiguration(eventPath: eventPath);
        var context = new GitHubContextReader(configuration).Read();

        Assert.Equal("owner/repo", context.Repository);
        Assert.Equal(42, context.PullRequestNumber);
        Assert.Equal("main", context.BaseRef);
        Assert.Equal("head-sha", context.HeadSha);
    }

    [Fact]
    public void Fails_outside_pull_request_context()
    {
        using var temp = new TempWorkspace();
        var eventPath = temp.WriteEvent("""{"ref":"refs/heads/main"}""");
        var configuration = TestConfiguration(eventPath: eventPath);

        var ex = Assert.Throws<ControlledFailureException>(() => new GitHubContextReader(configuration).Read());

        Assert.Equal(ExitCodes.ConfigurationFailure, ex.ExitCode);
        Assert.Contains("pull request event", ex.Message);
    }

    [Fact]
    public void Isolates_tokens_between_github_commands_and_copilot_sdk_options()
    {
        var configuration = TestConfiguration(githubToken: "github-secret", copilotToken: "copilot-secret");

        var githubEnv = PullRequestService(new FakeCommandRunner(), configuration).GitHubEnvironment();
        var copilotOptions = CopilotRunner(new FakeCopilotSdkClient(), configuration)
            .BuildSessionOptions();

        Assert.Equal("github-secret", githubEnv["GH_TOKEN"]);
        Assert.DoesNotContain(githubEnv, pair => pair.Value == "copilot-secret");
        Assert.Equal("copilot-secret", copilotOptions.GitHubToken);
        Assert.DoesNotContain("github-secret", new[] { copilotOptions.GitHubToken });
    }

    [Fact]
    public void Builds_streaming_sdk_options_with_review_only_tools()
    {
        var configuration = TestConfiguration(copilotModel: "gpt-test");
        var options = CopilotRunner(new FakeCopilotSdkClient(), configuration)
            .BuildSessionOptions();

        Assert.Equal("gpt-test", options.Model);
        Assert.Equal(configuration.Workspace, options.WorkingDirectory);
        Assert.Equal(new[] { "view", "grep", "glob", "web_fetch" }, options.AvailableTools);
    }

    [Fact]
    public void Generates_prompt_for_review_only_json_output()
    {
        var configuration = TestConfiguration(extraInstructions: "Focus on auth boundaries.");
        var context = TestContext();
        var files = new[]
        {
            TestFile("src/App.cs", "@@ -1 +1,2 @@\n class App\n+throw null;")
        };

        var prompt = new PromptBuilder(configuration).Build(context, files);

        Assert.Contains("senior code reviewer", prompt);
        Assert.Contains("Do not create, modify, move, or delete files", prompt);
        Assert.Contains("research public documentation on the web", prompt);
        Assert.Contains("Expected JSON structure:", prompt);
        Assert.Contains("The object must contain exactly summary and findings", prompt);
        Assert.Contains("summary and findings are published in a pull request summary comment", prompt);
        Assert.Contains("Example with no findings:", prompt);
        Assert.Contains("Example with a finding:", prompt);
        Assert.Contains("\"findings\":[]", prompt);
        Assert.Contains("Focus on auth boundaries.", prompt);
        Assert.Contains("src/App.cs", prompt);
        Assert.Contains("throw null", prompt);
        Assert.Contains("The first character of your response must be { and the last character must be }", prompt);
        Assert.Contains("do not start with ```json or end with ```", prompt);
        Assert.EndsWith($"Begin your response now with {{.{Environment.NewLine}", prompt);
        Assert.True(
            prompt.LastIndexOf("FINAL RESPONSE CONTRACT", StringComparison.Ordinal) >
            prompt.LastIndexOf("throw null", StringComparison.Ordinal));
    }

    [Fact]
    public void Preserves_final_response_contract_when_prompt_is_truncated()
    {
        var prompt = new PromptBuilder(TestConfiguration()).Build(
            TestContext(),
            [TestFile("src/Big.cs", "@@ -1 +1 @@\n+" + new string('x', 250_000))]);

        Assert.Equal(240_000, prompt.Length);
        Assert.Contains("[prompt truncated]", prompt);
        Assert.Contains("FINAL RESPONSE CONTRACT (mandatory):", prompt);
        Assert.EndsWith($"Begin your response now with {{.{Environment.NewLine}", prompt);
    }

    [Fact]
    public async Task Truncates_changed_file_diff_data()
    {
        var longPatch = "@@ -1 +1 @@\n+" + new string('x', 130_000);
        var runner = new FakeCommandRunner(new CommandResult(0, $$"""[[{"filename":"src/Big.cs","status":"modified","additions":1,"deletions":0,"patch":{{JsonString(longPatch)}}}]]""", string.Empty));
        var configuration = TestConfiguration();
        var service = PullRequestService(runner, configuration);

        var files = await service.GetChangedFilesAsync(TestContext());

        Assert.Single(files);
        Assert.True(files[0].Patch!.Length < longPatch.Length);
        Assert.Contains("[diff truncated]", files[0].Patch);
    }

    [Fact]
    public async Task Logs_a_short_summary_of_changed_files_instead_of_streaming_the_response()
    {
        const string patch = "@@ -1 +1,3 @@\n-old\n+new\n+more";
        var runner = new FakeCommandRunner(new CommandResult(
            0,
            $$"""[[{"filename":"src/App.cs","status":"modified","additions":2,"deletions":1,"patch":{{JsonString(patch)}}},{"filename":"README.md","status":"modified","additions":1,"deletions":0,"patch":"@@ -1 +1 @@\n+docs"}]]""",
            string.Empty));
        var logger = new FakeTextLogger();
        var service = PullRequestService(runner, TestConfiguration(), logger);

        await service.GetChangedFilesAsync(TestContext());

        Assert.Equal(CommandOutputLogging.CaptureOnly, Assert.Single(runner.Calls).StandardOutputLogging);
        Assert.Contains("Received 2 changed pull request file(s):", logger.InfoMessages);
        Assert.Contains("- src/App.cs: 3 change(s)", logger.InfoMessages);
        Assert.Contains("- README.md: 1 change(s)", logger.InfoMessages);
        Assert.DoesNotContain(logger.InfoMessages, message => message.Contains(patch, StringComparison.Ordinal));
    }

    [Fact]
    public void Excludes_paths_by_glob()
    {
        var files = new[]
        {
            TestFile("src/App.cs", "@@ -1 +1 @@\n+code"),
            TestFile("docs/readme.md", "@@ -1 +1 @@\n+docs"),
            TestFile("src/generated/Client.cs", "@@ -1 +1 @@\n+gen")
        };

        var filtered = new PathFilter().Apply(files, new[] { "docs/*", "src/generated/*" }).ToArray();

        Assert.Single(filtered);
        Assert.Equal("src/App.cs", filtered[0].FileName);
    }

    [Fact]
    public void Rejects_markdown_fenced_review_output()
    {
        var validation = new ReviewResponseValidator().Validate("""
            ```json
            {
              "summary": "Looks risky.",
              "findings": [
                {"severity":"high","file":"src/App.cs","line":12,"title":"Null dereference","body":"Check for null.","confidence":"high"}
              ]
            }
            ```
            """, Array.Empty<PullRequestFile>());
        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task Publishes_step_summary()
    {
        using var temp = new TempWorkspace();
        var summaryPath = Path.Combine(temp.Root, "summary.md");
        var configuration = TestConfiguration(stepSummaryPath: summaryPath);
        var writer = new StepSummaryWriter(configuration);
        var review = new ReviewResult("Review done.", new[] { new ReviewFinding("medium", "src/App.cs", 5, "Bug", "Fix it.", "high") });

        await writer.WriteAsync(TestContext(), new[] { TestFile("src/App.cs", "@@ -4 +5 @@\n+bug") }, review);

        var text = await File.ReadAllTextAsync(summaryPath);
        Assert.Contains("AI Pull Request Review", text);
        Assert.Contains("src/App.cs:5", text);
    }

    [Fact]
    public async Task Falls_back_to_summary_when_inline_comment_cannot_map_to_diff()
    {
        var runner = new FakeCommandRunner(new CommandResult(0, "{}", string.Empty));
        var configuration = TestConfiguration();
        var publisher = new ReviewPublisher(runner, configuration);
        var review = new ReviewResult("Review done.", new[] { new ReviewFinding("medium", "src/App.cs", 99, "Bug", "Fix it.", "high") });
        var files = new[] { TestFile("src/App.cs", "@@ -1 +1,2 @@\n line\n+new line") };

        var summary = await publisher.PublishAsync(TestContext(), files, review);

        Assert.Single(summary.FallbackFindings);
        Assert.Single(runner.Calls);
        Assert.Contains("/issues/7/comments", runner.Calls[0].Arguments[1]);
    }

    [Fact]
    public async Task Always_publishes_inline_findings_and_a_summary()
    {
        var runner = new FakeCommandRunner(
            new CommandResult(0, "{}", string.Empty),
            new CommandResult(0, "{}", string.Empty));
        var configuration = TestConfiguration();
        var publisher = new ReviewPublisher(runner, configuration);
        var review = new ReviewResult(
            "Review done.",
            [new ReviewFinding("medium", "src/App.cs", 2, "Bug", "Fix it.", "high")]);
        var files = new[] { TestFile("src/App.cs", "@@ -1 +1,2 @@\n line\n+new line") };

        var summary = await publisher.PublishAsync(TestContext(), files, review);

        Assert.Equal(1, summary.InlineCommentsPublished);
        Assert.Empty(summary.FallbackFindings);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Contains("/pulls/7/comments", runner.Calls[0].Arguments[1]);
        Assert.Contains("/issues/7/comments", runner.Calls[1].Arguments[1]);
    }

    [Fact]
    public async Task Fail_on_findings_returns_non_zero_when_threshold_matches()
    {
        using var temp = new TempWorkspace();
        var eventPath = temp.WriteEvent("""
            {
              "number": 7,
              "pull_request": {
                "base": {"ref": "main", "sha": "base-sha"},
                "head": {"ref": "feature", "sha": "head-sha"}
              }
            }
            """);

        var runner = new FakeCommandRunner(
            new CommandResult(0, """[[{"filename":"src/App.cs","status":"modified","additions":1,"deletions":0,"patch":"@@ -1 +1,2 @@\n line\n+bug"}]]""", string.Empty),
            new CommandResult(0, "{}", string.Empty));
        var review = new ReviewResult(
            "Review done.",
            [new ReviewFinding("high", "src/App.cs", 2, "Bug", "Fix it.", "high")]);
        var configuration = TestConfiguration(
            minSeverity: "medium",
            eventPath: eventPath,
            workspace: temp.Root,
            failOnFindings: true);

        var app = new PullRequestReviewApp(
            new TextLogger(),
            configuration,
            new GitHubContextReader(configuration),
            PullRequestService(runner, configuration),
            new PromptBuilder(configuration),
            new FakeCopilotRunner(review),
            new StepSummaryWriter(configuration),
            new ReviewPublisher(runner, configuration),
            new PathFilter(),
            new GitHubSecretMasker(),
            new ReviewFilter());

        var exitCode = await app.RunAsync();

        Assert.Equal(ExitCodes.FindingsFailure, exitCode);
    }

    private static IConfigurationHelper TestConfiguration(
        string minSeverity = "low",
        int maxFindings = 10,
        string? eventPath = null,
        string? workspace = null,
        string githubToken = "gh-token",
        string copilotToken = "copilot-token",
        string? copilotModel = null,
        string? extraInstructions = null,
        bool failOnFindings = false,
        string? stepSummaryPath = null)
        => CreateConfiguration(new Dictionary<string, string?>
        {
            ["GH_CLI_TOKEN"] = githubToken,
            ["COPILOT_GITHUB_TOKEN"] = copilotToken,
            ["INPUT_MAX_FINDINGS"] = maxFindings.ToString(),
            ["INPUT_MIN_SEVERITY"] = minSeverity,
            ["INPUT_COPILOT_MODEL"] = copilotModel,
            ["INPUT_COPILOT_EXTRA_INSTRUCTIONS"] = extraInstructions,
            ["INPUT_FAIL_ON_FINDINGS"] = failOnFindings.ToString(),
            ["GITHUB_REPOSITORY"] = "owner/repo",
            ["GITHUB_EVENT_PATH"] = eventPath ?? typeof(ReviewActionTests).Assembly.Location,
            ["GITHUB_WORKSPACE"] = workspace ?? Environment.CurrentDirectory,
            ["GITHUB_STEP_SUMMARY"] = stepSummaryPath
        });

    private static Dictionary<string, string?> ValidConfigurationValues()
        => new()
        {
            ["GH_CLI_TOKEN"] = "gh-token",
            ["COPILOT_GITHUB_TOKEN"] = "copilot-token",
            ["GITHUB_REPOSITORY"] = "owner/repo",
            ["GITHUB_EVENT_PATH"] = typeof(ReviewActionTests).Assembly.Location,
            ["GITHUB_WORKSPACE"] = Environment.CurrentDirectory
        };

    private static ConfigurationHelper CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        lock (EnvironmentLock)
        {
            var originalValues = ConfigurationVariableNames.ToDictionary(
                name => name,
                Environment.GetEnvironmentVariable,
                StringComparer.Ordinal);

            try
            {
                foreach (var name in ConfigurationVariableNames)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }

                foreach (var pair in values)
                {
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                }

                return new ConfigurationHelper();
            }
            finally
            {
                foreach (var pair in originalValues)
                {
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                }
            }
        }
    }

    private static PullRequestContext TestContext()
        => new("owner/repo", 7, "main", "base-sha", "feature", "head-sha");

    private static PullRequestFile TestFile(string fileName, string patch)
        => new(fileName, "modified", 1, 0, patch, new DiffParser().Parse(patch));

    private static GitHubPullRequestService PullRequestService(
        ICommandRunner runner,
        IConfigurationHelper configuration,
        ILogger? logger = null)
        => new(runner, configuration, new DiffParser(), logger ?? new FakeTextLogger());

    private static CopilotSdkRunner CopilotRunner(ICopilotSdkClient client, IConfigurationHelper configuration)
        => new(client, configuration, new ReviewResponseValidator(), new CopilotSessionEventLogger(null));

    private static string JsonString(string value)
        => System.Text.Json.JsonSerializer.Serialize(value);

}
