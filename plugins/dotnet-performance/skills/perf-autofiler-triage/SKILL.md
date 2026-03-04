---
name: perf-autofiler-triage
description: Parse and triage performance regression issues auto-filed by the dotnet/performance pipeline. USE FOR categorizing regressions by severity, detecting infrastructure artifacts vs real regressions, identifying data gaps, correlating with dotnet/performance repo changes. DO NOT USE FOR running benchmarks (use runtime-performance) or binary analysis (use wasm-binary-analysis).
---

# Performance Auto-Filer Triage

Systematically parse and triage performance regression issues created by the dotnet/performance auto-filing system in the `dotnet/perf-autofiling-issues` repository.

## Auto-Filed Issue Structure

Issues in `dotnet/perf-autofiling-issues` follow a standard format:

```
Title: [regression] System.Numerics.Tests.Perf_Vector128<Int32>.Benchmark
Body:
  Benchmark: System.Numerics.Tests.Perf_Vector128<Int32>.Benchmark
  Profile: ...
  Regression: X.XX (ratio)
  Baseline commit: <sha>
  Compare commit: <sha>
  Baseline run: <date>
  Compare run: <date>
  Configuration: CompilationMode=wasm, RunKind=micro, ...
```

### Key Fields to Extract

| Field | Where | Use |
|---|---|---|
| Benchmark name | Title + body | Identifies which test regressed |
| Regression ratio | Body | Severity classification |
| Baseline commit | Body | Starting point for bisection |
| Compare commit | Body | Ending point for bisection |
| Baseline run date | Body | Start of regression window |
| Compare run date | Body | End of regression window |
| Configuration | Body | Runtime flavor, compilation mode, architecture |

### Comments May Contain Grouped Regressions

The auto-filer often groups related regressions into comments on the same issue. **Always read ALL comments** — the issue body may show 1 regression but comments may list 50+.

## Step 1: Parse the Issue

```
1. Fetch the issue with `gh issue view <number> --repo dotnet/perf-autofiling-issues`
2. Fetch ALL comments with `gh issue view <number> --repo dotnet/perf-autofiling-issues --comments`
3. Extract every benchmark name and regression ratio
4. Extract the baseline and compare commit SHAs
5. Extract the date range (baseline run → compare run)
6. Note the configuration (especially CompilationMode, RunKind, Architecture)
```

## Step 2: Categorize by Severity Tier

| Tier | Ratio Range | Meaning | Likely Cause |
|---|---|---|---|
| **Tier 1 (catastrophic)** | >10x | Execution mode change | SIMD disabled, interpreter fallback, measurement methodology change |
| **Tier 2 (significant)** | 1.5x–10x | Algorithmic or optimization change | Code regression, build config change, or methodology diff |
| **Tier 3 (marginal)** | 1.1x–1.5x | Could be noise | Machine variance, minor code change, or infrastructure |

### SQL Tracking Template

```sql
CREATE TABLE IF NOT EXISTS regression_categories (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  issue_number INT,
  benchmark TEXT,
  ratio REAL,
  tier INT,
  category TEXT,      -- 'simd', 'general', 'noise', 'infrastructure'
  status TEXT DEFAULT 'untriaged',  -- 'untriaged', 'investigating', 'infrastructure', 'real', 'noise'
  notes TEXT
);

-- Example categorization query
SELECT tier, category, COUNT(*) as count, 
       ROUND(MAX(ratio), 2) as max_ratio, 
       ROUND(AVG(ratio), 2) as avg_ratio
FROM regression_categories
WHERE issue_number = 69444
GROUP BY tier, category
ORDER BY tier, count DESC;
```

## Step 3: Detect Data Gaps

**Data gaps are the #1 cause of false positives.** A data gap occurs when perf runs are broken for an extended period, then resume with different tooling.

### How to Detect

1. Check the date range: baseline run date vs compare run date
2. If gap > 2 weeks, investigate why runs were missing
3. Common causes:
   - New .NET version broke BDN (`net11.0` bump, `net12.0` bump)
   - Perf machine pool change
   - BDN version upgrade with breaking changes
   - WASM workload not available for new SDK preview

### How to Verify

```bash
# Check dotnet/performance PRs in the gap window
gh pr list --repo dotnet/performance --state merged \
  --search "merged:2025-12-01..2026-03-01" \
  --limit 50 --json number,title,mergedAt

# Look for framework version bumps
gh search prs --repo dotnet/performance "net11 OR net12 OR TFM OR framework" \
  --merged --sort updated --limit 20

# Look for BDN changes
gh search prs --repo dotnet/performance "BenchmarkDotNet OR BDN OR benchmark-main" \
  --merged --sort updated --limit 20
```

