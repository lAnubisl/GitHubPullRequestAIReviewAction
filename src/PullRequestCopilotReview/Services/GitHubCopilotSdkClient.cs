using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class GitHubCopilotSdkClient : ICopilotSdkClient
{
    public async Task<ICopilotSdkSession> CreateSessionAsync(
        CopilotSdkSessionOptions options,
        Action<SessionEvent> onEvent,
        CancellationToken cancellationToken = default)
    {
        var client = new CopilotClient(BuildClientOptions(options));
        await client.StartAsync(cancellationToken);
        try
        {
            var session = await client.CreateSessionAsync(BuildSessionConfig(options), cancellationToken);
            var subscription = session.On<SessionEvent>(onEvent);
            return new GitHubCopilotSdkSession(client, session, subscription);
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    public CopilotClientOptions BuildClientOptions(CopilotSdkSessionOptions options)
        => new()
        {
            Mode = CopilotClientMode.Empty,
            BaseDirectory = Path.Combine(
                Path.GetTempPath(),
                $"pull-request-copilot-review-{Environment.ProcessId}-{Guid.NewGuid():N}"),
            GitHubToken = options.GitHubToken,
            UseLoggedInUser = false,
            WorkingDirectory = options.WorkingDirectory,
            LogLevel = CopilotLogLevel.Warning,
        };

    public SessionConfig BuildSessionConfig(CopilotSdkSessionOptions options)
        => new()
        {
            ClientName = "pull-request-copilot-review",
            Model = options.Model,
            WorkingDirectory = options.WorkingDirectory,
            Streaming = true,
            IncludeSubAgentStreamingEvents = false,
            AvailableTools = new ToolSet().AddBuiltIn(options.AvailableTools),
            EnableConfigDiscovery = false,
            EnableFileHooks = false,
            EnableHostGitOperations = false,
            EnableOnDemandInstructionDiscovery = false,
            EnableSessionStore = false,
            EnableSkills = false,
            SkipCustomInstructions = true,
            Memory = new MemoryConfiguration { Enabled = false },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = HandlePermissionRequestAsync
        };

#pragma warning disable GHCP001 // PermissionDecision is the SDK's documented permission-handler contract.
    private static Task<PermissionDecision> HandlePermissionRequestAsync(
        PermissionRequest request,
        PermissionInvocation _)
        => Task.FromResult(request switch
        {
            PermissionRequestRead or PermissionRequestUrl => PermissionDecision.ApproveOnce(),
            _ => PermissionDecision.Reject("This automated review session permits read-only inspection only.")
        });
#pragma warning restore GHCP001
}
