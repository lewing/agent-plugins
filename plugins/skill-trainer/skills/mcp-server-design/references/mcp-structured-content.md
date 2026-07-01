# MCP Structured Content — Migration & Boundaries

**When to use:** You're migrating an MCP server from manual JSON serialization (`Task<string>` + `JsonSerializer.Serialize`) to the SDK's `UseStructuredContent` typed-return feature, or deciding which tools should use structured content at all.

**Origin:** Patterns earned while migrating a .NET MCP server (`lewing.helix.mcp`) from string returns to `UseStructuredContent = true`. The code samples are .NET; the boundary rules (raw text vs structured, wire-compat discipline, error-path migration) generalize to any SDK.

## Why this matters

`UseStructuredContent` auto-generates `outputSchema` in `tools/list` — that's good for client validation but has real wire-byte cost (see `mcp-wire-format-trim.md`). Knowing **when not** to use it is as important as knowing how to use it correctly.

## Patterns

### Result type placement

Define MCP result types in a **separate file** (e.g., `McpToolResults.cs`) — not inline in the tool class or co-located with service-layer models.

MCP result types are a **presentation concern**, distinct from domain models. Co-locating them with the service layer creates coupling that makes future schema changes (renames, field additions for the MCP consumer only) feel like domain model changes when they aren't.

### Wire compatibility via JsonPropertyName

Every property on result types **MUST** have a `[JsonPropertyName("camelCaseName")]` attribute matching the previous serialized output.

The SDK's default naming policy may differ from the manual `JsonSerializerOptions` that were used before. Explicit attributes make the wire format independent of SDK configuration — and protect you against an SDK update that changes the default.

```csharp
public sealed record BuildSummary
{
    [JsonPropertyName("buildId")]   public int BuildId { get; init; }
    [JsonPropertyName("status")]    public string Status { get; init; } = "";
    [JsonPropertyName("startTime")] public DateTimeOffset StartTime { get; init; }
}
```

### Raw text exception — when NOT to use structured content

Tools that return **raw text content** (console logs, file contents, error messages users read verbatim) should **NOT** use `UseStructuredContent`. Return `Task<string>` directly.

Structured content wrapping adds noise to text that consumers will just display as-is. The agent or end user has to dig past the wrapper to get to the content, the output schema adds bytes to `tools/list` for no client value, and the wrapped form often renders worse in UIs.

**Rule of thumb:**

| Return shape | Use `UseStructuredContent`? |
|---|---|
| `string` (log, file contents, free-form text) | ❌ No — `Task<string>` direct |
| Small DTO with named fields | ⚠️ Maybe — measure the schema cost (see wire-format-trim Pattern 2) |
| Complex object with nested types the client will programmatically inspect | ✅ Yes — schema earns its bytes |
| Array of homogeneous DTOs | ✅ Yes |

### Error path migration

Replace `return JsonSerializer.Serialize(new { error = "..." })` with `throw new McpException("...")`.

The MCP SDK translates exceptions into proper error responses on the wire. Returning JSON-with-error-field looks like a success on the wire and forces every client to special-case it.

```csharp
// ❌ Looks like success on the wire; every client must special-case
return JsonSerializer.Serialize(new { error = "Build not found" });

// ✅ Surfaces as a proper MCP error response
throw new McpException("Build not found");
```

Use:
- **`McpException`** — for tool-level errors the client should see
- **`ArgumentException`** — for parameter validation (SDK translates appropriately)

**Critical caveat — service-layer exceptions don't auto-surface:** The MCP SDK wraps non-`McpException` exceptions as generic `"An error occurred invoking '{tool}'"` messages, hiding the actual error from the client. Tool handlers **MUST** catch domain exceptions and rethrow as `McpException`:

```csharp
try
{
    var result = await _svc.DoSomethingAsync(...);
    return result;
}
catch (MyDomainException ex)
{
    throw new McpException(ex.Message);
}
```

Without this catch, the client sees only the generic SDK message and the actual cause is lost — even if you logged it server-side.

### Type naming

Avoid collisions with BCL types (e.g., `System.IO.FileInfo`) by using **domain-specific prefixes** (e.g., `HelixFileInfo`) rather than suffixes or underscores. The C# type name doesn't affect the wire format but should still follow conventions for maintainability.

## Anti-patterns

### Duplicating domain models

Do **NOT** copy all fields from service-layer records into MCP result types and keep them in sync manually.

Map only the fields the MCP consumer needs. If the tool adds computed fields (formatted duration, derived URLs, etc.), those belong **only** in the MCP result type — they don't pollute the domain model, and they don't have to be maintained in two places.

```csharp
// ❌ Mirror copy — drift over time, double maintenance
public sealed record McpBuild(int Id, string Branch, DateTimeOffset Start,
    DateTimeOffset End, TimeSpan Duration, string Status, string Url, string Org, ...);

// ✅ MCP-shaped projection of only what the client needs
public sealed record McpBuild(int Id, string Branch, string Status,
    [JsonPropertyName("durationSeconds")] double DurationSeconds);
```

### Missing JsonPropertyName

Relying on the SDK's default naming convention instead of explicit `[JsonPropertyName]` creates a **hidden coupling**. If the SDK changes its default or a different `JsonNamingPolicy` is configured downstream, the wire format breaks silently. The schema in `tools/list` and the actual call response can also diverge.

Make it explicit; cost is one attribute per property.

### Using UseStructuredContent on raw-text returns

Wrapping a log file or error string in a structured envelope:
- Bloats `tools/list` with a one-property schema
- Forces clients to unwrap before displaying
- Provides no client value because the content has no structure to validate

If the content is text, return `Task<string>`. Trust the SDK to handle string returns correctly.

## See also

- `mcp-wire-format-trim.md` — Pattern 2 ("drop outputSchema while preserving wire payload") and the measurement harness
- `tool-description-patterns.md` — description budget rules that apply alongside the schema-byte budget
- `mcp-tool-routing-copy.md` — how to direct callers to the right tool when structured vs raw outputs vary across the family
