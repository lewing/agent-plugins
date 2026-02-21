---
name: skill-trainer-knowledge
description: "Institutional knowledge for skill training — frontmatter rules, INVOKES patterns, stop signals, token budgets, anti-patterns, and eval integration. USE FOR: looking up skill-building conventions, understanding training methodology, creating Arena eval requests, checking external reference sources. DO NOT USE FOR: actually training a skill (use SkillTrainer agent), creating new skills from scratch (use skill-builder). INVOKES: no tools — pure knowledge skill."
---

# Skill Trainer Knowledge

Reference knowledge for the SkillTrainer agent. This skill provides institutional memory about how skills are built, tested, and improved — distilled from official docs, Arena empirical data, and real training sessions.

## What's here

- **[Training methodology](references/training-methodology.md)** — The assess→hypothesize→apply→eval→record cycle, regression heuristics, multi-model validation, common training patterns
- **[Eval integration](references/eval-integration.md)** — When to use Arena vs quick validation, issue templates, trigger tests, task evals, reading results
- **[Skill-builder knowledge](references/skill-builder-knowledge.md)** — Frontmatter rules, INVOKES pattern, stop signals, token budgets, anti-patterns, external reference sources

## Templates

### Training Log Entry

Use this format when recording training sessions. Append to `plugins/<group>/training-logs/<skill-name>.md`.

```markdown
## Session: {date} — {brief title}

**Trainer:** SkillTrainer | **Skill:** {skill-name} | **Trigger:** {what prompted this}

### Assessment
**Issues found (ranked):**
1. ❌ {critical — wrong guidance}
2. ⚠️ {incomplete — missing pattern}
3. 💡 {opportunity — not broken}

### Cycle 1: {issue addressed}
**Hypothesis:** Changing {X} will fix {Y} because {Z}.
**Edit:** {file}:{lines} — {description}

| Model | Before | After | Δ Tool Calls |
|-------|--------|-------|-------------|
| {model1} | {result} | {result} | {+/-N} |

**Outcome:** ✅/🔴/🟡 | **Decision:** kept/rolled back/modified

### Patterns Learned
- {new patterns → added to skill-builder-knowledge.md?}

### Open Items
- {deferred items, Arena issues filed}
```

### Arena Issue Body

```markdown
## Skill
- **Name:** {skill-name}
- **Location:** {path}
- **Commit/State:** {SHA or "local"}

## What Changed
{Brief description. Reference training-log entry.}

## Prompts for Eval Tasks
### Should improve
1. "{prompt that should now work better}"
### Should not regress
1. "{prompt for existing functionality}"

## Expected Improvement
- **Target metric:** {e.g., tool call count}
- **Expected direction:** {e.g., 10-20% fewer calls}
- **Regression concern:** {what could get worse}
```
