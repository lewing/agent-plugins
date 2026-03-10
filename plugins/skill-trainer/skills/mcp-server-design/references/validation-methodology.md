# Validation Methodology

MCP design patterns are hypotheses until tested. This reference describes how to validate claims about tool descriptions, naming, and knowledge tools using the same eval-driven approach we use for skill training.

## What to Measure

### 1. Tool selection accuracy
Does the agent pick the correct tool for a given task?

**How to test:** Give the agent a task description and a tool list. Check which tool it calls first.

Example: "Find the test failures in this Helix job" should route to `helix_status`, not `helix_parse_uploaded_trx`.

### 2. False-positive tool calls
Does the agent try tools it shouldn't? Each unnecessary call wastes tokens and time.

**How to test:** Count tool calls that return no useful results or that the agent abandons.

Example: Before renaming `helix_test_results`, agents called it first on every CI investigation, even though it only works for ~5% of repos.

### 3. Steps to completion
How many tool calls does it take to finish the task? Fewer is better, as long as accuracy is maintained.

**How to test:** Count total tool calls from task start to final answer.

### 4. Cross-model consistency
Do different model families behave the same way? A pattern that only works for one model isn't robust.

**How to test:** Run the same task across 3+ models from different families (e.g., Claude, GPT, Gemini).

## How to Run a Test

### A/B format
1. **Baseline:** Run a task with the current tool descriptions/names
2. **Change one variable:** Modify one description, rename one tool, add a knowledge tool
3. **Retest:** Run the same task with the change
4. **Compare:** Did the target metric improve? Did anything regress?

### Task design
Use realistic tasks, not knowledge quizzes.

| ❌ Knowledge quiz (tests recall) | ✅ Behavioral task (tests routing) |
|----------------------------------|-----------------------------------|
| "What tool would you use to search Helix logs?" | "This build failed. The Helix job ID is abc123. Find out what tests failed." |
| "Name the tool for getting build timelines" | "PR #12345 has a red CI check. Investigate why it failed." |

Knowledge quizzes test whether the agent memorized tool names. Behavioral tasks test whether the agent can discover and use the right tools from descriptions.

### Model selection
Test across at least 3 models from 2+ families:
- Claude family (Sonnet, Haiku, Opus)
- GPT family (GPT-4.1, GPT-5.1, etc.)
- Gemini family

If all models improve → strong signal. If only one family improves → the pattern may be model-specific.

## Test Templates

### Description length test
**Hypothesis:** Shorter descriptions improve tool selection accuracy.

```
Task: [realistic CI investigation task]
Baseline: Tool X has a 60-word description
Change: Tool X has a 20-word description
Measure: Tool selection accuracy, false positives, steps to completion
Models: [3 models, 2+ families]
```

### Name attractiveness test
**Hypothesis:** Generic-sounding names attract false-positive calls.

```
Task: [task where Tool X is NOT the right choice]
Baseline: Tool named "get_test_results"
Change: Tool named "parse_uploaded_trx"
Measure: False-positive calls to this tool
Models: [3 models, 2+ families]
```

### Knowledge tool test
**Hypothesis:** Adding a knowledge tool reduces wrong-first-choice errors.

```
Task: [task requiring domain-specific tool selection]
Baseline: No knowledge tool, domain info in descriptions
Change: Knowledge tool available, descriptions point to it
Measure: Correct first tool choice, total steps
Models: [3 models, 2+ families]
```

### Skip signal test
**Hypothesis:** "Niche" labels reduce calls to low-frequency tools.

```
Task: [task where the niche tool is NOT appropriate]
Baseline: Tool description without skip signal
Change: Description includes "Niche — most repos use X instead"
Measure: False-positive calls to the niche tool
Models: [3 models, 2+ families]
```

## Interpreting Results

### Strong signals
- All models improve on the target metric → adopt the change
- All models regress → reject the change
- Target metric improves with no regression on other metrics → adopt

### Weak signals
- One model improves, others unchanged → cautiously adopt, note model-specificity
- Target improves but a secondary metric regresses → evaluate tradeoff

### Red flags
- More than 20% increase in total steps → likely regression, consider rollback
- One model gets significantly worse → the change may exploit model-specific behavior
- Results vary across runs of the same model → sample size too small

## Connection to Skill Training

This methodology shares principles with skill training evals (see `skill-trainer-knowledge/references/training-methodology.md`):

- **Behavioral over recall:** test what agents do, not what they know
- **Multi-model validation:** patterns must work across families
- **One change per cycle:** don't stack unvalidated changes
- **Record everything:** future trainers need the data

The difference: skill evals test how agents follow guidance. MCP design evals test how agents discover and select tools from descriptions. Same methodology, different unit of measurement.

## Open Questions

These are patterns we believe work but haven't formally validated:

1. **What's the optimal description length?** We used ~20-35 words as a practical target. Is there a measurable sweet spot?
2. **Do "Niche" labels work across all model families?** Tested primarily with Claude and GPT.
3. **How much does the knowledge tool pattern reduce steps?** We have anecdotal evidence but no controlled measurement.
4. **Does consistent family naming (`helix_*`) measurably improve INVOKES routing?** The A/B test on INVOKES exists, but not specifically on family naming consistency.

Each of these could be a structured validation using the templates above.
