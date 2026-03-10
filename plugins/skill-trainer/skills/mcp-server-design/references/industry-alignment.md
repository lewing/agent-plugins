# Industry Alignment

How our MCP design patterns relate to vendor guidance and academic research.

## Authoritative Sources

### MCP Specification (2025-06-18)
https://modelcontextprotocol.io/specification/2025-06-18/server/tools

Key features for tool design:
- **Tool annotations:** `readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint` — hints for clients about tool behavior
- **`title` field:** Human-friendly display name separate from machine identifier
- **`outputSchema`:** Optional JSON Schema for structured results — helps clients parse responses
- **`inputSchema`:** JSON Schema for parameters — use `description` on each property for agent-friendly documentation

### Anthropic MCP Best Practices
https://github.com/anthropics/skills/blob/main/skills/mcp-builder/reference/mcp_best_practices.md

Key guidance:
- Tool naming: `{service}_{action}_{resource}` in snake_case
- Descriptions "must narrowly and unambiguously describe functionality"
- Server naming: Python `{service}_mcp`, Node `{service}-mcp-server`
- Pagination: `limit` + `has_more` + `next_offset` + `total_count`
- Response formats: support both JSON and Markdown
- Tool annotations required for directory submission

### Anthropic Submission Guides
- Remote: https://support.claude.com/en/articles/12922490-remote-mcp-server-submission-guide
- Local: https://support.claude.com/en/articles/12922832-local-mcp-server-submission-guide

Required for submission:
- `readOnlyHint` and `destructiveHint` annotations on all tools
- Clear, accurate descriptions
- Proper error handling with helpful messages

### Practical Guides
- Peter Steinberger's MCP Best Practices: https://steipete.me/posts/2025/mcp-best-practices
  - Parameter parsing should be lenient (accept `path` if `project_path` expected)
  - Include an `info` command for diagnostics
  - No stdout during normal operation (log to stderr or file)
  - Tool descriptions should be verifiable via MCP inspector

## Research Findings

### "MCP Tool Descriptions Are Smelly!" (arXiv:2602.14878)

Study of 856 tools across 103 MCP servers:
- **97.1%** of tool descriptions had at least one quality issue ("smell")
- **56%** failed to clearly state the tool's purpose
- 6 quality components identified: Purpose, Parameters, Return value, Preconditions, Side effects, Usage guidelines

Relevance: Validates that description quality is a widespread problem, not just ours.

### "From Docs to Descriptions" (arXiv:2602.18914)

Evaluated impact of description improvements:
- 4 quality dimensions: accuracy, functionality, information completeness, conciseness
- 18 specific smell categories identified
- Augmented descriptions improved success by ~5.85 percentage points
- **But increased execution steps significantly** — agents took more turns to complete tasks
- Well-written descriptions: tool selection probability improved from 20% → 72%

Relevance: Confirms the tradeoff — more detail helps accuracy but hurts efficiency. Supports our "compact descriptions + knowledge tools" approach as a better balance.

## Where We Align

| Practice | Vendor guidance | Our approach |
|----------|----------------|--------------|
| Naming | `snake_case`, service prefix | ✅ `helix_status`, `azdo_search_log` |
| Descriptions | "Narrowly and unambiguously" | ✅ Purpose-first, compact |
| Safety annotations | `readOnlyHint` / `destructiveHint` | ✅ All read tools marked read-only |
| Title field | Human-friendly display name | ✅ Every tool has a Title |
| Cross-referencing | Point agents to related tools | ✅ Descriptions mention alternatives |
| Error handling | Helpful messages with recovery suggestions | ✅ Structured error responses |

## Where We Go Further

These patterns extend beyond published vendor guidance. They're hypotheses supported by evidence, not industry standards (yet):

### Knowledge tool architecture
No vendor documentation describes the two-tier pattern (compact descriptions + on-demand knowledge endpoints). This emerged from practical experience with `helix_ci_guide` and is supported by the research finding that augmented descriptions increase steps.

### Context tax awareness
Vendor guides say "be concise" but don't quantify the cost of verbose descriptions across sessions. The concept of descriptions as always-loaded context with a per-session token cost is our framing.

### Skip signals
Using "Niche" labels and unattractive names to steer agents away from low-frequency tools isn't in any vendor guide. It's a routing pattern that emerged from the `helix_test_results` rename experience.

### Domain language resilience
The pattern of using domain language in skills (rather than tool names) for cross-MCP-server portability isn't documented by vendors, though it aligns with their emphasis on clear descriptions.

See `validation-methodology.md` for how to test these claims.

## Keeping Current

MCP is evolving. Check these sources periodically:
- MCP spec changelog: https://modelcontextprotocol.io/specification
- Anthropic skills repo: https://github.com/anthropics/skills
- New research on tool description quality (search arXiv for "MCP tool description")
