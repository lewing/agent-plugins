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
