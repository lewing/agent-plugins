# MCP Wire-Format Trim

**When to use:** You need to reduce `tools/list` token/byte cost without changing tool behavior, or you're auditing MCP metadata bloat in tool attributes / auto-generated schemas.

**Origin:** Patterns earned while shipping a 25-tool .NET MCP server (`lewing.helix.mcp`). The reflection-based measurement harness is .NET/SDK-specific; the underlying principles (default-annotation audit, `outputSchema` trimming, candidate triage, measurement-first discipline) generalize to any MCP server.

## Why this matters

`tools/list` is paid by every client every connection, in tokens charged against the context window for every conversation that loads the server. A 28 KB `tools/list` payload on a 25-tool server is ~7,000 tokens — and most of that is often metadata the SDK adds by default, not content the tool author wrote.

The three patterns below — default-annotation audit, `outputSchema` trim, candidate triage — typically reclaim 15-40% of `tools/list` bytes on a well-instrumented server.

## Pattern 1: Default-annotation audit

SDKs emit attribute properties even when their values match the SDK's defaults — explicit `OpenWorld=true` on every tool is identical wire output to omitting it, but costs bytes per tool.

1. **Verify defaults from the exact package version in use** — read the SDK source or use reflection. Don't trust documentation.
2. **Only remove attribute properties whose explicit value matches the SDK default.**
3. **Consequence:** matching-default annotations are removable noise; non-matching-default annotations carry information and must stay.

### Reference: ModelContextProtocol .NET SDK 1.3.0 defaults

| Property | Default |
|---|---|
| `OpenWorld` | `true` |
| `ReadOnly` | `false` |
| `Idempotent` | `false` |
| `Destructive` | `true` |
| `UseStructuredContent` | `false` |

So in this SDK: `OpenWorld=true` is removable noise, but `Destructive=false` must stay (non-default → carries information).

> For other SDKs (TypeScript, Python), the same principle holds — find the defaults, then remove only the matches. Don't assume the defaults are the same across SDKs.

## Pattern 2: Drop outputSchema while preserving wire payload

Use this only for **tiny results** where the output schema carries little value for the client and you must keep the actual tool-call payload stable.

The trick: the SDK advertises `outputSchema` in `tools/list` when `UseStructuredContent=true`, but you can return structured content in the *tool-call* response while suppressing the *schema declaration* in `tools/list`.

### Recipe (ModelContextProtocol .NET SDK 1.3.0)

1. Change the tool method return type to `CallToolResult` (or `Task<CallToolResult>`).
2. Remove `UseStructuredContent = true` from the `[McpServerTool]` attribute so the SDK stops advertising `outputSchema`.
3. Return `CallToolResult` manually with both:
   - `Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, McpJsonUtilities.DefaultOptions) }]`
   - `StructuredContent = JsonSerializer.SerializeToElement(value, McpJsonUtilities.DefaultOptions)`
4. This keeps the tool-call JSON payload/structured content intact while shrinking `tools/list`.

> **Tradeoff:** clients that *programmatically validate against `outputSchema`* lose that validation surface. Only do this when the tool returns small, well-known shapes where the schema's information value is low relative to its byte cost.

## Pattern 3: Candidate triage — what's worth trimming?

Not every tool benefits equally. Before investing time:

- **Already-primitive string tools** with `UseStructuredContent=false` are already optimal — they emit no `outputSchema`. Skip.
- **Small DTOs** are better candidates than broad result objects — the schema-to-payload ratio is worse for tiny return types.
- **Skip wrappers** whose only trim path would change the top-level wire shape (e.g., `{ "items": [...] }` → bare `[...]`). The schema savings rarely justify changing the contract.
- **Prioritize the fattest tools first** — measure first (see below); fix the top 3 — 5 sources of bloat, not the long tail.

## Measurement is mandatory

You cannot trim what you have not measured. Two options, depending on language:

### Option A: SDK reflection (fast, in-process, .NET-only)

For .NET MCP servers built on `ModelContextProtocol.Core`:

```csharp
// Generic harness — works for any .NET MCP server using the same SDK
var assembly = Assembly.LoadFrom("YourMcpServer.dll");
var toolTypes = assembly.GetTypes()
    .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
    .ToArray();

foreach (var toolType in toolTypes)
{
    // Use RuntimeHelpers.GetUninitializedObject to bypass constructors:
    // McpServerTool.Create requires a non-null instance for instance methods,
    // but never invokes them — schema generation is purely reflective.
    var shell = RuntimeHelpers.GetUninitializedObject(toolType);
    foreach (var method in toolType.GetMethods()
        .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null))
    {
        var mcpTool = McpServerTool.Create(method, shell, options: null);
        var proto = mcpTool.ProtocolTool;
        var json = JsonSerializer.Serialize(proto, McpJsonUtilities.DefaultOptions);
        var bytes = Encoding.UTF8.GetByteCount(json);
        // Per-field breakdown: proto.InputSchema (JsonElement, always present),
        // proto.OutputSchema (JsonElement?, null when UseStructuredContent=false).
    }
}

// Full tools/list payload bytes:
var listPayload = JsonSerializer.Serialize(
    new { tools = allProtos }, McpJsonUtilities.DefaultOptions);
var totalBytes = Encoding.UTF8.GetByteCount(listPayload);
```

Pros: < 200ms, no server boot, no credentials, runs as a unit test for CI regression guarding.
Cons: .NET-only.

### Option B: Live JSON-RPC (language-agnostic, ground truth)

Works for any MCP server in any language because `tools/list` is a standard JSON-RPC method:

```bash
# Three-message handshake over stdio
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"measure","version":"0"}}}
{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' \
  | your-mcp-server-binary \
  | tail -1 \
  | wc -c
```

Or scripted in Python/Node.js: spawn the server as subprocess, speak the 3-message handshake over stdin/stdout, read the `tools/list` response, `len(json.dumps(response).encode())`.

Pros: works for TypeScript, Python, Rust, Go, anything; measures exact wire bytes; catches runtime schema transforms reflection misses.
Cons: requires server boot (auth, env vars, dependencies); slower; can't run offline.

### When to use which

| Need | Pick |
|---|---|
| CI regression guard for .NET server | Option A (reflection) |
| One-shot measurement across polyglot servers | Option B (live JSON-RPC) |
| Comparing two SDKs/servers | Option B (apples-to-apples wire bytes) |
| Per-attribute breakdown without server boot | Option A |

## Reporting

When reporting trim results, include:

- **Before/after total bytes** of the full `tools/list` payload.
- **Per-change breakdown** — which annotation/schema change saved how many bytes.
- **Per-tool delta** for the trims that matter.

A single "saved 4 KB" number without breakdown is hard to defend in review; a per-tool table makes the change auditable.

## Quick-estimate heuristic (for triage only)

When you don't yet have a real measurement and need a back-of-envelope:

- Regex-extract `[Description(...)]` and `McpServerTool(Name=..., Title=...)` attributes
- Build a representative compact JSON object per tool (name, title, description, `inputSchema.properties`)
- `len(json.dumps(obj, separators=(',',':')))` for an approximate byte count

This understates real size — it excludes `outputSchema` entirely, uses placeholder param names, and ignores annotation fields. Typical understatement: 40% on servers with structured-content tools. **Use only for triage**, not for final reporting.

## See also

- `server-comparisons.md` — how to evaluate competing MCP servers (the catalog records measured `tools/list` sizes for several real servers)
- `mcp-structured-content.md` — when *not* to use `UseStructuredContent` (raw-text returns)
- `tool-description-patterns.md` — the description-budget side of the same problem
