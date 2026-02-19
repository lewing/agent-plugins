# lewing/agent-plugins

Public skills by @lewing — CI analysis, code review, trusted publishing

A **plugin marketplace** for [Copilot Agent Skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills). Install plugins to get skills and MCP server configs automatically.

## Installation

Register the marketplace, then install any plugin:
```
copilot plugin marketplace add lewing/agent-plugins
copilot plugin install dotnet-dnceng@lewing-public-skills
```

Or install directly from GitHub without registering:
```
copilot plugin install lewing/agent-plugins:plugins/dotnet-dnceng
```

Inside Copilot CLI, use `/plugin` instead of `copilot plugin`:
```
/plugin install lewing/agent-plugins:plugins/dotnet-dnceng
```

To uninstall a plugin:
```
copilot plugin uninstall dotnet-dnceng
```

## Available Plugins

### [dotnet-dnceng](plugins/dotnet-dnceng/)

Skills for .NET engineering infrastructure: CI/CD analysis, VMR codeflow, and build pipeline workflows

| Skill | References |
|-------|------------|
| [ci-analysis](plugins/dotnet-dnceng/skills/ci-analysis/SKILL.md) | [azdo-helix-reference](plugins/dotnet-dnceng/skills/ci-analysis/references/azdo-helix-reference.md), [azure-cli](plugins/dotnet-dnceng/skills/ci-analysis/references/azure-cli.md), [binlog-comparison](plugins/dotnet-dnceng/skills/ci-analysis/references/binlog-comparison.md), [build-progression-analysis](plugins/dotnet-dnceng/skills/ci-analysis/references/build-progression-analysis.md), [delegation-patterns](plugins/dotnet-dnceng/skills/ci-analysis/references/delegation-patterns.md), [helix-artifacts](plugins/dotnet-dnceng/skills/ci-analysis/references/helix-artifacts.md), [manual-investigation](plugins/dotnet-dnceng/skills/ci-analysis/references/manual-investigation.md), [sql-tracking](plugins/dotnet-dnceng/skills/ci-analysis/references/sql-tracking.md) |
| [flow-analysis](plugins/dotnet-dnceng/skills/flow-analysis/SKILL.md) | [vmr-build-topology](plugins/dotnet-dnceng/skills/flow-analysis/references/vmr-build-topology.md), [vmr-codeflow-reference](plugins/dotnet-dnceng/skills/flow-analysis/references/vmr-codeflow-reference.md) |

### [lewing](plugins/lewing/)

Personal skills by @lewing — skill development for Copilot CLI

| Skill | References |
|-------|------------|
| [nuget-trusted-publishing](plugins/lewing/skills/nuget-trusted-publishing/SKILL.md) | [package-types](plugins/lewing/skills/nuget-trusted-publishing/references/package-types.md), [publish-workflow](plugins/lewing/skills/nuget-trusted-publishing/references/publish-workflow.md) |
