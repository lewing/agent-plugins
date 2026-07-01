# Real-World MCP Server Comparisons

A working catalog of MCP servers we have evaluated, plus the methodology
we use to evaluate them. The patterns in
`tool-description-patterns.md`, `tool-naming-conventions.md`, and
`knowledge-tool-design.md` describe what good design looks like;
this file shows what those design choices look like in shipping
servers.

Each entry follows the same shape so comparisons across servers in
the same domain are easy to read.

---

## Methodology

When evaluating an MCP server you might configure in a plugin,
measure three things before reading marketing copy:

### 1. Flat context cost

Every session pays for `tools/list` (and `prompts/list`) once. Measure
the actual cost the agent sees, not the source-file size.

Run the server, send the standard initialize handshake, request
`tools/list` and `prompts/list`, capture the JSON responses, and
tokenize each `result.tools` / `result.prompts` array (re-serialized
to compact JSON) with the model's tokenizer:

```bash
(printf '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}\n'
 printf '{"jsonrpc":"2.0","method":"notifications/initialized"}\n'
 printf '{"jsonrpc":"2.0","id":2,"method":"tools/list"}\n'
 printf '{"jsonrpc":"2.0","id":3,"method":"prompts/list"}\n'
 sleep 8) | timeout 25 <server-command> 2>/dev/null > /tmp/mcp-out.txt
```

```python
import json, tiktoken
enc = tiktoken.encoding_for_model("gpt-4o")
# Tokenizes the tools/prompts arrays only — the JSON-RPC envelope
# (id, jsonrpc, result wrapper) is excluded since it is not counted
# against the agent's per-session tool-surface budget.
for line in open('/tmp/mcp-out.txt'):
    msg = json.loads(line.strip())
    if msg.get('id') in (2, 3) and 'result' in msg:
        key = 'tools' if msg['id'] == 2 else 'prompts'
        items = msg['result'].get(key, [])
        print(f'{key}: {len(items)}, {len(enc.encode(json.dumps(items)))} tokens')
```

Stack budgets are additive. A 7,000-token server combined with Helix
(~3,500) + AzDO (~2,500) + GitHub (~4,000) consumes ~17,000 tokens
of every session before the agent has done anything.

### 2. Distribution constraint

Where is the package hosted? Test the actual feeds your consumers use.

| Feed | Test |
|------|------|
| nuget.org | `curl https://api.nuget.org/v3-flatcontainer/<pkg>/index.json` |
| dnceng `dotnet-public` upstream mirror | `curl https://pkgs.dev.azure.com/dnceng/.../<pkg>/index.json` |
| dnceng `dotnet-tools` internal | Same pattern, different feed GUID |
| Private/EMU repo | Verify the source repo is publicly accessible |

A package on nuget.org alone may be invisible to dnceng-internal
consumers; a package on the dnceng `dotnet-tools` feed alone is
invisible to anyone outside Microsoft infrastructure.

### 3. Design style

Three recurring patterns:

- **Analyst** — pre-canned aggregations (`get_expensive_*`,
  `get_diagnostics(...)` with filters). Quick answers to common
  questions; per-tool overhead grows quickly because every variant
  is its own tool definition.
- **Debugger** — small set of composable primitives (`get_node`,
  `get_children`, `search`, `count`). Lower context cost; agents
  reason from the building blocks.
- **Hybrid** — primitives plus domain-specific shortcuts
  (`binlog_explain_property`, `binlog_compare`). Higher tool count
  but the shortcuts let agents skip multi-step compositions.

None is strictly better. Match the style to the workflow: analysts
work well in agentic workflows with narrow remits; debuggers shine
in open-ended investigation.

---

## Catalog

### Binlog (MSBuild binary log analysis) — three servers

