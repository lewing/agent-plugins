# Tool Description Patterns

Tool descriptions are always loaded into context — every word costs tokens across every session. Same constraints as skill frontmatter: be compact, be high-signal, defer detail.

## Description Structure

### Lead with a verb
Start every description with what the tool does:
- ✅ "Search work items in a Helix job for files matching a pattern"
- ❌ "This tool allows you to search for files in Helix work items"

### Purpose-first
The first few words should answer "what does this do?" — agents may truncate or skim.
- ✅ "Get pass/fail summary for a Helix job"
- ❌ "Returns structured JSON with job metadata, failed items with exit codes, state, duration..."

### Keep it compact
The exact budget depends on your server's tool count and the agent's context window. A practical guideline: if you can't describe the tool's purpose in 1-2 sentences, the tool may be doing too much.

**Evidence:** In helix.mcp, tightening 17 descriptions from ~60 to ~20 words removed ~550 words of always-loaded context. See `industry-alignment.md` for supporting research on the accuracy-vs-steps tradeoff.

## Where Detail Goes

Tool descriptions are the wrong place for:

| Detail type | Where it belongs |
|------------|-----------------|
| Parameter formats, valid values | Parameter descriptions (loaded only when agent considers the tool) |
| Repo-specific patterns | Knowledge tool (loaded on demand) |
| Recommended tool sequences | Knowledge tool |
| Error recovery guidance | Tool error responses |
| Cross-tool relationships | Brief mention in description ("Use X instead for most cases") |

### Parameter descriptions can be longer
Unlike tool descriptions, parameter descriptions are only loaded when the agent is already considering using the tool. They're a good place for format examples, valid ranges, and default behavior.

## Routing Signals

### Skip signals for niche tools
When a tool is useful but niche, signal it explicitly:
- "Niche — most repos use X instead"
- "Parse TRX files from Helix blob storage. Most dotnet repos publish results via AzDO instead."

**Evidence:** Renaming `helix_test_results` → `helix_parse_uploaded_trx` and adding "Niche" to its description reduced false-positive tool calls in CI analysis sessions.

### Cross-referencing related tools
When agents might confuse similar tools, point them:
- "Search a single log step. For searching ALL steps, use `azdo_search_log_across_steps`."
- This is one of the few places where naming a specific tool in a description is appropriate — it's tool-to-tool routing, not skill-to-tool.

### Negative routing
Tell agents when NOT to use a tool:
- "Get Helix work item details. Does NOT return test results — use `azdo_test_results` for structured results."

## Anti-patterns

### Embedding domain knowledge in descriptions
❌ "Search logs. Runtime uses '[FAIL]', aspnetcore uses '  Failed', SDK uses 'error MSB'"
✅ "Search a work item's console log for matching lines. Call `helix_ci_guide` for repo-specific patterns."

The domain knowledge changes; the description shouldn't have to.

### Describing return value schemas
❌ "Returns JSON with fields: jobId, workItems[], each containing name, exitCode, state, duration, machine, failureCategory"
✅ "Get pass/fail summary for a Helix job. Returns failed items with exit codes and metadata."

Agents see the actual response; they don't need the schema upfront.

### Duplicating knowledge tool content
If you have a knowledge tool, let it carry the weight. Descriptions should point to it, not repeat it.
