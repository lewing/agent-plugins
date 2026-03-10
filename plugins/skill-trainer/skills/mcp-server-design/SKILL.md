---
name: mcp-server-design
description: >
  Guide MCP server design for agent consumption: tool descriptions, naming, knowledge tools, annotations.
  USE FOR: writing tool descriptions, naming tools, designing knowledge endpoints, reviewing MCP server design, adding tool annotations.
  DO NOT USE FOR: building skills that consume MCP tools (use skill-builder), MCP protocol implementation details.
---

# MCP Server Design for Agent Consumption

Design MCP tools that agents can discover, select correctly, and use efficiently. This skill covers the agent-facing surface of MCP servers — not protocol mechanics, but the design choices that determine whether agents pick the right tool on the first try.

## Core Principles

1. **Descriptions are routing signals.** Tool descriptions work like skill frontmatter — they're always loaded into agent context and drive tool selection. Every word costs tokens; make each one count.

2. **Names set expectations.** Agents often select tools by name before reading descriptions. A name like `get_test_results` signals "start here" — even if the tool is niche.

3. **Defer detail to knowledge tools.** Don't bloat descriptions with situational patterns. Create on-demand knowledge endpoints that agents query when they need depth.

4. **Align with vendor standards.** Follow the MCP spec and Anthropic's naming conventions — they're designed for multi-server environments where naming collisions are real.

5. **Test your claims.** Design patterns are hypotheses until validated across models. Measure tool selection accuracy, false-positive calls, and steps to completion.

## Quick Reference

### Tool Descriptions
- Lead with a verb (what the tool does)
- Keep descriptions compact — every description loads into every session
- Put situational detail in parameter descriptions or knowledge tools
- Use "Niche" or similar qualifiers to signal low-frequency tools
- Cross-reference related tools ("Use X for most cases; this tool handles Y")

→ See `references/tool-description-patterns.md`

### Tool Naming
- Format: `{service}_{action}_{resource}` (Anthropic standard)
- snake_case, action-oriented, specific
- Names that sound generic ("get_results") attract false-positive calls
- Names that sound specialized ("parse_uploaded_trx") naturally deprioritize

→ See `references/tool-naming-conventions.md`

### Knowledge Tools
- Two-tier architecture: compact tool descriptions (always loaded) + knowledge endpoints (on demand)
- Knowledge tools carry repo-specific patterns, failure signatures, recommended tool sequences
- Agents call the knowledge tool first, then use the right data tools

→ See `references/knowledge-tool-design.md`

### Tool Annotations
From the MCP spec (2025-06-18):

| Annotation | Type | Default | Use |
|-----------|------|---------|-----|
| `readOnlyHint` | bool | false | Tool doesn't modify state |
| `destructiveHint` | bool | true | Tool may destructively modify state |
| `idempotentHint` | bool | false | Repeated calls are safe |
| `openWorldHint` | bool | true | Tool interacts with external systems |

Mark all read-only tools as `readOnlyHint: true`. Required for Anthropic directory submission.

### Agent Integration
- Skills that consume your MCP tools should use domain language, not tool names
- "Search the build logs" survives tool renames; "call `azdo_search_log`" doesn't
- CLI examples act as semantic bridges — self-describing syntax maps naturally to tool parameters

→ See `references/agent-integration-patterns.md`

## When to Create a Knowledge Tool

Create a knowledge endpoint when:
- Multiple tools need context that doesn't fit in descriptions (repo-specific patterns, recommended sequences)
- The same guidance applies across tool families but varies by domain (e.g., per-repo failure patterns)
- Agents consistently make wrong first choices without domain context

Don't create one when:
- Tool descriptions alone are sufficient for correct selection
- The knowledge is static and small enough for a parameter description
- Only one tool needs the context (put it in that tool's parameter descriptions)

## Validation

These patterns are hypotheses supported by evidence, not proven rules. Before adopting a specific threshold (word count, etc.), test it:

1. Pick a realistic task that exercises tool selection
2. Run it across 3+ models from different families
3. Change one variable (description length, name, etc.)
4. Measure: correct tool selection, false positives, steps to completion

→ See `references/validation-methodology.md`

## References

- `references/tool-description-patterns.md` — description budgets, structure, routing signals
- `references/tool-naming-conventions.md` — naming conventions, traps, family naming
- `references/knowledge-tool-design.md` — on-demand knowledge architecture
- `references/agent-integration-patterns.md` — domain language, CLI bridges, INVOKES
- `references/industry-alignment.md` — vendor guidance, research findings
- `references/validation-methodology.md` — how to test MCP design claims

## See Also

- `skill-builder/references/anti-patterns.md` — "Re-documenting MCP tools" (the skill-author perspective)
- `skill-builder/references/skill-patterns.md` — INVOKES pattern for connecting skills to MCP tool families
