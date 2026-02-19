# dotnet-dnceng

Skills for .NET engineering infrastructure: CI/CD analysis, VMR codeflow, and build pipeline workflows

## Installation

Via marketplace:
```
copilot plugin marketplace add lewing/agent-plugins
copilot plugin install dotnet-dnceng@lewing-public-skills
```

Or directly from GitHub:
```
copilot plugin install lewing/agent-plugins:plugins/dotnet-dnceng
```

## Skills

### [ci-analysis](skills/ci-analysis/SKILL.md)

Analyze CI build and test status from Azure DevOps and Helix for dotnet repository PRs. Use when checking CI status, investigating failures, determining if a PR is ready to merge, or given URLs containing dev.azure.com or helix.dot.net. Also use when asked "why is CI red", "test failures", "retry CI", "rerun tests", "is CI green", "build failed", "checks failing", or "flaky tests".

**References:**
- [azdo-helix-reference.md](skills/ci-analysis/references/azdo-helix-reference.md)
- [azure-cli.md](skills/ci-analysis/references/azure-cli.md)
- [binlog-comparison.md](skills/ci-analysis/references/binlog-comparison.md)
- [build-progression-analysis.md](skills/ci-analysis/references/build-progression-analysis.md)
- [delegation-patterns.md](skills/ci-analysis/references/delegation-patterns.md)
- [helix-artifacts.md](skills/ci-analysis/references/helix-artifacts.md)
- [manual-investigation.md](skills/ci-analysis/references/manual-investigation.md)
- [sql-tracking.md](skills/ci-analysis/references/sql-tracking.md)

### [flow-analysis](skills/flow-analysis/SKILL.md)

Analyze VMR codeflow health using maestro MCP tools and GitHub MCP tools. USE FOR: investigating stale codeflow PRs, checking if fixes have flowed through the VMR pipeline, debugging dependency update issues, checking overall flow status for a repo, diagnosing why backflow PRs are missing or blocked, subscription health, build freshness, URLs containing dotnet-maestro or "Source code updates from dotnet/dotnet". DO NOT USE FOR: CI build failures (use ci-analysis skill), code review (use code-review skill), general PR investigation without codeflow context. INVOKES: maestro MCP tools (maestro_subscriptions, maestro_subscription_health, maestro_build_freshness, maestro_latest_build, maestro_trigger_subscription), GitHub MCP tools (pull_request_read, get_file_contents, search_pull_requests), and Get-FlowHealth.ps1 script for batch flow health scanning.

**References:**
- [vmr-build-topology.md](skills/flow-analysis/references/vmr-build-topology.md)
- [vmr-codeflow-reference.md](skills/flow-analysis/references/vmr-codeflow-reference.md)

## MCP Servers

This plugin configures the following [MCP servers](https://modelcontextprotocol.io/) automatically when installed:

- **azure-devops** — `npx` tool
- **hlx** — `dnx` tool
- **maestro** — `dnx` tool
- **mcp-binlog-tool** — `dnx` tool
- **mihubot** — https://mihubot.xyz/mcp
