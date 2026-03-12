# Knowledge Tool Design

A knowledge tool is an MCP tool that returns domain guidance rather than data. Instead of querying a database or calling an API, it returns patterns, recommended approaches, and contextual information that helps agents use the other tools correctly.

## The Two-Tier Architecture

MCP servers for complex domains benefit from separating always-loaded routing information from on-demand domain knowledge:

**Tier 1: Tool descriptions** (always loaded)
- Compact, purpose-first descriptions
- Just enough to route the agent to the right tool
- Consistent across all sessions

**Tier 2: Knowledge tools** (loaded on demand)
- Detailed domain patterns, repo-specific guidance, recommended tool sequences
- Only loaded when the agent needs them
- Can be parameterized (e.g., return different guidance per repository)

### Why not just put everything in descriptions?

Augmented descriptions improve task success but increase execution steps significantly (see `industry-alignment.md` for research evidence). Compact descriptions with on-demand depth achieve a better tradeoff.

## Exemplar: `helix_ci_guide`

The `helix_ci_guide` tool in helix.mcp demonstrates the pattern:

**What it does:**
- Takes an optional `repo` parameter (e.g., "runtime", "aspnetcore", "sdk")
- Returns repo-specific CI investigation guidance: failure patterns, recommended tool sequences, exit code meanings, known gotchas, pipeline details
- Without a repo parameter, returns an overview of all supported repos

**What it replaced:**
- Before: repo-specific patterns were embedded in tool descriptions (e.g., "runtime uses '[FAIL]', aspnetcore uses '  Failed'")
- This made every tool description longer and coupled descriptions to domain knowledge that changes

**How agents use it:**
1. Agent receives a CI investigation task
2. Agent calls `helix_ci_guide(repo="aspnetcore")` first to understand the landscape
3. Agent then uses the right data tools (`azdo_timeline`, `helix_search_log`, etc.) with the correct patterns

**Key design choices:**
- The guide returns structured guidance, not raw data — it's opinionated about what to do
- It covers things that don't belong in any single tool's description: recommended tool sequences, cross-tool workflows, repo-specific failure patterns
- It's the bridge between "I have 20 tools available" and "here's which ones to use for this repo"

## When to Create a Knowledge Tool

### Create one when:
- **Multiple tools need shared context.** If 5 tools all need to know "runtime tests use '[FAIL]' as the failure marker," that context belongs in a knowledge tool, not repeated across 5 descriptions.
- **Guidance varies by parameter.** Per-repo, per-project, or per-environment guidance that changes the investigation approach.
- **Agents consistently make wrong first choices.** If agents try the wrong tool family without domain context, a knowledge tool that explains the landscape fixes this at the source.
- **The guidance changes independently from the tools.** When you add a new repo profile, you update the knowledge tool — not every tool description.

### Don't create one when:
- **Tool descriptions suffice.** If agents consistently pick the right tool from descriptions alone, adding a knowledge tool creates unnecessary indirection.
- **Only one tool needs the context.** Put it in that tool's parameter descriptions instead.
- **The knowledge is static and tiny.** A one-liner ("most repos use AzDO test results") belongs in a description, not a separate tool call.

## Design Patterns

### Parameterized guidance
The most useful knowledge tools accept parameters that scope the response:

```
helix_ci_guide(repo="aspnetcore")
→ aspnetcore-specific patterns, pipelines, failure markers

helix_ci_guide()
→ overview of all repos, when to use which tools
```

### Structured output
Return guidance in a structured format that agents can act on:

```json
{
  "repo": "aspnetcore",
  "pipeline": "aspnetcore-ci (def 83)",
  "org": "dnceng-public/public",
  "failure_patterns": ["  Failed", "Error Message:"],
  "test_results_via": "azdo_test_runs → azdo_test_results",
  "gotchas": ["failedTests count can show 0 when failures exist"]
}
```

### Cross-tool workflow guidance
Knowledge tools can describe multi-step workflows without creating rigid recipes:

```
"For aspnetcore CI failures:
1. Get the build timeline to find failed jobs
2. Check if the job sent work to Helix
3. If Helix: get job status, then search console logs
4. Test results are in AzDO, not Helix — use azdo_test_runs"
```

This is guidance, not a script. The agent adapts based on what it finds at each step.

## Integration with Skills

Skills that consume your MCP server should reference the knowledge tool in domain language:

✅ "Start by checking the CI investigation guide for repo-specific patterns"
❌ "Call `helix_ci_guide` with the repo parameter"

The first survives tool renames. The second breaks if the tool is renamed or moved to a different server.

See `skill-builder/references/anti-patterns.md` for more on this pattern.
