using System.Text.RegularExpressions;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed partial class DiffParser : IDiffParser
{
    public IReadOnlyList<DiffHunk> Parse(string? patch)
    {
        if (string.IsNullOrWhiteSpace(patch)) return Array.Empty<DiffHunk>();
        var hunks = new List<DiffHunk>();
        DiffHunk? current = null;
        HashSet<int>? currentNewLines = null;
        var newLine = 0;
        foreach (var line in patch.Split('\n'))
        {
            var header = HunkHeader().Match(line);
            if (header.Success)
            {
                if (current is not null) hunks.Add(current);
                currentNewLines = new HashSet<int>();
                current = new DiffHunk(
                    int.Parse(header.Groups["oldStart"].Value),
                    ParseCount(header.Groups["oldCount"].Value),
                    int.Parse(header.Groups["newStart"].Value),
                    ParseCount(header.Groups["newCount"].Value),
                    currentNewLines);
                newLine = current.NewStart;
            }
            else if (current is not null && !line.StartsWith("\\ No newline", StringComparison.Ordinal))
            {
                if (line.StartsWith("+", StringComparison.Ordinal) || line.StartsWith(" ", StringComparison.Ordinal)) currentNewLines!.Add(newLine++);
                else if (!line.StartsWith("-", StringComparison.Ordinal)) newLine++;
            }
        }
        if (current is not null) hunks.Add(current);
        return hunks;
    }

    private static int ParseCount(string value) => string.IsNullOrWhiteSpace(value) ? 1 : int.Parse(value);
    [GeneratedRegex("@@ -(?<oldStart>\\d+)(,(?<oldCount>\\d+))? \\+(?<newStart>\\d+)(,(?<newCount>\\d+))? @@")]
    private static partial Regex HunkHeader();
}
