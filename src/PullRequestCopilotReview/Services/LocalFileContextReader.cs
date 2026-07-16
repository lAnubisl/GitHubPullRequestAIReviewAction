using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class LocalFileContextReader : ILocalFileContextReader
{
    public string? Read(string workspace, string relativePath, IReadOnlyList<DiffHunk> hunks, int contextLines)
    {
        if (string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(relativePath) || contextLines <= 0) return null;
        var fullPath = Path.GetFullPath(Path.Combine(workspace, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToWorkspace = Path.GetRelativePath(Path.GetFullPath(workspace), fullPath);
        if (relativeToWorkspace.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeToWorkspace) || !File.Exists(fullPath)) return null;
        var lines = File.ReadAllLines(fullPath);
        var interesting = hunks.SelectMany(h => h.NewLines).SelectMany(line => Enumerable.Range(Math.Max(1, line - contextLines), Math.Min(lines.Length, line + contextLines) - Math.Max(1, line - contextLines) + 1)).Where(line => line >= 1 && line <= lines.Length).Distinct().Order().ToArray();
        return interesting.Length == 0 ? null : string.Join(Environment.NewLine, interesting.Select(line => $"{line}: {lines[line - 1]}"));
    }
}
