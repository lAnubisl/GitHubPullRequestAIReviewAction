using GitHub.Copilot;

namespace PullRequestCopilotReview.Interfaces;

public interface ICopilotSessionEventLogger
{
    void Handle(SessionEvent evt);
}
