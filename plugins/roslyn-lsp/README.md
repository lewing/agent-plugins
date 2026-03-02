# roslyn-lsp

Roslyn C# language server for code intelligence in Copilot CLI

## Installation

### Copilot CLI / Claude Code

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

### VS Code (Preview)

Add the marketplace to your VS Code settings:

```jsonc
// settings.json
{
  "chat.plugins.enabled": true,
  "chat.plugins.marketplaces": ["lewing/agent-plugins"]
}
```

Then use `/plugins` in Copilot Chat to browse and install.

## Uninstall

```
# Copilot CLI / Claude Code
/plugin uninstall roslyn-lsp@lewing-public

# VS Code: remove the marketplace entry from chat.plugins.marketplaces in settings.json
```

## LSP Servers

This plugin configures the following language servers:

- **[csharp](.claude-plugin/plugin.json#L17-L30)** — `dotnet`
