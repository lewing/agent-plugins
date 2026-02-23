# Skill-Builder Knowledge

Distilled patterns from official docs, Arena empirical data, and real skill-building sessions. This is the trainer's institutional memory — update it as you learn.

## Frontmatter Rules

### Supported fields
| Field | Required | Notes |
|-------|----------|-------|
| `name` | Yes | Max 64 chars, kebab-case. Must match parent directory name. No reserved words (`anthropic`, `claude`, `openai`, `copilot`). |
| `description` | Yes | Max 1024 chars (agentskills.io spec). This is the routing signal — invest here. |
| `argument-hint` | No | Hint text shown in slash command input |
| `user-invokable` | No | Controls visibility in `/skill-name` menu (default: true) |
| `disable-model-invocation` | No | Prevents auto-triggering (default: false) |

### Invocation modes
| user-invokable | disable-model-invocation | Behavior |
|:-:|:-:|---|
| true | false | **Default** — slash command + auto-trigger |
| true | true | Manual only — `/skill-name` only |
| false | false | Hidden — auto-trigger only, not in menu |
| false | true | Disabled — neither slash command nor auto |

### Description structure (must fit 1024 chars)
```
name: my-skill
description: "Brief summary sentence. USE FOR: scenario1, scenario2, keyword1, keyword2. DO NOT USE FOR: anti-scenario1, anti-scenario2. INVOKES: tool-family1 MCP tools, Script-Name.ps1 script."
```

Optional: Prefix with classification — `**WORKFLOW SKILL**`, `**UTILITY SKILL**`, `**ANALYSIS SKILL**`

Optional: Add `FOR SINGLE OPERATIONS: Use {mcp-tool} directly for simple queries` to prevent false-positive skill invocations on simple operations.

## INVOKES Pattern (Empirically Validated)

INVOKES is **not** part of the agentskills.io spec — it's our extension. Use sparingly within the 1024-char budget.

