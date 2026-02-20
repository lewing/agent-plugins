# flow-analysis Training Log

## Initial Assessment — 2026-02-20

**Skill**: flow-analysis (dotnet-dnceng plugin)
**Trigger**: Comparative eval showed flow-analysis giving completely wrong answers vs vmr-codeflow-status on the same PR.

### Architecture
- SKILL.md (~277 lines) with MCP-first workflow
- 2 reference docs (vmr-codeflow-reference.md, vmr-build-topology.md) — shared with vmr-codeflow-status
- 1 script: Get-FlowHealth.ps1 (repo-wide batch scanning only)
- **Missing**: No script for single-PR analysis — relied entirely on MCP tool orchestration

### Problem Discovery

Ran 4-agent comparative eval on PR dotnet/runtime#124532 (a real stale backflow PR):

| Skill | Model | Correct commit distance? | Forward flow PRs? | Staleness warning? | Recommendation? |
|-------|-------|--------------------------|-------------------|-------------------|----------------|
| flow-analysis | sonnet | ❌ (566 builds) | ⚠️ (2 of 9) | ✅ | ⚠️ partial |
| flow-analysis | haiku | ❌ (hallucinated) | ❌ (none) | ❌ | ❌ |
| vmr-codeflow-status | sonnet | ✅ (32 commits) | ✅ (all 9) | ✅ | ✅ |
| vmr-codeflow-status | haiku | ✅ (32 commits) | ✅ (all 9) | ✅ | ✅ |

### Root Cause Analysis

flow-analysis was "lobotomized" when translated from vmr-codeflow-status to use MCP tools. The critical gap: **no script for single-PR analysis**. Individual MCP calls cannot replicate what Get-CodeflowStatus.ps1 does:

1. **`maestro_subscription_health` reports BAR build count (566), not VMR commit distance (32)** — models confuse these metrics, reporting "566 builds behind" instead of "32 VMR commits behind"
2. **No single MCP call discovers all pending forward flow PRs** — the script searches GitHub systematically; models with MCP make ad-hoc queries and miss most PRs
3. **Maestro staleness warnings are in PR comments** — models don't know to look there without guided extraction
4. **VMR HEAD comparison requires reading the VMR repo's current commit** — not available via Maestro MCP tools

### Issues (Ranked)

1. ❌ **No PR analysis script** — flow-analysis relies on MCP orchestration for single-PR analysis, which gives wrong answers
2. ❌ **"Builds behind" vs "commits behind" confusion** — MCP tools expose build counts, not VMR commit distances
3. ⚠️ **Incomplete forward flow discovery** — ad-hoc MCP/GitHub queries miss most forward flow PRs
4. ⚠️ **Missing staleness warning extraction** — models don't parse PR comments for Maestro warnings without guidance

---

## Change 1: Add script-first PR analysis — 2026-02-20

