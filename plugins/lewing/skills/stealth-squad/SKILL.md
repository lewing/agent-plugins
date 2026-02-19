---
name: stealth-squad
description: >
  Set up a stealth Squad on any repo without modifying tracked files — side-repo + symlinks + git exclude.
  USE FOR: stealth Squad, hidden Squad, Squad without committing, Squad on a repo I don't own,
  Squad symlink setup, try Squad without touching repo, consulting Squad.
  DO NOT USE FOR: normal Squad setup (just run npx github:bradygaster/squad directly),
  using Squad after setup (just use @squad agent).
---

# Stealth Squad Setup

Set up a **stealth** [Squad](https://github.com/bradygaster/squad) on any repo — one that works locally but is invisible to git and your teammates. Uses a side-repo + symlinks + git exclude pattern so no tracked files are modified.

This is for when you want Squad's AI team on a repo you don't own or where committing Squad files isn't appropriate (consulting, trials, shared codebases). For repos you do own, just run `npx github:bradygaster/squad` directly.

## Prerequisites

- **Node.js / npm** — needed for `npx github:bradygaster/squad`
- **Symlink permissions** — on Windows, requires **Developer Mode enabled** or an **Administrator terminal**

## When to Use This Skill

Use this skill when:
- Setting up Squad on a repo the user doesn't fully own
- Trying Squad on a consulting engagement or client repo
- Asked to install Squad without touching `.gitignore` or tracked files
- Wanting Squad but can't commit experimental framework files
- Reconnecting an existing stealth Squad to a new worktree or fresh clone
- Setting up Squad on a forked repo
- Asked to "symlink Squad" or "try Squad temporarily"

Do **not** use this skill when:
- The user owns the repo and can commit Squad files — just run `npx github:bradygaster/squad` directly

## Architecture

```
C:\repos\my-project\              ← real repo (symlinks only, git-excluded)
C:\repos\squad-for-my-project\    ← side repo (owns all Squad files)
```

Squad requires three things in the working directory:
- `.ai-team/` — team state
- `.ai-team-templates/` — role definitions
- `.github/agents/squad.agent.md` — agent definition

All are symlinked from the side repo so Copilot discovers them, but git ignores them locally.

## Setup Process

### Step 1: Create the Side Repo

Create a separate repo next to the target project and initialize Squad there:

> ❌ **Never run `npx github:bradygaster/squad` in the target repo** — always initialize in the side repo first, then symlink.

```powershell
$projectDir = "C:\repos\my-project"
$squadDir = "C:\repos\squad-for-my-project"

New-Item -ItemType Directory -Path $squadDir -Force
Set-Location $squadDir
git init
npx github:bradygaster/squad
git add -A && git commit -m "Initialize Squad team"
```

> ⚠️ Ask the user for both paths before running anything.

On macOS/Linux, the same flow works — just use `ln -s` instead of `New-Item -ItemType SymbolicLink` in Step 2.

### Step 2: Create Symlinks

> ❌ **Never commit symlinks** — they must be git-excluded (Step 3) before any `git add`.

```powershell
Set-Location $projectDir

# Directory symlinks
New-Item -ItemType SymbolicLink -Path ".ai-team" -Target "$squadDir\.ai-team"
New-Item -ItemType SymbolicLink -Path ".ai-team-templates" -Target "$squadDir\.ai-team-templates"

# File symlink for agent definition
New-Item -ItemType Directory -Path ".github\agents" -Force
cmd /c mklink ".github\agents\squad.agent.md" "$squadDir\.github\agents\squad.agent.md"
```

> ⚠️ **Windows**: Requires Administrator terminal or Developer Mode enabled for symlink creation.

### Step 3: Add Git Exclude Entries

> ❌ **Never modify `.gitignore`** — that's a tracked file visible to all collaborators. Use `.git/info/exclude` exclusively.

Use `.git/info/exclude` (local-only, never committed) instead of `.gitignore`:

```powershell
Add-Content -Path ".git\info\exclude" -Value @"

# Squad (symlinked from side repo)
.ai-team
.ai-team/
.ai-team-templates
.ai-team-templates/
.github/agents/squad.agent.md
"@
```

> ⚠️ **Both forms needed on Windows**: Git with `core.symlinks=true` sees directory symlinks as files, so `.ai-team/` alone won't match. Include both `.ai-team` and `.ai-team/`.

> ⚠️ **Git worktrees**: `.git/info/exclude` lives in the main repo's `.git` directory, not in the worktree. Entries apply to all worktrees.

### Step 4: Verify

```powershell
git check-ignore -v .ai-team .ai-team-templates .github/agents/squad.agent.md
# Should show .git/info/exclude as the source for each entry

git status
# Squad files should NOT appear
```

## Reconnecting an Existing Stealth Squad

When the user already has a Squad side repo (e.g., after re-cloning, switching to a new worktree, or setting up on a second machine), skip Step 1 and run Steps 2–4 with the existing side repo path.

> ⚠️ Ask the user for the path to their existing Squad repo before proceeding. The git exclude append is idempotent — check for existing entries before writing:

```powershell
$exclude = ".git\info\exclude"
if (-not (Select-String -Path $exclude -Pattern "\.ai-team" -Quiet -ErrorAction SilentlyContinue)) {
    # append exclude entries per Step 3
}
```

## Teardown

To remove Squad without affecting the repo:

```powershell
# Remove symlinks (not the targets)
Remove-Item ".ai-team"
Remove-Item ".ai-team-templates"
Remove-Item ".github\agents\squad.agent.md"

# Optionally clean up empty dirs
Remove-Item ".github\agents" -ErrorAction SilentlyContinue
```

The side repo remains intact for reuse.
