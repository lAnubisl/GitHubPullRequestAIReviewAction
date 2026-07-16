using System.Reflection;
using System.Text.Json;
using Json.Schema;
using PullRequestCopilotReview.Interfaces;
using PullRequestCopilotReview.Models;

namespace PullRequestCopilotReview.Services;

public sealed class ReviewResponseValidator : IReviewResponseValidator
{
    private const int MaximumErrors = 12;
    private const string SchemaResourceSuffix = "Schemas.review-result.schema.json";
    private static readonly Lazy<JsonSchema> SharedSchema = new(LoadSchema);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public ReviewResponseValidationResult Validate(string content, IReadOnlyList<PullRequestFile> changedFiles)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (JsonException ex)
        {
            return Invalid($"Invalid JSON: {ex.Message}");
        }

        using (document)
        {
            var evaluation = SharedSchema.Value.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!evaluation.IsValid)
            {
                var schemaErrors = ((IEnumerable<EvaluationResults>?)evaluation.Details ?? Enumerable.Empty<EvaluationResults>())
                    .Where(detail => !detail.IsValid)
                    .SelectMany(detail => detail.Errors?.Select(error => $"{DisplayPath(detail.InstanceLocation.ToString())}: {error.Value}") ?? Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaximumErrors)
                    .ToArray();
                return new ReviewResponseValidationResult(null, schemaErrors.Length > 0 ? schemaErrors : ["The review document does not match the required schema."]);
            }
        }

        ReviewResultDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ReviewResultDto>(content, JsonOptions)
                ?? throw new JsonException("The review document was empty.");
        }
        catch (JsonException ex)
        {
            return Invalid($"Review JSON could not be deserialized: {ex.Message}");
        }

        var errors = new List<string>();
        var filesByPath = changedFiles
            .Select(file => (File: file, Path: NormalizePath(file.FileName)))
            .Where(item => item.Path is not null)
            .ToDictionary(item => item.Path!, item => item.File, StringComparer.Ordinal);

        foreach (var (finding, index) in dto.Findings!.Select((finding, index) => (finding, index)))
        {
            var findingPath = NormalizePath(finding.File!);
            if (findingPath is null)
            {
                errors.Add($"$.findings[{index}].file: \"{finding.File}\" is not a safe repository-relative path.");
                continue;
            }
            if (!filesByPath.TryGetValue(findingPath, out var file))
            {
                errors.Add($"$.findings[{index}].file: \"{finding.File}\" is not a changed file.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(file.Patch) || file.Hunks.Count == 0)
            {
                errors.Add($"$.findings[{index}].line: \"{file.FileName}\" has no commentable right-side diff lines.");
                continue;
            }
            if (!file.Hunks.Any(hunk => hunk.NewLines.Contains(finding.Line)))
            {
                errors.Add($"$.findings[{index}].line: line {finding.Line} is not commentable on the RIGHT side of \"{file.FileName}\".");
            }
        }

        if (errors.Count > 0) return new ReviewResponseValidationResult(null, errors.Take(MaximumErrors).ToArray());
        return ReviewResponseValidationResult.Valid(new ReviewResult(
            dto.Summary!,
            dto.Findings!.Select(finding => new ReviewFinding(finding.Severity!, NormalizePath(finding.File!)!, finding.Line, finding.Title!, finding.Body!, finding.Confidence!)).ToArray()));
    }

    private static ReviewResponseValidationResult Invalid(string error) => new(null, [error]);

    private static string? NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return null;
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or "..")) return null;
        return string.Join('/', segments);
    }

    private static JsonSchema LoadSchema()
    {
        var assembly = typeof(ReviewResponseValidator).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(name => name.EndsWith(SchemaResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The embedded review-result JSON Schema was not found.");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The embedded review-result JSON Schema could not be opened.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static string DisplayPath(string path) => string.IsNullOrWhiteSpace(path) ? "$" : path;
}
