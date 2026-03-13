# dotnet-dnceng

Skills for .NET engineering infrastructure: CI/CD analysis, VMR codeflow, and build pipeline workflows

## Installation

### Copilot CLI / Claude Code

Via marketplace:
```
/plugin marketplace add lewing/agent-plugins
/plugin install dotnet-dnceng@lewing-public
/plugin update dotnet-dnceng@lewing-public
```

Or install directly from GitHub (Copilot CLI only):
```
/plugin install lewing/agent-plugins:plugins/dotnet-dnceng
```

### VS Code (Preview)

Add the marketplace to your VS Code settings:

```jsonc
// settings.json
{
  "chat.plugins.enabled": true,
  "chat.plugins.marketplaces": ["lewing/agent-plugins"]
}
```

Then use `/plugins` in Copilot Chat to browse and install.

## Uninstall

```
# Copilot CLI / Claude Code
/plugin uninstall dotnet-dnceng@lewing-public

# VS Code: remove the marketplace entry from chat.plugins.marketplaces in settings.json
```

## Skills

### [ci-analysis](skills/ci-analysis/SKILL.md)

Analyze CI build and test status from Azure DevOps and Helix for dotnet repository PRs. Use when checking CI status, investigating failures, determining if a PR is ready to merge, or given URLs containing dev.azure.com or helix.dot.net. Also use when asked "why is CI red", "test failures", "retry CI", "rerun tests", "is CI green", "build failed", "checks failing", or "flaky tests". DO NOT USE FOR: investigating stale codeflow PRs or dependency update health, tracing whether a commit has flowed from one repo to another, reviewing code changes for correctness or style.

**References:**
- [analysis-workflow.md](skills/ci-analysis/references/analysis-workflow.md)
- [azdo-helix-reference.md](skills/ci-analysis/references/azdo-helix-reference.md)
- [azure-cli.md](skills/ci-analysis/references/azure-cli.md)
- [build-progression-analysis.md](skills/ci-analysis/references/build-progression-analysis.md)
- [delegation-patterns.md](skills/ci-analysis/references/delegation-patterns.md)
- [failure-interpretation.md](skills/ci-analysis/references/failure-interpretation.md)
- [helix-artifacts.md](skills/ci-analysis/references/helix-artifacts.md)
- [manual-investigation.md](skills/ci-analysis/references/manual-investigation.md)
- [recommendation-generation.md](skills/ci-analysis/references/recommendation-generation.md)
- [script-modes.md](skills/ci-analysis/references/script-modes.md)
- [sql-tracking.md](skills/ci-analysis/references/sql-tracking.md)

### [flow-analysis](skills/flow-analysis/SKILL.md)

Analyze VMR codeflow health using maestro MCP tools and GitHub MCP tools. USE FOR: investigating stale codeflow PRs, checking if fixes have flowed through the VMR pipeline, debugging dependency update issues, checking overall flow status for a repo, diagnosing why backflow PRs are missing or blocked, subscription health, build freshness, URLs containing dotnet-maestro or "Source code updates from dotnet/dotnet". DO NOT USE FOR: CI build failures (use ci-analysis skill), code review (use code-review skill), general PR investigation without codeflow context, tracing whether a specific commit/PR has reached another repo (use flow-tracing skill). INVOKES: maestro and GitHub MCP tools, flow-health.cs script.

**References:**
- [vmr-build-topology.md](skills/flow-analysis/references/vmr-build-topology.md)
- [vmr-codeflow-reference.md](skills/flow-analysis/references/vmr-codeflow-reference.md)

### [flow-tracing](skills/flow-tracing/SKILL.md)

Trace dependency flow across .NET repos through the VMR pipeline. USE FOR: checking if a PR/commit from repo A has reached repo B, finding what runtime SHA is in an SDK build, tracing dependency versions through the VMR, checking if a commit is included in an SDK build, decoding SDK version strings, "has my fix reached runtime", "did roslyn#80873 flow to runtime", "what SHA is in SDK version X", cross-repo dependency tracing, mapping SDK versions to VMR commits. DO NOT USE FOR: codeflow PR health or staleness (use flow-analysis skill), CI build failures (use ci-analysis skill). INVOKES: maestro and GitHub MCP tools, Get-SdkVersionTrace.ps1 script.

**References:**
- [azdo-pipelines.md](skills/flow-tracing/references/azdo-pipelines.md)
- [sdk-version-format.md](skills/flow-tracing/references/sdk-version-format.md)
- [servicing-topology.md](skills/flow-tracing/references/servicing-topology.md)

### [maestro-cli](skills/maestro-cli/SKILL.md)

Query Maestro/BAR dependency flow data using the mstro CLI tool via bash. USE FOR: subscription health checks, build flow tracing, codeflow status, channel discovery, triggering subscription updates — when MCP tools aren't loaded or when scripting with JSON output and jq. Also use when investigating "is this subscription stale", "what's the latest build", "check backflow status". DO NOT USE FOR: tasks where maestro MCP tools are already available in context (prefer flow-analysis or flow-tracing skills when MCP server is loaded). INVOKES: bash (mstro CLI commands with --json output).

**References:**
- [maestro-cli-reference.md](skills/maestro-cli/references/maestro-cli-reference.md)

## MCP Servers

This plugin configures the following [MCP servers](https://modelcontextprotocol.io/) automatically when installed:

- **[hlx](.claude-plugin/plugin.json#L26-L33)** — `dotnet` tool
- **[maestro](.claude-plugin/plugin.json#L34-L41)** — `dotnet` tool
- **[mihubot](.claude-plugin/plugin.json#L42-L45)** — https://mihubot.xyz/mcp
