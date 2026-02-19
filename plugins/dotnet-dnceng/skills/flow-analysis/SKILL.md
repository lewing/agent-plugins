---
name: flow-analysis
description: >
  Analyze VMR codeflow health using maestro MCP tools and GitHub MCP tools.
  USE FOR: investigating stale codeflow PRs, checking if fixes have flowed
  through the VMR pipeline, debugging dependency update issues, checking overall
  flow status for a repo, diagnosing why backflow PRs are missing or blocked,
  subscription health, build freshness, URLs containing dotnet-maestro or
  "Source code updates from dotnet/dotnet".
  DO NOT USE FOR: CI build failures (use ci-analysis skill), code review
  (use code-review skill), general PR investigation without codeflow context.
  INVOKES: maestro MCP tools (maestro_subscriptions, maestro_subscription_health,
  maestro_build_freshness, maestro_latest_build, maestro_trigger_subscription,
  maestro_codeflow_prs, maestro_tracked_pr, maestro_backflow_status,
  maestro_subscription_history),
  GitHub MCP tools (pull_request_read, get_file_contents, search_pull_requests),
  and Get-FlowHealth.ps1 script for batch flow health scanning.
---

# Flow Analysis

Analyze VMR codeflow PR health by combining **maestro MCP tools** (subscription/build/channel data) with **GitHub MCP tools** (PR body, comments, commits, file contents). For single-PR analysis, you reason over MCP data directly. For repo-wide flow health, a script handles batch GitHub scanning in parallel.

> 🚨 **NEVER** use `gh pr review --approve` or `--request-changes`. Only `--comment` is allowed.

## When to Use This Skill

Use this skill when:
- A codeflow PR (from `dotnet-maestro[bot]`) is stale or failing and you need to understand why
- You need to check if a specific fix has flowed through the VMR pipeline
- A PR has a Maestro staleness warning or conflict
- You want to check overall flow health for a repo ("what's the flow status for the sdk?")
- You need to diagnose why backflow PRs are missing or blocked
- You're asked "is this codeflow PR up to date", "why is the codeflow blocked", "what's the flow status for net11"

## Prerequisites

