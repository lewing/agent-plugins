---
name: target-new-framework
description: Perform the .NET major version bump (e.g., net11 to net12) in any dotnet repo. Use when asked to "update TFMs", "create workload manifest for new version", "update from netN to netN+1", or "create frozen manifest". Covers eng/Versions.props, Directory.Build.props, workload manifests, templates, test assets, and documentation.
---

# .NET Major Version Bump

Orchestrate the multi-phase process of bumping any dotnet repo from .NET N to .NET N+1. The core patterns (version properties, TFMs, workload manifests) are universal across dotnet repos; repo-specific content (templates, test assets, build configs) is discovered dynamically.

## When to Use This Skill

- Performing a .NET major TFM version bump (e.g., `net11` → `net12`, `net12` → `net13`)
- Creating a frozen workload manifest for the outgoing current version
- Updating `TargetFramework` references across a repository after a major version change
- Completing a partially-started TFM version bump PR

## Execution Guidelines

- **Track all discovered files in SQL.** Use the SQL tool to store every file that needs updating. Insert during Phase 0 discovery, update status as each file is changed, and query for missed files in Phase 6. Do **not** rely on memory to track which files have been updated across phases.
- **Maximize parallel tool calls.** Run independent discovery searches and file edits in parallel.

### SQL Schema

Create this table at the start of every run:

```sql
CREATE TABLE IF NOT EXISTS bump_files (
    path TEXT PRIMARY KEY,
    phase TEXT NOT NULL,        -- 'core-props', 'workloads', 'projects', 'testing', 'docs', 'build-config'
    category TEXT,              -- e.g., 'frozen-manifest', 'template', 'test-asset', 'tfm-ref'
    old_value TEXT,             -- the value being replaced (e.g., 'net11.0')
    new_value TEXT,             -- the replacement value (e.g., 'net12.0')
    status TEXT DEFAULT 'pending',  -- 'pending', 'updated', 'skipped', 'verified'
    notes TEXT                  -- why skipped, or what was changed
);
```

**Workflow:**
1. **Phase 0** — `INSERT` every discovered file with `status='pending'`
2. **Phases 1–5** — After editing a file: `UPDATE bump_files SET status='updated' WHERE path='...'`
3. **Intentional skips** — `UPDATE bump_files SET status='skipped', notes='intentional previous-version ref' WHERE path='...'`
4. **Phase 6** — `SELECT * FROM bump_files WHERE status='pending'` to find anything missed
5. **Progress check** — `SELECT phase, status, COUNT(*) FROM bump_files GROUP BY phase, status` at any time

## Required User Inputs

Ask the user for:
1. **Source version (N)**: Auto-detect from `eng/Versions.props` `<MajorVersion>` if not provided.
2. **Target version (N+1)**: Default: N+1.

## Phase 0: Discovery

Before making changes, understand what this repo contains. Run these searches and note what exists:

```bash
# Core properties (all repos)
grep -l "MajorVersion" eng/Versions.props
grep -l "NetCoreAppCurrentVersion" Directory.Build.props

# Workload manifests (if present)
find . -type d -name "*.Manifest" | head -20

# Manifest registration — explicit list vs wildcard?
find . -name "manifest-packages.*" -exec cat {} \;

# Templates, test assets, docs (repo-specific)
find . -type f -name "template.json" -path "*/.template.config/*" | head -10
git grep -l "TargetFramework.*netN\.0" -- "**/*.csproj" | head -20
```

This determines which phases apply. A small repo may only need Phase 1 + 6. A complex repo like dotnet/runtime needs all phases.

**Insert every discovered file into SQL immediately:**

```sql
-- Example: core props files found
INSERT OR IGNORE INTO bump_files (path, phase, category) VALUES
  ('eng/Versions.props', 'core-props', 'version-props'),
  ('Directory.Build.props', 'core-props', 'tfm-props');

-- Example: workload manifests found
INSERT OR IGNORE INTO bump_files (path, phase, category) VALUES
  ('src/mono/nuget/Microsoft.NET.Workload.Mono.Toolchain.Current.Manifest/...', 'workloads', 'current-manifest');

-- Example: project files from git grep
INSERT OR IGNORE INTO bump_files (path, phase, category) VALUES
  ('src/mono/wasm/testassets/BlazorBasicTestApp/BlazorBasicTestApp.csproj', 'projects', 'test-asset');
```

