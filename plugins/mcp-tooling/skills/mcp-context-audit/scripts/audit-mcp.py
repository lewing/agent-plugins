#!/usr/bin/env python3
"""Tokenize an MCP server's tools/list + prompts/list response and report cost.

Reads JSON-RPC messages (one per line) from --input, finds the responses to
id=2 (tools/list) and id=3 (prompts/list), and produces:

  1. Total tokens for each array (re-serialized to compact JSON).
  2. Per-field breakdown — outputSchema vs inputSchema vs description, etc.
  3. Per-tool ranking, sorted by token cost.
  4. Optional diff against a --baseline snapshot.

Tokenizes the `tools` and `prompts` arrays only. The JSON-RPC envelope (id,
jsonrpc, result wrapper) is excluded because it does not count against the
agent's per-session tool-surface budget.

Usage:
    audit-mcp.py --input mcp-out.txt [--output snapshot.json] [--baseline old.json] [--model gpt-4o]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

import tiktoken


# Fields broken out separately in the per-field summary. Anything not in this
# set (and not consumed elsewhere) lands in `_other`.
TRACKED_FIELDS = (
    "name",
    "title",
    "description",
    "inputSchema",
    "outputSchema",
    "annotations",
)


def load_messages(path: Path) -> list[dict[str, Any]]:
    """Parse the captured stdout — one JSON-RPC message per line, blank lines ignored."""
    msgs: list[dict[str, Any]] = []
    for line in path.read_text().splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            msgs.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return msgs


def find_result(msgs: list[dict[str, Any]], req_id: int, key: str) -> list[Any]:
    for m in msgs:
        if m.get("id") == req_id and isinstance(m.get("result"), dict):
            return list(m["result"].get(key, []))
    return []


def encode(enc: tiktoken.Encoding, value: Any) -> int:
    """Tokenize `value` as compact JSON. Lists/dicts are serialized verbatim."""
    if isinstance(value, str):
        return len(enc.encode(value))
    return len(enc.encode(json.dumps(value, separators=(",", ":"))))


def per_field(enc: tiktoken.Encoding, item: dict[str, Any]) -> dict[str, int]:
    """Split a single tool/prompt object into a per-field token map.

    `_wrap` captures the overhead from braces/keys/commas in the object — the
    delta between the full re-serialized object and the sum of its tracked
    field values. This is the JSON-RPC-object-shape cost per tool.
    """
    counts: dict[str, int] = {}
    for field in TRACKED_FIELDS:
        if field in item:
            counts[field] = encode(enc, item[field])

    other_keys = [k for k in item if k not in TRACKED_FIELDS]
    if other_keys:
        counts["_other"] = sum(encode(enc, item[k]) for k in other_keys)

    full = encode(enc, item)
    accounted = sum(counts.values())
    counts["_wrap"] = max(0, full - accounted)
    return counts


def summarize(enc: tiktoken.Encoding, items: list[dict[str, Any]]) -> dict[str, Any]:
    total = encode(enc, items)
    fields: dict[str, int] = {}
    per_tool: list[dict[str, Any]] = []
    for item in items:
        breakdown = per_field(enc, item)
        for k, v in breakdown.items():
            fields[k] = fields.get(k, 0) + v
        per_tool.append({
            "name": item.get("name", "<unknown>"),
            "total": sum(breakdown.values()),
            "fields": breakdown,
        })
    per_tool.sort(key=lambda t: t["total"], reverse=True)
    return {"count": len(items), "total_tokens": total, "fields": fields, "tools": per_tool}


def fmt_pct(part: int, whole: int) -> str:
    return f"{(100.0 * part / whole):5.1f}%" if whole else "  0.0%"


def print_report(tools_sum: dict[str, Any], prompts_sum: dict[str, Any]) -> None:
    tools_total = tools_sum["total_tokens"]
    prompts_total = prompts_sum["total_tokens"]
    grand = tools_total + prompts_total

    print("=== Summary ===")
    print(f"tools:   {tools_sum['count']:>3} items, {tools_total:>6,} tokens")
    print(f"prompts: {prompts_sum['count']:>3} items, {prompts_total:>6,} tokens")
    print(f"TOTAL:           {grand:>6,} tokens")
    print()

    if tools_total:
        print("=== Per-field breakdown (tools) ===")
        fields = sorted(tools_sum["fields"].items(), key=lambda kv: kv[1], reverse=True)
        for name, count in fields:
            print(f"{name:<14} {count:>6,}  {fmt_pct(count, tools_total)}")
        print()

    if tools_sum["tools"]:
        print("=== Top 10 tools by cost ===")
        for t in tools_sum["tools"][:10]:
            top_fields = sorted(t["fields"].items(), key=lambda kv: kv[1], reverse=True)[:3]
            extras = ", ".join(f"{k} {v}" for k, v in top_fields)
            print(f"{t['total']:>6,}  {t['name']:<32} ({extras})")
        print()


def print_diff(current: dict[str, Any], baseline: dict[str, Any]) -> None:
    print("=== Diff vs baseline ===")
    cur_total = current["total_tokens"]
    base_total = baseline["total_tokens"]
    delta = cur_total - base_total
    sign = "+" if delta >= 0 else ""
    pct = (100.0 * delta / base_total) if base_total else 0.0
    print(f"total: {base_total:,} -> {cur_total:,} ({sign}{delta:,}, {sign}{pct:.1f}%)")
    print()

    cur_by_name = {t["name"]: t for t in current["tools"]}
    base_by_name = {t["name"]: t for t in baseline["tools"]}
    all_names = sorted(set(cur_by_name) | set(base_by_name))

    rows = []
    for n in all_names:
        c = cur_by_name.get(n, {"total": 0})["total"]
        b = base_by_name.get(n, {"total": 0})["total"]
        d = c - b
        if d != 0:
            rows.append((d, n, b, c))
    rows.sort(key=lambda r: abs(r[0]), reverse=True)

    if rows:
        print("Tools changed (sorted by |delta|):")
        for d, n, b, c in rows[:20]:
            tag = " [NEW]" if b == 0 else " [REMOVED]" if c == 0 else ""
            print(f"  {d:>+6,}   {n:<32} {b:>5,} -> {c:>5,}{tag}")
    else:
        print("(no per-tool changes)")
    print()


def main() -> int:
    p = argparse.ArgumentParser(description="Audit MCP server context cost.")
    p.add_argument("--input", required=True, type=Path, help="Captured MCP server stdout")
    p.add_argument("--output", type=Path, help="Write snapshot JSON for later --baseline diff")
    p.add_argument("--baseline", type=Path, help="Compare against a prior snapshot")
    p.add_argument("--model", default="gpt-4o", help="tiktoken encoding (default: gpt-4o)")
    args = p.parse_args()

    try:
        enc = tiktoken.encoding_for_model(args.model)
    except KeyError:
        print(f"audit-mcp.py: unknown model '{args.model}'", file=sys.stderr)
        return 2

    msgs = load_messages(args.input)
    if not msgs:
        print(f"audit-mcp.py: no JSON-RPC messages in {args.input}", file=sys.stderr)
        return 1

    tools = find_result(msgs, 2, "tools")
    prompts = find_result(msgs, 3, "prompts")
    if not tools and not prompts:
        print("audit-mcp.py: no tools/list or prompts/list result found", file=sys.stderr)
        return 1

    tools_sum = summarize(enc, tools)
    prompts_sum = summarize(enc, prompts)

    print_report(tools_sum, prompts_sum)

    snapshot = {"tools": tools_sum, "prompts": prompts_sum}

    if args.baseline:
        if not args.baseline.exists():
            print(f"audit-mcp.py: baseline {args.baseline} not found", file=sys.stderr)
            return 1
        baseline = json.loads(args.baseline.read_text())
        print_diff(tools_sum, baseline["tools"])

    if args.output:
        args.output.write_text(json.dumps(snapshot, indent=2))
        print(f"snapshot written: {args.output}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
