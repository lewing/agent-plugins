# Tool Naming Conventions

Tool names are the first thing agents see. In many cases, agents select (or skip) tools based on name alone, before reading the full description. A well-chosen name routes agents correctly; a poorly-chosen name creates false-positive calls or hides useful tools.

## Standard Format

Follow the Anthropic convention:

```
{service}_{action}_{resource}
```

Examples:
- `helix_status` — get Helix job status
- `azdo_search_log` — search an AzDO build log
- `maestro_subscription_health` — check Maestro subscription health

### Rules
- **snake_case** — required by MCP spec (start with letter, lowercase + numbers + underscores only)
- **Service prefix** — anticipate multi-server environments. `search_log` collides; `azdo_search_log` doesn't
- **Action-oriented** — start with a verb: `get_`, `list_`, `search_`, `create_`, `update_`, `delete_`
- **Specific over generic** — `parse_uploaded_trx` beats `get_test_results`

## Naming Traps

### The "default" trap
Generic-sounding names make agents assume the tool is THE primary way to do something:

| Name | Agent assumption | Reality |
|------|-----------------|---------|
| `helix_test_results` | "THE way to get test results" | Niche — only works for repos that upload TRX to Helix |
| `get_build_info` | "Start here for build data" | May be one of several build tools |

**Fix:** Make niche tools sound niche. `helix_parse_uploaded_trx` doesn't invite first-try usage.

### The collision trap
Without a service prefix, tools from different servers collide:
- `search_logs` — which service's logs?
- `get_status` — status of what?

**Fix:** Always prefix with the service: `helix_search_log`, `azdo_search_log`.

### The verbosity trap
Overly long names hurt scannability:
- ❌ `azdo_search_log_across_all_build_steps_with_ranking`
- ✅ `azdo_search_log_across_steps`

## Tool Family Naming

When you have a group of related tools, use consistent prefixes to make the family discoverable:

```
helix_status          — job summary
helix_work_item       — work item details
helix_logs            — console log content
helix_search_log      — search within a log
helix_files           — list uploaded files
helix_find_files      — search files across work items
helix_download        — download files
```

This consistency matters for:
- **Skills using INVOKES:** `INVOKES: Helix MCP tools` connects to the `helix_*` family
- **Agent discovery:** agents can infer "there might be a `helix_files` tool" from seeing `helix_status`
- **Parameter consistency:** tools in a family should accept the same core parameters (e.g., `jobId`) in the same format

## Server Naming

Follow platform conventions for the server package itself:

| Platform | Format | Example |
|----------|--------|---------|
| Python | `{service}_mcp` | `slack_mcp` |
| Node/TypeScript | `{service}-mcp-server` | `slack-mcp-server` |
| .NET | `{service}.mcp` | `helix.mcp` |

Server names use the platform's casing convention (hyphens for npm, dots for NuGet). Tool names always use snake_case regardless of platform.

## CLI Command Names

If your MCP server also exposes a CLI, the CLI command names can differ from tool names. CLI commands often use kebab-case and may use different verbs:

| MCP tool | CLI command | Why |
|----------|------------|-----|
| `helix_status` | `status` | CLI doesn't need service prefix (already scoped by binary name) |
| `helix_parse_uploaded_trx` | `parse-uploaded-trx` | kebab-case is CLI convention |

The MCP tool name is the machine-facing identifier; the CLI command is the human-facing one. Both should be clear about what they do, but they serve different audiences.

## Title Field

The MCP spec supports an optional `title` field for human-friendly display names:

```json
{
  "name": "helix_search_log",
  "title": "Search Helix Work Item Log",
  "description": "Search a work item's console log for matching lines."
}
```

Use titles for UI display. Keep names for machine routing.
