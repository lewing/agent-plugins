# dotnet-performance

Performance benchmarking and micro-benchmark skills for .NET

## Installation

### Copilot CLI / Claude Code

Via marketplace:
```
/plugin marketplace add lewing/agent-plugins
/plugin install dotnet-performance@lewing-public
/plugin update dotnet-performance@lewing-public
```

Or install directly from GitHub (Copilot CLI only):
```
/plugin install lewing/agent-plugins:plugins/dotnet-performance
```

### VS Code (Preview)

Add the marketplace to your VS Code settings:

```jsonc
// settings.json
{
  "chat.plugins.enabled": true,
  "chat.plugins.marketplaces": ["lewing/agent-plugins"]
}
```

Then use `/plugins` in Copilot Chat to browse and install.

## Uninstall

```
# Copilot CLI / Claude Code
/plugin uninstall dotnet-performance@lewing-public

# VS Code: remove the marketplace entry from chat.plugins.marketplaces in settings.json
```

## Skills

### [perf-autofiler-triage](skills/perf-autofiler-triage/SKILL.md)

Parse and triage performance regression issues auto-filed by the dotnet/performance pipeline. USE FOR categorizing regressions by severity, detecting infrastructure artifacts vs real regressions, identifying data gaps, correlating with dotnet/performance repo changes. DO NOT USE FOR running benchmarks (use runtime-performance) or binary analysis (use wasm-binary-analysis).

### [wasm-binary-analysis](skills/wasm-binary-analysis/SKILL.md)

Analyze WebAssembly binaries from .NET WASM builds using wasm-objdump and related tools. USE FOR comparing dotnet.native.wasm across runtime pack versions, verifying SIMD instruction presence, diagnosing execution mode changes, file size forensics. DO NOT USE FOR building WASM apps from scratch or running benchmarks.

## Agents

### [WasmPerfInvestigator](agents/WasmPerfInvestigator.agent.md)

Investigate WASM microbenchmark performance regressions reported by the dotnet/performance auto-filer. USE FOR: triaging perf-autofiling-issues, correlating regressions to runtime or infrastructure changes, bisecting runtime pack versions, reproducing regressions in codespaces, differentiating real regressions from measurement artifacts. DO NOT USE FOR: general .NET performance work (use runtime-performance skill), non-WASM regressions, writing benchmarks from scratch.
