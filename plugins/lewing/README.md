# lewing

Personal skills by @lewing — skill development for Copilot CLI

## Installation

Inside Copilot CLI:
```
/plugin install lewing/agent-plugins:plugins/lewing
/plugin uninstall lewing
```

From the terminal:
```
copilot plugin install lewing/agent-plugins:plugins/lewing
copilot plugin uninstall lewing
```

Or via marketplace:
```
copilot plugin marketplace add lewing/agent-plugins
copilot plugin install lewing@lewing-public-skills
```

## Skills

### [nuget-trusted-publishing](skills/nuget-trusted-publishing/SKILL.md)

Set up NuGet trusted publishing (OIDC) on a GitHub Actions repo — replaces long-lived API keys with short-lived tokens. USE FOR: trusted publishing, NuGet OIDC, keyless NuGet publish, migrate from NuGet API key, NuGet/login, secure NuGet publishing. DO NOT USE FOR: publishing to private feeds or Azure Artifacts (OIDC is nuget.org only). INVOKES: powershell, edit, create, ask_user for guided repo setup.

**References:**
- [package-types.md](skills/nuget-trusted-publishing/references/package-types.md)
- [publish-workflow.md](skills/nuget-trusted-publishing/references/publish-workflow.md)