- **Maestro MCP server** — must be configured (provides `maestro_*` tools). See [lewing/maestro.mcp](https://github.com/lewing/maestro.mcp) for setup.
- **GitHub CLI (`gh`)** — must be installed and authenticated. Required by `Get-FlowHealth.ps1`.

## Quick Start

For **"what's the flow status for repo X?"** — use the Codeflow Overview:
1. Check subscription health for the target repository to find stale subscriptions
2. List tracked codeflow PRs (filter by channel name or grep for your repo — output includes all repos)
3. For stale subscriptions with no tracked PR → check subscription history to find the failure point
4. Check build freshness for relevant channels to rule out VMR build failures
5. Check for open forward flow PRs into `dotnet/dotnet` from the repo — these can cause staleness blocks on backflow

For **investigating a specific PR** — use PR Analysis (Step 1 below).

## Codeflow Concepts

- **Backflow** (VMR → product repo): Maestro creates PRs titled `[branch] Source code updates from dotnet/dotnet` in product repos (e.g., `dotnet/sdk`)
- **Forward flow** (product repo → VMR): PRs titled `[branch] Source code updates from dotnet/<repo>` into `dotnet/dotnet`
- **Staleness**: When forward flow merges while a backflow PR is open, Maestro blocks further updates

## Channel Resolution

Users refer to channels with shorthand. **.NET major = year − 2015** (2026 → .NET 11, 2025 → .NET 10).

| User says | Resolve to |
|-----------|-----------|
| `net11` | Filter `maestro_channels` for names containing `11.0` |
| `11.0.1xx` | Use directly with build freshness or channel queries |
| `release/10.0.3xx` | Strip `release/` → `10.0.3xx`, match channels |
| `main` | Current dev branch (major = year − 2015, band = `1xx`) |

## Analysis Modes

| Question | Mode | Approach |
|----------|------|----------|
| "What's the flow status for X?" | **Codeflow overview** | Subscription health + codeflow PRs + build freshness |
| "Why is this PR stale/blocked?" | **PR analysis** | Read PR → extract metadata → check subscription health/history → assess freshness |
| "Full flow health report for X" | **Flow health** | `Get-FlowHealth.ps1` script for batch GitHub scanning + maestro enrichment |

## Codeflow Overview Workflow

When the user asks "what codeflow PRs are active?" or "what's the flow status?", start by checking subscription health and listing tracked PRs:

### Step 1: Check Subscription Health

Check subscription health for the target repository. This shows which subscriptions are stale (behind on builds) and which are current.

> ⚠️ **"Builds behind" compares last *merged* build vs latest available.** High numbers are a real problem — codeflow PRs can get stuck for weeks or months without merging. Cross-check with the tracked PR: if the PR is less than a day old, the number may just reflect normal processing lag. If the PR has been open for days, it's genuinely stuck and needs investigation.

### Step 2: List Tracked PRs

List all codeflow PRs currently tracked by Maestro, optionally filtering by channel name.

> ⚠️ **Output is large** (200+ PRs across all repos). Filter by `channelName` parameter, or grep/search the output for your target repo.

### Step 3: Drill Into Problems

For subscriptions that are stale — whether they have a stuck PR or no PR at all:
- Check the subscription's update history to find the failure point
- Check build freshness to rule out VMR build failures (if builds are stale, it's a VMR issue, not Maestro)
- Check for open forward flow PRs from the product repo into `dotnet/dotnet` — an open forward flow PR can block backflow
- For stuck PRs, check the PR's age and recent activity — a PR open >3 days with no progress needs attention

### Step 4: Enrich with GitHub Data

Use GitHub PR details to check state, comments, and merge status for any PRs flagged as problematic.

## PR Analysis Workflow

### Step 1: Read the PR and Extract Metadata

Read the PR details and extract codeflow metadata from the body:
- **Subscription ID** — GUID between `[marker]: <> (Begin:<id>)` tags
- **BAR build ID** — number in parentheses after the build link
- **VMR commit SHA** — the `**Commit**:` field (snapshot this PR is based on)
- **VMR branch** — the `**Branch**:` field

### Step 2: Check Subscription Health and History

Check the subscription's health status to assess whether Maestro is processing builds for this flow.

Then check the subscription's update history to see the timeline of build applications — this shows when each build was processed, whether it succeeded, and what PR was created/updated. Use this to answer "when did this subscription get stuck?" or "was there a failed attempt?"

You can also look up the tracked PR for a subscription to confirm Maestro's view of the active PR — this is faster than searching GitHub if you just need to confirm what Maestro is tracking.

### Step 3: Assess PR State

Check PR comments for Maestro bot warnings:
- **Staleness**: "codeflow cannot continue" or "source repository has received code changes"
- **Conflict**: "Conflict detected" with file list

Cross-reference with PR checks/mergeable status — if Codeflow verification passes or PR is mergeable, the issue may already be resolved.

### Step 4: Check Build Freshness

Call `maestro_build_freshness` with the channel short name (e.g., `11.0.1xx`). Compare the build date against the PR's build date to determine if newer builds exist.

### Step 5: Trace a Fix (Optional)

To check if a specific fix has reached the PR:
1. Read `src/source-manifest.json` from the VMR at the PR's snapshot commit — find the product repo's `commitSha`
2. Check if the fix commit is an ancestor of that SHA

## Flow Health Workflow (Script + MCP)

Flow health scanning uses a **hybrid approach**: the `Get-FlowHealth.ps1` script handles batch GitHub API calls in parallel, while maestro MCP tools provide subscription and build freshness data.

> 💡 **Why a script for flow health?** Scanning all branches requires 10-30+ parallel GitHub API calls (PR searches, body fetches, VMR HEAD lookups, commit comparisons). The script fires these in parallel using `Start-ThreadJob`; sequential MCP calls would be prohibitively slow.

### Step 1: Run the Script

```powershell
# Scan all branches for a repo
./scripts/Get-FlowHealth.ps1 -Repository "dotnet/sdk"

# Scan a specific branch only
./scripts/Get-FlowHealth.ps1 -Repository "dotnet/sdk" -Branch "main"
```

The script outputs structured JSON with:
- **`backflow.branches[]`**: Per-branch status (healthy/stale/conflict/missing/up-to-date/released-preview), PR numbers, VMR commit mapping, ahead-by counts
- **`backflow.summary`**: Counts of healthy/upToDate/blocked/missing branches
- **`forwardFlow.prs[]`**: Open forward flow PRs with health status
- **`forwardFlow.summary`**: Counts of healthy/stale/conflicted forward PRs

### Step 2: Enrich with Maestro MCP Data

After the script runs, enrich with maestro data:

1. **Build freshness**: For each `vmrBranch` found in the script output, check build freshness with the channel short name to verify official VMR builds are healthy.

2. **Subscription health**: For branches with `status: "missing"`, check subscription health for the target repository to diagnose *why* — is the subscription stuck, disabled, or is the channel frozen?

3. **Update history**: For stuck subscriptions, check the subscription's update history to see the timeline — when was the last successful application? Was there a failed attempt?

4. **Tracked PRs**: Cross-reference script results with the codeflow PR list to see Maestro's view of tracked PRs — the script sees GitHub state while Maestro may have a different picture.

5. **Latest builds**: For stuck subscriptions, find the latest build to get the buildId needed for triggering.

### Step 3: Synthesize

Combine script output (GitHub PR state) + MCP data (Maestro health) to produce the diagnosis:
- If multiple branches show `missing` AND `maestro_build_freshness` is stale → VMR build failure (not a Maestro issue)
- If one branch is `missing` but builds are fresh → Maestro is stuck, suggest triggering
- If a branch has `status: "conflict"` → suggest `darc vmr resolve-conflict`

> ⚠️ **Branch names differ across repos.** When the user says "net10":
> - `runtime`, `aspnetcore`, `efcore`, `winforms`, `wpf` → `release/10.0`
> - `sdk`, `msbuild` → `release/10.0.1xx` (or `10.0.2xx`, `10.0.3xx`)
> - `roslyn` → `release/dev18.0`
>
> When asked about a major version, check **all branches** — don't ask for clarification.

## Interpreting Results

### Current State
- **✅ MERGED**: No action needed
- **✖️ CLOSED**: Maestro should create a replacement; check subscription health
- **📭 NO-OP**: Empty diff — changes landed via other paths
- **🔄 IN PROGRESS**: Recent force push within 24h — someone is working on it
- **⏳ STALE**: No activity for >3 days — needs attention
- **✅ ACTIVE**: PR has content and recent activity

### Subscription Health Diagnostics
- **`maestro-stuck`**: Subscription enabled, but last applied build is older than latest — Maestro isn't processing. Trigger the subscription to remediate.
- **`subscription-disabled`**: Subscription turned off — intentional or oversight
- **`channel-frozen`**: Latest build is `Released` — no action needed (preview shipped)
- **`subscription-missing`**: No subscription exists — expected for shipped previews

### Subscription History Patterns
- **`ApplyingUpdates` failure**: Maestro couldn't create or update the PR branch — typically a git conflict or API error
- **`MergingPullRequest` failure**: PR exists but merge failed — usually CI checks blocking merge
- **Alternating failures**: Normal retry behavior — Maestro retries on the next build
- **Long gap with no entries**: Subscription may be disabled or channel has no new builds

> ❌ **Never assume "Unknown" means healthy.** API failures produce Unknown status — exclude from positive counts.

## Generating Recommendations

Check `isCodeflowPR` first — if the PR isn't from `dotnet-maestro[bot]`, skip codeflow advice.

| State | Action |
|-------|--------|
| MERGED | Mention Maestro will create new PR if VMR has newer content |
| CLOSED | Suggest triggering subscription if ID available |
| NO-OP | Recommend closing/merging to clear state |
| IN_PROGRESS | Wait, then check back |
| STALE | Check warnings for what's blocking |
| ACTIVE | Check freshness and warnings for nuance |

### Remediation via Maestro

| Action | When |
|--------|------|
| Trigger subscription | PR was closed or no PR exists for an enabled subscription |
| Check latest build for trigger | Need a buildId to trigger with |
| Check subscription history | Diagnosing when a subscription got stuck or failed |
| Check backflow status for a VMR build | Understanding which product repos received a VMR build |
| Bypass cache after action | After triggering, verify state changed using noCache |

> ⚠️ **Force trigger vs normal trigger**: The MCP trigger is a normal trigger. For force-trigger (overwrites existing PR branch), use `darc trigger-subscriptions --id <id> --force` via shell. The MCP server doesn't yet support force-trigger.

### Darc Commands (When MCP Insufficient)

```bash
darc trigger-subscriptions --id <subscription-id> --force    # Force trigger (not yet in MCP)
darc vmr resolve-conflict --subscription <subscription-id>   # Resolve conflicts locally
```

## Widespread Staleness Pattern

When multiple repos are missing backflow simultaneously, the root cause is usually **VMR build failures**, not Maestro:

1. Use `maestro_build_freshness` across multiple channels — if all are stale, VMR builds are broken
2. Check public VMR CI builds at `dnceng-public/public` pipeline 278 for failures
3. Search `dotnet/dotnet` issues with `[Operational Issue]` label

## References

- **VMR codeflow concepts & darc commands**: [references/vmr-codeflow-reference.md](references/vmr-codeflow-reference.md)
- **VMR build topology**: [references/vmr-build-topology.md](references/vmr-build-topology.md)
- **Maestro MCP server**: [github.com/lewing/maestro.mcp](https://github.com/lewing/maestro.mcp)
