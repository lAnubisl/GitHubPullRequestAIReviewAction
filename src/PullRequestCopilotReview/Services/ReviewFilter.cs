using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class ReviewFilter : IReviewFilter
{
    public ReviewResult Apply(ReviewResult review, string minSeverity, int maxFindings, IEnumerable<string>? changedFiles = null)
    {
        var changedFileSet = changedFiles?.Select(NormalizePath).ToHashSet(StringComparer.Ordinal);
        var findings = review.Findings.Where(f => changedFileSet is null || changedFileSet.Contains(NormalizePath(f.File))).Where(f => f.IsAtLeast(minSeverity)).Take(maxFindings).Select(f => f with { File = NormalizePath(f.File) }).ToArray();
        return review with { Findings = findings };
    }
    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
