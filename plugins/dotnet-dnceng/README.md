# dotnet-dnceng

Skills for .NET engineering infrastructure: CI/CD analysis, VMR codeflow, and build pipeline workflows

## Installation

Via marketplace (supports updates):
```
/plugin marketplace add lewing/agent-plugins
/plugin install dotnet-dnceng@lewing-public
/plugin update dotnet-dnceng@lewing-public
```

Or install directly from GitHub:
```
/plugin install lewing/agent-plugins:plugins/dotnet-dnceng
```

## Uninstall

```
/plugin uninstall dotnet-dnceng@lewing-public
```

## Skills

### [ci-analysis](skills/ci-analysis/SKILL.md)

Analyze CI build and test status from Azure DevOps and Helix for dotnet repository PRs. Use when checking CI status, investigating failures, determining if a PR is ready to merge, or given URLs containing dev.azure.com or helix.dot.net. Also use when asked "why is CI red", "test failures", "retry CI", "rerun tests", "is CI green", "build failed", "checks failing", or "flaky tests". DO NOT USE FOR: investigating stale codeflow PRs or dependency update health, tracing whether a commit has flowed from one repo to another, reviewing code changes for correctness or style.

**References:**
- [analysis-workflow.md](skills/ci-analysis/references/analysis-workflow.md)
- [azdo-helix-reference.md](skills/ci-analysis/references/azdo-helix-reference.md)
- [azure-cli.md](skills/ci-analysis/references/azure-cli.md)
- [binlog-comparison.md](skills/ci-analysis/references/binlog-comparison.md)
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

## MCP Servers

This plugin configures the following [MCP servers](https://modelcontextprotocol.io/) automatically when installed:

- **[ado-dnceng-public](plugin.json#L24-L34)** — `npx` tool
- **[ado-dnceng](plugin.json#L35-L45)** — `npx` tool
- **[hlx](plugin.json#L46-L53)** — `dotnet` tool
- **[maestro](plugin.json#L54-L61)** — `dotnet` tool
- **[mcp-binlog-tool](plugin.json#L62-L69)** — `dotnet` tool
- **[mihubot](plugin.json#L70-L73)** — https://mihubot.xyz/mcp
