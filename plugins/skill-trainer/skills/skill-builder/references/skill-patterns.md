# Skill Patterns

Structural patterns for building Copilot CLI skills. Everything here is empirical — proven across vmr-codeflow-status, ci-analysis, and stephentoub's code-review skill.

## Skill Archetypes

### Degrees of Freedom

Match the level of specificity to the task's fragility:

| Freedom | When | Implementation |
|---------|------|----------------|
| **High** (prose instructions) | Multiple valid approaches, decisions depend on context | Knowledge-driven SKILL.md with guidelines, not rules |
| **Medium** (pseudocode/parameterized scripts) | Preferred pattern exists but variation is acceptable | Script with parameters, agent chooses when/how to call |
| **Low** (deterministic scripts, few params) | Operations are fragile, consistency is critical, specific sequence required | Script does the work, agent just invokes |

Think of the agent exploring a path: a narrow bridge with cliffs needs specific guardrails (low freedom), while an open field allows many valid routes (high freedom).

| Signal | Archetype | Example |
|--------|-----------|---------|
| Skill runs scripts, calls APIs, processes data | **Script-driven** | vmr-codeflow-status, ci-analysis |
| Skill teaches conventions, review patterns, domain rules | **Knowledge-driven** | code-review (54KB SKILL.md, no scripts) |
| Skill does both | Start knowledge-driven, add scripts only for what agents can't do natively | — |
| You're tempted to write a script that wraps agent tools | **Don't** — the agent already has create, edit, task, powershell, gh CLI | — |

### Script-driven skills
The SKILL.md describes *when* and *how* to invoke scripts. The scripts do the heavy lifting — API calls, data processing, complex logic that benefits from being deterministic and testable outside an agent context.

### Knowledge-driven skills
The SKILL.md *is* the skill. It contains domain rules, review patterns, process instructions, or behavioral guidance that shapes how the agent thinks and acts. No scripts needed — the agent applies the knowledge using its existing tools.

## Directory Layout

```
skill-name/
├── SKILL.md              # Always required — the entry point (case-sensitive!)
├── scripts/              # Script-driven only
│   └── Get-{Action}.ps1  # PowerShell scripts
└── references/           # Deep reference docs (both archetypes)
    └── *.md              # Loaded on-demand by agent
```

### Skill Locations

| Audience | Path | Notes |
|----------|------|-------|
| Personal (all repos) | `~/.copilot/skills/{name}/` | Also: `~/.agents/skills/{name}/` |
| Repo-specific | `.github/skills/{name}/` | Also: `.agents/skills/{name}/` |

> 💡 The `.agents/` paths work for cross-agent compatibility. Use `.copilot/` / `.github/` as primary. Note that `~/.copilot/skills/` edits don't auto-sync to repo copies — always sync both when deploying changes.

### What NOT to Include

A skill should only contain files that directly support its function. Do **not** create:
- `README.md` — SKILL.md IS the readme
- `CHANGELOG.md`, `INSTALLATION_GUIDE.md`, `QUICK_REFERENCE.md`
- User-facing documentation about the skill itself
- Setup/testing procedures (test via multi-model subagents, not checked-in test harnesses)

The skill exists for an agent to do a job. Everything else is clutter that wastes context tokens.

## SKILL.md Anatomy

### Frontmatter (required)
```yaml
---
name: {skill-name}
description: {One-line trigger-focused description. Include keywords that match user intent.}
---
```

**Constraints:**
- `name`: lowercase with hyphens, no spaces or capitals (max 64 characters). Must match parent directory name. No reserved words (`anthropic`, `claude`, `openai`, `copilot`). Example: `ci-analysis`, not `CI Analysis`
- `description`: max 1024 characters (agentskills.io spec). Be specific about both capabilities AND use cases — this is what Copilot uses to decide when to load the skill

