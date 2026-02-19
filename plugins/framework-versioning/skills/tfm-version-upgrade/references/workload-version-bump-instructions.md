# Workload Version Bump Instructions

Specific instructions for updating workload-related files when bumping the .NET major version. These complement the general version bump instructions.

## Key Principle

When you encounter lists with "Current + previous versions", update them to follow:
- **Current (N+1)** + **N** + previous versions (N-1, N-2, etc.)

**Example** (bumping to net12): Before: Current, net10, net9, net8 → After: Current (now net12), net11, net10, net9, net8

The previous "Current" (net11) becomes an explicit version entry, and Current now represents net12.

## Prerequisites

1. Core version properties must be updated first (`eng/Versions.props`, `Directory.Build.props`)
2. The frozen workload manifest is **created as part of this process**

---

## Creating the Frozen Manifest (netN.Manifest)

> ⚠️ **When bumping from .NET N to .NET N+1, you create a frozen manifest for netN (the previous Current version), NOT netN+1.**

Example: When bumping to .NET 12 (where Current becomes net12), you create the **net11** frozen manifest.

### Step 1: Create Directory Structure

**New Directory:** Create `{Family}.netN.Manifest/` alongside the existing `{Family}.Current.Manifest/` and `{Family}.netN-1.Manifest/` directories.

> 📖 See [workload-manifest-patterns.md](workload-manifest-patterns.md) for how to discover manifest locations and determine the correct file set.

1. Create the directory
2. Copy the project file from `netN-1.Manifest` and update:
   - Package name to include `netN`
   - Version property references (`PackageVersionNetN-1` → `PackageVersionNetN`)
   - Verify the structure matches `netN-1.Manifest` (same elements and conditions)
3. Copy `localize/` directory from `netN-1.Manifest` (if present)

**Example:** When bumping to net12, create `net11.Manifest/`, copy `.pkgproj` from `net10.Manifest`

> ⚠️ **Always diff the new `.pkgproj` against `netN-1.Manifest/.pkgproj`** to ensure only version numbers changed, not structure.

### Step 2: Create WorkloadManifest.json.in

**Source:** Copy from `Current.Manifest/WorkloadManifest.json.in`
**Pattern Reference:** Use `netN-1.Manifest/WorkloadManifest.json.in` to understand the naming pattern

**Transformations:**
1. Copy `Current.Manifest/WorkloadManifest.json.in`
2. Study `netN-1.Manifest/WorkloadManifest.json.in` for the frozen manifest pattern
3. Update workload IDs to add `-netN` suffix:
   - `wasm-tools` → `wasm-tools-netN`
   - `wasm-experimental` → `wasm-experimental-netN`
   - `wasi-experimental` → `wasi-experimental-netN`
   - All other workload IDs get `-netN` suffix (e.g., `mobile-librarybuilder-netN`)
4. Update descriptions to reference ".NET N.0"
5. Replace `${NetVersion}` with `netN` throughout
6. Replace `${PackageVersion}` with `${PackageVersionNetN}` throughout

### Step 3: Create WorkloadManifest.targets.in

> ⚠️ **This is the most critical transformation. Getting this wrong breaks workload installation.**

**What to include — ONLY the TFM-specific conditional logic:**
- The section after `<!-- start of TFM specific logic, make sure every node has a TargetsCurrent/TargetsNet* condition -->` in Current.Manifest
- `ImportGroup` with conditions like `TargetsCurrent`
- SDK package imports for runtime packs, AOT cross-compilers, WebAssembly SDK
- Version-specific property settings

**What NOT to include:**
- ❌ The general/shared section at the top of Current.Manifest's `.targets.in` (before TFM-specific logic)
- ❌ Telemetry files (`Microsoft.NET.Sdk.WebAssembly.Pack.Telemetry.*.targets.in`)
- ❌ Settings files (`Microsoft.NET.Sdk.WebAssembly.Pack.Settings.*.targets.in`)
- ❌ Multi-version selection logic or conditional logic spanning multiple TFMs

