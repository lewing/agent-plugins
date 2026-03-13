# Maestro CLI Reference

Complete command reference and workflow guide for the `mstro` CLI tool.

## Command Reference

| Command | Description |
|---------|-------------|
| `subscriptions` | List subscriptions filtered by source/target repository and/or channel |
| `subscription` | Get a subscription by GUID ID with health diagnostic |
| `subscription-health` | Check subscription health for a target repo (detects stale subscriptions) |
| `subscription-history` | Get update history for a subscription |
| `latest-build` | Get the latest build for a repository, optionally filtered by channel |
| `build` | Get a specific build by BAR build ID |
| `builds` | List builds filtered by repository, channel, commit, or build number |
| `build-graph` | Get the full dependency graph for a build |
| `build-freshness` | Check build freshness for a channel via aka.ms redirect |
| `channels` | List all Maestro channels |
| `channel` | Get a specific channel by ID or name |
| `default-channels` | List default channel mappings (repo/branch → channel) |
| `codeflow-prs` | List active codeflow PRs managed by Maestro |
| `codeflow-statuses` | Get codeflow status (forward/backflow) for a repo and branch |
| `tracked-pr` | Get the tracked PR for a specific subscription |
| `backflow-status` | Get backflow status for a VMR build |
| `flow-graph` | Get the dependency flow graph for a channel |
| `trigger-subscription` | Trigger a subscription update (requires auth). Takes positional `<guid>`. Provide `--build-id` OR `--source-repository` + `--channel-name` to resolve the build. Add `--force` to overwrite stale PR branch. |
| `trigger-daily-update` | Trigger all daily-update subscriptions (requires auth) |
| `cache` | Cache management (`clear`, `status`) |

## Key JSON Response Fields

> **Canonical source:** Run `mstro <command> --schema` for always-in-sync field names.
> The fields below are a convenience snapshot for offline reference.

When piping to `jq`, these are the fields you'll encounter most often:

**`subscription-health --json`** returns:
- `.StaleSubs[]` — array of stale subscriptions, each with `.Id` (GUID), `.SourceRepository`, `.TargetRepository`, `.ChannelName`, `.BuildsBehind` (int), `.LastAppliedBuildId`
- `.HealthySubs[]` — same shape, for up-to-date subscriptions

**`latest-build --json`** returns: `.Id` (int), `.Repository`, `.Commit`, `.DateProduced`, `.Channels[]`

**`subscription --json`** returns: `.Id` (GUID), `.SourceRepository`, `.TargetRepository`, `.Channel.Name`, `.Enabled`, `.LastAppliedBuild.Id`

**`codeflow-statuses --json`** returns: array with `.SourceRepository`, `.Branch`, `.ForwardFlowStatus`, `.BackflowStatus`, `.CommitDistance`

## Workflow: Investigate Stale Subscription

```bash
# 1. Find stale subscriptions for a target repo
mstro subscription-health --target-repository https://github.com/dotnet/dotnet --json

# 2. Find the worst (most builds behind)
mstro subscription-health --target-repository https://github.com/dotnet/dotnet --json \
  | jq '.StaleSubs | sort_by(.BuildsBehind) | reverse | .[0]'

# 3. Drill into a specific subscription
mstro subscription <guid> --json

# 4. Check update history for errors
mstro subscription-history <guid> --json

# 5. Trigger the subscription manually (auto-resolve latest build)
mstro trigger-subscription <guid> \
  --source-repository https://github.com/dotnet/runtime \
  --channel-name ".NET 10.0.1xx SDK"

# 6. Force-trigger if PR branch is stale (overwrites PR)
mstro trigger-subscription <guid> --build-id <id> --force
```

## Workflow: Trace Build Flow

```bash
# 1. Find latest build on a channel
BUILD_ID=$(mstro latest-build https://github.com/dotnet/runtime \
  --channel-name ".NET 10.0.1xx SDK" --json | jq -r '.Id')

# 2. Get build details
mstro build $BUILD_ID --json

# 3. Get full dependency graph
mstro build-graph $BUILD_ID --json
```

## Workflow: Check Codeflow Health

```bash
# 1. Get forward/backflow status for the VMR
mstro codeflow-statuses --json

# 2. List active codeflow PRs
mstro codeflow-prs --json

# 3. Check backflow for a specific VMR build
mstro backflow-status <vmr-build-id> --json

# 4. Get tracked PR for a subscription
mstro tracked-pr <subscription-id> --json
```

## Workflow: Channel Discovery

```bash
# List all channels
mstro channels --json | jq '.[] | select(.Name | contains("10.0"))'

# Get specific channel by name
mstro channel ".NET 10.0.1xx SDK" --json

# Check default channel mappings for a repo
mstro default-channels --repository https://github.com/dotnet/runtime --json

# Check build freshness (channel short name)
mstro build-freshness 10.0.1xx --json
```

## Authentication Details

The tool uses a 3-tier authentication cascade:

1. **Explicit PAT** — Set `MAESTRO_BAR_TOKEN` environment variable
2. **Cached Entra ID** — Reuses credentials from `darc authenticate` (stored at `~/.darc/.auth-record-*`)
3. **Anonymous** — Read-only fallback (may be rate-limited)

**Recommended:** Run `darc authenticate` once (from [arcade-services](https://github.com/dotnet/arcade-services)) to cache credentials. All authenticated actions (trigger-subscription, trigger-daily-update) require tier 1 or 2.

## Cache Details

- **Location:** `~/.mstro/cache.db` (SQLite with WAL mode)
- **Cross-process sharing:** CLI and MCP server instances share the same cache
- **TTLs:** Subscriptions (5m), Latest Builds (5m), Channels (15m), Build by ID (30m), Build Freshness (10m)
- **Cache bypass:** `--no-cache` on any command forces fresh API calls
- **Action dedup:** Trigger commands have a 2-minute cooldown window (not cleared by `cache clear`)
- **Status/clear:** `mstro cache status` / `mstro cache clear`

## JSON Output

All query commands support `--json` for machine-parseable output. The JSON matches the data returned by MCP tools but in structured format instead of markdown.

```bash
# Filter stale subs with jq
mstro subscription-health --target-repository https://github.com/dotnet/dotnet --json \
  | jq '.StaleSubs[] | "\(.SourceRepository) → \(.TargetRepository) (\(.BuildsBehind) builds behind)"'

# Count by source repo
mstro subscriptions --target-repository https://github.com/dotnet/dotnet --json \
  | jq 'group_by(.SourceRepository) | map({repo: .[0].SourceRepository, count: length})'
```

## Relationship to MCP Server

The same binary (`mstro`) operates in two modes:
- **CLI mode:** When invoked with a command (e.g., `mstro subscription-health`)
- **MCP server mode:** When launched by an MCP host (stdin piped)

Both modes share the same cached data layer, authentication cascade, and API client. Using the CLI warms the cache for MCP server instances and vice versa.
