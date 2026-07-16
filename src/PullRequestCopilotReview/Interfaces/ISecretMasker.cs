namespace PullRequestCopilotReview.Interfaces;

public interface ISecretMasker
{
    void Mask(params string?[] secrets);
}
