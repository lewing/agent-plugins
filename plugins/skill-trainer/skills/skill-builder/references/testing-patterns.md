# Testing Patterns

Multi-model subagent testing methodology for Copilot CLI skills. This approach was independently validated by both our iterative skill development and stephentoub's code-review skill (which includes multi-model review as a first-class process).

## Why Multi-Model Testing

Different models have different blind spots:
- Some excel at code correctness but miss UX issues
- Some catch edge cases others overlook
- Some produce false positives that others correctly ignore
- **Consensus findings** (flagged by 2+ models) are almost always real issues

## The Process

### 1. Select Models

Choose the top-tier model from each available model family. Use at least 2, at most 4. Skip fast/cheap tiers — you want the best reasoning from each family.

Example selection:
```
claude-opus-4.5 or claude-sonnet-4  (Anthropic)
gpt-5.3-codex                        (OpenAI)
gpt-5.2                              (OpenAI, alternative perspective)
```

> ⚠️ `gemini-3-pro-preview` frequently fails with 400 errors on general-purpose task agents. Prefer OpenAI or Anthropic models until Gemini stability improves.

### 2. Construct the Test Prompt

Give each agent the **same prompt** containing:
- The skill's purpose and context
- A realistic task that exercises the skill
- Instructions to report findings with severity

**For script-driven skills** — ask agents to run the skill and evaluate output:
```
Use the skill at {path} to {task}. After running, evaluate:
1. Did the skill produce correct, useful output?
2. Are there edge cases it mishandled?
3. Is the output clear and actionable?
4. Any bugs, errors, or misleading information?
Report findings as: ❌ error / ⚠️ warning / 💡 suggestion
```

**For knowledge-driven skills** — ask agents to apply the skill's rules:
```
Read the skill at {path} and use it to {task}. After applying, evaluate:
1. Were the instructions clear enough to follow?
2. Did any rules conflict or create ambiguity?
3. Were there gaps — situations where the skill gave no guidance?
4. Any rules that seem wrong or overly broad?
Report findings as: ❌ error / ⚠️ warning / 💡 suggestion
```

**For the SKILL.md itself** — ask for structural review:
```
Review the skill at {path} as if you were a developer evaluating whether
to adopt it. Consider: trigger description quality, section organization,
completeness, accuracy, actionability. Would you trust this skill's guidance?
```

### 3. Launch in Parallel

Use the `task` tool with different `model` parameters:

```
task agent_type="general-purpose" model="claude-opus-4.5" prompt="..."
task agent_type="general-purpose" model="gpt-5.2" prompt="..."
task agent_type="general-purpose" model="gemini-3-pro-preview" prompt="..."
```

Launch all in parallel (mode="background") when possible.

### 4. Synthesize Results

After all agents complete:

1. **Deduplicate**: Group findings that describe the same issue
2. **Elevate consensus**: Issues flagged by 2+ models → high confidence, fix first
3. **Include unique catches**: Single-model findings that meet the confidence bar
4. **Discard noise**: Vague suggestions without specific evidence

### 5. Prioritize Actions

| Priority | Criteria |
|----------|----------|
| Fix now | ❌ errors from any model, ⚠️ warnings from 2+ models |
| Fix soon | ⚠️ warnings from 1 model with clear evidence |
| Consider | 💡 suggestions with consensus or strong rationale |
| Skip | 💡 suggestions from 1 model without evidence, style-only feedback |

## A/B Testing: Before/After Comparison

When iterating on a skill, run the **same task** before and after changes to measure improvement. This catches cases where a fix for one problem introduces a regression elsewhere.

### Setup

1. **Pick a reproducible task** — a real investigation with a known correct answer works best
2. **Record the "before" run** — launch a subagent with the current skill, note: elapsed time, tool call count, whether it got the correct answer, and any wrong turns
3. **Apply your skill changes** (edit SKILL.md, references, scripts)
4. **Run the "after" test** — same prompt, same model, same task
5. **Compare results**

### What to Measure

