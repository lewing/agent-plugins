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
  maestro_build_freshness, maestro_latest_build, maestro_trigger_subscription),
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

## Codeflow Concepts

- **Backflow** (VMR → product repo): Maestro creates PRs titled `[branch] Source code updates from dotnet/dotnet` in product repos (e.g., `dotnet/sdk`)
- **Forward flow** (product repo → VMR): PRs titled `[branch] Source code updates from dotnet/<repo>` into `dotnet/dotnet`
- **Staleness**: When forward flow merges while a backflow PR is open, Maestro blocks further updates

## Channel Resolution

Users refer to channels with shorthand. Resolve to Maestro channel queries using these rules:

| User says | Interpretation | How to resolve |
|-----------|---------------|----------------|
| `net11`, `net 11` | .NET 11 (latest band) | Call `maestro_channels`, filter names containing `11.0` |
| `11.0.1xx` | Specific SDK band | Use directly with `maestro_build_freshness` or match against `maestro_channels` |
| `release/10.0.3xx` | Branch name | Strip `release/` → `10.0.3xx`, match channels containing that |
| `main` | Current dev branch | Major version = current year − 2015, band = `1xx` |

The version formula: **.NET major = year − 2015** (2026 → .NET 11, 2025 → .NET 10). When ambiguous, call `maestro_channels` and let the user pick.

## Two Analysis Modes

| Mode | Use When | Approach |
|------|----------|----------|
| **PR analysis** | Investigating a specific codeflow PR | MCP tools only — read PR → extract metadata → check subscription health → assess freshness |
| **Flow health** | Checking overall repo flow status | Script + MCP — `Get-FlowHealth.ps1` for batch GitHub scanning, maestro MCP for subscription/build data |

> 💡 **Why a script for flow health?** Scanning all branches requires 10-30+ parallel GitHub API calls (PR searches, body fetches, VMR HEAD lookups, commit comparisons). The script fires these in parallel using `Start-ThreadJob`; sequential MCP calls would be prohibitively slow.

## PR Analysis Workflow

### Step 1: Read the PR and Extract Metadata

Read the PR details and extract codeflow metadata from the body:
- **Subscription ID** — GUID between `[marker]: <> (Begin:<id>)` tags
- **BAR build ID** — number in parentheses after the build link
- **VMR commit SHA** — the `**Commit**:` field (snapshot this PR is based on)
- **VMR branch** — the `**Branch**:` field

### Step 2: Check Subscription Health

Query `maestro_subscription` with the subscription ID to assess whether Maestro is processing builds for this flow.

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

After the script runs, use maestro MCP tools to add subscription and build context:

1. **Build freshness**: For each `vmrBranch` found in the script output, call `maestro_build_freshness` with the channel short name to check if official VMR builds are healthy.

2. **Subscription health**: For branches with `status: "missing"`, call `maestro_subscription_health` with `targetRepository` to diagnose *why* — is the subscription stuck, disabled, or is the channel frozen?

3. **Latest builds**: For stuck subscriptions, call `maestro_latest_build` to find the buildId needed for `maestro_trigger_subscription`.

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
- **`maestro-stuck`**: Subscription enabled, but last applied build is older than latest — Maestro isn't processing. Use `maestro_trigger_subscription` to remediate.
- **`subscription-disabled`**: Subscription turned off — intentional or oversight
- **`channel-frozen`**: Latest build is `Released` — no action needed (preview shipped)
- **`subscription-missing`**: No subscription exists — expected for shipped previews

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

### Remediation via MCP Tools

| Action | MCP Tool | When |
|--------|----------|------|
| Trigger subscription | `maestro_trigger_subscription` | PR was closed or no PR exists |
| Check latest build for trigger | `maestro_latest_build` | Need buildId for trigger |
| Bypass cache after action | Any tool with `noCache: true` | After triggering, verify state changed |

> ⚠️ **Force trigger vs normal trigger**: `maestro_trigger_subscription` is a normal trigger. For force-trigger (overwrites existing PR branch), use `darc trigger-subscriptions --id <id> --force` via shell. The MCP server doesn't yet support force-trigger.

### Darc Commands (When MCP Insufficient)

```bash
# Force trigger (not yet in MCP)
darc trigger-subscriptions --id <subscription-id> --force

# Resolve conflicts locally (not in MCP)
darc vmr resolve-conflict --subscription <subscription-id>
```

## Widespread Staleness Pattern

When multiple repos are missing backflow simultaneously, the root cause is usually **VMR build failures**, not Maestro:

1. Use `maestro_build_freshness` across multiple channels — if all are stale, VMR builds are broken
2. Check public VMR CI builds at `dnceng-public/public` pipeline 278 for failures
3. Search `dotnet/dotnet` issues with `[Operational Issue]` label

## Deep Analysis with SQL

Store subscription health results for cross-repo comparison:

```sql
CREATE TABLE IF NOT EXISTS flow_status (
    id TEXT PRIMARY KEY,
    repository TEXT,
    target_branch TEXT,
    subscription_id TEXT,
    is_stale BOOLEAN,
    builds_behind INTEGER,
    last_checked TEXT DEFAULT (datetime('now'))
);
```

## References

- **VMR codeflow concepts & darc commands**: [references/vmr-codeflow-reference.md](references/vmr-codeflow-reference.md)
- **VMR build topology**: [references/vmr-build-topology.md](references/vmr-build-topology.md)
- **Maestro MCP server**: [github.com/lewing/maestro.mcp](https://github.com/lewing/maestro.mcp)
