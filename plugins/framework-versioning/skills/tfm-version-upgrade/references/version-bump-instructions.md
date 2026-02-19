# General Version Bump Instructions

Detailed instructions for each category of changes when bumping a dotnet repo's major version from .NET N to .NET N+1. These patterns apply universally across dotnet repos — specific file paths are discovered dynamically.

## General Strategy

Use the current framework version as a sentinel value to find all locations needing updates:

```bash
git grep -i "netN"          # TFM references
git grep "N\.0"             # Version number references
git grep "TargetsNetN"      # Workload target conditions
git grep "N0100"            # SDK band version references
```

**Important:** Not all references to version N should change — some intentionally refer to the previous version for compatibility or baseline testing.

---

## 1. Core Version Properties

**File:** `eng/Versions.props`

- `<MajorVersion>` N → N+1
- `<ProductVersion>` N.0.0 → N+1.0.0
- Create `<PackageVersionNetN>` with .NET N's final version
- Update previous version calculations (`PackageVersionNetN-1`, etc.)
- Reset pre-release: `<PreReleaseVersionLabel>` to `alpha`, `<PreReleaseVersionIteration>` to `1`
- Update workload manifest references: all `N0100` → `N+10100` (SDK band versions)
  - Example: `MicrosoftNETWorkloadEmscriptenCurrentManifest110100TransportVersion` → replace `110100` with `120100`

---

## 2. Target Framework Monikers

**File:** `Directory.Build.props`

- `<NetCoreAppCurrentVersion>`: N.0 → N+1.0
- `<NetCoreAppPrevious>`: Clear or set to netN.0
- `<NetCoreAppMinimum>`: netN-1.0 → netN.0
- `<ApiCompatNetCoreAppBaselineVersion>`: N-1.0.0 → N.0.0
- `<ApiCompatNetCoreAppBaselineTFM>`: netN-1.0 → netN.0

---

## 3. Target Framework References

**File:** `Directory.Build.targets`

- Update hardcoded `netN.0` references to `netN+1.0`
- Update conditional imports: `TargetsNetN` → `TargetsNetN+1`
- Update SDK import paths: `Microsoft.NET.Runtime.MonoTargets.Sdk.netN` → `.netN+1`
- Update AOT cross-compiler references: `Microsoft.NETCore.App.Runtime.AOT.Cross.netN.browser-wasm` → `.netN+1.browser-wasm`

---

## 4. Workload Manifest (Frozen) and Related Files

See [workload-version-bump-instructions.md](workload-version-bump-instructions.md) for the complete frozen manifest creation process — this is the most complex and error-prone part of the version bump.

Key files to discover and update:
- Manifest registration file (e.g., `manifest-packages.proj` or `manifest-packages.csproj`) — register new frozen manifest if explicit list
- `eng/Versions.props` — SDK band version (`SdkBandVersionForWorkload_FromRuntimeVersions`)
- Workload testing targets under `eng/testing/` — baseline and shared framework channels, workload ID entries

> 💡 **dotnet/runtime example paths:** `src/mono/nuget/manifest-packages.proj`, `eng/testing/workloads-testing.targets`, `eng/testing/workloads-browser.targets`, `eng/testing/workloads-wasi.targets`

---

## 5. Workload Testing Configuration

Discover workload testing targets: `find eng/testing/ -name "workloads-*.targets" 2>/dev/null`

Common updates:
- Update baseline SDK channel: `--channel N.0` → `--channel N+1.0`
- Update shared framework channel: `-Channel N-1.0` → `-Channel N.0`
- Add netN to `WorkloadIdForTesting` item groups
- Add netN to `WorkloadCombinationsToInstall`

Discover AutoImport files: `find . -name "AutoImport.props" -path "*/Sdk/*" 2>/dev/null`

Common updates in AutoImport.props:
- Update `TargetsNetN` → `TargetsNetN+1` conditions
- Update `_RuntimePackInWorkloadVersionN` → `_RuntimePackInWorkloadVersionN+1`

> 💡 **dotnet/runtime example paths:** `eng/testing/workloads-browser.targets`, `eng/testing/workloads-wasi.targets`, `src/mono/nuget/Microsoft.NET.Runtime.WebAssembly.Sdk/Sdk/AutoImport.props`

---

## 6. Project Template Updates

Discover template configs: `find . -name "template.json" -path "*/.template.config/*"`

Changes in each template config:
- `"identity"`: Update version number N.0 → N+1.0
- `symbols/Framework/choices`: Update `"choice"`, `"description"`, `"displayName"` from netN.0/.NET N to netN+1.0/.NET N+1
- `"defaultValue"`: netN.0 → netN+1.0

Discover template project files: `git grep -l "PackageId.*netN" -- "**/*.csproj"`

- Update `<PackageId>` from netN to netN+1

> 💡 **dotnet/runtime example paths:** `src/mono/wasm/templates/templates/browser/.template.config/template.json`, `src/mono/wasm/templates/Microsoft.NET.Runtime.WebAssembly.Templates.csproj`

---

## 7. Test Asset Project Files

Discover test assets: `git grep -l "TargetFramework.*netN\.0" -- "**/*.csproj"`

Change: `<TargetFramework>netN.0</TargetFramework>` → `<TargetFramework>netN+1.0</TargetFramework>`

> 💡 **dotnet/runtime examples:** `src/mono/wasm/testassets/BlazorBasicTestApp/`, `src/mono/wasm/testassets/WasmBasicTestApp/`, `src/mono/wasm/testassets/WasmOnAspNetCore/`, `src/mono/wasm/testassets/LibraryMode/`

---

## 8. Build Configuration Files

