# MCP Tool Routing Copy

**When to use:** Your MCP surface already has the tools the caller needs, but callers still waste rounds because they cannot tell **which tool to start with** or **what to do after a predictable failure**.

**Origin:** Patterns earned while iterating on a 25-tool .NET MCP server (`lewing.helix.mcp`) where the right tool sequence depended on repo, pipeline, and artifact-availability variations. The fix wasn't more tools — it was better routing copy in the descriptions and failure messages.

## Why this matters

**Context cost isn't only bytes per tool — it's also wasted agent rounds.** An agent that picks the wrong tool, gets an unclear failure, and has to retry has paid the wire cost of `tools/list` plus the round cost of an entire failed call sequence. On a busy agent loop, that compounds quickly.

The goal of this reference is **discoverability through copy**, not a bigger tool surface. Adding a composite tool to "fix routing" is usually the wrong fix — it grows the surface area and doesn't address the root cause (the existing tools don't tell callers when they apply).

## Patterns

### Lead with the behavioral contract

The first sentence of the tool description should say **what the tool is for** and **when it is the right first move**. Implementation detail comes later, if at all.

```csharp
// ❌ Implementation-led — caller has to infer the use case
[Description("Parses TRX and xUnit XML test result files from Helix blob storage and returns counts.")]

// ✅ Contract-led — when to use is the first thing the caller reads
[Description("First choice for getting pass/fail counts on a Helix work item. " +
             "Returns structured TRX/xUnit results when uploaded. " +
             "Returns empty when no result files were uploaded — use console-log search as fallback.")]
```

### Name the scope explicitly when it's a subset

If the tool only works for a **subset** of repos / pipelines / artifact layouts, say that explicitly in the description. Don't make the caller discover the limit by trial and error.

> "Works only on arcade-onboarded repos that publish `PostBuildLogs_*` artifacts via `eng/common/core-templates/steps/publish-logs.yml`. For other pipelines, use the live-log search instead."

The cost of this sentence in `tools/list` bytes is far less than the cost of a wrong call.

### Route to the next tool in failure messages

Failure messages should **end with the next concrete tool sequence** instead of stopping at "not found" or "unsupported."

```csharp
// ❌ Dead end
throw new McpException("No TRX files uploaded for this work item.");

// ✅ Routes the caller forward
throw new McpException(
    "No TRX files uploaded for this work item. " +
    "Use `hlx_search_log` with pattern '[FAIL]' for the test summary, " +
    "or `hlx_files` to list other uploaded artifacts.");
```

The caller's next action is now obvious without consulting documentation or guessing.

### Keep routing concise

**"Use X when / otherwise use Y" beats long implementation detail.** Long descriptions in `tools/list` are a real context-cost burden — see `tool-description-patterns.md` for the budget. A routing sentence pays for itself; an implementation paragraph usually doesn't.

### Mirror the routing across related surfaces

The same routing story should appear in:
- Tool descriptions (in `tools/list`)
- Guide tools (if you have a `get_llm_guide`-style help tool)
- Built-in help / `--help` output
- README / docs

When these drift, the agent gets contradictory advice from different surfaces and behavior becomes unpredictable. Pick one canonical phrasing for each routing rule and mirror it.

### Add regression tests at the copy seam

Routing copy is high-leverage and easy to break in unrelated PRs. Test it explicitly:

- **Reflect method-level `DescriptionAttribute` text** for MCP tools and assert key routing phrases are present ("substring search, not regex"; "use X when … otherwise Y").
- **Assert guide / help section order** so routing guidance stays *ahead of* deep detail.
- **For false-confidence risks**, test both the success-selection copy ("this is the right first move when X") and the failure-path copy ("if you get Y, switch to Z"). A warning without the next tool sequence is not enough.

## Examples

- **Structured-results tool says** it parses Helix-hosted results when present, **but directs callers to a build-system results API** when those files are usually absent.
- **Log-search tool says** it is the remote-first path for console investigation **and points callers to a guide tool** for repo-specific search patterns.
- **Repo guide adds a short "Start Here" section** before deeper details so callers can choose the correct workflow quickly.
- **Regression test reflects an MCP method description** and asserts it still says "substring search, not regex" plus the repo-specific fallback/selection wording.
- **Guide-rendering test verifies** the "use AzDO results here" line appears before search-pattern inventories, and that the recommended order pivots to structured results before log scraping.

## Anti-patterns

### Warning without naming the fallback

> "This tool may fail if no binlog artifact is published."

OK, but **what do I do then**? Every "may fail" warning should be paired with the next tool to try.

### Burying the workflow choice

Long inventories of fields, parameters, or implementation notes **before** the routing guidance push the "which tool first?" decision past the agent's effective reading window. Lead with routing; trail with detail.

### Adding a composite tool before tightening routing copy

When callers misuse two tools, the temptation is to add a third tool that picks between them. **Don't.** First, tighten the routing copy on the existing tools. A composite tool grows the surface area, adds another set of descriptions to maintain, and often hides the underlying choice (which the agent still needs to understand when the composite fails).

Add composite tools only when you've tightened routing copy and **measured** that the misuse continues.

### Letting descriptions, guide output, and built-in help drift

If your README says "use X first," your guide tool says "use Y first," and your `--help` says nothing — agents will pick a different tool every session. Canonical phrasing in one place, mirrored elsewhere.

## See also

- `tool-description-patterns.md` — how long a description should be and what belongs in it (the budget side of the same problem)
- `tool-naming-conventions.md` — naming conventions, traps, family naming (naming is the first-line routing signal)
- `mcp-wire-format-trim.md` — context-cost measurement so you can defend "this routing sentence saves more rounds than it costs bytes"
- `mcp-structured-content.md` — when tools return raw text vs structured, which is itself a routing distinction callers need to understand
