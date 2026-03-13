# Agent Integration Patterns

How skills, agents, and other consumers should interact with MCP tools — and how MCP server design choices affect that interaction.

## Domain Language Over Tool Names

Skills and agents should reference MCP tool capabilities using domain language, not opaque tool names.

### Why this matters
Tool names are implementation details — they change on renames, server moves, or replacements. Skills that hardcode tool names break; skills that use domain language survive.

### Examples

| ❌ Hardcoded | ✅ Domain language |
|-------------|-------------------|
| "Call `helix_search_log` with the pattern" | "Search the Helix console logs for the failure pattern" |
| "Use `azdo_timeline` to find failed jobs" | "Get the build timeline and identify failed jobs" |
| "Run `helix_status` for each job ID" | "Check the pass/fail summary for each Helix job" |

### How it works
Agents match domain language against tool descriptions semantically. "Search the build logs" matches "Search a build step log for matching lines" — regardless of tool name.

## CLI Examples as Semantic Bridges

Self-describing CLI commands help models understand MCP tool capabilities:

```
# CLI syntax is self-documenting:
gh issue view 123 --repo dotnet/runtime --comments

# Maps naturally to MCP tool parameters:
issue_read(owner="dotnet", repo="runtime", issue_number=123, method="get_comments")
```

The CLI flags (`--repo`, `--comments`) encode the same semantics as MCP parameters. Models that know the CLI syntax can infer the MCP tool's parameter structure.

### When CLI examples help
- The CLI tool is well-known (gh, az, dotnet, git)
- The CLI syntax clearly maps to the domain action
- Multiple MCP servers could handle the same request

### When they don't help
- No equivalent CLI exists
- The CLI syntax is obscure or misleading
- The MCP tool does something the CLI can't

## CLI-as-Skill: CLI Instead of MCP

When a tool has both a CLI and an MCP server interface (same binary, same cache, same auth), agents can use the CLI via bash instead of loading MCP tool descriptions into context. This trades per-call process overhead for a dramatic reduction in context tax.

### When to prefer CLI over MCP
- **MCP server isn't configured** — the agent can still access the data via `dotnet tool install` + bash
- **Scripting/chaining** — CLI commands pipe to jq, grep, and other shell tools naturally
- **One-shot queries** — single command, structured JSON output, done
- **Context tax matters** — 20 MCP tool descriptions (~1,300 tokens) vs. ~50 tokens for a skill description that says "use the CLI"

### When to prefer MCP
- **MCP server is already loaded** — tool descriptions are already in context, no point routing through bash
- **Multi-turn investigation** — MCP tools integrate with the agent's reasoning loop more naturally
- **Markdown-rich output** — MCP tools can return formatted output with emojis and visual indicators

### Progressive discovery pattern
Instead of documenting every command in the skill, teach the agent to discover:

```bash
tool --help              # All commands, one line each
tool <command> --help    # Parameters for a specific command
tool <command> --schema  # Response field names (for jq pipelines)
tool guide               # Workflow-organized overview
```

This keeps the SKILL.md minimal (~80-100 lines) while giving agents full access on demand.

### Design requirements for MCP servers supporting this pattern
1. **Consistent `--json` flag** on all query commands
2. **`--schema` per command** showing response field names — this was the #1 gap in multi-model eval (agents had to guess field names for jq pipelines)
3. **Shared cache** between CLI and MCP server (SQLite WAL mode works well)
4. **Same auth cascade** — CLI and MCP should accept the same credentials
5. **`guide` command** — workflow-organized overview for agents that need the full picture

### Evidence
Multi-model eval (Claude Haiku 4.5, GPT-4.1, Gemini 3 Pro) on maestro-cli skill: avg 4.5/5. All models correctly used CLI instead of MCP. The only gap was JSON field name guessing — addressed by the `--schema` flag recommendation.

### Examples in production
- `mstro` (lewing/maestro.mcp) — 20 MCP tools, CLI-as-skill reduces context to ~50 tokens
- `hlx` (lewing/helix.mcp) — 21 MCP tools, same pattern planned (issue #21)

## The INVOKES Pattern (Skill → MCP)

Skills declare which MCP tool families they use via the `INVOKES` pattern in their frontmatter description:

```yaml
description: >
  Analyze CI failures for dotnet repos.
  INVOKES: Helix and AzDO MCP tools, analyze-ci.ps1 script
```

### Design implications for MCP servers

This pattern means:
1. **Name tool families consistently.** `helix_*` tools form a family. Skills reference "Helix MCP tools" — the family prefix connects them.
2. **Scripts are invisible.** MCP tools appear in the agent's tool list; scripts don't. INVOKES is especially critical for scripts because it's their only discovery mechanism.
3. **Don't name individual tools in INVOKES.** Name the family ("Helix MCP tools"), not specific tools ("helix_status, helix_search_log"). The agent discovers individual tools from descriptions.

### What this means for MCP server authors
- Consistent service prefixes (`helix_*`, `azdo_*`, `maestro_*`) make your tools discoverable as a family
- Families should be intuitively nameable — "Helix MCP tools" is clear; "hlx MCP tools" is not
- If your tools can't be described as a family, consider whether they belong in the same server

## Multi-Server Environments

Agents often have multiple MCP servers available simultaneously. Your server design should account for this:

### Namespace isolation
Service prefixes (`helix_`, `azdo_`, `maestro_`) prevent collisions across servers. Without them, `search_log` from two different servers creates ambiguity.

### Complementary, not competing
When two servers offer similar capabilities, descriptions should clarify scope:
- Server A: "Search AzDO build logs by step"
- Server B: "Search Helix work item console logs"

An agent seeing both can select the right one based on context.

### Graceful absence
Skills should work (degraded) when an MCP server isn't available. The `INVOKES` pattern helps here — it names what's expected, and agents can report what's missing rather than silently failing.

## Cross-References

- `skill-builder/references/anti-patterns.md` — "Re-documenting MCP tools": what NOT to put in skills about MCP tools
- `skill-builder/references/skill-patterns.md` — INVOKES pattern details and A/B test evidence
- `tool-description-patterns.md` — how descriptions should be written for semantic matching
