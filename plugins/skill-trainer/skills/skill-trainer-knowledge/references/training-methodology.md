# Training Methodology

How to assess, improve, and validate skills through structured iteration.

## The Training Cycle

```
Assess → Hypothesize → Apply → Eval → Record → (repeat or stop)
```

Each cycle targets **one issue**. Never stack unvalidated fixes.

## Assessment

### Reading a skill

1. **SKILL.md** — Is the frontmatter correct? Is the description within 1024 chars? Are stop signals present and specific?
2. **references/** — Are they loaded on demand or bloating the main file? Token budget: 4K orchestrating, 15K knowledge, 20K total.
3. **training-logs/<skill-name>.md** — What was tried before? What worked? What regressed? Lives at `plugins/<group>/training-logs/`, NOT inside the skill directory.
4. **Existing evals** — Check Arena for recent results. Check `evals/<skill-name>/` in the repo.

### Quick multi-model evaluation

Select 3 models from 2+ families. Current recommendations:
- **Claude Sonnet 4** or **Claude Sonnet 4.5** (Anthropic)
- **GPT-5.1-Codex** or **GPT-5.2** (OpenAI)
- **Claude Opus 4.6** (premium, for critical skills)

> ⚠️ Gemini 3 Pro has been unreliable (400 errors). Use `gpt-5.3-codex` as the third family if needed.

Use the `task` tool with `model` parameter. Give each model the same realistic prompt. Compare:
- Correctness of output
- Tool call count and sequence
- Whether guidance was followed or misapplied

### Issue ranking

Rank findings by impact:
- **❌ Wrong** — Guidance causes incorrect behavior. Fix immediately.
- **⚠️ Incomplete** — Missing guidance causes models to guess. Fix soon.
- **💡 Opportunity** — Could be better but isn't broken. Fix if time permits.

## Hypothesis-Driven Fixes

Before editing, state:
```
Hypothesis: Changing [specific text] will fix [observed problem] because [reasoning].
Evidence: [what you observed that led to this hypothesis]
Risk: [what could regress]
```

This goes in the training log whether the fix works or not.

## Validation

### Regression heuristics (from Arena)

| Metric | Flag | Signal |
|--------|------|--------|
| Tool call increase > 20% on any task | 🔴 Regression | Rollback the change |
| Tool call decrease > 10% | 🟢 Improvement | Record as evidence |
| Model misapplies new guidance | 🔴 Regression | Needs anti-pattern or rewording |
| Models converge on correct behavior | 🟢 Improvement | High confidence |
| One model improves, others unchanged | 🟡 Partial | Likely acceptable |

### Before/after comparison

Run the same prompt on the same models. Capture:
- Output correctness (qualitative)
- Tool call count (quantitative)
- Any guidance the model cited or ignored
- Time to completion

## When to Stop

- **After 3 change-eval cycles per session.** More than that risks fatigue and compounding changes.
- **When scores are ≥ 4/5 across all test models.** Diminishing returns beyond this.
- **When the remaining issues are 💡 not ❌ or ⚠️.** Ship what you have.
- **When you're unsure if a change helps.** Open an Arena issue instead of guessing.

## Common Training Patterns

### Pattern: Stop signal is missing or vague
The highest-leverage single edit. Arena data: one stop signal sentence saved 10+ tool calls (42→25).
- **Bad:** "Try to be efficient"
- **Good:** "Stop when you have identified root cause. Check at most 3 work items before reporting."
- Place stop signals at decision points, not just at the top.

### Pattern: Model uses wrong tool or skips available tool
Usually means the INVOKES bridge is missing or too specific.
- Use tool **family** names, not individual tools: "INVOKES: maestro and GitHub MCP tools"
- Always list scripts — they're undiscoverable from the tool list
- A/B test evidence: GPT-5.1 went from 4→6 correct tools with family-level INVOKES

### Pattern: Model over-investigates
Add explicit numeric bounds: "Check at most N items", "Stop after finding the first match"
- Domain examples teach reasoning better than procedural steps

### Pattern: Guidance causes model to do the wrong thing
Add an inline anti-pattern near the step where the mistake occurs.
- Format: `> ❌ **NEVER** [the mistake]. [Why it's wrong]. [What to do instead].`
- Then have the *same model* explain why it made the mistake — its self-analysis reveals guidance gaps

### Pattern: Different models interpret guidance differently
The guidance is ambiguous. Rewrite with concrete examples.
- If one model gets it right, study what it focused on — that's the signal to amplify

### Pattern: Guidance references properties not in the output
If guidance says "for GitHub-hosted repos, do X" but the model can't tell which repos are GitHub-hosted from the tool output, it will guess wrong. Reference **observable signals** in the output instead (e.g., a `~` prefix, a field being null, a specific format).
- ✅ "Entries with `~` prefix need manual computation" (model can see the `~`)
- ❌ "GitHub-hosted repos show real distances" (model can't determine hosting from the output)
- Quantify consequences: "overstates by 10x-300x" anchors models better than "meaningless number"

### Pattern: Eval tests recall instead of behavior
Knowledge-quiz evals ("name the tool") can produce false negatives when skills correctly use domain language instead of tool names. Use behavioral evals (give tool list, test mapping) instead.

### Anti-Pattern: Optimizing for false economy
Don't add skill guidance that trades **user experience** for **agent efficiency metrics**. Example: adding `initial_wait: 60` to avoid 3-7 cheap `read_powershell` poll calls makes the user stare at nothing for 60s instead of seeing incremental output at 30s. Tool call count is an agent metric, not a user metric. Poll loops are normal behavior — the agent already knows how to poll from its system prompt.
- ❌ "Use initial_wait: 60 to avoid extra tool calls" (penalizes user to help agent)
- ✅ Let the agent use its default behavior — polling is cheap and shows progress
- **General rule:** If a tip optimizes an eval metric at the cost of UX, remove it. Skills should optimize for the user's experience, not the trainer's scorecard.
