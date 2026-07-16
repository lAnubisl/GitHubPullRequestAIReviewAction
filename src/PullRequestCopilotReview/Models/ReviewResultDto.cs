using System.Text.Json.Serialization;

namespace PullRequestCopilotReview.Models;

internal sealed class ReviewResultDto
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("findings")]
    public ReviewFindingDto[]? Findings { get; set; }
}
