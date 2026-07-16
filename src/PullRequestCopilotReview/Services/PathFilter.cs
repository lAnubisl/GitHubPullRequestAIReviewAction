using System.Text.RegularExpressions;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class PathFilter : IPathFilter
{
    public IReadOnlyList<PullRequestFile> Apply(IEnumerable<PullRequestFile> files, IReadOnlyList<string> excludePaths)
    {
        var patterns = excludePaths.Select(ToRegex).ToArray();
        return files.Where(file => !patterns.Any(pattern => pattern.IsMatch(file.FileName.Replace('\\', '/')))).ToArray();
    }
    private static Regex ToRegex(string glob) => new($"^{Regex.Escape(glob.Replace('\\', '/').Trim()).Replace(@"\*\*", ".*", StringComparison.Ordinal).Replace(@"\*", @"[^/]*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal)}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
