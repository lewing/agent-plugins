# lewing

Personal skills by @lewing — stealth Squad setup, NuGet trusted publishing

## Installation

Via marketplace:
```
/plugin marketplace add lewing/agent-plugins
/plugin install lewing@lewing-public
/plugin update lewing@lewing-public
```

Or install directly from GitHub (Copilot CLI only):
```
/plugin install lewing/agent-plugins:plugins/lewing
```

## Uninstall

```
/plugin uninstall lewing@lewing-public
```

## Skills

### [nuget-trusted-publishing](skills/nuget-trusted-publishing/SKILL.md)

Set up NuGet trusted publishing (OIDC) on a GitHub Actions repo — replaces long-lived API keys with short-lived tokens. USE FOR: trusted publishing, NuGet OIDC, keyless NuGet publish, migrate from NuGet API key, NuGet/login, secure NuGet publishing. DO NOT USE FOR: publishing to private feeds or Azure Artifacts (OIDC is nuget.org only).

**References:**
- [package-types.md](skills/nuget-trusted-publishing/references/package-types.md)
- [publish-workflow.md](skills/nuget-trusted-publishing/references/publish-workflow.md)

### [stealth-squad](skills/stealth-squad/SKILL.md)

Set up a stealth Squad on any repo without modifying tracked files — side-repo + symlinks + git exclude. USE FOR: stealth Squad, hidden Squad, Squad without committing, Squad on a repo I don't own, Squad symlink setup, try Squad without touching repo, consulting Squad. DO NOT USE FOR: normal Squad setup (just run npx github:bradygaster/squad directly), using Squad after setup (just use @squad agent).
