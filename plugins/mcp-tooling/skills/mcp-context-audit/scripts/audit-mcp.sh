#!/usr/bin/env bash
# audit-mcp.sh — Measure the per-session context cost of an MCP server's tool surface.
#
# Usage:
#   audit-mcp.sh [--output FILE] [--baseline FILE] [--model MODEL] -- <server-command...>
#
# Example:
#   audit-mcp.sh -- dotnet run --project src/MyServer
#   audit-mcp.sh --output baseline.json -- dotnet dnx myserver@1.0.0 -y --
#   audit-mcp.sh --baseline baseline.json -- ./bin/myserver mcp
#
# The server must speak MCP over stdio. Everything after `--` is passed verbatim
# to the shell (so it can include `dotnet dnx pkg@ver -y --` etc.).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT=""
BASELINE=""
MODEL="gpt-4o"
TIMEOUT_SECS="${MCP_AUDIT_TIMEOUT:-25}"
SLEEP_SECS="${MCP_AUDIT_SLEEP:-8}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --output) OUTPUT="$2"; shift 2 ;;
        --baseline) BASELINE="$2"; shift 2 ;;
        --model) MODEL="$2"; shift 2 ;;
        --timeout) TIMEOUT_SECS="$2"; shift 2 ;;
        --sleep) SLEEP_SECS="$2"; shift 2 ;;
        --help|-h)
            sed -n '2,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        --) shift; break ;;
        *) echo "audit-mcp.sh: unknown arg: $1" >&2; exit 2 ;;
    esac
done

if [[ $# -eq 0 ]]; then
    echo "audit-mcp.sh: missing server command (pass after --)" >&2
    exit 2
fi

if ! command -v python3 >/dev/null 2>&1; then
    echo "audit-mcp.sh: python3 is required" >&2
    exit 1
fi

if ! python3 -c "import tiktoken" 2>/dev/null; then
    echo "audit-mcp.sh: tiktoken is required (pip install tiktoken)" >&2
    exit 1
fi

RAW=$(mktemp)
trap 'rm -f "$RAW"' EXIT

# Standard MCP handshake + list calls. `sleep` keeps stdin open long enough for
# the server to finish writing all three responses before EOF.
{
    printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"mcp-context-audit","version":"0.1.0"}}}'
    printf '%s\n' '{"jsonrpc":"2.0","method":"notifications/initialized"}'
    printf '%s\n' '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    printf '%s\n' '{"jsonrpc":"2.0","id":3,"method":"prompts/list"}'
    sleep "$SLEEP_SECS"
} | timeout "$TIMEOUT_SECS" "$@" 2>/dev/null > "$RAW" || true

if [[ ! -s "$RAW" ]]; then
    echo "audit-mcp.sh: no output captured from server. Try a longer --sleep or --timeout." >&2
    exit 1
fi

ARGS=("--model" "$MODEL" "--input" "$RAW")
[[ -n "$OUTPUT" ]] && ARGS+=("--output" "$OUTPUT")
[[ -n "$BASELINE" ]] && ARGS+=("--baseline" "$BASELINE")

python3 "$SCRIPT_DIR/audit-mcp.py" "${ARGS[@]}"