**Rationale:** All workload manifests (Current, netN+1, netN, etc.) are loaded simultaneously by the SDK. Non-TFM-specific logic should only exist in Current.Manifest to avoid duplication.

> ⚠️ **Check the general/shared section too.** Sometimes Current.Manifest has items in the shared section (e.g., `KnownWebAssemblySdkPack`, `KnownAppHostPack` updates) that should be **frozen into the TFM-specific section** of the new manifest. Compare what the shared section references against what the TFM-specific section provides — if the shared section uses a property or item update that applies to the current TFM, it may need a TFM-conditioned copy in the frozen manifest.

**Transformations to apply:**
1. Copy everything from `<!-- start of TFM specific logic -->` to end of Current.Manifest's `.targets.in`
2. Scan the shared/general section for items that reference `$(_RuntimePackInWorkloadVersionCurrent)` or `${NetVersion}` — if they set pack versions (e.g., `KnownWebAssemblySdkPack`), add TFM-conditioned equivalents to the frozen manifest
3. Use `netN-1.Manifest/WorkloadManifest.targets.in` as structural reference — the new file should have a similar shape, but **may have new content** if Current.Manifest gained features since the last freeze
4. Change `TargetsCurrent` → `TargetsNetN`
5. Replace `${NetVersion}` → `netN`
6. Update SDK package references from `.${NetVersion}` → `.netN`
7. Replace `$(_RuntimePackInWorkloadVersionCurrent)` → `$(_RuntimePackInWorkloadVersionN)`
8. Update TFM references from `${NetVersion}.0` → `netN.0`
9. Ensure all `ImportGroup` and `PropertyGroup` nodes have explicit `TargetsNetN` conditions

> ⚠️ **Diff the result against `netN-1.Manifest/WorkloadManifest.targets.in`** — the new file should have a similar structure with version numbers changed. If it has **more** content than netN-1, verify the additions correspond to items from Current.Manifest's shared section that need freezing. If it has **less** content, investigate — you may have dropped something.

### Step 4: Update Localized Resources

**Source:** Copy from `netN-1.Manifest/localize/` directory

**Files:** All 14 locale files (cs, de, en, es, fr, it, ja, ko, pl, pt-BR, ru, tr, zh-Hans, zh-Hant)

**Changes:**
- `wasm-tools-netN-1` → `wasm-tools-netN`
- `wasm-experimental-netN-1` → `wasm-experimental-netN`
- `wasi-experimental-netN-1` → `wasi-experimental-netN` (if present)
- Version references: ".NET N-1.0" → ".NET N.0" (in all languages)

### Step 5: Register the New Manifest

Check the manifest registration file (found via `find . -name "manifest-packages.*"`):

- **If explicit list** — add a `ProjectReference` entry for the new frozen manifest after Current.Manifest
- **If wildcard glob** — no registration needed; manifests are auto-discovered

Keep all previous version manifests (netN-1, netN-2, etc.) for backwards compatibility.

> ❌ **Never remove old manifest entries.** Previous version manifests are needed for backwards compatibility — users may still target older TFMs.

---

## Updating Existing Workload Files

### 1. SDK Band Version (eng/Versions.props)

```xml
<!-- N.0.100 → N+1.0.100 -->
<SdkBandVersionForWorkload_FromRuntimeVersions>N+1.0.100</SdkBandVersionForWorkload_FromRuntimeVersions>
```

### 2. AutoImport Files

Discover with: `find . -name "AutoImport.props" -path "*/Sdk/*" 2>/dev/null`

