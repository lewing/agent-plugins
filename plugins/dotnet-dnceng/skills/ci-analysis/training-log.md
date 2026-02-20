# ci-analysis Training Log

## Initial Assessment — 2026-02-20

**Trainer:** Skill Trainer (session bbb1d2b1)
**Trigger:** General dotnet-dnceng plugin performance review

### Skill Profile

| Metric | Value |
|--------|-------|
| SKILL.md | 78 lines (lean, well-structured) |
| References | 12 files, 932 lines total |
| Script | Get-CIStatus.ps1, 1932 lines |
| Total content | ~2942 lines |
| Commit history | 20+ training iterations (mature skill) |

### Structural Assessment

**Strengths:**
- SKILL.md is concise — delegates detail to references (good pattern)
- Clear workflow: Step 0 (context) → script → analyze → recommend
- Anti-patterns are specific with evidence requirements ("don't call infrastructure without Build Analysis match")
- Tips section addresses known model failure modes (initial_wait, output re-querying, JSON field usage)
- PR type classification table helps models adjust interpretation
- Reference docs are well-scoped — each covers one investigation pattern

**Potential Issues (ranked by severity):**

1. ❌ **Token budget risk** — If a model loads SKILL.md + all 12 references + the script, that's ~2942 lines before any work begins. Models may hit context limits on complex investigations. Need to verify models load references on-demand, not upfront.

2. ⚠️ **Service access fallback chain** — Line 14 says "Start with MCP tools, then fall back to CLI" but doesn't specify WHICH MCP tools. Models may waste turns trying wrong tool names. The azdo-helix-reference.md has org-matching guidance but it's buried in a reference doc.

3. ⚠️ **Script path resolution** — Script examples use `./scripts/Get-CIStatus.ps1` (relative). The skill doesn't specify the working directory. Models running from the repo root vs the skill directory will get different results.

4. 💡 **Description length** — Frontmatter description is 47 words. Long descriptions can cause false-positive skill activation. But the trigger phrases are accurate and specific.

5. 💡 **Delegation patterns complexity** — delegation-patterns.md has 5 patterns (83 lines). Models may over-delegate simple investigations or under-delegate complex ones.

### What Needs Evaluation

- [ ] Do models correctly set initial_wait ≥ 60 when running Get-CIStatus.ps1?
- [ ] Do models cross-reference failures with known issues (not present as separate lists)?
- [ ] Do models load references on-demand or upfront?
- [ ] Do models correctly identify the AzDO organization from build URLs?
- [ ] Do models follow Step 0 (gather PR context) before running the script?

---

## Session Analysis — b563ef92 (2026-02-20)

**Task:** "what is failing on PR #123245" then "what is failing on PR #123614"
**Total:** 42 tool calls, 37 assistant turns, 4 user messages (1 corrective intervention)

### PR #123245 (expired builds) — 17 turns

| Turn | Action | Issue |
|------|--------|-------|
| 3 | Script ran, `initial_wait: 90` ✅ | — |
| 4-5 | Read output, stopped script | Script reported no active builds |
| 6 | Tried `ado-dnceng` internal org | ❌ Wrong org for PR builds |
| 7-8 | `gh pr checks` via powershell (2 calls) | Redundant — script already tried this |
| 9-10 | `ado-dnceng-public` with `project:"public"` then GUID | ⚠️ Org/project trial-and-error |
| 11-12 | Got PR comments, got PR status | Gathering more context (ok) |
| 13 | `get_build_log` | Still chasing expired builds |
| 14 | `Invoke-RestMethod` for timeline | ❌ REST API when MCP available |
| 15 | Listed projects | Still orienting |
| 16 | `get_builds` with IDs | Finally confirmed expired |
| 17 | Delivered answer | 14 turns to say "builds expired" |

**Verdict:** 14 turns to discover builds were expired. Script likely indicated this. Model should have reported "builds are no longer available" in 1-2 turns.

### PR #123614 (active builds) — 19 turns

| Turn | Action | Issue |
|------|--------|-------|
| 18 | Script + PR read in parallel ✅, `initial_wait: 120` ✅ | Good |
| 19 | Script had `[CI_ANALYSIS_SUMMARY]` JSON → ignored it, went to `get_build_log` MCP | ❌ Tip #7 violated |
| 20-24 | 5 turns of `Invoke-RestMethod` for AzDO timeline | ❌ REST API when MCP available, 2 read_powershell failures |
| 25 | Finally tried MCP `get_build_log_by_id` | After user corrected |
| 26-31 | More MCP log exploration (6 turns) | Searching through build logs for error |
| 32-35 | Helix MCP tools (logs, test results, search) | Good — proper tool usage |
| 36 | Delivered answer | — |

**User intervention at turn 26:** "why are you using web API" — model was using `Invoke-RestMethod` instead of MCP tools.

### Issues Ranked (from session evidence)

1. ❌ **Not using script output** — Script produced `[CI_ANALYSIS_SUMMARY]` JSON with all failure data, but model ignored it and re-queried AzDO directly. Tip #7 ("search the output file, don't re-query") was violated. This wasted ~8 turns.

2. ❌ **REST API fallback with MCP tools available** — Model used `Invoke-RestMethod` for AzDO timeline queries when `ado-dnceng-public-pipelines_get_build_log_by_id` was available. User had to intervene. The skill says "Start with MCP tools" but doesn't strongly enough prevent REST fallback.

3. ⚠️ **Slow expired-build detection** — 14 turns to conclude "builds expired." The skill doesn't have guidance for "what to do when builds are expired/unavailable." Model tried every possible workaround instead of reporting the limitation.

