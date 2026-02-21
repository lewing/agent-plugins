# lewing/agent-plugins

![plugins](https://img.shields.io/badge/plugins-4-blue) ![skills](https://img.shields.io/badge/skills-8-green) ![agents](https://img.shields.io/badge/agents-2-purple)

![GitHub Copilot](https://img.shields.io/badge/GitHub_Copilot-compatible-black?logo=github) ![Claude Code](https://img.shields.io/badge/Claude_Code-compatible-cc785c?logo=anthropic)

Public skills by @lewing — CI analysis, code review, trusted publishing

A **plugin marketplace** for [Copilot Agent Skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills). Install plugins to get skills and MCP server configs automatically.

## Installation

### Claude Code

Via marketplace (supports updates):
```
/plugin marketplace add lewing/agent-plugins
# Browse available plugins
/plugin   # → go to Discover tab
/plugin install <plugin-name>@lewing-public
/plugin update <plugin-name>@lewing-public
/plugin uninstall <plugin-name>@lewing-public
```

Or install directly from GitHub:
```
/plugin install lewing/agent-plugins:plugins/<plugin-name>
```

### GitHub Copilot CLI

Via marketplace:
```
/plugin marketplace add lewing/agent-plugins
/plugin marketplace browse lewing-public
/plugin install <plugin-name>@lewing-public
/plugin update <plugin-name>@lewing-public
/plugin uninstall <plugin-name>@lewing-public
/plugin list
```

Or install directly from GitHub:
```
/plugin install lewing/agent-plugins:plugins/<plugin-name>
```

List and manage installed skills:
```
/skills list
/skills        # toggle on/off with arrow keys + spacebar
/skills reload # pick up newly added skills
```

## Available Plugins

### [dotnet-dnceng](plugins/dotnet-dnceng/)

Skills for .NET engineering infrastructure: CI/CD analysis, VMR codeflow, and build pipeline workflows

| Skill | References |
|-------|------------|
| [ci-analysis](plugins/dotnet-dnceng/skills/ci-analysis/SKILL.md) | [analysis-workflow](plugins/dotnet-dnceng/skills/ci-analysis/references/analysis-workflow.md), [azdo-helix-reference](plugins/dotnet-dnceng/skills/ci-analysis/references/azdo-helix-reference.md), [azure-cli](plugins/dotnet-dnceng/skills/ci-analysis/references/azure-cli.md), [binlog-comparison](plugins/dotnet-dnceng/skills/ci-analysis/references/binlog-comparison.md), [build-progression-analysis](plugins/dotnet-dnceng/skills/ci-analysis/references/build-progression-analysis.md), [delegation-patterns](plugins/dotnet-dnceng/skills/ci-analysis/references/delegation-patterns.md), [failure-interpretation](plugins/dotnet-dnceng/skills/ci-analysis/references/failure-interpretation.md), [helix-artifacts](plugins/dotnet-dnceng/skills/ci-analysis/references/helix-artifacts.md), [manual-investigation](plugins/dotnet-dnceng/skills/ci-analysis/references/manual-investigation.md), [recommendation-generation](plugins/dotnet-dnceng/skills/ci-analysis/references/recommendation-generation.md), [script-modes](plugins/dotnet-dnceng/skills/ci-analysis/references/script-modes.md), [sql-tracking](plugins/dotnet-dnceng/skills/ci-analysis/references/sql-tracking.md) |
| [flow-analysis](plugins/dotnet-dnceng/skills/flow-analysis/SKILL.md) | [vmr-build-topology](plugins/dotnet-dnceng/skills/flow-analysis/references/vmr-build-topology.md), [vmr-codeflow-reference](plugins/dotnet-dnceng/skills/flow-analysis/references/vmr-codeflow-reference.md) |
| [flow-tracing](plugins/dotnet-dnceng/skills/flow-tracing/SKILL.md) | [azdo-pipelines](plugins/dotnet-dnceng/skills/flow-tracing/references/azdo-pipelines.md), [sdk-version-format](plugins/dotnet-dnceng/skills/flow-tracing/references/sdk-version-format.md), [servicing-topology](plugins/dotnet-dnceng/skills/flow-tracing/references/servicing-topology.md) |

### [framework-versioning](plugins/framework-versioning/)

Skills and agents for .NET major version bumps: TFM updates, workload manifest creation, and version property management

| Skill | References |
|-------|------------|
| [target-new-framework](plugins/framework-versioning/skills/target-new-framework/SKILL.md) | [version-bump-instructions](plugins/framework-versioning/skills/target-new-framework/references/version-bump-instructions.md), [workload-manifest-patterns](plugins/framework-versioning/skills/target-new-framework/references/workload-manifest-patterns.md), [workload-version-bump-instructions](plugins/framework-versioning/skills/target-new-framework/references/workload-version-bump-instructions.md) |

### [skill-trainer](plugins/skill-trainer/)

Skills for building, testing, and training Copilot CLI skills — patterns, anti-patterns, eval methodology

| Skill | References |
|-------|------------|
| [skill-builder](plugins/skill-trainer/skills/skill-builder/SKILL.md) | [agent-conventions](plugins/skill-trainer/skills/skill-builder/references/agent-conventions.md), [anti-patterns](plugins/skill-trainer/skills/skill-builder/references/anti-patterns.md), [skill-lifecycle](plugins/skill-trainer/skills/skill-builder/references/skill-lifecycle.md), [skill-patterns](plugins/skill-trainer/skills/skill-builder/references/skill-patterns.md), [testing-patterns](plugins/skill-trainer/skills/skill-builder/references/testing-patterns.md) |
| [skill-trainer-knowledge](plugins/skill-trainer/skills/skill-trainer-knowledge/SKILL.md) | [eval-integration](plugins/skill-trainer/skills/skill-trainer-knowledge/references/eval-integration.md), [skill-builder-knowledge](plugins/skill-trainer/skills/skill-trainer-knowledge/references/skill-builder-knowledge.md), [training-methodology](plugins/skill-trainer/skills/skill-trainer-knowledge/references/training-methodology.md) |

### [lewing](plugins/lewing/)

Personal skills by @lewing — stealth Squad setup, NuGet trusted publishing

| Skill | References |
|-------|------------|
| [nuget-trusted-publishing](plugins/lewing/skills/nuget-trusted-publishing/SKILL.md) | [package-types](plugins/lewing/skills/nuget-trusted-publishing/references/package-types.md), [publish-workflow](plugins/lewing/skills/nuget-trusted-publishing/references/publish-workflow.md) |
| [stealth-squad](plugins/lewing/skills/stealth-squad/SKILL.md) |  |
