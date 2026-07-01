# Baseline Comparison

How to measure a real reduction (or detect a stealth regression) instead of relying on word-counts or PR descriptions.

## The trap: word-count claims

`lewing/helix.mcp#57` ("tighten MCP tool descriptions") landed with a table reporting per-tool word-count savings. The actual measured cost dropped only **−164 tokens (−2.0%)** because:

- Description trimming attacks the smallest field (~7% of total).
- An unrelated PR in the same window (PR #56 — timeline filter presets) added `inputSchema` bytes that partially canceled the wins.
- The PR-time table excluded `inputSchema` and `outputSchema`, which is where 70% of the budget actually lives.

The biggest unflagged wins in that release came from **PR #51** (`LimitedResults<T>` DTO consolidation), which the release notes barely mentioned — −164 tokens on `azdo_test_runs` alone, −93 on `azdo_changes`. The "low-impact refactor" was actually the largest win.

**Word counts lie. Measure end-to-end after every release.**

## The pattern

Two snapshots, one diff:

```bash
# Step 1: capture baseline before the change
plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh \
  --output baseline-v0.6.0.json \
  -- dotnet dnx hlx@0.6.0 -y --

# Step 2: capture the new build
plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh \
  --baseline baseline-v0.6.0.json \
  -- dotnet run --project src/HelixMcp.Server
```

The driver prints a sorted-by-`|delta|` table. Two things to look at:

1. **Net delta.** Did the budget actually fall? `-2%` after a "major cleanup" is usually a warning sign that the wrong field was attacked.
2. **Per-tool diffs.** Tools that grew should be flagged in PR notes; tools that shrank should be matched to the responsible refactor.

## What to publish in PR descriptions

Replace per-tool word-count claims with a single block:

```
mcp-context-audit (gpt-4o tokenizer):
  baseline: 8,376 tokens (25 tools)
  this PR:  8,212 tokens (25 tools)
  delta:    -164 tokens (-2.0%)
  field movements:
    outputSchema: -120
    description:  -101
    inputSchema:  +57   ← from new `filter` parameter on azdo_timeline
```

This makes the tradeoff visible and forces the author to acknowledge accidental inflation.

## Catching stealth regressions

A "no-op" refactor that adds 5 enum values to one parameter can silently grow the budget by hundreds of tokens. Run the audit in CI on every release tag, and store the snapshot as a release artifact so the next release can diff against it.

A minimal CI step (GitHub Actions):

```yaml
- name: Audit MCP context cost
  run: |
    pip install tiktoken
    plugins/mcp-tooling/skills/mcp-context-audit/scripts/audit-mcp.sh \
      --output mcp-audit.json \
      -- dotnet run --project src/MyServer
- uses: actions/upload-artifact@v4
  with:
    name: mcp-context-audit
    path: mcp-audit.json
```

Then a release-time check compares against the previous artifact and posts the diff on the release PR.

## When the diff is misleading

- **New tools or removed tools.** The `[NEW]`/`[REMOVED]` tags in the diff output highlight these; treat them as separate line items, not as part of "the refactor saved X."
- **Tokenizer version drift.** `tiktoken` updates can shift counts by 1–2 tokens per string. Pin the tiktoken version in CI.
- **Protocol version churn.** Once the MCP `2025-11-25+` revision is widely deployed, the `initialize` handshake will change shape and the snippet may need updating.
