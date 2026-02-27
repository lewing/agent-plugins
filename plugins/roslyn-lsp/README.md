# roslyn-lsp

Roslyn C# language server for code intelligence in Copilot CLI

## Installation

Via marketplace:
```
/plugin marketplace add lewing/agent-plugins
/plugin install roslyn-lsp@lewing-public
/plugin update roslyn-lsp@lewing-public
```

Or install directly from GitHub (Copilot CLI only):
```
/plugin install lewing/agent-plugins:plugins/roslyn-lsp
```

## Uninstall

```
/plugin uninstall roslyn-lsp@lewing-public
```

## LSP Servers

This plugin configures the following language servers:

- **[csharp](.claude-plugin/plugin.json#L17-L30)** — `dotnet`