Update version-specific conditions and SDK references within these files (not in manifest directories — manifests don't have AutoImport.props).

### 3. Current.Manifest WorkloadManifest.targets.in

Update TFM conditions that referenced the previous version to now reference the new current version. The shared/general logic stays as-is.

---

## Testing Infrastructure Updates

### 1. Workload Testing Configuration (eng/testing/workloads-testing.targets)

- Baseline SDK channel: `--channel N.0` → `--channel N+1.0`
- Shared framework channel: `-Channel N-1.0` → `-Channel N.0`

### 2. Workload Browser/WASI Testing Targets

Discover with: `find eng/testing/ -name "workloads-*.targets" 2>/dev/null`

For each workload-specific targets file (e.g., browser, wasi), add netN entries after the netN-1 entry, following the same pattern as existing entries:

```xml
<!-- Example pattern for browser targets -->
<WorkloadIdForTesting Include="wasm-tools-netN;wasm-experimental-netN"
                      ManifestName="Microsoft.NET.Workload.Mono.ToolChain.netN"
                      Variant="netN"
                      Version="$(PackageVersionForWorkloadManifests)" />

<WorkloadCombinationsToInstall Include="netN" Variants="netN"
                               Condition="'$(WorkloadsTestPreviousVersions)' == 'true'" />
```

---

## Template Updates

Discover templates: `find . -name "template.json" -path "*/.template.config/*" 2>/dev/null`

**Changes in each template config:**
```json
// Identity
"identity": "...N" → "...N+1"

// Framework choice
"displayName": "Current (.NET N)" → "Current (.NET N+1)"
"defaultValue": "netN.0" → "netN+1.0"
```

Discover template project files: `git grep -l "PackageId.*netN\|PackageId.*\.N" -- "**/*.csproj"`

Update `<PackageId>` version references from N to N+1.

---

## Test Projects

### 1. Test Base Classes

Discover with: `git grep -l "TargetMajorVersion" -- "**/*BuildTestBase*"`

```csharp
public const int TargetMajorVersion = N;
// →
public const int TargetMajorVersion = N+1;
```

### 2. Test Project Files

Add new runtime pack version environment variable:
```xml
<EnvironmentVariables Include="RUNTIME_PACK_VERN+1=$(MicrosoftNETCoreAppRuntimewinx64PackageVersion)" />
```

Keep existing versions (VERN, VERN-1, etc.) for compatibility.

### 3. Test Assets

Discover with: `git grep -l "TargetFramework.*netN\.0" -- "**/*.csproj"`

```xml
<TargetFramework>netN.0</TargetFramework>
<!-- → -->
<TargetFramework>netN+1.0</TargetFramework>
```

---

## Files That Should NOT Be Changed

### eng/Version.Details.xml

This file contains package dependency versions (all the `N.0.0-alpha.1.XXXXX` entries). These are **automatically updated** by the Arcade build system (Darc/Maestro). **Never modify manually.**

### eng/testing/workloads-wasm.targets

Provides shared functionality for workload testing. Does not define `WorkloadIdForTesting` items. No changes needed.

---

## Version Number Formats

| Format | Example | Usage |
|--------|---------|-------|
| TFM | `netN.0` | TargetFramework, conditions |
| Package Version | `N.0.0` | NuGet packages |
| SDK Band | `N.0.100` | Workload manifests |
| Channel | `N.0` | SDK install channels |

---

## Verification Checklist

- [ ] Frozen manifest directory created (netN.Manifest) with same file set as netN-1.Manifest
- [ ] Frozen manifest `.pkgproj` has correct package name and version properties
- [ ] Frozen manifest `.targets.in` contains only TFM-specific logic (diff against netN-1 for validation)
- [ ] Frozen manifest registered in `manifest-packages.proj`
- [ ] All template.json files updated (3 files)
- [ ] All test assets target netN+1.0
- [ ] Test base classes have `TargetMajorVersion = N+1`
- [ ] Workload manifest references use N+10100 band (e.g., `120100`)
- [ ] WebAssembly SDK AutoImport.props files updated
- [ ] workloads-testing.targets uses N+1.0 and N.0 channels
- [ ] workloads-browser.targets includes netN WorkloadIdForTesting entry
- [ ] workloads-wasi.targets includes netN WorkloadIdForTesting entry
- [ ] Test .csproj files have RUNTIME_PACK_VERN+1 environment variable
- [ ] Template project uses correct PackageId
- [ ] Build command completes with exit code 0
