# Skill Development Lifecycle

End-to-end workflow for building a Copilot CLI skill, from inception through deployment and iteration.

## Phase 1: Inception

### Evaluate first — identify the gaps
Before writing any skill content, test the agent on your use case **without** the skill:

1. Give the agent a realistic task in the skill's target domain
2. Note where it fails, gets confused, or produces wrong output
3. Those failures are your skill's requirements — write guidance to fix only those gaps

**Why**: The agent already knows most things. A skill that teaches what the agent already knows wastes context tokens. A skill that fills the exact gaps the agent has is maximally efficient.

### Identify the opportunity
A skill is worth building when you find yourself repeatedly:
- Performing the same multi-step investigation manually
- Explaining the same domain knowledge to agents that don't have context
- Following a checklist that could be encoded as instructions

**Value test**: If the manual process takes >5 minutes and happens weekly, a skill pays for itself quickly.

### Choose the archetype

| Your situation | Archetype |
|---------------|-----------|
| "I keep running the same API calls and parsing the output" | Script-driven |
| "I keep explaining the same review rules / conventions / patterns" | Knowledge-driven |
| "I need both automation AND behavioral guidance" | Start knowledge-driven, add scripts for what agents can't do |

## Phase 2: Research

### Study existing skills
Before building, examine existing skills in the target repo and in `~/.copilot/skills/`:
- Read their SKILL.md for structural patterns
- Note what works well in their trigger descriptions
- Look at how they organize references/

### For knowledge-driven skills: Extract patterns
Mine real sources for domain knowledge:
- **PR review comments**: `gh api "/repos/{owner}/{repo}/pulls/comments?per_page=100&sort=created&direction=desc"` — look for repeated feedback themes
- **Documentation**: Existing docs often contain rules that aren't enforced
- **Tribal knowledge**: Ask domain experts what they wish agents knew
- **Past conversations**: Your own agent sessions contain patterns (check session checkpoints)

Key principle from stephentoub's code-review skill: every rule should include a real quote or citation showing it was actually enforced, not just theoretically desired.

### For script-driven skills: Prototype the manual process
Run through the investigation manually once. Note:
- Which APIs/tools you call and in what order
- What decisions require human judgment vs. can be automated
- Where errors occur and how you handle them
- What output format is most useful

## Phase 3: Scaffold

Use the agent's `create` tool to build the directory structure:

```
{skill-name}/
├── SKILL.md
├── scripts/          # if script-driven
│   └── Get-{Action}.ps1
├── references/
│   └── {topic}.md
└── assets/           # if skill produces files (templates, schemas, etc.)
```

Start with SKILL.md — it's the entry point. Use the template skeletons from [skill-patterns.md](skill-patterns.md).

For script-driven skills, write the script skeleton with parameter block, Write-Section helper, and error handling patterns before adding logic.

## Phase 4: Build Iteratively

### Cycle: content → test → refine

1. **Write core content** (SKILL.md + one reference doc, or script core logic)
2. **Test locally**: Deploy to `~/.copilot/skills/{name}/` and invoke the skill in a real conversation
3. **Get subagent feedback**: Run multi-model testing (see [testing-patterns.md](testing-patterns.md))
4. **Refine**: Address feedback, prioritizing bugs > correctness > UX > style
5. **Repeat** with additional reference docs or script features

### Prioritization
- **Round 1**: Core functionality works end-to-end
- **Round 2**: Error handling and edge cases
- **Round 3**: Output quality and UX
- **Round 4+**: Polish based on real-world usage

## Phase 5: Deploy Locally

Copy the skill to `~/.copilot/skills/{name}/`:
```powershell
Copy-Item -Recurse ./skill-name/ ~/.copilot/skills/skill-name/
```

