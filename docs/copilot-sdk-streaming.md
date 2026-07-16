# Copilot review-session and validation contract

The reviewer creates one `CopilotSession` for a pull-request review and retains it for all response-repair attempts. `SendAndWaitAsync` returns the only authoritative `AssistantMessageEvent`; its `Data.Content` is validated as the review document. A null return or an SDK exception is a transport failure and immediately fails the action.

## Streaming observability

The session uses `Streaming = true`, but `SessionEvent` handling is logging-only. Root delta character counts, intent, tool start/completion, token usage, complete root assistant messages, session errors, and idle events are logged. Reasoning and sub-agent content are ignored. No streamed event determines the selected response, whether a session succeeded, or whether a response was aborted.

## Validation and repair

Each returned response must pass strict JSON parsing, the embedded `review-result.schema.json`, strict DTO deserialization, and semantic location validation. Every finding must name a safe repository-relative changed file and a right-side commentable line from a parsed diff hunk. Deleted-only lines, binary files, and patchless files are rejected.

The first request uses the review prompt. On a validation error, the reviewer sends a correction prompt in the same session that lists bounded actionable errors and requires a complete replacement JSON document with no Markdown, prose, or patches. There are at most three total attempts. The final validation failure fails closed before summary or review publication.

## Security boundary

The SDK client runs in `CopilotClientMode.Empty` with the checked-out pull request revision as its working directory and explicit read-only built-in tools. It disables file hooks, configuration discovery, repository instructions, skills, host Git context, memory, session storage, and infinite sessions. The permission handler approves only read and URL requests.
