# lewing/agent-plugins

Public skills by @lewing — CI analysis, code review, trusted publishing

A **plugin marketplace** for [Copilot Agent Skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills). Install plugins to get skills and MCP server configs automatically.

## Installation

Via marketplace (supports updates):
```
/plugin marketplace add lewing/agent-plugins
/plugin install dotnet-dnceng@lewing-public
/plugin update dotnet-dnceng@lewing-public
/plugin uninstall dotnet-dnceng@lewing-public
```

Or install directly from GitHub:
```
/plugin install lewing/agent-plugins:plugins/dotnet-dnceng
```

## Available Plugins

### [dotnet-dnceng](plugins/dotnet-dnceng/)

Skills for .NET engineering infrastructure: CI/CD analysis, VMR codeflow, and build pipeline workflows

| Skill | References |
|-------|------------|
| [ci-analysis](plugins/dotnet-dnceng/skills/ci-analysis/SKILL.md) | [azdo-helix-reference](plugins/dotnet-dnceng/skills/ci-analysis/references/azdo-helix-reference.md), [azure-cli](plugins/dotnet-dnceng/skills/ci-analysis/references/azure-cli.md), [binlog-comparison](plugins/dotnet-dnceng/skills/ci-analysis/references/binlog-comparison.md), [build-progression-analysis](plugins/dotnet-dnceng/skills/ci-analysis/references/build-progression-analysis.md), [delegation-patterns](plugins/dotnet-dnceng/skills/ci-analysis/references/delegation-patterns.md), [helix-artifacts](plugins/dotnet-dnceng/skills/ci-analysis/references/helix-artifacts.md), [manual-investigation](plugins/dotnet-dnceng/skills/ci-analysis/references/manual-investigation.md), [sql-tracking](plugins/dotnet-dnceng/skills/ci-analysis/references/sql-tracking.md) |
| [flow-analysis](plugins/dotnet-dnceng/skills/flow-analysis/SKILL.md) | [vmr-build-topology](plugins/dotnet-dnceng/skills/flow-analysis/references/vmr-build-topology.md), [vmr-codeflow-reference](plugins/dotnet-dnceng/skills/flow-analysis/references/vmr-codeflow-reference.md) |
| [flow-tracing](plugins/dotnet-dnceng/skills/flow-tracing/SKILL.md) | [azdo-pipelines](plugins/dotnet-dnceng/skills/flow-tracing/references/azdo-pipelines.md), [sdk-version-format](plugins/dotnet-dnceng/skills/flow-tracing/references/sdk-version-format.md), [servicing-topology](plugins/dotnet-dnceng/skills/flow-tracing/references/servicing-topology.md) |

### [framework-versioning](plugins/framework-versioning/)

Skills and agents for .NET major version bumps: TFM updates, workload manifest creation, and version property management

| Skill | References |
|-------|------------|
| [target-new-framework](plugins/framework-versioning/skills/target-new-framework/SKILL.md) | [version-bump-instructions](plugins/framework-versioning/skills/target-new-framework/references/version-bump-instructions.md), [workload-manifest-patterns](plugins/framework-versioning/skills/target-new-framework/references/workload-manifest-patterns.md), [workload-version-bump-instructions](plugins/framework-versioning/skills/target-new-framework/references/workload-version-bump-instructions.md) |

### [lewing](plugins/lewing/)

Personal skills by @lewing — skill development for Copilot CLI

| Skill | References |
|-------|------------|
| [nuget-trusted-publishing](plugins/lewing/skills/nuget-trusted-publishing/SKILL.md) | [package-types](plugins/lewing/skills/nuget-trusted-publishing/references/package-types.md), [publish-workflow](plugins/lewing/skills/nuget-trusted-publishing/references/publish-workflow.md) |
