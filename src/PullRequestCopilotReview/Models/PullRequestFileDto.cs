using System.Text.Json.Serialization;

namespace PullRequestCopilotReview.Models;

internal sealed class PullRequestFileDto
{
    [JsonPropertyName("filename")]
    public string? FileName { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }

    [JsonPropertyName("patch")]
    public string? Patch { get; set; }
}
