---
name: repo-contributor
description: >
  Guide contributions to this plugin marketplace repository.
  USE FOR: installing skills, syncing assets, diffing installed vs repo,
  checking what's out of date, "how do I install a skill", "sync my skills",
  "what's out of date", "how do I use the CLI".
  DO NOT USE FOR: building skills from scratch (use skill-builder).
---

# Contributing to agent-plugins

This repo is a plugin marketplace. Use the included CLI tool to install and manage skills.

## CLI Tool

Run with `dotnet scripts/plugin-cli.cs -- <command>`.

> **Prerequisite:** .NET 10 SDK (or later).

### Commands

Each asset category (`skills`, `agents`, `prompts`, `instructions`, `mcp`, `settings`) supports:

| Subcommand | What it does |
|------------|--------------|
| `list` | Show what's installed vs what's in the repo |
| `install` | Copy from repo → installed location |
| `uninstall` | Remove repo-managed assets from installed location |
| `diff` | Compare repo vs installed — shows missing, extra, and changed files |

Use `all` to operate across every category: `all list`, `all install`, `all diff`.

### Common Workflows

```powershell
# See what's out of sync
dotnet scripts/plugin-cli.cs -- skills diff --verbose

# Install a specific skill
dotnet scripts/plugin-cli.cs -- skills install --skill ci-analysis --force

# Install everything from the repo
dotnet scripts/plugin-cli.cs -- all install --force

# Dry-run to preview changes
dotnet scripts/plugin-cli.cs -- all install --dry-run

# Full sync: install repo assets AND remove extras not in repo
dotnet scripts/plugin-cli.cs -- all install --exact
```

### Key Options

| Option | Effect |
|--------|--------|
| `--skill <name>` | Filter to one skill |
| `--plugin <name>` | Filter to one plugin group |
| `--scope personal\|project` | Install to `~/.copilot/skills/` (default) or `.github/skills/` |
| `--force` | Overwrite without prompting |
| `--exact` | Full sync — removes installed files not in repo (after backup) |
| `--dry-run` | Preview without changes |
| `--verbose` | Show detailed output |

> 💡 After editing a skill, run `skills install --skill <name> --force` to update your local copy.

## Adding a Skill

Every new skill requires updates to **4 locations**:

| Step | File | Action |
|------|------|--------|
| 1 | `plugins/<group>/skills/<name>/SKILL.md` | Create with `name` + `description` in YAML frontmatter |
| 2 | `plugins/<group>/plugin.json` | Add path to `"skills"` array |
| 3 | `.github/plugin/marketplace.json` | Ensure plugin group is listed |
| 4 | `README.md` | Verify skill appears in Available Plugins |