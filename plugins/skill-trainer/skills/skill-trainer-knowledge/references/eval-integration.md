# Arena Eval Integration

How SkillTrainer communicates with Arena for formal skill evaluation.

## When to Use Arena vs Quick Validation

| Scenario | Method |
|----------|--------|
| Quick check during training | Multi-model subagents (task tool) |
| Need regression comparison (before vs after) | Arena issue |
| New skill needs baseline eval | Arena issue |
| Hypothesis needs controlled A/B conditions | Arena issue |
| Tracking skill quality over time | Arena issue + eval spec |

## Opening an Arena Eval Request

Use the `gh` CLI to create an issue on the Arena repo:

```powershell
gh issue create --repo Blazor-Playground/arena \
  --title "Eval request: {skill-name} — {brief description}" \
  --label "eval-request" \
  --body-file /path/to/issue-body.md
```

Use the arena-issue template from the SKILL.md Templates section for the issue body.

### What to include

1. **Skill name and version** — which skill, what commit/state
2. **What changed** — brief description of the training session changes
3. **Prompts that exercise the change** — realistic user prompts Arena can use as eval tasks
4. **Session pointers** — session IDs or training-log excerpts that provide context
5. **Expected improvement** — what metric should get better and by how much
6. **Regression concerns** — what could get worse; what to watch for

### What Arena does with it

Arena's Squad picks up eval-request issues and:
1. Creates or updates `evals/<skill-name>/` task specs
2. Runs evals across multiple models
3. Posts results as issue comments (tool call counts, pass/fail, transcripts)
4. Closes the issue with a summary when complete

## Creating Trigger Tests

Trigger tests verify a skill activates for the right prompts and doesn't activate for wrong ones.

Structure (per EVAL_AUTHORING.md):
```yaml
triggers:
  should_trigger:
    - prompt: "improve the ci-analysis skill"
      confidence: high
    - prompt: "skill X is failing when I ask about build status"
      confidence: medium
  should_not_trigger:
    - prompt: "what's failing in this PR"  # ci-analysis, not trainer
      confidence: high
    - prompt: "create a new skill for code review"  # skill-builder, not trainer
      confidence: high
  edge_cases:
    - prompt: "make the build analysis better"
      expected: should_not_trigger  # ambiguous — could be CI tool, not skill training
      notes: "Trainer should only activate when explicitly asked to train/improve a skill"
```

### Coverage targets
- 8-12 should-trigger prompts (vary phrasing, include keyword synonyms)
- 6-8 should-not-trigger prompts (neighboring skills, similar keywords)
- 3-5 edge cases with explicit rationale

## Creating Task Evals

Task evals verify the skill produces correct results for realistic scenarios.

Structure (per EVAL_AUTHORING.md):
```yaml
task:
  name: "train-skill-builder-frontmatter"
  prompt: "The skill-builder skill has wrong frontmatter guidance. It says NEVER add unsupported fields but several new fields are now officially supported. Can you fix this?"
  timeout: 900
  graders:
    - name: "identifies_wrong_guidance"
      type: "contains"
      check: "references SKILL.md line 71 or skill-patterns.md lines 64-81"
    - name: "makes_correct_fix"
      type: "semantic"
      check: "updates guidance to mention argument-hint, user-invokable, disable-model-invocation"
    - name: "records_in_training_log"
      type: "contains"
      check: "appends entry to training-log.md with rationale"
    - name: "output_reasonable_length"
      type: "assertion"
      check: "len(output) > 200"
```

### Coverage strategy (8-18 tasks for mature evals)
- **Core happy path**: assess a skill, find a real issue, fix it, validate
- **Recording**: verify training-log entries are structured and complete
- **Self-improvement**: trainer identifies a skill-builder gap during training
- **Arena integration**: trainer opens a well-formatted Arena issue
- **Stop signals**: trainer stops at the right time (doesn't over-investigate)
- **Regression handling**: trainer rolls back when validation shows regression

## Reading Eval Results

Arena posts results as issue comments. Key metrics:
- **Tool call count** per task (fewer = better guidance)
- **Pass rate** across models (higher = more robust)
- **Transcript excerpts** for failures (shows where guidance was misapplied)

### Acting on results

| Result | Action |
|--------|--------|
| All pass, tool calls down | ✅ Record improvement in training-log, close issue |
| Mixed pass/fail | Analyze which models fail and why → targeted fix → new eval cycle |
| Regression detected | Rollback change, update training-log with failure, rethink approach |
| New pattern discovered | Add to skill-builder-knowledge.md, consider skill-builder training session |
