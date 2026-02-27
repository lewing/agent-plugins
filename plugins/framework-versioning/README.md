# framework-versioning

Skills and agents for .NET major version bumps: TFM updates, workload manifest creation, and version property management

## Installation

Via marketplace:
```
/plugin marketplace add lewing/agent-plugins
/plugin install framework-versioning@lewing-public
/plugin update framework-versioning@lewing-public
```

Or install directly from GitHub (Copilot CLI only):
```
/plugin install lewing/agent-plugins:plugins/framework-versioning
```

## Uninstall

```
/plugin uninstall framework-versioning@lewing-public
```

## Skills

### [target-new-framework](skills/target-new-framework/SKILL.md)

Perform the .NET major version bump (e.g., net11 to net12) in any dotnet repo. Use when asked to "update TFMs", "create workload manifest for new version", "update from netN to netN+1", or "create frozen manifest". Covers eng/Versions.props, Directory.Build.props, workload manifests, templates, test assets, and documentation.

**References:**
- [version-bump-instructions.md](skills/target-new-framework/references/version-bump-instructions.md)
- [workload-manifest-patterns.md](skills/target-new-framework/references/workload-manifest-patterns.md)
- [workload-version-bump-instructions.md](skills/target-new-framework/references/workload-version-bump-instructions.md)

## Agents

### [FrameworkVersioning](agents/FrameworkVersioning.agent.md)

An agent that orchestrates the full .NET major version bump process across a repository. Drives discovery, updates, verification, and PR creation with human gates between phases. Uses the target-new-framework skill for domain knowledge.
