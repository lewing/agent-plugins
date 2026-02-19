# Workload Manifest Structural Patterns

How workload manifests are organized across dotnet repos, and how to discover and handle them during a version bump.

## Discovery

Find all workload manifest families in the current repo:

```bash
# Find all manifest directories
find . -type d -name "*.Current.Manifest" 2>/dev/null

# Find the registration file
find . -name "manifest-packages.*" -type f 2>/dev/null
```

Each `*.Current.Manifest` directory represents a **manifest family** that needs a frozen version created.

## Manifest Family Pattern

Each family follows the same structure:
- `{Family}.Current.Manifest/` — active development version (represents netN+1 after bump)
- `{Family}.netN.Manifest/` — frozen manifests for each supported previous version
- Some families may also have `{Family}.Current.Transport.Manifest/` — these are Current-only and do NOT get frozen

### How to identify what gets frozen

Look at the existing frozen manifests. If `{Family}.netN-1.Manifest` exists, you need to create `{Family}.netN.Manifest`.

If only `{Family}.Current.Manifest` exists with no frozen versions, check with the user whether a frozen version is needed.

## Registration Patterns

Two patterns exist for registering manifests:

### Explicit list (must add manually)
```xml
<ProjectReference Include="Family.Current.Manifest\Family.Current.Manifest.pkgproj" />
<ProjectReference Include="Family.net9.Manifest\Family.net9.Manifest.pkgproj" />
<!-- Add new frozen manifest here -->
```

### Wildcard glob (auto-discovered)
```xml
<ProjectReference Include="**\*.Manifest.proj" />
```

**How to tell:** Read the `manifest-packages.*` file. If it has explicit entries, you must add one. If it uses a wildcard, no registration needed.

## Current.Manifest vs Frozen Manifest Files

**Current.Manifest** has the full set of files — it's the complete active manifest:
- Project file (`.pkgproj`, `.proj`, or `.csproj`)
- `WorkloadManifest.json.in`
- `WorkloadManifest.targets.in`
- May include: `WorkloadManifest.Wasi.targets.in`, `WasmFeatures.props`, `WorkloadTelemetry.targets`
- May include: `localize/` (14 locale files)

**Frozen manifests** are minimal — they contain only:
- Project file
- `WorkloadManifest.json.in`
- `WorkloadManifest.targets.in`
- `localize/` (if the previous frozen manifest has it)

> ⚠️ **Frozen manifests do NOT include** extra files like `WasmFeatures.props`, `WorkloadTelemetry.targets`, or `Wasi.targets.in`. Those belong only in Current.Manifest.

**How to determine the frozen set:** Look at `{Family}.netN-1.Manifest/` — your new frozen manifest should have exactly the same file set with updated version numbers.

## Project File Template Variables

Manifest project files use `GenerateFileFromTemplate` with template variables. To understand what variables a frozen manifest needs:

1. Read `{Family}.netN-1.Manifest/{Family}.netN-1.Manifest.{ext}` — it has the exact variable set
2. Update version-specific values (e.g., `NetVersion`, `RuntimeVersion`, version feature properties)
3. Note: Current.Manifest has additional variables for all previous versions (e.g., `RuntimeVersionNet9`, `RuntimeVersionNet10`) — the new frozen version needs to be added to Current's variable list

### Common variable patterns:

```xml
<!-- Mono.Toolchain frozen manifest -->
<_WorkloadManifestValues Include="NetVersion" Value="netN" />
<_WorkloadManifestValues Include="WorkloadVersion" Value="$(PackageVersion)" />
<_WorkloadManifestValues Include="RuntimeVersion" Value="N.0.$(VersionFeatureN00ForWorkloads)" />

<!-- Emscripten frozen manifest (if present) -->
<_WorkloadManifestValues Include="NetVersion" Value="netN" />
<_WorkloadManifestValues Include="WorkloadVersion" Value="$(PackageVersion)" />
<_WorkloadManifestValues Include="EmsdkVersion" Value="N.0.$(VersionFeatureN00ForWorkloads)" />
<_WorkloadManifestValues Include="EmscriptenVersion" Value="$(EmscriptenVersionNetN)" />
```

## Known Manifest Families

These are the manifest families found in major dotnet repos (as of .NET 11):

### dotnet/runtime
- `Microsoft.NET.Workload.Mono.Toolchain` — project files use `.pkgproj`

### dotnet/sdk
- `Microsoft.NET.Workload.Mono.Toolchain` — project files use `.proj`
- `Microsoft.NET.Workload.Emscripten` — project files use `.proj`
- `Microsoft.NET.Workload.Emscripten.Current.Transport` — Current-only, NOT frozen

> 💡 This list may not be exhaustive. Always discover manifest families dynamically rather than relying on this list.

## Build Validation

After creating frozen manifests, validate:

```bash
# Find the manifest build target
# dotnet/runtime:
./build.sh mono.manifests

# Other repos: look for manifest-specific build targets, or:
dotnet restore <manifest-packages-file>
dotnet build <manifest-packages-file>
```

## Reference PRs

### dotnet/runtime
- .NET 10 → 11: https://github.com/dotnet/runtime/pull/121853
- .NET 9 → 10: https://github.com/dotnet/runtime/pull/106599

### dotnet/sdk
- Search the main branch history for "TFM bump" or version update PRs