## Step 4: Check Infrastructure Changes

Before investigating runtime code, **always check dotnet/performance repo changes first.** Infrastructure changes cause the majority of auto-filed false positives.

### Changes That Cause False Regressions

| Change Type | Impact | How to Detect |
|---|---|---|
| BDN version upgrade | Measurement methodology shift | PR touching `Directory.Build.props` BDN version |
| Entry point change | Different runtime initialization | `test-main.js` → `benchmark-main.mjs` |
| SDK type change | Different build pipeline | `Microsoft.NET.Sdk` → `Microsoft.NET.Sdk.WebAssembly` |
| Trimming config | Different code linked | `PublishTrimmed` changes |
| Machine pool change | Different hardware baseline | Helix queue changes in YAML |
| Runtime args removed | Different execution mode | `--interpreter-pgo`, `--disable-on-demand-gc` |

### Key Files to Check in dotnet/performance

| Path | What Changes |
|---|---|
| `src/benchmarks/micro/Serializers/WasmOverridePacks.targets` | Runtime pack selection |
| `src/benchmarks/micro/wasm/` | WASM benchmark entry points |
| `eng/performance/` | CI scripts and Helix job definitions |
| `Directory.Build.props` | BDN version, SDK version |
| `global.json` | SDK version pin |

### Definitive Infrastructure Test

If you find an infrastructure change in the regression window:

1. Check if the baseline run used the OLD methodology
2. Check if the compare run used the NEW methodology
3. If yes → **the regression is an infrastructure artifact**, not a runtime issue
4. Verify by checking that the runtime commit in both runs has no relevant code changes

## Step 5: Classify and Report

### Classification Decision Tree

```
Is there a data gap > 2 weeks?
├── YES → Were dotnet/performance changes merged during the gap?
│   ├── YES → Methodology/infrastructure change? 
│   │   ├── YES → INFRASTRUCTURE ARTIFACT — re-baseline needed
│   │   └── NO  → Investigate runtime changes in the gap
│   └── NO  → Investigate why perf runs were missing
└── NO  → Were dotnet/performance changes merged in the window?
    ├── YES → Do they affect measurement methodology?
    │   ├── YES → INFRASTRUCTURE ARTIFACT
    │   └── NO  → Investigate runtime changes
    └── NO  → LIKELY REAL REGRESSION — proceed to runtime investigation
```

### Report Template

```markdown
## Triage Summary: dotnet/perf-autofiling-issues#XXXX

**Classification:** [Infrastructure Artifact | Real Regression | Needs Investigation]
**Confidence:** [High | Medium | Low]

### Regression Summary
- Total benchmarks affected: N
- Tier 1 (>10x): N benchmarks
- Tier 2 (1.5-10x): N benchmarks  
- Tier 3 (1.1-1.5x): N benchmarks

### Date Range
- Baseline run: YYYY-MM-DD (commit SHA)
- Compare run: YYYY-MM-DD (commit SHA)
- Gap: N days

### Infrastructure Changes in Window
- [ ] BDN version change
- [ ] Entry point change
- [ ] SDK type change
- [ ] Machine pool change
- [ ] Trimming config change

### Evidence
[Binary comparison data, bisection results, or methodology diff]

### Recommendation
[Close as infrastructure artifact | Assign to team | Needs bisection]
```

## Common Patterns

### Pattern: "Everything regressed by 100x+"

Almost always a measurement methodology change. The auto-filer compares absolute numbers, not relative to methodology. Check for entry point or SDK type changes.

### Pattern: "Only SIMD benchmarks regressed"

Check `PackedSimd.IsSupported` and SIMD instruction count in `dotnet.native.wasm`. If SIMD instructions dropped to 0, it's a build config regression. Use the `wasm-binary-analysis` skill.

### Pattern: "Small regressions across many benchmarks"

Could be:
- Machine pool change (different hardware)
- GC configuration change
- Emscripten version bump
- Legitimate runtime change affecting hot paths

Needs bisection to confirm.

### Pattern: "Regressions only in one configuration"

Compare the configuration details carefully. If only `CompilationMode=wasm` regressed but `CompilationMode=jit` is stable, the issue is WASM-specific. Check Mono interpreter and emscripten changes.
