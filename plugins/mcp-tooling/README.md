# mcp-tooling

Tools for MCP server authors: context-cost auditing, tool-surface measurement, and reduction-lever guidance

## Installation

### Copilot CLI / Claude Code

Via marketplace:
```
/plugin marketplace add lewing/agent-plugins
/plugin install mcp-tooling@lewing-public
/plugin update mcp-tooling@lewing-public
```

Or install directly from GitHub (Copilot CLI only):
```
/plugin install lewing/agent-plugins:plugins/mcp-tooling
```

### VS Code (Preview)

Add the marketplace to your VS Code settings:

```jsonc
// settings.json
{
  "chat.plugins.enabled": true,
  "chat.plugins.marketplaces": ["lewing/agent-plugins"]
}
```

Then use `/plugins` in Copilot Chat to browse and install.

## Uninstall

```
# Copilot CLI / Claude Code
/plugin uninstall mcp-tooling@lewing-public

# VS Code: remove the marketplace entry from chat.plugins.marketplaces in settings.json
```

## Skills

### [mcp-context-audit](skills/mcp-context-audit/SKILL.md)

Measure and reduce the per-session context cost of an MCP server's tool surface. USE FOR: auditing my MCP server tokens, where are my MCP tokens spent, shrink my tool surface, MCP context cost, tools/list cost, why is my MCP server expensive, measuring tool description size, compare MCP versions before/after, per-field MCP token breakdown, MCP token budget, ranking MCP tools by cost. DO NOT USE FOR: designing tool descriptions from scratch (use mcp-server-design), implementing MCP protocol mechanics, benchmarking tool latency or runtime cost (this skill measures static tool-surface tokens, not execution).

**References:**
- [baseline-comparison.md](skills/mcp-context-audit/references/baseline-comparison.md)
- [methodology.md](skills/mcp-context-audit/references/methodology.md)
- [reduction-levers.md](skills/mcp-context-audit/references/reduction-levers.md)
