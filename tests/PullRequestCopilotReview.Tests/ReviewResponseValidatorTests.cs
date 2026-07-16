using PullRequestCopilotReview.Models;
using PullRequestCopilotReview.Services;

namespace PullRequestCopilotReview.Tests;

public sealed class ReviewResponseValidatorTests
{
    private readonly ReviewResponseValidator _validator = new();
    private static readonly PullRequestFile File = new("src/App.cs", "modified", 1, 0, "@@ -11 +12 @@\n+code", new DiffParser().Parse("@@ -11 +12 @@\n+code"), null);

    [Fact]
    public void Accepts_a_structurally_and_semantically_valid_document()
    {
        var validation = _validator.Validate(Json("src/App.cs", 12), [File]);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Equal("src/App.cs", Assert.Single(validation.Result!.Findings).File);
    }

    [Theory]
    [InlineData("```json\n{}\n```")]
    [InlineData("{\"summary\":\"ok\",\"findings\":[],}")]
    [InlineData("{\"summary\":\"ok\",\"findings\":[],\"extra\":true}")]
    public void Rejects_invalid_json_and_schema_documents(string json)
    {
        var validation = _validator.Validate(json, [File]);
        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public void Reports_each_invalid_semantic_location()
    {
        var json = """{"summary":"Review","findings":[{"severity":"high","file":"src/Old.cs","line":1,"title":"Old","body":"Details.","confidence":"high"},{"severity":"medium","file":"src/App.cs","line":99,"title":"Line","body":"Details.","confidence":"high"}]}""";
        var validation = _validator.Validate(json, [File]);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("not a changed file"));
        Assert.Contains(validation.Errors, error => error.Contains("not commentable on the RIGHT side"));
    }

    [Fact]
    public void Rejects_binary_or_patchless_file_locations()
    {
        var binary = File with { Patch = null, Hunks = [] };
        var validation = _validator.Validate(Json("src/App.cs", 12), [binary]);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("no commentable"));
    }

    private static string Json(string file, int line) => $$"""{"summary":"Review","findings":[{"severity":"high","file":"{{file}}","line":{{line}},"title":"Bug","body":"Details.","confidence":"high"}]}""";
}