4. ⚠️ **AzDO org/project confusion** — Tried `dnceng` internal, then `dnceng-public/public`, then `dnceng-public/{GUID}`. The azdo-helix-reference.md explains this but the model still fumbled.

### Optimal Paths

**PR 123245 (expired):** Script → read output → "builds expired, here's what I can see from GitHub" = 3-4 turns
**PR 123614 (active):** Script → parse JSON → cross-reference failures → answer = 5-8 turns
**Optimal total:** ~10-12 turns, ~15-18 tool calls
**Actual:** 37 turns, 42 tool calls (3.5x overhead)

### Next Steps

- [x] Fix #1: Strengthen "use script output" guidance — make it a hard rule, not a tip
- [x] Fix #2: Add anti-pattern for REST API when MCP tools available
- [x] Fix #3: Add guidance for expired/unavailable builds
- [ ] Validate: Run multi-model eval on PR analysis task

---

## Change 1 — 2026-02-20 (session bbb1d2b1)

**Target:** Issues #1 (ignored script output), #2 (REST over MCP), #3 (slow expired-build detection)

**Hypothesis:** Promoting "use script output" from Tip to a hard-rule workflow section, adding MCP-before-REST anti-pattern, and adding expired-builds fast-fail will reduce tool calls by ~40% on standard PR analysis. Helix deep-dive behavior preserved by noting Helix data persists independently.

**Changes:**
- Renamed "After the Script: Analyze and Recommend" → "After the Script: Use Its Output" with 🚨 directive
- Added expired-builds guidance: report immediately, don't re-query AzDO, but note Helix may still have data
- Added anti-pattern: ❌ Don't use `Invoke-RestMethod`/`curl` when MCP tools available
- Updated Tip 5: Helix data persists even when AzDO builds expire
- Removed Tips 7-8 (content promoted to main workflow section)

**Key design decision:** Expired AzDO builds = fast-fail. Helix canceled/timed-out = dig deeper. These are different failure modes: AzDO retention is ~30 days and data is gone; Helix may still have results.

**Before eval:** Session b563ef92 — 42 tool calls, 37 turns, 3.5x overhead

**After eval (PR #123614, build in progress):**

| Model | Tool Calls | Used Script Output? | REST Fallback? | Duration | Verdict |
|-------|-----------|---------------------|----------------|----------|---------|
| claude-sonnet-4 | 4 | ✅ Yes | ❌ No | 65s | Correct: CI not failing |
| claude-haiku-4.5 | 2 | ✅ Yes | ❌ No | 34s | Correct: CI not failing |
| gpt-5.1 | ?* | Unknown | Unknown | >434s, timed out | Still running after 7+ min |

*GPT-5.1 completed at 759s — followed guidance correctly but slow due to model latency and reading 4 reference docs.

**Assessment:**
- **All 3 models followed the updated guidance:** parsed script JSON, no REST API re-querying ✅
- Sonnet used 4 calls (skill read + PR context + script + gh checks) — near optimal
- Haiku used only 2 calls (PR context + script) — impressively lean
- GPT-5.1 used 8 calls (read 4 reference docs — excessive but not harmful, no wasted API calls)
- **Compared to baseline (42 calls, 37 turns):** 81-95% reduction in tool calls across all 3 models
- GPT-5.1's 759s is model latency + reference reads, not a guidance failure

**Pattern learned:** "🚨 Do NOT re-query" as a workflow section rule (not a tip) is effective across all 3 model families. The anti-pattern for REST API also held — no model used `Invoke-RestMethod` when MCP tools were available.

**After eval (PR #124125, real failures — wasm build error + test failures):**

| Model | Tool Calls | Used Script Output? | REST Fallback? | Duration | Verdict |
|-------|-----------|---------------------|----------------|----------|---------|
| claude-sonnet-4 | 3 | ✅ Yes | ❌ No | 317s | Retry (matched known issue #109653) |
| claude-haiku-4.5 | 3 | ✅ Yes | ❌ No | 75s | PR-related, do not merge |
| gpt-5.1 | 8 | ✅ Yes | ❌ No | 374s | PR-related, investigate and fix |

**Assessment (round 2):**
- **All 3 models again followed updated guidance** — parsed `[CI_ANALYSIS_SUMMARY]` JSON, no REST API re-querying ✅
- **Tool call efficiency maintained under real failures:** 3-8 calls vs baseline 42 (81-93% reduction)
- **Cross-referencing performed by all 3:** Each model attempted to match failures to known issues and correlate with changed files
- **Interesting disagreement on classification:** Sonnet classified as infrastructure (matched known issue #109653 re: wasm-tools workload), while Haiku and GPT-5.1 correctly identified the build error is in the PR's changed file (`Microsoft.NET.Sdk.WebAssembly.Browser.targets`) and classified as PR-related
- Haiku's analysis was particularly strong: concise (75s), correct classification, clear per-failure table
- GPT-5.1's 8 calls include overhead (intent log, skill load, 3 GH metadata reads) but stayed disciplined — no API re-querying
- Sonnet's 2 Helix queries were legitimate deep-dives (not wasted), but its classification was arguably too generous to the PR

**Classification accuracy note:** This PR has genuine ambiguity — the build error IS in a file the PR changed, but there's also a known issue (#109653) about wasm workload errors. The "correct" answer likely depends on human judgment. However, 2 of 3 models correctly prioritized the changed-file correlation, which is the safer recommendation.

**Change 1 final verdict: ✅ PASS**
- Primary goals achieved: script output usage (3/3), no REST fallback (3/3), efficient tool calls (3/3)
- Helix deep-dive preserved: Sonnet did 2 additional Helix queries (appropriate for failure investigation)
- Only gap: cross-referencing accuracy varies (Sonnet over-indexed on known issue match)