| Metric | How | Good signal |
|--------|-----|-------------|
| **Correctness** | Did the agent reach the right conclusion? | Before: ❌ → After: ✅ |
| **Elapsed time** | Agent completion time (seconds) | >30% faster |
| **Tool calls** | Count of tool invocations | Fewer = more efficient |
| **Wrong turns** | Steps that didn't contribute to the answer | Fewer = better guidance |

### Example (from ci-analysis improvement)

```
Task: "Compare Csc args between passing and failing Helix binlogs"

Round 1 (before fixes):  623s, wrong root cause (Debug/Release noise)
Round 2 (after fixes):   272s, correct root cause (extra analyzerconfig arg)

Changes made: Added "focus on arg count, not value differences" to
binlog-comparison.md delegation prompt template.
```

### Tips

- **Use the same model** for before/after — different models have different capabilities
- **Known-answer tasks** are best — you can objectively score correctness
- **Don't optimize for speed alone** — a slower agent that gets the right answer beats a fast wrong one
- **Save the before prompt** — you'll need the exact same prompt for the after run

## Writer-Critic Convergence Loop

For skill creation or major restructuring, a single review pass often misses structural issues that only surface when someone tries to *apply* the feedback. The writer-critic pattern uses two agents iteratively until the skill converges.

### Process

1. **Writer agent** creates or modifies the skill (SKILL.md, scripts, references)
2. **Critic agent** reviews the result — produces a structured feedback document with ❌/⚠️/💡 findings
3. **Writer agent** reads the feedback and applies fixes
4. **Critic agent** reviews again — only flags *new or remaining* issues
5. **Repeat** until the critic has no meaningful findings (usually 2-3 rounds)

### Setup

Use two `task` calls in sequence (not parallel — each depends on the previous output):

```
# Round 1: Writer creates the skill
task agent_type="general-purpose" prompt="Create a skill at {path} that {does X}..."

# Round 1: Critic reviews
task agent_type="general-purpose" model="{different-model}" prompt="Review the skill at {path}. Report ❌/⚠️/💡 findings. Save feedback to {path}/feedback.md"

# Round 2: Writer applies feedback
task agent_type="general-purpose" prompt="Read {path}/feedback.md and apply the feedback to the skill at {path}. Delete feedback.md when done."

# Round 2: Critic reviews again
task agent_type="general-purpose" model="{different-model}" prompt="Review the skill at {path}. Only flag NEW or REMAINING issues..."
```

### Key design choices

- **Use different models** for writer and critic — same-model pairs are too agreeable
- **The human stays in the loop** between rounds to steer direction and override bad suggestions
- **Save feedback as a file** (e.g., `feedback.md` in the skill directory) so the writer agent has full context without you relaying it
- **Delete feedback files** after they're applied — they're transient, not part of the skill
- **Stop when the critic produces only 💡 suggestions** — that's convergence. Don't chase zero findings.

### When to use this vs. multi-model review

| Scenario | Approach |
|----------|----------|
| Testing an existing skill against a real task | Multi-model review (parallel, single-shot) |
| Creating a new skill from scratch | Writer-critic loop (2-3 rounds) |
| Major restructuring of a skill | Writer-critic loop |
| Small fixes or incremental improvements | Multi-model review |
| Validating after writer-critic converges | Multi-model review as final check |

The two approaches complement each other: writer-critic for creation/iteration, multi-model for validation.

## Waza Eval Testing

For repeatable, quantitative skill testing, use the **waza-eval** skill (`/plugin install github.com/lewing/dotnet-skills dotnet-skills-experimental`). It provides:

- **Structured eval suites** — define tasks with prompts, expected outputs, and graders
- **Progression testing** — compare tool efficiency across skill versions from git history
- **Session capture** — commit result transcripts as golden sessions for regression detection
- **CI integration** — gate PRs on eval pass rates

Use waza evals when you need to *measure* whether a skill change improved behavior. Use multi-model review (above) when you need *qualitative* structural feedback.

