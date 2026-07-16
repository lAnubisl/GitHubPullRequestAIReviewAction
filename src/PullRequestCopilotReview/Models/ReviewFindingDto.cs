using System.Text.Json.Serialization;

namespace PullRequestCopilotReview.Models;

internal sealed class ReviewFindingDto
{
    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("confidence")]
    public string? Confidence { get; set; }
}
