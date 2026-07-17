using GitHub.Copilot;

namespace PullRequestCopilotReview.Interfaces;

public interface ICopilotSessionEventLogger
{
    void LogPrompt(int attempt, string prompt);
    void Handle(SessionEvent evt);
}