See the [waza-eval SKILL.md](https://github.com/lewing/dotnet-skills/tree/master/plugins/dotnet-skills-experimental/skills/waza-eval/SKILL.md) for full usage.

### Regression Heuristics

When comparing before/after eval results:

| Metric | Threshold | Action |
|--------|-----------|--------|
| Tool call increase > 20% on any task | 🔴 Regression | Roll back the change |
| Tool call decrease > 10% | 🟢 Improvement | Record as evidence |
| Elapsed time increase > 30% | 🔴 Regression | Investigate bottleneck |
| Correct before, wrong after | 🔴 Regression | Roll back — correctness trumps efficiency |
| Model misapplies new guidance | 🔴 Regression | Needs anti-pattern or rewording |
| One model improves, others unchanged | 🟡 Partial | Likely acceptable |

### Trigger Test Structure

Evals should include trigger tests (does the skill activate correctly?):
- **Should trigger** (8-12 prompts): varied phrasings of the skill's use cases, with high/medium confidence ratings
- **Should not trigger** (6-8 prompts): neighboring skills, similar keywords that belong elsewhere
- **Edge cases** (3-5 prompts): ambiguous prompts with explicit expected behavior and rationale

### Pre-submission Checklist

Before shipping a skill change:
- [ ] Description matches trigger tests (USE FOR phrases appear in should-trigger prompts)
- [ ] Stop signals are explicit with numeric bounds
- [ ] Domain examples present (not just tool schemas)
- [ ] Token budget met (SKILL.md under 4K orchestrating / 15K knowledge)
- [ ] Multi-model validation ≥ 4/5 across 2+ families

## Common False Positives

From real experience — automated reviewers frequently flag these incorrectly:

### PowerShell compatibility
- **Claim**: `-UseBasicParsing` is "not supported in pwsh"
- **Reality**: It's a no-op in pwsh (accepted, silently ignored). Required in Windows PowerShell 5.1.
- **Response**: "Keeping it — no-op in pwsh, required in WinPS 5.1 to avoid IE COM dependency."

### API field names
- **Claim**: `gh pr checks` should use `--json conclusion` instead of `--json state`
- **Reality**: `conclusion` is not a valid field. `state` contains `SUCCESS`/`FAILURE` directly.
- **Response**: Verify with `gh pr checks --json` error output: "Unknown JSON field: 'conclusion'"

### Training data staleness
- **Claim**: "This API/method doesn't exist" or "is deprecated"
- **Reality**: Models have knowledge cutoffs. The API may be current.
- **Response**: "Verified — this API exists and works. Model training data may be stale."

### MCP tool name prefixes
- **Claim**: Skill docs should use fully-qualified MCP tool names like `hlx-hlx_status` or `github-mcp-server-list_workflow_runs` instead of short names like `hlx_status` or `list_workflow_runs`
- **Reality**: The official Agent Skills examples (GitHub docs, agentskills.io) all use short/descriptive tool names. The server prefix (e.g., `hlx-`, `github-mcp-server-`) is an implementation detail of MCP server registration — agents resolve the mapping. Use the short form in skill docs for readability.
- **Response**: "Short names are consistent with the Agent Skills convention. The agent resolves MCP server prefixes automatically."

### Over-disposal
- **Claim**: Every HTTP response/client needs try/finally/dispose
- **Reality**: Sometimes correct! But reviewers often suggest disposal patterns that add complexity without value (e.g., disposing a client that's about to go out of scope at function return).
- **Response**: Apply disposal for long-running functions or loops. Skip for simple one-shot calls at function end.

## Review Thread Workflow

When addressing PR review comments programmatically:

### Reply to a thread
```powershell
$body = "Your evidence-based reply" | ConvertTo-Json
$query = @"
mutation {
  addPullRequestReviewThreadReply(input: {
    pullRequestReviewThreadId: "$threadId",
    body: $body
  }) { clientMutationId }
}
"@
gh api graphql -f query="$query"
```

### Resolve a thread
```powershell
$query = @"
mutation {
  resolveReviewThread(input: {
    threadId: "$threadId"
  }) { clientMutationId }
}
"@
gh api graphql -f query="$query"
```

### Best practices
- **Read all threads first** before responding — some may be duplicates
- **Reply before resolving** — so the conversation is preserved
- **Batch replies** for the same issue across multiple threads
- **Include evidence** — "verified by running X" or "tested against real API"
- **Be concise** — one paragraph per reply is usually enough
