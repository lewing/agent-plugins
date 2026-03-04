---
name: wasm-binary-analysis
description: Analyze WebAssembly binaries from .NET WASM builds using wasm-objdump and related tools. USE FOR comparing dotnet.native.wasm across runtime pack versions, verifying SIMD instruction presence, diagnosing execution mode changes, file size forensics. DO NOT USE FOR building WASM apps from scratch or running benchmarks.
---

# WASM Binary Analysis

Analyze WebAssembly binaries produced by .NET WASM builds to verify SIMD support, compare versions, and diagnose regressions at the binary level.

## Prerequisites

| Requirement | Details |
|---|---|
| `wabt` | WebAssembly Binary Toolkit — install via `npm install -g wabt` or `apt install wabt` |
| .NET SDK | With `wasm-tools` workload installed (`dotnet workload install wasm-tools`) |
| Runtime packs | Downloaded from NuGet or present in SDK `packs/` directory |

## Key Files in a WASM Runtime Pack

The `Microsoft.NETCore.App.Runtime.Mono.browser-wasm` nupkg contains:

```
runtimes/browser-wasm/native/
  dotnet.native.wasm          ← Main native binary (interpreter + runtime + CoreLib AOT)
  dotnet.native.js             ← JS glue code
  dotnet.native.worker.mjs     ← Web worker support
runtimes/browser-wasm/lib/net{X}.0/
  System.Private.CoreLib.dll   ← Managed CoreLib
  System.*.dll                 ← Framework assemblies
```

## Extracting Runtime Packs

### From NuGet (any version)

```bash
# List available versions from the NuGet flat container API
curl -s "https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.runtime.mono.browser-wasm/index.json" | jq '.versions[-10:]'

# Download a specific version
curl -LO "https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.runtime.mono.browser-wasm/{VERSION}/microsoft.netcore.app.runtime.mono.browser-wasm.{VERSION}.nupkg"

# Extract native files
unzip -o *.nupkg -d pack-{VERSION}
```

### From dnceng Azure DevOps feed (preview builds)

```bash
# For preview/daily builds not yet on nuget.org
FEED="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/flat2"
curl -s "$FEED/microsoft.netcore.app.runtime.mono.browser-wasm/index.json" | jq '.versions[-10:]'
```

### From installed SDK

```bash
# Find installed packs
ls $DOTNET_ROOT/packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm/
```

## Analysis Commands

### SIMD Instruction Count

The single most useful diagnostic. If SIMD count drops to 0, SIMD was disabled (build config regression).

```bash
# Count all SIMD (v128) instructions
wasm-objdump -d dotnet.native.wasm | grep -c 'v128\.'

# Typical healthy count: ~900-1000 v128 instructions
# Zero count = SIMD disabled (critical regression)
```

### Compare Two Versions

```bash
# Side-by-side comparison
echo "=== Version A ==="
wasm-objdump -d pack-A/runtimes/browser-wasm/native/dotnet.native.wasm | grep -c 'v128\.'
ls -la pack-A/runtimes/browser-wasm/native/dotnet.native.wasm

echo "=== Version B ==="
wasm-objdump -d pack-B/runtimes/browser-wasm/native/dotnet.native.wasm | grep -c 'v128\.'
ls -la pack-B/runtimes/browser-wasm/native/dotnet.native.wasm
```

### SIMD Instruction Breakdown

```bash
# Breakdown by SIMD instruction type
wasm-objdump -d dotnet.native.wasm | grep 'v128\.' | sed 's/.*: //' | awk '{print $1}' | sort | uniq -c | sort -rn | head -20
```

### Full Disassembly Search

```bash
# Find specific function implementations
wasm-objdump -d dotnet.native.wasm | grep -A 5 'PackedSimd'

# Export function list
wasm-objdump -x dotnet.native.wasm | grep '<.*>' | head -50

# Convert to text format for detailed inspection
wasm2wat dotnet.native.wasm -o dotnet.native.wat
grep -c 'v128' dotnet.native.wat
```

### File Size Comparison

```bash
# Quick size comparison across versions
for d in pack-*/; do
  ver=$(basename "$d" | sed 's/pack-//')
  size=$(stat -f%z "$d/runtimes/browser-wasm/native/dotnet.native.wasm" 2>/dev/null || stat -c%s "$d/runtimes/browser-wasm/native/dotnet.native.wasm")
  echo "$ver: $size bytes ($(echo "scale=1; $size/1048576" | bc)MB)"
done
```

## Interpreting Results

### SIMD Verification Matrix

| v128 count | File size | Diagnosis |
|---|---|---|
| ~900-1000 | ~35-40MB | Healthy — SIMD enabled, normal build |
| 0 | ~30-35MB | **SIMD disabled** — check `WasmEnableSIMD`, `-msimd128` flag |
| ~900-1000 | Significantly larger | Extra code linked — check trimming config |
| Same across versions | Same across versions | No runtime change — regression is infrastructure/methodology |

### Common Build Flags

These flags control WASM binary output in dotnet/runtime:

| Flag | Default | Effect |
|---|---|---|
| `WasmEnableSIMD` | `true` | Enables SIMD intrinsic transform in interpreter |
| `-msimd128` | Set when SIMD enabled | Emscripten flag enabling WASM SIMD |
| `PublishTrimmed` | Varies | Affects binary size and linked code |
| Emscripten version | `3.1.56` (as of .NET 10) | Codegen differences between versions |

### dotnet/runtime Source Locations

| Path | Purpose |
|---|---|
| `src/mono/mono/mini/interp/transform-simd.c` | SIMD intrinsic transform (MINT_SIMD opcodes) |
| `src/mono/mono/mini/interp/interp-simd.c` | SIMD execution |
| `src/mono/mono/mini/interp/interp.h` | `INTERP_OPT_SIMD` flag in `INTERP_OPT_DEFAULT` |
| `src/mono/mono/mini/interp/transform.c` | Main interpreter transform |
| `src/mono/browser/runtime/` | JS interop and browser host |

## Swapping Runtime Packs for Bisection

To test a specific runtime pack version with your current SDK:

```bash
SDK_VERSION=$(dotnet --version)
PACK_DIR="$DOTNET_ROOT/packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm/$SDK_VERSION"

# Backup original
cp -r "$PACK_DIR" "${PACK_DIR}.bak"

# Swap in files from downloaded nupkg
cp pack-{VERSION}/runtimes/browser-wasm/native/* "$PACK_DIR/runtimes/browser-wasm/native/"

# Rebuild your app
dotnet publish -c Release

# Restore original when done
rm -rf "$PACK_DIR"
mv "${PACK_DIR}.bak" "$PACK_DIR"
```

## Codespace-Based Bisection

Shared machines introduce variance that can mask or fabricate small regressions. For definitive bisection, use a dedicated GitHub Codespace.

### Creating the Codespace

```bash
# 16-core gives consistent results; 4-core is too noisy for <1.5x regressions
gh codespace create --repo dotnet/runtime --machine largePremiumLinux \
  --devcontainer-path .devcontainer/devcontainer.json

# IMPORTANT: Use the default devcontainer, NOT .devcontainer/wasm/
# The wasm devcontainer lacks SSHD needed for SSH-based workflows
```

### Environment Setup

The default devcontainer is missing tools needed for WASM work:

```bash
# Node.js (required for WASM benchmark execution)
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt-get install -y nodejs

# Chrome shared libraries (required for browser-wasm tests)
sudo apt-get install -y libglib2.0-0 libnss3 libnspr4 libdbus-1-3 \
  libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libxkbcommon0 \
  libxcomposite1 libxdamage1 libxrandr2 libgbm1 libpango-1.0-0 \
  libcairo2 libasound2

# wabt for binary analysis
npm install -g wabt
```

### Installing Multiple SDK Versions Side-by-Side

```bash
# Install specific SDK versions for comparison
# Use dotnet-install.sh (already in dotnet/runtime repo)
export DOTNET_ROOT=$HOME/.dotnet

# Install baseline SDK
./eng/common/dotnet-install.sh --version 10.0.100-preview.3.25201.16
dotnet workload install wasm-tools

# Install comparison SDK  
./eng/common/dotnet-install.sh --version 10.0.100-preview.4.25258.1
dotnet workload install wasm-tools
```

### Interleaved A/B Testing

Run baseline and comparison alternately to eliminate thermal throttling and background process drift:

```bash
#!/bin/bash
# interleaved-bisect.sh — eliminates systematic drift
BASELINE_PACK="pack-baseline"
COMPARE_PACK="pack-compare"
ROUNDS=5
WARMUP=5
ITERATIONS=10000000

for round in $(seq 1 $ROUNDS); do
  echo "=== Round $round ==="
  
  # Swap in baseline pack
  cp "$BASELINE_PACK/runtimes/browser-wasm/native/"* "$PACK_DIR/runtimes/browser-wasm/native/"
  dotnet publish -c Release -o out-baseline 2>/dev/null
  echo -n "Baseline: "
  node --experimental-vm-modules run-benchmark.mjs out-baseline $WARMUP $ITERATIONS
  
  # Swap in comparison pack  
  cp "$COMPARE_PACK/runtimes/browser-wasm/native/"* "$PACK_DIR/runtimes/browser-wasm/native/"
  dotnet publish -c Release -o out-compare 2>/dev/null
  echo -n "Compare:  "
  node --experimental-vm-modules run-benchmark.mjs out-compare $WARMUP $ITERATIONS
done

# Use median of all rounds, not mean (outlier-resistant)
```

### Interpreting Codespace Results

| Variance (CoV) | Verdict |
|---|---|
| <3% | High confidence — results are reliable |
| 3-7% | Moderate — differences <10% may be noise |
| >7% | Unreliable — check for background processes, try larger machine |

If baseline and comparison results overlap across 5 interleaved rounds → **no runtime regression** (the auto-filed issue is an infrastructure artifact).

## Quick Reference

```bash
# One-liner: "Is SIMD working in this wasm?"
wasm-objdump -d dotnet.native.wasm | grep -c 'v128\.' | xargs -I{} echo "SIMD instructions: {}"

# One-liner: compare two packs
diff <(wasm-objdump -x packA/dotnet.native.wasm) <(wasm-objdump -x packB/dotnet.native.wasm) | head -40
```