Test by starting a new conversation and asking a question that should trigger the skill. Verify:
- Skill is invoked (you'll see it in the skill invocation output)
- SKILL.md instructions are followed
- Scripts execute correctly (if applicable)
- Output is useful and correctly formatted

## Phase 6: Deploy to Repository

### Create branch and PR
```powershell
cd /path/to/repo
git checkout -b add-{skill-name}-skill
mkdir -p .github/skills/{skill-name}
Copy-Item -Recurse ~/.copilot/skills/{skill-name}/* .github/skills/{skill-name}/
git add .github/skills/
git commit -m "Add {skill-name} Copilot skill"
git push origin HEAD
```

> ⚠️ **Always use `--body-file` for PR descriptions.** PowerShell mangles backticks, dollar signs, and special characters when passed as inline `--body` strings. Write the description to a temp file instead:
```powershell
$description | Out-File -FilePath "$env:TEMP/pr-desc.md" -Encoding utf8NoBOM
gh pr create --title "Add {skill-name} skill" --body-file "$env:TEMP/pr-desc.md"
Remove-Item "$env:TEMP/pr-desc.md"
```
The same applies to `gh pr edit --body-file` and `gh api graphql` — use file-based input to avoid escaping issues.

### Keep local and repo copies in sync
After pushing changes, always copy back:
```powershell
Copy-Item .github/skills/{name}/SKILL.md ~/.copilot/skills/{name}/SKILL.md
# ... etc for all changed files
```

### Wire into copilot-instructions.md (optional)
If the skill should be used automatically (e.g., code review before every commit):
```markdown
Before completing, use the `{skill-name}` skill to {action}.
```

## Phase 7: Review Loop

### Automated reviewer patterns
The PR will likely get automated review comments. Common workflow:

1. **Read all comments** before responding
2. **Verify each claim** — automated reviewers have high false-positive rates on:
   - PowerShell compatibility claims (e.g., `-UseBasicParsing` "unsupported in pwsh" — it's a no-op, not unsupported)
   - API field names (e.g., `gh pr checks --json conclusion` — field doesn't exist)
   - "Missing" error handling that's actually handled upstream
3. **Reply with evidence** when pushing back:
   ```
   gh api graphql -f query='mutation { addPullRequestReviewThreadReply(input: {
     pullRequestReviewThreadId: "{thread-id}", body: "{evidence-based reply}"
   }) { clientMutationId } }'
   ```
4. **Resolve threads** after addressing or rebutting:
   ```
   gh api graphql -f query='mutation { resolveReviewThread(input: {
     threadId: "{thread-id}"
   }) { clientMutationId } }'
   ```
5. **Commit, push, update description, repeat** until all threads resolved

> ⚠️ **Always review the PR description after pushing new commits.** The description drifts as you address review feedback, add features, or fix bugs. After each push, check if the description still accurately reflects the PR's current content. Update it via `--body-file` if anything changed.

### Human reviewer patterns
- Address all feedback, even if you disagree — explain your reasoning
- Large refactoring suggestions: agree on direction, implement in follow-up PR
- If reviewer asks for something already done: point to the commit

## Phase 8: Iterate

### Subagent testing after each round
After addressing review feedback, run multi-model testing again. Different models catch different issues:
- Structural/logical bugs
- UX/output clarity
- Missing edge cases
- Inconsistencies between docs and implementation

### Subagent retrospective: ask why, not just what

When a subagent makes a wrong classification or misapplies guidance, don't just note the error — **launch a follow-up agent asking the same model why it made that choice**. This often reveals gaps in your skill's guidance that you wouldn't discover otherwise.

**Pattern:**
1. Run multi-model panel test
2. Identify agents that misclassified or contradicted the skill's guidance
3. Launch a new agent (same model) with a prompt like: "You previously analyzed X and classified it as Y. But the failure is actually Z because [evidence]. Why did you classify it that way? What would have made you classify it differently?"
4. The model's self-analysis reveals the **exact reasoning seam** where your guidance was ambiguous
5. Write a targeted anti-pattern that closes that specific gap

**Real example:** Sonnet 4 correctly quoted the skill's "be cautious labeling failures as infrastructure" warning, but then classified a deterministic test defect (test expects .NET 2.2, not installed) as "LIKELY INFRASTRUCTURE" because the word "environment" appeared in the error. When asked why, it explained it had conflated "environment-related" with "infrastructure failure." This led to a new anti-pattern distinguishing transient infrastructure issues from deterministic environment-assumption failures — a distinction the skill hadn't made explicit.

**Why this works:** Models are good at post-hoc introspection about their own reasoning. The follow-up prompt doesn't need to be adversarial — a genuine "help me understand" framing gets the most useful response.

### When to stop
A skill is "done enough" when:
- Core use cases work reliably
- Error handling covers API failures gracefully
- At least 2 different models produce useful output when testing
- Review threads are all resolved
- Real-world usage confirms the skill is triggered correctly
