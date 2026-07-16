using PullRequestCopilotReview.Interfaces;

namespace PullRequestCopilotReview.Services;

public sealed class GitHubSecretMasker : ISecretMasker
{
    public void Mask(params string?[] secrets)
    {
        foreach (var secret in secrets.Where(secret => !string.IsNullOrWhiteSpace(secret))) Console.WriteLine($"::add-mask::{secret}");
    }
}
