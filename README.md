# Pull Request Copilot Review Action

A reusable composite GitHub Action that reviews pull request changes with a .NET 10 console app and the [GitHub Copilot SDK for .NET](https://github.com/github/copilot-sdk/tree/main/dotnet).

The action runs for a checked-out `pull_request` event. It reads the event and changed files, asks Copilot for a detailed JSON review, validates that JSON, and publishes a summary, inline comments, or both.

## Usage

```yaml
name: AI Pull Request Review

on:
  pull_request:
    types: [opened, synchronize, reopened, labeled]

permissions:
  contents: read
  pull-requests: write

jobs:
  ai-review:
    if: github.event.action != 'labeled' || github.event.label.name == 'ai_review'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
        with:
          fetch-depth: 0

      - uses: your-org/pull-request-copilot-review@v1
        with:
          review_mode: summary-and-comments
          min_severity: medium
          max_findings: 10
        env:
          GH_CLI_TOKEN: ${{ secrets.GH_CLI_TOKEN }}
          COPILOT_GITHUB_TOKEN: ${{ secrets.COPILOT_GITHUB_TOKEN }}
```

The `ai_review` label provides an on-demand review trigger. Other labels do not
start the review job. To request another label-triggered review later, remove
the label and add it again.

## Required secrets

- `GH_CLI_TOKEN` is used only for GitHub API operations that read pull request files and publish comments.
- `COPILOT_GITHUB_TOKEN` is passed directly to the Copilot SDK client for authentication. `COPILOT_CLI_TOKEN` remains accepted as a legacy alias.

The action masks all supported token variables. GitHub API credentials are not passed to Copilot, and Copilot credentials are not passed to GitHub API commands.

## Permissions and untrusted pull requests

Use the smallest practical workflow permissions:

```yaml
permissions:
  contents: read
  pull-requests: write
```

The reviewer does not modify files, create commits, push branches, or create pull requests. Prefer the `pull_request` event. Repository secrets are intentionally unavailable to untrusted forked pull requests by default; do not switch to `pull_request_target` merely to expose them.

## Inputs

| Input | Default | Description |
| --- | --- | --- |
| `review_mode` | `summary-and-comments` | `summary`, `comments`, or `summary-and-comments`. |
| `max_findings` | `10` | Maximum number of findings to publish. |
| `min_severity` | `low` | Lowest severity to publish: `low`, `medium`, or `high`. |
| `include_file_context` | `true` | Include surrounding checked-out source lines around changed hunks. |
| `file_context_lines` | `4` | Number of local context lines around changed hunks. |
| `exclude_paths` | empty | Comma-separated paths or globs to ignore. |
| `copilot_model` | empty | Optional Copilot model name. |
| `copilot_extra_instructions` | empty | Optional extra reviewer instructions. |
| `fail_on_findings` | `false` | Exit non-zero when findings matching `min_severity` are returned. |

## SDK review flow

The action:

1. Validates inputs and reads the pull request event.
2. Fetches changed-file patches through the GitHub CLI and optionally adds local source context.
3. Builds a review-only prompt that requires one machine-readable JSON document.
4. Creates a GitHub Copilot SDK client in `Empty` mode with `GITHUB_WORKSPACE`—the checked-out pull request revision—as its working directory.
5. Starts one streaming session and subscribes to the SDK's strongly typed session events. See [GitHub's streaming-events guide](https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/streaming-events#subscribing-to-events).
6. Uses the complete `AssistantMessageEvent` returned directly by `SendAndWaitAsync` as the authoritative response. Streaming is observability only: it logs safe intent, tool, usage, complete-message, and progress metadata.
7. Strictly validates JSON structure and every inline location: paths must be safe changed-file paths and lines must be commentable right-side diff lines. Binary and patchless files cannot receive inline findings.
8. When validation fails, sends up to two correction prompts in the same session. After three total invalid attempts, or after an SDK error or null response, it fails closed and publishes nothing.
9. Applies `min_severity` and `max_findings`, writes the step summary, and publishes the requested PR output.

The SDK package supplies and manages its compatible Copilot runtime; the action no longer invokes a standalone `copilot` command, parses CLI JSONL, or installs validation hooks.

### Review-only SDK configuration

The SDK client uses `CopilotClientMode.Empty`, an isolated temporary Copilot base directory, and the checked-out repository as its working directory. This lets the read-only SDK tools inspect source files from the pull request revision while satisfying the SDK's explicit persistence-location requirement for empty mode. The session disables configuration discovery, repository custom instructions, file hooks, skills, host Git context, memory, cross-session storage, and infinite-session persistence.

Only these built-in tools are exposed:

- `view`
- `grep`
- `glob`
- `web_fetch`

The permission callback approves only read and URL requests. Every other permission request is rejected, providing a second boundary if the runtime requests an unexpected capability.

### Detailed JSON output

The final assistant message must contain only this shape:

```json
{
  "summary": "Concise overall review summary.",
  "findings": [
    {
      "severity": "high|medium|low",
      "file": "path/to/file.cs",
      "line": 42,
      "title": "Short finding title",
      "body": "Explain the issue, impact, and suggested fix.",
      "confidence": "high|medium|low"
    }
  ]
}
```

Markdown fences, leading or trailing prose, comments, trailing commas, missing fields, unknown fields, invalid enum values, and malformed findings are rejected. Prompt-directed JSON is followed by strict JSON Schema, DTO, and diff-location validation before publication. Invalid responses receive a correction prompt requiring a complete replacement document only; transport failures are never retried as formatting repairs.

### Logging and failure behavior

Reasoning events and tool-result payloads are not logged. Streaming deltas are counted rather than printed chunk by chunk. Complete root assistant messages are logged once so the detailed JSON review remains visible in the action log, along with model, tool-request, and token-usage metadata. Streamed events never select the response or change session success.

Assistant responses can quote repository source code. Anyone with access to the GitHub Actions logs may therefore see source excerpts included in those responses.

See [docs/copilot-sdk-streaming.md](docs/copilot-sdk-streaming.md) for the review-session and validation contract.

## Development

```bash
dotnet test PullRequestCopilotReview.sln
```

The normal suite uses a fake SDK transport and does not consume Copilot requests. A token-gated smoke project exercises a real SDK session:

```bash
RUN_COPILOT_SDK_SMOKE=true COPILOT_GITHUB_TOKEN=... \
  dotnet test tests/PullRequestCopilotReview.Smoke/PullRequestCopilotReview.Smoke.csproj
```