### What to include
- **Tool family names** (not individual tools): "maestro and GitHub MCP tools"
- **Script names** (always — they're undiscoverable): "Get-FlowHealth.ps1 script"
- **Never** list individual MCP tool names — the agent already has tool descriptions

### Evidence
A/B tested with subagents (session 4e816252):
- Sonnet 4: minimal impact (9→10 tools found)
- GPT-5.1: significant impact (4→6 tools found, also stopped trying to invoke wrong skills)
- Scripts are the biggest win — completely undiscoverable from the tool list

### Pattern
```
INVOKES: {family1} and {family2} MCP tools, {Script1}.ps1 script.
```

## Stop Signals (Highest-Leverage Edit)

Arena data: single stop signal sentence saved 10+ tool calls (42→25).

### Requirements
- **Explicit**: "Stop when you have root cause" not "try to be efficient"
- **Numeric bounds**: "Check at most 3 work items" not "a few"
- **At decision points**: Place where the agent might over-investigate, not just at the top
- **Cover over-investigation patterns**: What the agent tends to do too much of

### Examples
```markdown
## Stop Signals
- Stop investigating when you've found the failing test and its error message. Don't trace the full call stack.
- Check at most 5 recent builds. If the pattern isn't clear by then, report what you have.
- Don't enumerate all possible causes. Identify the most likely one and state your confidence.
```

## Token Budgets (Validated by Arena + Experience)

| Type | Budget | Notes |
|------|--------|-------|
| Orchestrating SKILL.md | 2K–4K tokens | Coordinates work, loads references on demand |
| Knowledge-only SKILL.md | Up to 15K tokens | Applied once per task (e.g., code-review) |
| Total skill (SKILL.md + refs) | Up to 20K tokens | References loaded on demand, not all at once |

> **Intentional divergence from aka.ms/skills/guidance** which recommends 500/5000. Our numbers are validated by Arena (SKILL-FORMAT.md enforces 4K/15K/20K) and real-world skills (54KB code-review skill works well). The 500-token number is Azure-ecosystem-specific where many skills compete for context.

## Anti-Patterns (Empirical)

### Re-documenting MCP tools
**Never** restate tool parameter schemas or chain tool calls into rigid step-by-step recipes. The agent has tool descriptions in its context. DO provide domain examples that add context the tool description lacks (branch ref patterns, field names, log locations).

Caught empirically: user immediately flagged agent over-documenting dual AzDO org configuration. "Does it need to know that?" — keep guidance to intent, not tool details.

### Wrong org/service selection
AzDO MCP tools are org-specific (dnceng vs dnceng-public). Wrong org = silent null results. This is a recurring confusion vector. Skills targeting dotnet repos should name both orgs and provide selection guidance.

### Encoding reasoning in scripts
Scripts collect data; agents reason over it. If a script has `if/elseif` chains producing prose recommendations, move that logic to SKILL.md guidance and have the script emit structured JSON.

### Burying critical rules as numbered workflow steps
When a workflow has numbered steps (Step 1, Step 2, ...), models treat mid-sequence steps as optional — especially haiku, which optimizes for speed. **Place 🚨 rules in the workflow preamble (before Step 1), not as a numbered step.** Confirmed across 3 skills:
- ci-analysis: "Use Its Output" rule at Step 1 top → works. As a tip → ignored.
- flow-analysis PR workflow: 🚨 before Step 1 → both models comply.
- flow-analysis Codeflow Overview: Step 5 rule → haiku skipped entirely, sonnet read but didn't internalize. Moved to preamble → both models comply.

## Skill Locations

| Audience | Primary path | Also works |
|----------|-------------|-----------|
| Just you | `~/.copilot/skills/{name}/` | `~/.agents/skills/{name}/` |
| Your team/repo | `.github/skills/{name}/` | `.agents/skills/{name}/` |

> ⚠️ `~/.copilot/skills/` edits don't auto-sync to repo copies. This is a recurring gap — always sync both when deploying changes.

## Skills vs Other Configuration

| Need | Use |
|------|-----|
| Reusable investigation/automation with triggers | **Skill** |
| Repo-wide conventions applied to every prompt | **copilot-instructions.md** |
| Path-specific rules (e.g., "all files in src/api/ must...") | **.instructions.md** |
| Agent with identity, tools, autonomy | **.agent.md** |

## Testing Insights

- **Gemini 3 Pro** consistently fails with 400 errors in skill testing. Use `gpt-5.3-codex` as third-family alternative.
- **Different models catch different issues.** GPT-5.2 found missing permissions; Opus found version table mismatch. Use 2+ families.
- **Best practices check** is a distinct step from multi-model eval: check token budget, script conventions (CmdletBinding, graceful exits), progress output formatting.
- **Regression heuristic**: >20% tool call increase = rollback, >10% decrease = improvement. Also track elapsed time (>30%) and correctness regressions.
- **"Never stack unvalidated fixes"** — one change, one eval, one result.
- **Validation consensus**: 3-model validation of skill-builder scored 4-5/5 across accuracy, completeness, clarity. Token efficiency averaged 3.7/5 — modularity via references mitigates this.
- **Reviewer feedback is additive**: All 3 models flagged the same 2 gaps (time-based regression threshold, secrets-in-git-history) — high confidence these were real gaps.

## POC Learnings (skill-builder training, session 1edd71d2)

1. **Actively wrong guidance is highest priority** — the frontmatter "NEVER add unsupported fields" was causing agents to skip useful fields. Fix wrong before filling gaps.
2. **Stop signals section wrote itself** — Arena data made the case; the pattern is clear enough to template.
3. **External link additions are low-risk, high-value** — agentskills.io and aka.ms/skills/guidance are authoritative references agents can fetch on demand.
4. **Security anti-patterns are obvious once listed** — but no one lists them. The section took 5 minutes and covers the common mistakes.
5. **Multi-model validation catches real gaps** — Opus uniquely identified correctness regression as distinct from "misapplies guidance" and flagged secrets-in-git-history. Worth the 3-model cost every time.

## External References

Periodically re-fetch these for updates. Each has specific things we rely on.

| Source | URL | What we use from it | Last reviewed |
|--------|-----|---------------------|---------------|
| GitHub: About Agent Skills | https://docs.github.com/en/copilot/concepts/agents/about-agent-skills | Skill discovery, SKILL.md naming, location paths (`.github/skills/`, `~/.copilot/skills/`) | 2026-02-20 |
| GitHub: Creating Skills for CLI | https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-skills | CLI-specific skill creation, `/skills` command, `/plugin` marketplace | 2026-02-20 |
| GitHub: Customizing CLI | https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/quickstart-for-customizing | Plugins, skills, hooks, agents overview for Copilot CLI | 2026-02-20 |
| VS Code: Agent Skills | https://code.visualstudio.com/docs/copilot/customization/agent-skills | Frontmatter fields (`user-invokable`, `disable-model-invocation`, `argument-hint`), `name` must match directory name | 2026-02-20 |
| GitHub: Custom Instructions | https://docs.github.com/en/copilot/customizing-copilot/adding-repository-custom-instructions-for-github-copilot | Distinction between skills vs copilot-instructions.md vs .instructions.md | 2026-02-20 |
| agentskills.io | https://agentskills.io | Open standard: name max 64 chars kebab-case, description max 1024 chars, reserved words list | 2026-02-20 |
| agentskills.io (GitHub spec) | https://github.com/agentskills/agentskills | Formal spec repo — canonical field definitions, cross-platform support matrix | 2026-02-20 |
| Skills, Tools & MCP Guide | https://aka.ms/skills/guidance | Skill classification (utility/workflow/analysis), routing patterns, USE FOR/DO NOT USE FOR, FOR SINGLE OPERATIONS, token budgets (500/5000 — we intentionally diverge to 4K/15K) | 2026-02-20 |
| Plugin Marketplaces (Claude Code) | https://code.claude.com/docs/en/plugin-marketplaces | Marketplace creation and distribution — copilot-skills uses this model | 2026-02-20 |
| Plugins Reference (Claude Code) | https://code.claude.com/docs/en/plugins-reference | Full plugin manifest schema (`plugin.json`), MCP server declaration | 2026-02-20 |
| Skill Authoring Best Practices (Anthropic) | https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices | Progressive disclosure, context budgets, description <200 chars optimal, body ~50 lines target | 2026-02-22 |
| Sample Skills (Anthropic) | https://github.com/anthropics/skills | Reference skill implementations from Anthropic — good for structure patterns | 2026-02-22 |
| Arena SKILL-FORMAT.md | `Blazor-Playground/arena` repo | Token budget validation (4K/15K/20K enforced), stop signal evidence (42→25 tool calls), domain examples > tool schemas | 2026-02-20 |
| Arena decisions.md | `Blazor-Playground/arena` repo | 16+ architectural decisions, INVOKES A/B test data, model-specific behaviors | 2026-02-20 |
| copilot-skills README | `Blazor-Playground/copilot-skills` README.md | Marketplace structure, plugin groups, CLI tool (`blazor-ai.cs`) commands, installation paths | 2026-02-20 |
| copilot-skills copilot-instructions | `Blazor-Playground/copilot-skills` .github/copilot-instructions.md | CI validation rules, catalog update checklist, SKILL.md conventions | 2026-02-20 |
| SkillImporter agent | `Blazor-Playground/copilot-skills` .github/agents/SkillImporter.agent.md | `git filter-repo` import workflow, catalog update steps, history preservation | 2026-02-20 |

### What to check on review
- **agentskills.io**: New supported frontmatter fields? Changed constraints on name/description?
- **GitHub docs**: New skill locations? Changes to discovery mechanism? New CLI commands?
- **VS Code docs**: New frontmatter fields? Changes to invocation mode behavior?
- **aka.ms/skills/guidance**: Updated token budget recommendations? New classification types? New routing patterns?
- **Anthropic best practices**: Updated progressive disclosure guidance? Changed description/body size recommendations?
- **Claude Code docs**: Plugin manifest schema changes? New marketplace features?
- **Arena**: New empirical findings? Updated SKILL-FORMAT constraints?
- **copilot-skills repo**: New plugin groups? Changed CI validation rules? New CLI tool features?