All three use Kirill Osenkov's
[`StructuredLogger`](https://github.com/KirillOsenkov/MSBuildStructuredLog)
library (NuGet `MSBuild.StructuredLogger`) as the underlying parser.
The differentiation is the MCP surface
and the distribution story.

| | baronfel.binlog.mcp | binlogmcp (Kirill) | AITools.BinlogMcp |
|---|---|---|---|
| Author | Chet Husk (personal) | Kirill Osenkov | Microsoft (internal "AI tools" team) |
| Source repo | github.com/baronfel/mcp-binlog-tool (public) | github.com/KirillOsenkov/MSBuildStructuredLog (public, first-party) | github.com/dotnet-microsoft/ai-tools (private EMU) |
| Distribution | nuget.org | nuget.org | dnceng `dotnet-tools` feed only |
| Latest measured | 0.0.14 | 0.1.3 | 1.0.0-preview.26271.2 |
| Tool count | 25 | 18 | 29 |
| Prompt count | 0 | 0 | 2 |
| **Total tokens** | **7,176** | **4,679** | **4,283** |
| Avg tokens/tool | 287 | 260 | 147 |
| Style | Analyst | Debugger | Hybrid |
| Telemetry | none | none | opt-in OpenTelemetry |
| Pre-load via CLI arg | no | no | `--binlog <path>` |
| Stability commitment | "simple demo" | first-party, active | "internal technical detail" — no compatibility guarantee for direct use |

#### What each has uniquely

**baronfel** — analyst aggregations:
`get_expensive_projects`/`targets`/`tasks`/`analyzers`,
`get_evaluation_global_properties`/`properties_by_name`/`items_by_name`,
`get_target_info_by_id`/`by_name`. Each is its own tool definition;
the surface area is large because variants are separate tools.

**Kirill (binlogmcp)** — debugger primitives:
`get_node`, `get_children` (with `kind=`/`nameContains=` filters),
`get_ancestors`, `print_subtree`, `count` (no-cap totals separate
from `search`), `preprocess_file` (effective MSBuild XML after
imports/conditions), `get_llm_guide` / `get_search_syntax_help`
(self-describing). Full lifecycle: load multiple binlogs at once,
unload, reload.

**AITools** — domain shortcuts:
`binlog_explain_property` (origin tracing — which file/target/task
set this value?), `binlog_compare` (built-in two-binlog diff for
properties + packages), `binlog_nuget` (NuGet-specific queries),
`binlog_target_reasons` ("why did this target run?"),
`binlog_double_writes`, `binlog_compiler`, plus MCP **Prompts**
(`analyze_errors`, `analyze_performance`) that agents can invoke as
workflow templates.

#### Tradeoff summary

| If you need... | Pick |
|---|---|
| Public package, stable contract, lowest investigation cost | **Kirill** |
| Pre-canned perf aggregations, minimal agent reasoning | **baronfel** |
| Property-origin tracing, built-in binlog diff, NuGet-aware analysis — and you only run inside dnceng | **AITools** |

#### Distribution gotcha

`binlogmcp` (Kirill's) is on nuget.org but **not** mirrored to the
dnceng `dotnet-public` upstream feed. Anywhere a consumer's
`NuGet.config` pins to that feed without nuget.org fallback,
`dnx -y binlogmcp@<ver>` will fail with `PackageNotFoundException`.
A mirror request can fix this.

`AITools.BinlogMcp` is **only** on the dnceng `dotnet-tools` feed
and the source repo is in `dotnet-microsoft` (Microsoft EMU). Useful
inside Microsoft infrastructure; impossible for external consumers
to install or audit.

#### When to revisit

Switch from Kirill to AITools if and only if:
- AITools ships to nuget.org with a versioning commitment, AND
- The "internal technical detail" disclaimer is removed, AND
- Property-origin tracing or built-in binlog diff is on a critical
  path for an agent we're shipping.

Switch from Kirill back to baronfel only if a workflow concretely
needs `get_expensive_analyzers` aggregation that can't be
synthesized from `search $csc $time` + child drilling.

#### Decision rationale (current)

We configure `binlogmcp@0.1.3` (Kirill's) in both
`plugins/dotnet-msbuild/plugin.json` and `plugins/lewing/plugin.json`
(after PR #278 merges) because it is the public, stable, nuget.org
option with the lowest context cost (4,679 tokens) and the strongest
stability commitment.

The MCP namespace name in `plugins/dotnet-msbuild/plugin.json` is
**`binlog-mcp`** (with hyphen) — intentionally distinct from
`dotnet/skills/dotnet-msbuild` and `dotnet/arcade-skills/dotnet-dnceng`
which register `Microsoft.AITools.BinlogMcp` under namespace `binlog`
(no hyphen). The distinct name lets the two coexist as independent
MCP instances when both plugin families are installed; renaming would
create a name-vs-package collision. See
`plugins/dotnet-msbuild/skills/msbuild-agentic-workflows/references/binlog-mcp-alternatives.md`
for the consumer-side decision record.

---

## Adding new entries

When evaluating another MCP server family (Helix, AzDO, NuGet,
GitHub, etc.):

1. Measure all three things (cost, distribution, style) before
   reading docs.
2. Identify what each unique tool gives you that the others don't —
   resist double-counting near-duplicate variants.
3. Name the distribution gotchas explicitly. A "great" server that
   doesn't reach your consumers isn't great.
4. Write a "when to revisit" section so future readers don't redo
   the investigation when the landscape shifts.
