# roslyn-lsp

Roslyn C# language server for code intelligence in Copilot CLI

## Installation

Via marketplace (supports updates):
```
/plugin marketplace add lewing/agent-plugins
/plugin install roslyn-lsp@lewing-public
/plugin update roslyn-lsp@lewing-public
```

Or install directly from GitHub:
```
/plugin install lewing/agent-plugins:plugins/roslyn-lsp
```

## Uninstall

```
/plugin uninstall roslyn-lsp@lewing-public
```

## LSP Servers

This plugin configures the following language servers:

- **csharp** — `dotnet`