Continue inserting files discovered in later phases — not all files are found in Phase 0.

## Phase 1: Core Version Properties

Update the primary version numbers that the entire build system depends on.

**Key files (if they exist):**
- `eng/Versions.props` — `<MajorVersion>`, `<ProductVersion>`, SDK band versions (e.g., `110100` → `120100`), workload manifest version properties
- `Directory.Build.props` — `<NetCoreAppCurrentVersion>`, `<NetCoreAppPrevious>`, `<NetCoreAppMinimum>`, `<ApiCompatNetCoreAppBaseline*>`

> ❌ **Never modify `eng/Version.Details.xml`** — it is auto-managed by Arcade/Maestro dependency flow.

> ❌ **Not all version N references should change.** Some intentionally refer to the *previous* version for compatibility or baseline testing.

> ⚠️ **Don't set `NetCoreAppPrevious` too early.** It may be intentionally cleared during early development.

📖 See [references/version-bump-instructions.md](references/version-bump-instructions.md) sections 1-3 for property details.

## Phase 2: Workload Infrastructure

**Skip if no workload manifests found in Phase 0.**

Create a frozen workload manifest for **netN** (the previous Current version) and update workload references.

> ❌ **The frozen manifest is for netN, NOT netN+1.** When bumping to .NET 12, you create the **net11** frozen manifest.

**Dynamic discovery steps:**
1. Find all `*.Current.Manifest` directories — each manifest family needs a frozen version
2. Find the most recent `netN-1.Manifest` in each family — use as template
3. Determine project file type by examining existing manifests (`.pkgproj`, `.proj`, `.csproj`)
4. Check manifest registration: if `manifest-packages.*` uses explicit `ProjectReference` entries, add the new one; if it uses a wildcard glob, no registration needed
5. Look for `localize/` in `netN-1.Manifest` — if present, copy and update version refs

**For each manifest family:**
1. Create `{Family}.netN.Manifest/` directory
2. Create project file — copy from `netN-1.Manifest`, update package name and version properties
3. Create `WorkloadManifest.json.in` — copy from Current.Manifest, apply netN transformations
4. Create `WorkloadManifest.targets.in` — **only the TFM-specific section**, not the shared logic
5. Copy `localize/` from `netN-1.Manifest` if present, update version refs
6. Update Current.Manifest project file to add `RuntimeVersionNetN` / version variable for the newly-frozen version

> ❌ **Do NOT blindly copy the entire shared/general section into the frozen manifest's .targets.in.** Start with the TFM-specific conditional logic (after the `<!-- start of TFM specific logic -->` comment in Current.Manifest), then scan the shared section for items that reference current-version properties — those may need TFM-conditioned equivalents in the frozen manifest.

> ⚠️ **Always diff the new frozen manifest against `netN-1.Manifest`** — structure should be similar, with version numbers changed. New content from Current.Manifest's shared section may legitimately expand the frozen manifest beyond what netN-1 had.

**Gate:** Frozen manifest has same file set as `netN-1.Manifest`.

📖 See [references/workload-version-bump-instructions.md](references/workload-version-bump-instructions.md) for transformation rules and examples.
📖 See [references/workload-manifest-patterns.md](references/workload-manifest-patterns.md) for structural patterns across repos.

## Phase 3: Project Files and Templates

**Discover dynamically** — search for files referencing the old version:

```bash
git grep -l "netN\.0" -- "**/*.csproj" "**/*.fsproj" "**/*.vbproj"
git grep -l "netN\.0" -- "**/.template.config/template.json"
git grep -l "ProductVersion.*N\.0" -- "**/package.json"
```

Update TFM references, template identities/choices/defaults, and PackageId values.