Discover build config files with version refs: `git grep -l "_NetCoreAppToolCurrent\|ProductVersion.*N\.0" -- "**/*.props" "**/package.json"`

Common patterns:
- `_NetCoreAppToolCurrent`: `netN.0` → `netN+1.0`
- `ProductVersion`: `N.0.0-dev` → `N+1.0.0-dev`

> 💡 **dotnet/runtime examples:** `src/mono/wasm/build/WasmApp.LocalBuild.props`, `src/mono/msbuild/apple/build/AppleBuild.LocalBuild.props`, `src/native/package.json`

---

## 9. Documentation Updates

Discover docs with version refs: `git grep -rl "netN\.0\|\.NET N\b" -- "docs/" "**/*.md"`

Update version references in all docs found. Check NativeAOT-specific docs too.

> 💡 **dotnet/runtime examples:** `docs/coding-guidelines/adding-api-guidelines.md`, `docs/project/dogfooding.md`, `docs/workflow/building/libraries/README.md`, `src/coreclr/nativeaot/docs/compiling.md`

---

## 10. Pipeline and Engineering Files

Discover with: `git grep -l "netN\.0\|N0100" -- "eng/**/*.targets" "eng/**/*.props" "eng/**/*.yml" "eng/**/*.csproj"`

Common patterns:
- TFM-specific rules in pruning/targeting targets
- Version refs in pipeline templates
- Test templates with hardcoded TFMs

> 💡 **dotnet/runtime examples:** `eng/pruning.targets`, `eng/targetingpacks.targets`, `eng/testing/linker/project.csproj.template`, `eng/testing/tests.wasm.targets`

---

## 11. Compatibility Suppressions

Discover with: `git grep -l "netN\.0" -- "**/*CompatibilitySuppressions*.xml"`

Update `<Left>ref/netN.0/` → `ref/netN+1.0/` and `<Right>lib/netN.0/` → `lib/netN+1.0/` patterns.

---

## 12. API Compatibility Baselines

Discover with: `find . -path "*/apicompat/*Baseline*" -type f 2>/dev/null`

Review and update baseline suppressions. May need regeneration after version bump.

---

## 13. Test Infrastructure

Discover with:
```bash
git grep -l "TargetMajorVersion" -- "**/*BuildTestBase*"
git grep -l "RUNTIME_PACK_VER" -- "**/*.csproj"
```

Common updates:
- `TargetMajorVersion` constants in test base classes: N → N+1
- Add `RUNTIME_PACK_VERN+1` environment variables in test `.csproj` files
- Default framework refs in template test files

---

## 14. Native Managed Code

Discover with: `git grep -l "UseLocalTargetingRuntimePack\|netN\.0" -- "src/native/**/*.props" 2>/dev/null`

Check output path configurations and TFM references.

---

## 15. Tooling Updates

Discover with: `git grep -l "netN\.0\|N\.0" -- "src/tools/**/*.csproj" "src/tools/**/*.cs" 2>/dev/null`

Check for TFM updates and compiler warning suppressions. Note: some tools may intentionally target older TFMs for compatibility.

---

## 16. Scripts with Hardcoded Versions

Discover with: `git grep -l "N\.0\|netN" -- "**/*.py" "**/*.ps1" "**/*.sh" 2>/dev/null | head -20`

Check framework version references in test scripts, benchmark scripts, and automation scripts.

---

## Systematic Search and Replace Patterns

Use these to catch remaining references after completing all categories:

| Pattern | Replacement | Context |
|---------|-------------|---------|
| `netN.0` | `netN+1.0` | TFM references |
| `N.0` | `N+1.0` | Version contexts |
| `.NET N` | `.NET N+1` | Display strings |
| `NetN` | `NetN+1` | Constants and variables |
| `TargetsNetN` | `TargetsNetN+1` | Build conditions |
| `VersionN` | `VersionN+1` | Version properties |
| `N0100` | `N+10100` | SDK band versions (e.g., `110100` → `120100`) |
| `Manifest-N` | `Manifest-N+1` | Manifest identifiers |

---

## Special Considerations

1. **Blazor Template Version:** Blazor templates might initially target a different version than the main framework. Check `DefaultTargetFrameworkForBlazorTemplate` usage.

2. **Minimum Version:** When updating `NetCoreAppMinimum`, consider projects needing older runtime support (like `WasmSymbolicator.csproj`).

3. **Previous Version Support:** `NetCoreAppPrevious` might be intentionally cleared during early development and set later when the previous version is finalized.

4. **Workload Manifest Timing:** The new workload manifest might not be fully functional until SDK changes are also merged in dotnet/sdk.

5. **API Compatibility:** Some compatibility suppressions can be cleaned up after the version bump, removing obsolete netN-1.0 references.

6. **Test Filtering:** Some tests might need `[ActiveIssue]` attributes if they depend on external services not yet updated.

---

## Order of Operations

> ℹ️ This mirrors the phase structure in SKILL.md. See SKILL.md for phase gate criteria and anti-patterns.

1. **Phase 1** — Core Version Updates (`eng/Versions.props`, `Directory.Build.props`, `Directory.Build.targets`)
2. **Phase 2** — Workload Infrastructure (frozen manifest, manifest references, workload testing targets)
3. **Phase 3** — Project Files (test assets, templates, build configs)
4. **Phase 4** — Testing Infrastructure (test constants, workload testing targets, env vars)
5. **Phase 5** — Documentation (docs/, NativeAOT docs)
6. **Phase 6** — Verification (run search commands, build, review remaining references)

**Reference PRs:**
- .NET 10 → 11: https://github.com/dotnet/runtime/pull/121853
- .NET 9 → 10: https://github.com/dotnet/runtime/pull/106599
