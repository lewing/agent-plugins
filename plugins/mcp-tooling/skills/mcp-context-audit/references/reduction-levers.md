# Reduction Levers

Ordered by impact. Pull the top lever first; description-trimming is the last resort, not the first.

## Field cost distribution (typical .NET MCP server)

Measured against `lewing/helix.mcp` v0.7.3 (25 tools, 8,212 tokens), but the ratios hold across servers we've audited:

| Field | Share | What dominates the cost |
|---|---|---|
| `outputSchema` | **37.0%** | Response-DTO JSON Schema. Discriminators, nullable arrays, nested records all serialize verbose. |
| `inputSchema` | **33.5%** | Parameter list + `enum` arrays + `description` strings inside parameter properties. |
| `annotations` | 9.7% | `title`, `readOnlyHint`, `destructiveHint`, etc. Often set redundantly to default values. |
| `_wrap` | 8.8% | JSON-object overhead (braces, commas, keys) per tool. Drops only by removing tools. |
| `description` | 7.2% | Tool-level description string. Smallest meaningful slice. |
| `title` | 2.0% | Display title. |
| `name` | 1.8% | Tool name. |

**JSON Schema is ~70% of every server's tool-surface cost.** Description-only optimizations are arithmetically capped at the 7% slice — and that's before you account for `inputSchema.properties[].description`, which sneaks into the 33.5% slice.

## Lever 1: Consolidate output DTOs (largest win)

Wrapping list-returning tools in a shared paging type collapses 5–10 near-duplicate output schemas into one referenced shape:

```csharp
// Before — three tools, three full outputSchemas
public sealed record TestRunsResponse(IReadOnlyList<TestRun> Items, int Total, string? NextCursor);
public sealed record ChangesResponse(IReadOnlyList<Change> Items, int Total, string? NextCursor);
public sealed record BuildsResponse(IReadOnlyList<Build> Items, int Total, string? NextCursor);

// After — three tools share one wrapper type
public sealed record LimitedResults<T>(IReadOnlyList<T> Items, int Total, string? NextCursor);
```

`lewing/helix.mcp` PR #51 shrank `azdo_test_runs` by 164 tokens and `azdo_changes` by 93 tokens this way — ~3% of the whole server, larger than the description-trimming PR #57 that got more attention.

**Watch for:** the SDK may still inline the schema for each generic instantiation. Verify with `audit-mcp.sh` after the change; a "shared type" that still serializes inline is no savings.

## Lever 2: Trim input parameters

- Drop unused parameters — every `{"name":"foo","schema":{"type":"string"},"description":"..."}` is ~40–80 tokens.
- Replace large string `enum` arrays with `pattern` or freeform when the validation is advisory.
- Collapse `oneOf` / `anyOf` unions when only one branch is materially used in practice.
- Move long-form parameter docs out of `description` and into the tool's main description (one string instead of N).

## Lever 3: Audit `annotations`

The MCP spec defines these defaults:

| Field | Default |
|---|---|
| `readOnlyHint` | `false` (assumed-mutating) |
| `destructiveHint` | `true` (assumed-destructive — only meaningful if `readOnlyHint=false`) |
| `idempotentHint` | `false` |
| `openWorldHint` | **`true`** (assumed external) |

Defaults are the conservative/risky assumption — tools opt **in** to safer properties. **Do not set fields to their default values explicitly** — that pays tokens for no semantic gain.

Pattern to apply:
- Read-only tool (most queries): set `readOnlyHint=true`. `destructiveHint` becomes meaningless and can be omitted.
- Closed-world tool (e.g. in-memory module inspection): set `openWorldHint=false`.
- Otherwise, omit the annotation entirely.

`nesm` is the canonical "should set `openWorldHint=false`" case — many tools that operate on a loaded WASM module, but the default tells the agent the server can reach arbitrary external systems.

## Lever 4: Then descriptions

Last resort. Capped at ~7% of the budget.

Useful patterns:
- Lead with a verb (`Returns ...`, `Lists ...`). Cuts boilerplate.
- Avoid restating the parameter list in prose — `inputSchema` already encodes that.
- Cross-reference rather than re-explain: `"Use X for most cases; this tool handles Y."`
- Remove "this tool", "you can use this to", and similar filler.

## Anti-pattern: hidden inflation from new features

Adding a filter parameter feels like a small description tweak but materially adds `inputSchema` bytes. `helix.mcp` PR #56 (timeline filter presets) added an `enum` array to one tool that wiped out half the description-trim savings of PR #57 in the very next release.

Run `audit-mcp.sh --baseline` after every feature PR, not just after dedicated cleanup PRs.