📖 See [references/version-bump-instructions.md](references/version-bump-instructions.md) sections 6-8 for common patterns.

## Phase 4: Testing Infrastructure

**Discover dynamically** — search for test-related version references:

```bash
git grep -rn "TargetMajorVersion.*=.*N" -- "**/*Test*.cs" "**/*BuildTestBase*"
git grep -l "RUNTIME_PACK_VER" -- "**/*.csproj" "**/*.targets"
git grep -l "workloads-.*\.targets" -- "eng/testing/"
```

Update constants, env vars, and workload testing configs found.

📖 See [references/workload-version-bump-instructions.md](references/workload-version-bump-instructions.md) "Testing Infrastructure Updates".

## Phase 5: Documentation

```bash
git grep -rl "netN\.0\|\.NET N\b" -- "docs/" "**/*.md" | head -30
```

Update version references in all documentation files found.

## Phase 6: Verification

> 💡 **Keep the feedback loop tight.** Run the fastest check first, fix, repeat.

#### 6a. Check for missed files (~instant)

```sql
-- Anything still pending?
SELECT path, phase, category FROM bump_files WHERE status = 'pending';

-- Progress dashboard
SELECT phase, status, COUNT(*) as count FROM bump_files GROUP BY phase, status ORDER BY phase, status;
```

If pending files remain, update them or mark as skipped with a reason.

#### 6b. Search for remaining references (~seconds)

> ⚠️ **Always substitute actual version numbers.** Never run `netN.0` literally.

```bash
git grep -i "netN\.0" -- ':!*.md'          # Remaining TFM refs
git grep "N0100" -- '*.props' '*.targets'  # SDK band versions
git grep "TargetsNetN" -- '*.props' '*.targets'  # TFM conditions
```

Review each match — some are intentional previous-version refs.

Any new files found here that weren't in SQL should be inserted and resolved:

```sql
INSERT OR IGNORE INTO bump_files (path, phase, category, notes) VALUES
  ('...', 'build-config', 'tfm-ref', 'found in 6b sweep');
```

#### 6c. Structural diff (~seconds)

Diff each new frozen manifest against `netN-1.Manifest`. Only version numbers should differ.

#### 6d. Build validation (incremental)

**Fix errors at each level before moving to the next:**

| Level | Command | What it catches |
|-------|---------|-----------------|
| 1 | `./build.sh -restore` (or `dotnet restore`) | MSBuild parse errors, missing imports |
| 2 | Manifest-specific build target (if exists) | Workload manifest packaging |
| 3 | Full subset build | Version property propagation |

> ⚠️ **Level 1 must pass before attempting Level 2.**

#### 6e. Cross-reference with prior PRs

Search for the previous version bump PR in the same repo for any missed files.

#### 6f. Final SQL check

```sql
-- Confirm everything is resolved
SELECT COUNT(*) as remaining FROM bump_files WHERE status = 'pending';
-- Should return 0
```

## Definition of Done

- [ ] `eng/Versions.props` `<MajorVersion>` updated to N+1
- [ ] `Directory.Build.props` `<NetCoreAppCurrentVersion>` updated to N+1.0 (if present)
- [ ] Frozen manifest created for each manifest family found (if any)
- [ ] Manifest registration updated (if explicit list, not wildcard)
- [ ] All discovered templates, test assets, and build configs updated
- [ ] No unintended netN references remain in active configuration
- [ ] Each frozen manifest structurally matches `netN-1.Manifest`
- [ ] Restore completes with exit code 0
- [ ] Relevant build targets complete with exit code 0
- [ ] All entries in `bump_files` table are `updated`, `skipped`, or `verified` — none `pending`

## References

- **General version bump patterns**: [references/version-bump-instructions.md](references/version-bump-instructions.md)
- **Workload manifest creation**: [references/workload-version-bump-instructions.md](references/workload-version-bump-instructions.md)
- **Workload manifest structural patterns**: [references/workload-manifest-patterns.md](references/workload-manifest-patterns.md)
