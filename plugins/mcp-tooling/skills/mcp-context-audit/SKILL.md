---
name: mcp-context-audit
description: >
  Measure and reduce the per-session context cost of an MCP server's tool surface.
  USE FOR: auditing my MCP server tokens, where are my MCP tokens spent, shrink my tool surface, MCP context cost,
  tools/list cost, why is my MCP server expensive, measuring tool description size, compare MCP versions before/after,
  per-field MCP token breakdown, MCP token budget, ranking MCP tools by cost.
  DO NOT USE FOR: designing tool descriptions from scratch (use mcp-server-design), implementing MCP protocol mechanics,
  benchmarking tool latency or runtime cost (this skill measures static tool-surface tokens, not execution).
---

# MCP Context Audit

Measure how many tokens an MCP server's `tools/list` (and `prompts/list`) response spends in every agent session, then identify the biggest fields to shrink. Designed to run against your own MCP server from inside the repo that builds it.

## Why this matters

Every agent session pays for `tools/list` once, up front, before the agent does any work. A 7,000-token server stacked with Helix (~3,500) + AzDO (~2,500) + GitHub (~4,000) burns ~17,000 tokens of every session on tool surface alone. Description trims feel virtuous but are usually attacking the smallest slice of the budget.

A real-world measurement from `lewing/helix.mcp` v0.7.3 (25 tools, 8,212 total tokens):

| Field | % of budget |
|---|---|
| `outputSchema` | **37.0%** |
| `inputSchema` | **33.5%** |
| `annotations` | 9.7% |
| `_wrap` (JSON-RPC overhead per tool object) | 8.8% |
| `description` | 7.2% |
| `title` | 2.0% |
| `name` | 1.8% |

**JSON Schema is 70.5% of the cost.** A description-only PR shaved 2% off this server. A `LimitedResults<T>` DTO consolidation in an earlier PR shaved ~250 tokens off two tools alone.

## When to run this

- **Before** publishing a new MCP server version — establish a baseline.
- **After** a refactor — diff against the prior version with `--baseline`.
- When a server feels "heavy" — get a ranked list of tools by token cost.
- When deciding which reduction lever to pull next — see `references/reduction-levers.md`.

## Quick start

From the root of an MCP-server repo (the server must be runnable as a command):

```bash
# Audit a server that runs via `dotnet run`
plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh -- dotnet run --project src/MyServer

# Or via dnx
plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh -- dotnet dnx myserver@1.2.3 -y --

# Or any binary that speaks MCP over stdio
plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh -- ./bin/myserver mcp
```

The driver:
1. Sends `initialize` + `notifications/initialized` + `tools/list` + `prompts/list` over stdio.
2. Captures the JSON-RPC responses.
3. Tokenizes the `tools` and `prompts` arrays with `tiktoken` (gpt-4o encoding) — the JSON-RPC envelope is excluded because it does not count against the per-session tool-surface budget.
4. Prints a per-tool ranking and a per-field breakdown.

Output looks like:

```
=== Summary ===
tools:   25 items, 8,212 tokens
prompts:  0 items, 0 tokens
TOTAL:           8,212 tokens

=== Per-field breakdown ===
outputSchema  3,032  37.0%
inputSchema   2,742  33.5%
annotations     797   9.7%
_wrap           722   8.8%
description     591   7.2%
title           164   2.0%
name            148   1.8%

=== Top 10 tools by cost ===
626  azdo_timeline           (outputSchema 393, inputSchema 142, ...)
512  azdo_test_runs          (outputSchema 311, inputSchema 98, ...)
...
```

## Comparing two versions

```bash
# Measure current build
plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh \
  --output /tmp/baseline.json -- dotnet dnx myserver@1.0.0 -y --

# Measure refactor
plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh \
  --baseline /tmp/baseline.json -- dotnet run --project src/MyServer
```

The script prints a per-tool delta. See `references/baseline-comparison.md` for the methodology that uncovered the `helix.mcp` PR #57 misleading word-count claim.

## What to do with the result

See `references/reduction-levers.md` for the ordered list of reduction techniques — the short version:

1. **Consolidate DTOs** (`LimitedResults<T>`, paging wrappers). Hits `outputSchema`, the biggest slice.
2. **Trim input parameters**, especially `enum` arrays and large `oneOf`/`anyOf` unions.
3. **Remove redundant `annotations`**. Defaults are conservative — if the default matches reality, omit the field.
4. **Then** trim descriptions. Last resort, smallest win.

## Methodology details

See `references/methodology.md` for the full evaluation framework — context cost is one of three dimensions (cost, distribution, design style) we use when comparing MCP servers across an ecosystem.

## Caveat: protocol drift

The driver uses MCP protocol version `2024-11-05` (matching the stable spec at time of writing). The draft `2025-11-25+` revision proposes replacing `initialize`/`notifications/initialized` with `server/discover` or a `_meta`-carried protocol version on the first request. When the SDK ships that change, this script's handshake will need updating. See https://modelcontextprotocol.io/specification/2025-11-25 for the current published spec.