**Supported fields:**
| Field | Required | Notes |
|-------|----------|-------|
| `name` | Yes | Kebab-case, max 64 chars, must match directory name |
| `description` | Yes | Max 1024 chars — the routing signal. Invest here. |
| `argument-hint` | No | Hint text shown in slash command input |
| `user-invokable` | No | Controls `/skill-name` menu visibility (default: true) |
| `disable-model-invocation` | No | Prevents auto-triggering (default: false) |

> ⚠️ Fields not in this table (`license`, `version`) may be silently ignored or cause errors. Re-check official docs periodically as supported fields expand.

#### Invocation Modes

Skills can be triggered automatically or invoked manually as `/skill-name` slash commands. The `user-invokable` and `disable-model-invocation` fields control this:

| user-invokable | disable-model-invocation | Behavior |
|:-:|:-:|---|
| true (default) | false (default) | Both slash command and auto-trigger |
| true | true | Manual only — `/skill-name` in chat |
| false | false | Auto-trigger only — hidden from menu |
| false | true | Effectively disabled |

Most skills use the default (both on). Use manual-only for rarely-needed utilities that would otherwise false-positive on common keywords.

The `description` is how Copilot decides when to invoke the skill. Write it for **trigger matching**, not documentation:
- ❌ `"A tool for CI analysis"` (too vague, won't trigger)
- ✅ Structured description with routing signals (see below)

#### Structured Description Pattern

Pack routing signals into the `description` field using these keywords:

```yaml
description: >
  Analyze CI build and test status from Azure DevOps and Helix for dotnet
  repository PRs. USE FOR: checking CI status, investigating failures,
  determining if a PR is ready to merge, URLs containing dev.azure.com or
  helix.dot.net. DO NOT USE FOR: code review (use code-review skill),
  codeflow status (use vmr-codeflow-status skill).
  INVOKES: Helix and AzDO MCP tools, Get-CiStatus.ps1 script.
```

| Keyword | Purpose | When to include |
|---------|---------|-----------------|
| **USE FOR:** | Trigger phrases matching user intent | Always — list 3-8 phrases users would actually say |
| **DO NOT USE FOR:** | Anti-triggers that route elsewhere | When other skills/tools cover adjacent scenarios |
| **INVOKES:** | Tool families and scripts this skill uses | When the skill uses MCP tools or scripts — name families, not individual tools |
| **FOR SINGLE OPERATIONS:** | Bypass for simple queries | When an MCP tool handles the simple case directly |

**Optional classification prefix**: Start the description with `**WORKFLOW SKILL**`, `**UTILITY SKILL**`, or `**ANALYSIS SKILL**` to add a routing signal for the skill type. This is optional but can improve routing in ecosystems with many skills.

**Why this matters**: Copilot routes via semantic similarity against descriptions. `USE FOR` seeds the embedding with positive matches; `DO NOT USE FOR` prevents false positives when similar skills exist. `INVOKES` bridges the skill to its tool families — especially helpful for scripts the agent can't discover from its tool list.

> ⚠️ **Name tool families, not individual tools.** Write `INVOKES: maestro and GitHub MCP tools, Get-FlowHealth.ps1 script` — not a list of every tool name. The family reference connects the skill to the MCP server descriptions; the agent discovers individual tools from there. Scripts are skill-local and need explicit mention.

**Minimal example** (single-purpose skill, no adjacent skills):
```yaml
description: >
  Analyze CI build and test status from Azure DevOps and Helix for dotnet
  repository PRs. Use when checking CI status, investigating failures,
  or given URLs containing dev.azure.com or helix.dot.net.
```

**Full example** (multi-skill ecosystem with routing concerns):
```yaml
description: >
  Analyze CI build and test status from Azure DevOps and Helix for dotnet
  repository PRs. USE FOR: checking CI status, investigating failures,
  determining if a PR is ready to merge, URLs containing dev.azure.com.
  DO NOT USE FOR: code review (use code-review), codeflow PRs (use
  vmr-codeflow-status). INVOKES: Helix and AzDO MCP tools.
```

> 💡 Not every skill needs the full pattern. A single skill in its own domain can use the minimal form. Add `DO NOT USE FOR` when there are adjacent skills that could be confused with yours. Add `INVOKES` when the skill uses MCP tools or scripts.

**Progressive Disclosure — three loading levels:**

| Level | What | When loaded | Budget |
|-------|------|-------------|--------|
| 1. **Metadata** | `name` + `description` from frontmatter | Every query (~100 words) | Tiny — always present |
| 2. **SKILL.md body** | Instructions, workflow, anti-patterns | When skill triggers | 2K–4K tokens (orchestrating) or up to 15K (applied-once knowledge) |
| 3. **Resources** | `scripts/`, `references/`, `assets/` | On demand by agent | Unlimited — scripts can execute without loading into context |

This is why keeping SKILL.md focused matters — level 1 happens for every query, level 2 only when the skill matches, level 3 only when the agent needs depth. When SKILL.md grows past ~500 lines, split content into `references/` and link clearly from SKILL.md so the agent knows the files exist.

### Sections by Archetype

**Script-driven SKILL.md:**

| Section | Purpose |
|---------|---------|
| Title + overview | What it does in 1-2 sentences |
| Prerequisites | Tools required (gh CLI, darc, etc.) |
| When to Use This Skill | Bullet list of trigger scenarios and keywords |
| Quick Start | Code examples with common flag combinations |
| Key Parameters | Table: Parameter, Required, Default, Description |
| What the Script Does | Numbered steps explaining the script's logic |
| Interpreting Results | Subsections with emoji status meanings |
| References | Links to `references/*.md` docs |

**Knowledge-driven SKILL.md:**

| Section | Purpose |
|---------|---------|
| Title + overview | What domain knowledge it encodes |
| When to Use This Skill | Trigger scenarios |
| Process / Methodology | Step-by-step instructions the agent follows |
| Domain Rules | Organized by category, with severity (❌/⚠️/💡) |
| Anti-patterns (inline) | Top 3-5 critical mistakes, near relevant steps |
| References | Links to deeper reference docs |

### Template: Script-driven SKILL.md

```markdown
---
name: {skill-name}
description: >
  {One-line description}. USE FOR: {trigger phrase 1}, {trigger phrase 2},
  {trigger phrase 3}. DO NOT USE FOR: {scenario} (use {other-skill}).
  INVOKES: {tool-family} MCP tools, {script-name} script.
---

# {Skill Title}

{One-line overview of what this skill does.}

## Prerequisites

- **{tool}** — {why it's needed}

## When to Use This Skill

Use this skill when:
- {scenario 1 with keywords users would say}
- {scenario 2}
- {scenario 3}

## Quick Start

` ` `powershell
./scripts/Get-{Action}.ps1 -{MainParam} {value}
./scripts/Get-{Action}.ps1 -{MainParam} {value} -{OptionalFlag}
` ` `

## Key Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `-{Param}` | Yes | — | {description} |

## What the Script Does

1. **{Step name}** — {what and why}
2. **{Step name}** — {what and why}

## Interpreting Results

### {Category}
- **✅ {Good state}**: {what it means}
- **⚠️ {Warning state}**: {what it means, what to do}
- **🔴 {Error state}**: {what it means, what to do}

## References

- **{Topic}**: See [references/{file}.md](references/{file}.md)
```

> 💡 Add a `## Stop Signals` section after Interpreting Results with explicit bounds: "Check at most N items", "Stop when you've found X". See [Stop Signals](#stop-signals).

### Template: Knowledge-driven SKILL.md

```markdown
---
name: {skill-name}
description: >
  {One-line description}. USE FOR: {scenario 1}, {scenario 2}.
  DO NOT USE FOR: {scenario} (use {other-skill}).
---

# {Domain} {Purpose}

{Overview: what knowledge this encodes and where it came from.}

## When to Use This Skill

Use this skill when:
- {scenario 1}
- {scenario 2}

## Process

1. **{First step}** — {instructions}
2. **{Second step}** — {instructions}

## {Domain Category 1}

### {Rule name}
- **{Pattern or convention}** — {explanation with rationale}
  > "{Quote from real review/source}" — {author}

## ❌ Critical Anti-patterns

{Inline the 3-5 most important mistakes near the steps where they'd occur.}

1. **Never {bad thing}** — {why it breaks, with evidence}
2. **Never {bad thing}** — {why it breaks}

## References

- See [references/{file}.md](references/{file}.md)
```

## Context Budget

Rule of thumb: **~750 words ≈ 1K tokens** for English prose with code snippets.

| Skill Type | SKILL.md Target | Notes |
|------------|----------------|-------|
| Script-driven | 1K–3K tokens | Compact; depth lives in scripts and references |
| Knowledge-driven (applied once) | Up to 15K tokens | Stephen's 54KB code-review works — agent loads once, applies repeatedly |
| Knowledge-driven (orchestrating) | 2K–4K tokens | Keep lean; agent needs context for the work itself |
| References (each) | 2K–8K tokens | Loaded on-demand; can be larger |

> 💡 The agent is already very smart. Only add context it doesn't already have. Challenge each paragraph: "Does this justify its token cost?"

## Stop Signals

The **highest-leverage single edit** you can make to a skill. Arena data: one well-placed stop signal sentence saved 10+ tool calls (42→25).

Every skill should have explicit stop signals. Place them:
- In a dedicated `## Stop Signals` section (for skills with complex workflows)
- Inline near decision points where the agent tends to over-investigate

### Requirements
- **Be explicit**: "Stop when you have root cause" not "try to be efficient"
- **Include numeric bounds**: "Check at most 3 work items" not "a few"
- **Cover over-investigation patterns**: What does the agent tend to do too much of?
- **Place at decision points**: Where the agent chooses between going deeper vs reporting

### Good stop signals
```markdown
## Stop Signals
- Stop investigating when you've found the failing test and its error message. Don't trace the full call stack.
- Check at most 5 recent builds. If the pattern isn't clear, report what you have.
- Don't enumerate all possible causes. Identify the most likely one and state your confidence.
- If the user asked a yes/no question, answer it. Don't provide a full investigation.
```

### Bad stop signals
```markdown
- Try to be efficient (too vague — not actionable)
- Don't waste time (no specific guidance on what to stop doing)
- Be thorough but concise (contradictory — agent will err toward thorough)
```

## Output Patterns

Use these when skills need consistent output format.

### Template Pattern

Provide templates matching the level of strictness needed:

**Strict** (API responses, data formats, structured reports):
```markdown
## Report structure
ALWAYS use this exact structure:
# [Title]
## Summary
[One paragraph]
## Findings
- Finding 1 with data
## Recommendations
1. Specific action
```

**Flexible** (when adaptation is useful):
```markdown
## Report structure
Sensible default — adapt as needed:
# [Title]
## Summary / ## Findings / ## Recommendations
Adjust sections based on what you discover.
```

### Examples Pattern

When output quality depends on style, provide input/output pairs:
```markdown
## Commit message format
Input: Added user authentication with JWT tokens
Output: feat(auth): implement JWT-based authentication
```

Examples teach style more efficiently than descriptions.

## Intermediate Data Storage

When a skill collects, filters, or correlates **multi-item data** (PRs, files, test results, manifests), it needs somewhere to put that data between tool calls. Choose the right storage mechanism:

| Mechanism | When to use | Advantages |
|-----------|------------|------------|
| **SQL tool** | Multi-item structured data needing filtering, dedup, status tracking, or cross-referencing | Queryable, persistent across tool calls, no approval prompts, supports ad-hoc progress queries |
| **In-memory (script variables)** | Single-pass scripts that collect → correlate → emit in one execution | Simple, no setup |
| **Temp files** | ❌ Avoid — triggers approval prompts in Copilot CLI, requires cleanup, fragile | — |

### SQL Tool Pattern

For orchestrating skills that discover items and process them across multiple phases, define a schema in SKILL.md and instruct the agent to use it:

```sql
CREATE TABLE IF NOT EXISTS work_items (
    id TEXT PRIMARY KEY,
    category TEXT NOT NULL,
    status TEXT DEFAULT 'pending',  -- 'pending', 'done', 'skipped'
    notes TEXT
);
```

**When to use SQL:**
- Skill discovers N items (files, PRs, test cases) and must process each across multiple phases
- Items can be filtered, skipped, or need deduplication
- Agent needs to answer "what's left?" or "what did I skip and why?" mid-task
- Long-running tasks where the agent may lose track of state

**When NOT to use SQL:**
- Single-item skills (one PR review, one test case, one API addition)
- Script-driven skills where the script handles all data internally in one pass
- Fewer than ~5 items that the agent can track naturally

**Evidence:** The `libraries-release-notes` skill (dotnet/core) uses SQL to track 100+ PRs through a collect → filter → enrich → dedup → author pipeline. The `tfm-version-upgrade` skill uses SQL to track dozens of files across 6 phases — preventing the agent from losing track of which files have been updated during long version bump operations.

## Script Conventions (Script-driven Only)

### Naming
- Pattern: `Get-{DomainAction}.ps1` (e.g., `Get-CodeflowStatus.ps1`, `Get-HelixFailures.ps1`)
- Use PowerShell verb-noun convention

### Parameter Block
```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [int]$PRNumber,

    [string]$Repository = "dotnet/sdk",

    [switch]$Verbose
)
```

### Output Patterns
```powershell
# Section headers
function Write-Section($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

# Status indicators — consistent emoji + color
Write-Host "  ✅ Healthy" -ForegroundColor Green
Write-Host "  ⚠️  Warning: $detail" -ForegroundColor Yellow
Write-Host "  🔴 Error: $detail" -ForegroundColor Red
Write-Host "  ℹ️  Info" -ForegroundColor DarkGray
```

### Error Handling
```powershell
# Fail-closed: Unknown states are NOT healthy
$result = @{ Status = "⚠️ Unknown"; Color = "Yellow" }
$json = gh pr view $PR -R $Repo --json body 2>$null
if ($LASTEXITCODE -ne 0 -or -not $json) {
    return $result  # Return Unknown, don't assume healthy
}
# Only set healthy after confirmed
$result.Status = "✅ Healthy"
$result.Color = "Green"

# Disposal in finally blocks
$client = [System.Net.Http.HttpClient]::new($handler)
try {
    $resp = $client.GetAsync($url).Result
    # ... use resp ...
} finally {
    if ($resp) { $resp.Dispose() }
    $client.Dispose()
    $handler.Dispose()
}
```

### Graceful Degradation: Script-as-Scaffold

Design multi-step scripts so the agent can **continue manually when any step fails**. Each step should:
1. Emit human-readable progress showing what was discovered so far
2. On failure, emit a `[SKILL_SUMMARY]` JSON with `status: "{step}_failed"` and a `fallback` field telling the agent exactly what to do next
3. Exit cleanly so the agent keeps the context from completed steps

```powershell
# ✅ Good — step fails gracefully with actionable fallback
$content = Invoke-SomeAPI -Ref $commit 2>$null
if (-not $content) {
    Write-Status "⚠️" "Cannot fetch file at $commit" Yellow
    $summary = [ordered]@{
        status   = "step3_failed"
        # Pass through everything discovered so far
        vmrCommit = $vmrCommit
        buildDate = $buildDateStr
        # Tell the agent what to do instead
        fallback = "Use github-mcp-server-get_file_contents for dotnet/dotnet path/to/file at sha $vmrCommit"
    }
    Write-Host "[SKILL_SUMMARY]"
    Write-Host ($summary | ConvertTo-Json -Depth 4 -Compress)
    Write-Host "[/SKILL_SUMMARY]"
    exit 0  # exit 0, not 1 — agent should continue, not stop
}
```

**Why this matters**: In practice, scripts fail on API quirks, auth issues, or data format surprises. If a 6-step script crashes at step 3 with an unhandled exception, the agent loses all progress and starts over. With graceful degradation, the agent gets steps 1-2 for free and only has to do steps 3-6 manually — still a significant improvement over no skill at all.

**Evidence**: `sdk-version-trace` script crashed at step 3 (API format mismatch), but the agent completed the trace in 5.3 min using domain knowledge from SKILL.md references. Without the skill, the same question took 15 min and 71 tool calls. The script's partial output (decoded version, found AzDO build, identified VMR commit) saved ~10 min of work even though it didn't finish.

### Data vs. Reasoning Boundary

Scripts should **collect and correlate data**, not generate analysis. The agent is better at reasoning over facts — it adapts to context, handles edge cases, and produces nuanced advice. Scripts produce the same canned output regardless of context.

**Rule of thumb**: If a script section is a chain of `if/elseif` branches producing prose text, that's reasoning — move it to the agent.

| Script should do | Agent should do |
|-----------------|-----------------|
| API calls, pagination, rate limiting | Synthesize recommendations from facts |
| Regex parsing, data extraction | Interpret what the data means in context |
| Cross-referencing timestamps, SHAs | Decide what action the user should take |
| Computing deltas, counting items | Explain why and weigh tradeoffs |
| Deterministic status (pass/fail/unknown) | Contextual judgment (severity, priority) |

**Pattern: Structured JSON handoff**

The script emits human-readable output for each data-gathering step, then a structured JSON summary block at the end. The agent reads both, then generates recommendations.

```powershell
# Script collects data in Steps 1-N, writes human-readable output...
# Then at the end:
$summary = [ordered]@{
    status      = $currentState
    freshness   = [ordered]@{ aheadBy = $ahead; isUpToDate = $isUpToDate }
    warnings    = [ordered]@{ conflicts = $conflictCount; stale = $isStale }
    # ... all key facts ...
}
Write-Host "[SKILL_SUMMARY]"
Write-Host ($summary | ConvertTo-Json -Depth 4 -Compress)
Write-Host "[/SKILL_SUMMARY]"
```

The SKILL.md then includes a "Generating Recommendations" section that teaches the agent how to reason over the summary — decision tables, priority rules, tone guidance.

**Evidence**: vmr-codeflow-status had 130+ lines of hardcoded recommendation logic (9-branch if/elseif chain). Replacing it with a JSON summary + 30 lines of SKILL.md guidance produced better, more contextual recommendations that adapt to edge cases the script never anticipated (e.g., closed PRs, non-codeflow PRs, partially-resolved conflicts).

## copilot-instructions.md Integration

To make a skill part of a repo's standard workflow, add to `.github/copilot-instructions.md`:

```markdown
Before completing, use the `{skill-name}` skill to {action}. Any issues flagged as errors should be addressed before completing.
```

This wires the skill into every agent session in that repo automatically.

## Trigger Collision Prevention

When multiple skills share a domain (e.g., CI analysis and code review both touch PRs), their descriptions can overlap, causing unpredictable routing. Prevent this with reciprocal boundaries.

### The Problem

```yaml
# Skill A
description: "Help with PRs including CI status and code review"
# Skill B
description: "Help with PRs including code review and suggestions"
# → Both match "review my PR" → unpredictable routing
```

### The Fix: Reciprocal USE FOR / DO NOT USE FOR

Each skill's `DO NOT USE FOR` should name the other skill:

```yaml
# ci-analysis
description: >
  Analyze CI build and test status for dotnet PRs.
  USE FOR: CI status, test failures, build errors, helix logs.
  DO NOT USE FOR: code review (use code-review skill),
  codeflow status (use vmr-codeflow-status skill).

# code-review
description: >
  Review code changes for correctness, performance, and conventions.
  USE FOR: reviewing PRs, code changes, diff analysis.
  DO NOT USE FOR: CI failures (use ci-analysis skill),
  codeflow status (use vmr-codeflow-status skill).
```

### Testing for Collisions

When adding a skill to a domain that already has skills, test ambiguous prompts:

| Prompt | Should route to | Should NOT route to |
|--------|----------------|---------------------|
| "Check CI on my PR" | ci-analysis | code-review |
| "Review my changes" | code-review | ci-analysis |
| "Why is my PR failing?" | ci-analysis | code-review |
| "Is this code correct?" | code-review | ci-analysis |

If an ambiguous prompt routes to the wrong skill, strengthen the anti-triggers in both descriptions.

### Hybrid Routing: Names vs Prompts

Skills reference other skills in two places with different audiences:

| Location | Audience | Best approach |
|----------|----------|---------------|
| **`description` frontmatter** | Routing LLM (sees ALL skill descriptions) | Name skills directly: `DO NOT USE FOR: X (use other-skill)` |
| **SKILL.md body** | Agent (already loaded the wrong skill) | Suggest prompts: *"rephrase as 'check CI status' to match the right skill"* |

**Why the difference**: The `description` is seen pre-load when the LLM is deciding which skill to invoke — it can see all skill names and route accordingly. The SKILL.md body is only seen *after* the wrong skill loaded — naming another skill won't trigger re-routing, but suggesting a prompt the user can say gives them a path to the right skill.

```yaml
# In description (direct naming — LLM can route):
description: >
  Trace component SHAs in SDK builds.
  DO NOT USE FOR: codeflow PR health (use vmr-codeflow-status skill).
```

```markdown
<!-- In SKILL.md body (prompt suggestion — helps user re-route): -->
Do **NOT** use this skill when:
- Asked about codeflow PR health — rephrase as: *"check codeflow status for dotnet/runtime"*
```

## Orchestrator Pattern (Multi-Skill Ecosystems)

When building 5+ related skills in a domain, consider adding a routing orchestrator — a lightweight skill that handles cross-cutting questions and delegates to specialized skills.

### When to Use

- Users frequently ask comparison questions ("Should I use A or B?")
- Multiple skills share patterns (auth, networking, error handling)
- The domain is growing and needs unified entry points

### When NOT to Use

- Fewer than 5 skills with minimal overlap
- Services are unrelated (no cross-skill questions)
- Maintenance overhead isn't justified

### Structure

```
skills/
├── domain-overview/           # Orchestrator — cross-cutting concerns
│   ├── SKILL.md               # Decision trees, comparisons, routing
│   └── references/
├── domain-service-a/          # Specialist — deep knowledge
├── domain-service-b/          # Specialist — deep knowledge
└── domain-service-c/          # Specialist — deep knowledge
```

### Routing Design

The orchestrator handles "which?" and "compare" questions; specialists handle "how?" questions:

```yaml
# Orchestrator
description: >
  Data service selection and cross-cutting patterns.
  USE FOR: compare databases, choose data store, migration strategy.
  DO NOT USE FOR: service-specific implementation (use database-postgres,
  database-mysql directly).

# Specialist
description: >
  PostgreSQL configuration and operations.
  USE FOR: PostgreSQL setup, query optimization, auth configuration.
  DO NOT USE FOR: comparing database options (use data-services).
```

**Key principle**: Orchestrator and specialist descriptions must be complementary — every `DO NOT USE FOR` on one should match a `USE FOR` on the other.
