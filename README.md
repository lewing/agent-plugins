# lewing-public

Public skills by @lewing — CI analysis, code review, trusted publishing

A **plugin marketplace** for [Agent Skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills). Install plugins to get skills and MCP server configs automatically.

## Installation

```bash
# Add the marketplace
/plugin marketplace add lewing/agent-plugins

# Install a plugin
/plugin install dotnet-runtime@lewing-public-skills
```

## Available Plugins

| Plugin | Skills | Description |
|--------|--------|-------------|| **dotnet-runtime** | code-review, jit-regression-test | Skills for dotnet/runtime development workflows: code review, JIT APIs, regression tests |
| **dotnet-dnceng** | ci-analysis, vmr-codeflow-status | Skills for .NET engineering infrastructure: CI/CD analysis, VMR codeflow, and build pipeline workflows |
| **lewing** | nuget-trusted-publishing | Personal skills by @lewing — skill development for Copilot CLI |

---
*Auto-synced from source @ `ce0b45f`*

