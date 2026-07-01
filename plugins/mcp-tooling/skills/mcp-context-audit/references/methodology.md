# MCP Audit Methodology

The full evaluation framework — context cost is one of three dimensions used when comparing MCP servers across an ecosystem. The scripts in `../scripts/` automate dimension 1.

## 1. Flat context cost

Every session pays for `tools/list` (and `prompts/list`) once. Measure the actual cost the agent sees, not the source-file size.

Run the server, send the standard initialize handshake, request `tools/list` and `prompts/list`, capture the JSON responses, and tokenize each `result.tools` / `result.prompts` array (re-serialized to compact JSON) with the model's tokenizer:

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

`audit-mcp.sh` packages this into a single command, adds a per-field breakdown, and a per-tool ranking. Use the raw snippet only when debugging the script itself.

Stack budgets are additive. A 7,000-token server combined with Helix (~3,500) + AzDO (~2,500) + GitHub (~4,000) consumes ~17,000 tokens of every session before the agent has done anything.

## 2. Distribution constraint

Where is the package hosted? Test the actual feeds your consumers use.

| Feed | Test |
|------|------|
| nuget.org | `curl https://api.nuget.org/v3-flatcontainer/<pkg>/index.json` |
| dnceng `dotnet-public` upstream mirror | `curl https://pkgs.dev.azure.com/dnceng/.../<pkg>/index.json` |
| dnceng `dotnet-tools` internal | Same pattern, different feed GUID |
| Private/EMU repo | Verify the source repo is publicly accessible |

A package on nuget.org alone may be invisible to dnceng-internal consumers; a package on the dnceng `dotnet-tools` feed alone is invisible to anyone outside Microsoft infrastructure.

## 3. Design style

Three recurring patterns:

- **Analyst** — pre-canned aggregations (`get_expensive_*`, `get_diagnostics(...)` with filters). Quick answers to common questions; per-tool overhead grows quickly because every variant is its own tool definition.
- **Debugger** — small set of composable primitives (`get_node`, `get_children`, `search`, `count`). Lower context cost; agents reason from the building blocks.
- **Hybrid** — primitives plus domain-specific shortcuts (`binlog_explain_property`, `binlog_compare`). Higher tool count but the shortcuts let agents skip multi-step compositions.

None is strictly better. Match the style to the workflow: analysts work well in agentic workflows with narrow remits; debuggers shine in open-ended investigation.