### Hypothesis
Adding Get-CodeflowStatus.ps1 to flow-analysis and restructuring the PR Analysis Workflow to be script-first (like ci-analysis's "use its output" pattern) will fix all 4 issues because the script already handles VMR commit comparison, forward flow discovery, and staleness detection.

### Change
1. **Copied Get-CodeflowStatus.ps1** from vmr-codeflow-status into flow-analysis/scripts/
2. **Rewrote PR Analysis Workflow** (SKILL.md lines 125-173):
   - Added 🚨 "Script-first" directive at top
   - Step 1: Run the script with examples (-PrUrl, -Repository/-Branch, -CheckMissing)
   - Step 2: "After the Script — Use Its Output" with explicit field mapping (vmrComparison.aheadBy = commits NOT builds)
   - MCP tools listed as enrichment-only (build freshness, subscription history, triggering)
3. **Updated intro paragraph** to describe script+MCP hybrid approach
4. **Updated Quick Start** and Analysis Modes table to reference script

### Eval Results (PR dotnet/runtime#124532)

| Metric | Before (sonnet) | Before (haiku) | **After (sonnet)** | **After (haiku)** |
|--------|-----------------|----------------|--------------------|--------------------|
| Correct commit distance | ❌ (566 builds) | ❌ (hallucinated) | ✅ (32 commits) | ✅ (32 commits) |
| Forward flow PRs | ⚠️ (2 of 9) | ❌ (none) | ✅ (all 9) | ✅ (all 9) |
| Staleness warning | ✅ | ❌ | ✅ | ✅ |
| Correct recommendation | ⚠️ partial | ❌ | ✅ | ✅ |
| Tool calls | ~15+ | ~12+ | **7** | **4** |

### Verdict: ✅ KEEP

All 4 identified issues resolved. Both model families produce correct, comprehensive results. Tool call reduction of 50-70%. No regressions — Codeflow Overview workflow (MCP-based) was not modified and remains available for repo-wide queries.

### Pattern Learned
Same pattern as ci-analysis Change 1: **script-first with "use its output" hard rule** works across skill types. When a script produces structured JSON with the key facts, models reliably parse it and avoid re-querying. The 🚨 emoji + "Do NOT re-query" phrasing is effective for both sonnet and haiku.

### Commit
`43bcf1c` — flow-analysis: add script-first PR analysis workflow

---

## Change 2: Fix routing for repo+branch queries — 2026-02-20

### Hypothesis
Arena eval `flow-status-001` ("What is the current state of codeflow for dotnet/runtime on main?") exposed a routing gap: Quick Start directed single-repo+branch queries to the MCP-based Codeflow Overview, which can't compute VMR commit distance. Updating the Quick Start routing to send these queries to `Get-CodeflowStatus.ps1 -Repository -Branch -CheckMissing` will fix accuracy without breaking multi-repo scans.

### Change
1. **Updated Quick Start** (lines ~38-44): Added explicit routing — single-repo+branch → script, multi-repo → Codeflow Overview
2. **Updated Analysis Modes table** (lines ~76-81): Added 4th row for "Is backflow healthy for X on Y?" → PR Analysis script
3. **No changes to Codeflow Overview workflow** — reserved for multi-repo scanning where MCP parallelism is appropriate

### Arena Eval Results

**Task: flow-status-001** ("What is the current state of codeflow for dotnet/runtime on main?")

| Metric | Before C2 (sonnet) | Before C2 (haiku) | **After C2 (sonnet)** | **After C2 (haiku)** |
|--------|--------------------|--------------------|----------------------|----------------------|
| Correct commit distance | ❌ (566 builds) | ❌ (566 builds) | ✅ (32 commits) | ✅ (32 commits) |
| Forward flow PRs | ✅ | ✅ | ✅ | ✅ |
| Staleness warnings | ✅ | ✅ | ✅ | ✅ |
| Tool calls | 13 | 7 | **7** | **6** |
| Workflow used | MCP Codeflow Overview | MCP Codeflow Overview | Script (-Repository -Branch) | Script (PR Analysis) |

**Task: pr-analysis-001** ("Analyze dotnet/sdk PR #53001") — regression check

| Metric | Before C2 (sonnet) | Before C2 (haiku) | **After C2 (sonnet)** | **After C2 (haiku)** |
|--------|--------------------|--------------------|----------------------|----------------------|
| Correct | ✅ | ✅ | ✅ | ✅ |
| Tool calls | 4 | 4 | **13** | **5** |

Sonnet PR analysis regressed from 4→13 calls (added MCP enrichment beyond script output), but accuracy remained perfect. Haiku had no regression. Acceptable — accuracy is the priority.

### Verdict: ✅ KEEP

Flow-status routing fixed: both models now use the script for single-repo queries and get correct commit distance. Multi-repo Codeflow Overview workflow preserved. PR analysis still works correctly.

### Pattern Learned
**Quick Start routing is critical for skills with multiple workflows.** When a skill has both script-based and MCP-based paths, models follow Quick Start guidance literally. If it doesn't explicitly route query types to workflows, models default to whichever workflow appears first or seems more general — which may be the wrong one.

### Related: Filed [lewing/maestro.mcp#4](https://github.com/lewing/maestro.mcp/issues/4)
Enhancement request: add VMR commit distance to `maestro_subscription_health` response. This would make MCP-only workflows accurate for commit distance, eliminating the need for script workarounds on multi-repo scans.

---

## Change 3: Hybrid multi-repo workflow (MCP scan + script for stale repos) — 2026-02-20

### Hypothesis
Arena eval `branch-mapping-001` showed both models report "builds behind" (BAR build count) instead of VMR commit distance in the Codeflow Overview workflow. Adding a mandatory step to run `Get-CodeflowStatus.ps1 -CheckMissing` for stale repos after the MCP scan will fix the distance reporting.

### Change (two iterations)

**v3 (failed):** Added Step 5 between existing steps 4 and 5, with 🚨 warning about builds≠commits. Result: haiku skipped it entirely, sonnet ran scripts but still reported "builds behind" in summary. The directive was buried too deep.

**v3b (succeeded):** Moved the 🚨 rule to the **top of the Codeflow Overview Workflow** (before Step 1), rewording as a hard "Do NOT report" directive. Simplified Step 5 to just the script command. Same pattern that worked for ci-analysis: **critical rules must appear before the first action step, not buried in middle steps.**

### Eval Results (branch-mapping-001)

| Metric | Before (sonnet) | v3 (sonnet) | **v3b (sonnet)** | Before (haiku) | v3 (haiku) | **v3b (haiku)** |
|--------|----------------|-------------|------------------|----------------|------------|-----------------|
| Reports commits | ❌ builds | ❌ builds | ✅ commits | ❌ builds | ❌ builds | ✅ commits |
| Runs script for stale | No | Yes (3) | Yes (6) | No | No | Yes (5) |
| Tool calls | 12 | 13 | 15 | 11 | 13 | 13 |
| Time | 67s | 481s | 140s | 46s | 52s | 163s |

### Verdict: ✅ KEEP (v3b)

Both models now report VMR commits instead of builds behind. Tool call increase (sonnet +25%, haiku +18%) is justified by accuracy fix. Time increase is moderate (both under 3 minutes) and expected since scripts run for stale repos.

### Pattern Learned
**🚨 rules must appear before the first action, not as middle steps.** When a workflow has numbered steps, models (especially haiku) optimize by skipping steps that look optional. Placing the hard rule at the workflow introduction — before Step 1 — ensures both model families read it before starting work. This is now a confirmed pattern across 3 skills (ci-analysis, flow-analysis PR workflow, flow-analysis Codeflow Overview).

### Anti-pattern confirmed
❌ **Burying critical constraints as numbered steps.** v3 placed the "don't report builds behind" rule at Step 5 of 6. Haiku skipped it (52s — same as no change). Sonnet read it but didn't internalize it into output formatting. Moving to workflow preamble fixed both.
