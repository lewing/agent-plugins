#!/usr/bin/env dotnet
#:package System.CommandLine@*

#pragma warning disable CS8604  // Nullable option default values are guaranteed by DefaultValueFactory
#pragma warning disable CS8619  // Nullability of Path.GetFileName in HashSet context
#pragma warning disable CS8321  // PrintError defined for consumer use

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// ============================================================================
// Configuration
// ============================================================================

var repoRoot = GetRepoRoot();
string? _backupTimestamp = null;
string? _customBackupPath = null;
string? _cachedGitHubUser = null;

var requiredSettings = new Dictionary<string, JsonNode?>
{
    ["chat.plugins.enabled"] = JsonValue.Create(true),
    ["chat.useAgentSkills"] = JsonValue.Create(true),
    ["chat.useNestedAgentsMdFiles"] = JsonValue.Create(true),
    ["chat.customAgentInSubagent.enabled"] = JsonValue.Create(true),
    ["chat.instructionFilesLocations"] = new JsonObject
    {
        ["$HOME/.copilot-instructions/instructions"] = true
    }
};

// ============================================================================
// Global Options
// ============================================================================

var editionOption = new Option<string>("--edition")
{
    Description = "Target VS Code edition (insiders, stable, or both)",
    DefaultValueFactory = _ => "both",
    Recursive = true
};
editionOption.AcceptOnlyFromAmong("insiders", "stable", "both");

var targetOption = new Option<string>("--target")
{
    Description = "Install target tool(s): auto (detect installed), copilot, claude, vscode, or all",
    DefaultValueFactory = _ => "auto",
    Recursive = true
};
targetOption.AcceptOnlyFromAmong("auto", "copilot", "claude", "vscode", "all");

var exactOption = new Option<bool>("--exact")
{
    Description = "Full sync: remove target files/entries not in repo (after backup)",
    Recursive = true
};

var forceOption = new Option<bool>("--force")
{
    Description = "Overwrite existing files without prompting",
    Recursive = true
};

var dryRunOption = new Option<bool>("--dry-run")
{
    Description = "Show what would be done without making changes",
    Recursive = true
};

var verboseOption = new Option<bool>("--verbose")
{
    Description = "Show detailed output",
    Recursive = true
};

var backupPathOption = new Option<string?>("--backup-path")
{
    Description = "Custom backup directory (default: <repo-root>/backup)",
    Recursive = true
};

// ============================================================================
// Root Command
// ============================================================================

var rootCommand = new RootCommand("Plugin marketplace asset manager")
{
    editionOption, targetOption, exactOption, forceOption, dryRunOption, verboseOption, backupPathOption
};

// ============================================================================
// Category Commands
// ============================================================================

// Helper to create an action subcommand and wire up the handler
Command Action(string name, string description, Action<ParseResult> handler)
{
    var cmd = new Command(name, description);
    cmd.SetAction(handler);
    return cmd;
}

// ----- Skills -----
var pluginOption = new Option<string?>("--plugin")
{
    Description = "Filter to a specific plugin group (e.g., dotnet-runtime)",
    Recursive = true
};
var skillOption = new Option<string?>("--skill")
{
    Description = "Filter to a specific skill by name (e.g., ci-analysis)",
    Recursive = true
};
var scopeOption = new Option<string>("--scope")
{
    Description = "Install target: personal (~/.copilot/skills/ + ~/.claude/skills/) or project (.github/skills/)",
    DefaultValueFactory = _ => "personal",
    Recursive = true
};
scopeOption.AcceptOnlyFromAmong("personal", "project");
var skillsCmd = new Command("skills", "Manage agent skills (SKILL.md folders)");
skillsCmd.Options.Add(pluginOption);
skillsCmd.Options.Add(skillOption);
skillsCmd.Options.Add(scopeOption);
skillsCmd.Subcommands.Add(Action("list", "List installed skills", pr =>
    RunSkillsList(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
skillsCmd.Subcommands.Add(Action("install", "Install skills from repo", pr =>
    RunSkillsInstall(pr.GetValue(exactOption), pr.GetValue(forceOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
skillsCmd.Subcommands.Add(Action("uninstall", "Remove repo-managed skills", pr =>
    RunSkillsUninstall(pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
skillsCmd.Subcommands.Add(Action("diff", "Compare repo vs installed skills", pr =>
    RunSkillsDiff(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
rootCommand.Subcommands.Add(skillsCmd);

// ----- Prompts -----
var promptsCmd = new Command("prompts", "Manage prompt files (.prompt.md)");
promptsCmd.Subcommands.Add(Action("list", "List installed prompts", pr =>
    RunFileCategoryList(pr.GetValue(editionOption), "prompts", "prompts", "*.prompt.md", "Prompts", pr.GetValue(verboseOption))));
promptsCmd.Subcommands.Add(Action("install", "Install prompts from repo", pr =>
    RunFileCategoryInstall(pr.GetValue(editionOption), "prompts", "prompts", "*.prompt.md", "Prompts",
        pr.GetValue(exactOption), pr.GetValue(forceOption), pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
promptsCmd.Subcommands.Add(Action("uninstall", "Remove repo-managed prompts", pr =>
    RunFileCategoryUninstall(pr.GetValue(editionOption), "prompts", "prompts", "*.prompt.md", "Prompts",
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
promptsCmd.Subcommands.Add(Action("diff", "Compare repo vs installed prompts", pr =>
    RunFileCategoryDiff(pr.GetValue(editionOption), "prompts", "prompts", "*.prompt.md", "Prompts", pr.GetValue(verboseOption))));
rootCommand.Subcommands.Add(promptsCmd);

// ----- Agents -----
var agentsCmd = new Command("agents", "Manage custom agents (.agent.md)");
agentsCmd.Options.Add(pluginOption);
agentsCmd.Options.Add(scopeOption);
agentsCmd.Subcommands.Add(Action("list", "List installed agents", pr =>
    RunAgentsList(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
agentsCmd.Subcommands.Add(Action("install", "Install agents from repo", pr =>
    RunAgentsInstall(pr.GetValue(exactOption), pr.GetValue(forceOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
agentsCmd.Subcommands.Add(Action("uninstall", "Remove repo-managed agents", pr =>
    RunAgentsUninstall(pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
agentsCmd.Subcommands.Add(Action("diff", "Compare repo vs installed agents", pr =>
    RunAgentsDiff(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption), pr.GetValue(targetOption))));
rootCommand.Subcommands.Add(agentsCmd);

// ----- Plugin -----
var pluginCmd = new Command("plugin", "Manage plugin groups (skills + agents + MCP servers + LSP servers)");
pluginCmd.Options.Add(scopeOption);
{
    var nameArg = new Argument<string?>("name")
    {
        Description = "Plugin group name (e.g., dotnet-dnceng). Omit to operate on all plugins.",
        Arity = ArgumentArity.ZeroOrOne
    };
    var installCmd = new Command("install", "Install all assets from a plugin group") { nameArg };
    installCmd.SetAction(pr => RunPluginInstall(pr.GetValue(nameArg),
        pr.GetValue(forceOption), pr.GetValue(dryRunOption), pr.GetValue(verboseOption),
        pr.GetValue(editionOption), pr.GetValue(scopeOption), pr.GetValue(targetOption)));
    pluginCmd.Subcommands.Add(installCmd);

    var listNameArg = new Argument<string?>("name") { Description = nameArg.Description, Arity = ArgumentArity.ZeroOrOne };
    var listCmd = new Command("list", "Show plugins and their contents") { listNameArg };
    listCmd.SetAction(pr => RunPluginList(pr.GetValue(listNameArg), pr.GetValue(verboseOption)));
    pluginCmd.Subcommands.Add(listCmd);

    var diffNameArg = new Argument<string?>("name") { Description = nameArg.Description, Arity = ArgumentArity.ZeroOrOne };
    var diffCmd = new Command("diff", "Compare plugin assets: repo vs installed") { diffNameArg };
    diffCmd.SetAction(pr => RunPluginDiff(pr.GetValue(diffNameArg), pr.GetValue(verboseOption),
        pr.GetValue(editionOption), pr.GetValue(scopeOption), pr.GetValue(targetOption)));
    pluginCmd.Subcommands.Add(diffCmd);

    var uninstallNameArg = new Argument<string?>("name") { Description = nameArg.Description, Arity = ArgumentArity.ZeroOrOne };
    var uninstallCmd = new Command("uninstall", "Remove a plugin's installed assets") { uninstallNameArg };
    uninstallCmd.SetAction(pr => RunPluginUninstall(pr.GetValue(uninstallNameArg),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption),
        pr.GetValue(editionOption), pr.GetValue(scopeOption), pr.GetValue(targetOption)));
    pluginCmd.Subcommands.Add(uninstallCmd);
}
rootCommand.Subcommands.Add(pluginCmd);

// ----- Instructions -----
var instrCmd = new Command("instructions", "Manage instruction files");
instrCmd.Subcommands.Add(Action("list", "List installed instructions", pr =>
    RunInstructionsList(pr.GetValue(verboseOption))));
instrCmd.Subcommands.Add(Action("install", "Install instructions from repo", pr =>
    RunInstructionsInstall(pr.GetValue(editionOption), pr.GetValue(exactOption), pr.GetValue(forceOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
instrCmd.Subcommands.Add(Action("uninstall", "Remove repo-managed instructions", pr =>
    RunInstructionsUninstall(pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
instrCmd.Subcommands.Add(Action("diff", "Compare repo vs installed instructions", pr =>
    RunInstructionsDiff(pr.GetValue(verboseOption))));
rootCommand.Subcommands.Add(instrCmd);

// ----- MCP -----
var mcpCmd = new Command("mcp", "Manage MCP server configuration");
mcpCmd.Subcommands.Add(Action("list", "List configured MCP servers", pr =>
    RunMcpList(pr.GetValue(editionOption), pr.GetValue(targetOption), pr.GetValue(verboseOption))));
mcpCmd.Subcommands.Add(Action("install", "Merge template MCP servers into config", pr =>
    RunMcpInstall(pr.GetValue(editionOption), pr.GetValue(targetOption), pr.GetValue(exactOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
mcpCmd.Subcommands.Add(Action("uninstall", "Remove template MCP servers from config", pr =>
    RunMcpUninstall(pr.GetValue(editionOption), pr.GetValue(targetOption), pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
mcpCmd.Subcommands.Add(Action("diff", "Compare template vs installed MCP servers", pr =>
    RunMcpDiff(pr.GetValue(editionOption), pr.GetValue(targetOption), pr.GetValue(verboseOption))));
rootCommand.Subcommands.Add(mcpCmd);

// ----- Settings -----
var settingsCmd = new Command("settings", "Manage VS Code settings");
settingsCmd.Subcommands.Add(Action("list", "List copilot/chat settings", pr =>
    RunSettingsList(pr.GetValue(editionOption), pr.GetValue(verboseOption))));
settingsCmd.Subcommands.Add(Action("update", "Ensure required settings are present", pr =>
    RunSettingsUpdate(pr.GetValue(editionOption), pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
settingsCmd.Subcommands.Add(Action("diff", "Show missing or different settings", pr =>
    RunSettingsDiff(pr.GetValue(editionOption), pr.GetValue(verboseOption))));
rootCommand.Subcommands.Add(settingsCmd);

// ----- Marketplace (repo-scoped) -----
var mktCmd = new Command("marketplace", "Manage repo-scoped marketplace settings (.github/copilot/settings.json)");
{
    var targetDirOption = new Option<string?>("--target-dir")
    {
        Description = "Target repo directory (defaults to current working directory)"
    };
    var mirrorOption = new Option<string?>("--mirror")
    {
        Description = "Mirror name from public-mirrors.json (defaults to this repo's marketplace)"
    };
    var pluginsOption = new Option<string[]>("--plugin")
    {
        Description = "Plugin names to enable (defaults to all in marketplace)",
        AllowMultipleArgumentsPerToken = true
    };
    var mktInstallCmd = new Command("install", "Generate .github/copilot/settings.json for a target repo")
    {
        targetDirOption, mirrorOption, pluginsOption
    };
    mktInstallCmd.SetAction(pr => RunMarketplaceInstall(
        pr.GetValue(targetDirOption), pr.GetValue(mirrorOption), pr.GetValue(pluginsOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption)));
    mktCmd.Subcommands.Add(mktInstallCmd);

    var mktShowCmd = new Command("show", "Show what .github/copilot/settings.json would contain")
    {
        targetDirOption, mirrorOption, pluginsOption
    };
    mktShowCmd.SetAction(pr => RunMarketplaceInstall(
        pr.GetValue(targetDirOption), pr.GetValue(mirrorOption), pr.GetValue(pluginsOption),
        true, pr.GetValue(verboseOption)));
    mktCmd.Subcommands.Add(mktShowCmd);
}
rootCommand.Subcommands.Add(mktCmd);

// ----- Upstream -----
var upstreamCmd = new Command("upstream", "Compare and sync skills with upstream repos (PR-based)");
{
    var upstreamNameOption = new Option<string?>("--upstream")
    {
        Description = "Upstream name from public-mirrors.json (defaults to all)"
    };
    var upstreamSkillOption = new Option<string?>("--skill")
    {
        Description = "Filter to a specific skill"
    };
    var upstreamPluginOption = new Option<string?>("--plugin")
    {
        Description = "Filter to a specific plugin group"
    };
    var jsonOption = new Option<bool>("--json")
    {
        Description = "Output machine-readable JSON"
    };

    var upDiffCmd = new Command("diff", "Compare local vs upstream skill content (bidirectional)")
    {
        upstreamNameOption, upstreamSkillOption, upstreamPluginOption, jsonOption, verboseOption
    };
    upDiffCmd.SetAction(pr => RunUpstreamDiff(
        pr.GetValue(upstreamNameOption), pr.GetValue(upstreamPluginOption),
        pr.GetValue(upstreamSkillOption), pr.GetValue(jsonOption), pr.GetValue(verboseOption)));
    upstreamCmd.Subcommands.Add(upDiffCmd);

    var upSyncCmd = new Command("sync", "Create PRs to push local changes to upstream repos")
    {
        upstreamNameOption, upstreamSkillOption, upstreamPluginOption, dryRunOption, verboseOption
    };
    upSyncCmd.SetAction(pr => RunUpstreamSync(
        pr.GetValue(upstreamNameOption), pr.GetValue(upstreamPluginOption),
        pr.GetValue(upstreamSkillOption), pr.GetValue(dryRunOption), pr.GetValue(verboseOption)));
    upstreamCmd.Subcommands.Add(upSyncCmd);

    var upPullCmd = new Command("pull", "Pull remote-ahead or diverged changes from upstream into local repo")
    {
        upstreamNameOption, upstreamSkillOption, upstreamPluginOption, dryRunOption, forceOption, verboseOption
    };
    upPullCmd.SetAction(pr => RunUpstreamPull(
        pr.GetValue(upstreamNameOption), pr.GetValue(upstreamPluginOption),
        pr.GetValue(upstreamSkillOption), pr.GetValue(dryRunOption), pr.GetValue(forceOption),
        pr.GetValue(verboseOption)));
    upstreamCmd.Subcommands.Add(upPullCmd);
}
rootCommand.Subcommands.Add(upstreamCmd);

// ----- All -----
var allCmd = new Command("all", "Bulk operations across all categories");
allCmd.Subcommands.Add(Action("list", "List all asset types", pr =>
{
    var edition = pr.GetValue(editionOption);
    var target = pr.GetValue(targetOption);
    var verbose = pr.GetValue(verboseOption);
    RunPluginList(null, verbose);
    RunFileCategoryList(edition, "prompts", "prompts", "*.prompt.md", "Prompts", verbose);
    RunInstructionsList(verbose);
    RunMcpList(edition, target, verbose);
    RunSettingsList(edition, verbose);
}));
allCmd.Subcommands.Add(Action("install", "Install everything from repo", pr =>
{
    var edition = pr.GetValue(editionOption);
    var target = pr.GetValue(targetOption);
    var exact = pr.GetValue(exactOption);
    var force = pr.GetValue(forceOption);
    var dryRun = pr.GetValue(dryRunOption);
    var verbose = pr.GetValue(verboseOption);
    var scope = pr.GetValue(scopeOption);
    RunPluginInstall(null, force, dryRun, verbose, edition, scope, target);
    RunFileCategoryInstall(edition, "prompts", "prompts", "*.prompt.md", "Prompts", exact, force, dryRun, verbose);
    RunInstructionsInstall(edition, exact, force, dryRun, verbose);
    RunMcpInstall(edition, target, exact, dryRun, verbose);
    RunSettingsUpdate(edition, dryRun, verbose);
}));
allCmd.Subcommands.Add(Action("uninstall", "Uninstall everything", pr =>
{
    var edition = pr.GetValue(editionOption);
    var target = pr.GetValue(targetOption);
    var dryRun = pr.GetValue(dryRunOption);
    var verbose = pr.GetValue(verboseOption);
    var scope = pr.GetValue(scopeOption);
    RunPluginUninstall(null, dryRun, verbose, edition, scope, target);
    RunFileCategoryUninstall(edition, "prompts", "prompts", "*.prompt.md", "Prompts", dryRun, verbose);
    RunInstructionsUninstall(dryRun, verbose);
    RunMcpUninstall(edition, target, dryRun, verbose);
}));
allCmd.Subcommands.Add(Action("diff", "Diff all asset types", pr =>
{
    var edition = pr.GetValue(editionOption);
    var target = pr.GetValue(targetOption);
    var verbose = pr.GetValue(verboseOption);
    var scope = pr.GetValue(scopeOption);
    RunPluginDiff(null, verbose, edition, scope, target);
    RunFileCategoryDiff(edition, "prompts", "prompts", "*.prompt.md", "Prompts", verbose);
    RunInstructionsDiff(verbose);
    RunMcpDiff(edition, target, verbose);
    RunSettingsDiff(edition, verbose);
}));
rootCommand.Subcommands.Add(allCmd);

// ----- Bootstrap -----
var bootstrapCmd = new Command("bootstrap", "Clone repo, install all assets, and clean up");
bootstrapCmd.SetAction(pr =>
{
    var edition = pr.GetValue(editionOption);
    var dryRun = pr.GetValue(dryRunOption);
    var verbose = pr.GetValue(verboseOption);
    var originalCwd = Environment.CurrentDirectory;

    // Default backup path to CWD when bootstrapping (temp repo will be deleted)
    _customBackupPath ??= Path.Combine(originalCwd, "backup");

    var tempDir = Path.Combine(Path.GetTempPath(), $"plugin-cli-{Guid.NewGuid():N}");

    PrintHeader("Bootstrap");
    Console.WriteLine($"  Cloning repo to {tempDir}...");

    var cloneUrl = GetGitRemoteUrl() ?? "https://github.com/Blazor-Playground/copilot-skills.git";
    var exitCode = RunProcess("git", $"clone --depth 1 {cloneUrl} \"{tempDir}\"");
    if (exitCode != 0)
    {
        PrintError("  Failed to clone repository. Is git installed and on PATH?");
        return;
    }

    // Point all install logic at the cloned repo
    repoRoot = tempDir;

    Console.WriteLine();
    RunSkillsInstall(false, true, dryRun, verbose, null, null, "personal", "auto");
    RunAgentsInstall(false, true, dryRun, verbose, null, "personal", "auto");
    RunFileCategoryInstall(edition, "prompts", "prompts", "*.prompt.md", "Prompts", false, true, dryRun, verbose);
    RunInstructionsInstall(edition, false, true, dryRun, verbose);
    RunMcpInstall(edition, "auto", false, dryRun, verbose);
    RunSettingsUpdate(edition, dryRun, verbose);

    // Clean up temp clone
    Console.WriteLine();
    if (dryRun)
    {
        PrintInfo($"  Would delete temp clone: {tempDir}");
    }
    else
    {
        try
        {
            // Git creates read-only files; clear attributes before deleting
            foreach (var file in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(tempDir, true);
            PrintSuccess("  Cleaned up temp clone");
        }
        catch (Exception ex)
        {
            PrintWarning($"  Could not fully clean up {tempDir}: {ex.Message}");
            PrintWarning("  You can delete it manually.");
        }
    }

    Console.WriteLine();
    PrintSuccess("  Bootstrap complete!");
    if (_customBackupPath != null && Directory.Exists(_customBackupPath))
        PrintInfo($"  Backups saved to: {_customBackupPath}");
});
rootCommand.Subcommands.Add(bootstrapCmd);

// ============================================================================
// Parse & Invoke
// ============================================================================

var parseResult = rootCommand.Parse(args);
_customBackupPath = parseResult.GetValue(backupPathOption);
return parseResult.Invoke();

// ============================================================================
// Skills Handlers (folder-based, edition-independent)
// ============================================================================

// Discovers assets from plugins/*/<subDir>/ (marketplace layout)
// For skills: discovers subdirectories containing a sentinel file (e.g., SKILL.md)
// For agents: discovers matching files (e.g., *.agent.md)
// Falls back to flat <fallbackDir>/ if plugins/ doesn't exist
List<(string pluginName, string assetName, string assetPath)> GetSourceAssets(
    string subDir, string? pluginFilter, string? nameFilter,
    string? sentinelFile = null, string? filePattern = null)
{
    var pluginsDir = Path.Combine(repoRoot, "plugins");
    if (Directory.Exists(pluginsDir))
    {
        var plugins = Directory.GetDirectories(pluginsDir);
        if (pluginFilter != null)
            plugins = plugins.Where(p => Path.GetFileName(p)!
                .Equals(pluginFilter, StringComparison.OrdinalIgnoreCase)).ToArray();

        var results = plugins
            .SelectMany(p =>
            {
                var assetDir = Path.Combine(p, subDir);
                if (!Directory.Exists(assetDir))
                    return Enumerable.Empty<(string pluginName, string assetName, string assetPath)>();
                if (sentinelFile != null)
                {
                    // Directory-based assets (skills)
                    return Directory.GetDirectories(assetDir)
                        .Where(d => File.Exists(Path.Combine(d, sentinelFile)))
                        .Select(d => (pluginName: Path.GetFileName(p)!,
                                      assetName: Path.GetFileName(d)!,
                                      assetPath: d));
                }
                else if (filePattern != null)
                {
                    // File-based assets (agents)
                    return Directory.GetFiles(assetDir, filePattern)
                        .Select(f => (pluginName: Path.GetFileName(p)!,
                                      assetName: Path.GetFileNameWithoutExtension(
                                          Path.GetFileNameWithoutExtension(f))!,
                                      assetPath: f));
                }
                return Enumerable.Empty<(string pluginName, string assetName, string assetPath)>();
            })
            .ToList();

        if (nameFilter != null)
            results = results.Where(s => s.assetName
                .Equals(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        return results;
    }

    // Fallback: flat directory
    var flatDir = Path.Combine(repoRoot, subDir);
    if (!Directory.Exists(flatDir)) return [];

    List<(string pluginName, string assetName, string assetPath)> flat;
    if (sentinelFile != null)
    {
        flat = Directory.GetDirectories(flatDir)
            .Where(d => File.Exists(Path.Combine(d, sentinelFile)))
            .Select(d => (pluginName: "(flat)", assetName: Path.GetFileName(d)!, assetPath: d))
            .ToList();
    }
    else if (filePattern != null)
    {
        flat = Directory.GetFiles(flatDir, filePattern)
            .Select(f => (pluginName: "(flat)",
                          assetName: Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f))!,
                          assetPath: f))
            .ToList();
    }
    else return [];

    if (nameFilter != null)
        flat = flat.Where(s => s.assetName
            .Equals(nameFilter, StringComparison.OrdinalIgnoreCase)).ToList();

    return flat;
}

List<(string pluginName, string assetName, string assetPath)> GetSourceSkills(string? pluginFilter, string? skillFilter = null)
    => GetSourceAssets("skills", pluginFilter, skillFilter, sentinelFile: "SKILL.md");

List<(string pluginName, string assetName, string assetPath)> GetSourceAgents(string? pluginFilter, string? nameFilter = null)
    => GetSourceAssets("agents", pluginFilter, nameFilter, filePattern: "*.agent.md");

/// <summary>Get all plugin group names from the plugins/ directory.</summary>
List<string> GetPluginNames(string? pluginFilter = null)
{
    var pluginsDir = Path.Combine(repoRoot, "plugins");
    if (!Directory.Exists(pluginsDir)) return [];
    var names = Directory.GetDirectories(pluginsDir)
        .Select(Path.GetFileName)
        .Where(n => n != null)
        .Cast<string>()
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();
    if (pluginFilter != null)
        names = names.Where(n => n.Equals(pluginFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    return names;
}

/// <summary>Get plugin names or print a warning if none found. Returns empty list on failure.</summary>
List<string> GetPluginsOrWarn(string? pluginFilter)
{
    var plugins = GetPluginNames(pluginFilter);
    if (plugins.Count == 0)
        PrintWarning(pluginFilter != null
            ? $"  Plugin '{pluginFilter}' not found"
            : "  No plugins found");
    return plugins;
}

/// <summary>Read a plugin.json manifest and extract mcpServers and lspServers.</summary>
(Dictionary<string, JsonNode?>? mcpServers, Dictionary<string, JsonNode?>? lspServers)
    ReadPluginServers(string pluginName)
{
    var pluginJsonPath = Path.Combine(repoRoot, "plugins", pluginName, "plugin.json");
    if (!File.Exists(pluginJsonPath)) return (null, null);

    var node = ReadJsoncNode(pluginJsonPath);
    if (node is not JsonObject obj) return (null, null);

    Dictionary<string, JsonNode?>? mcpServers = null;
    if (obj.ContainsKey("mcpServers") && obj["mcpServers"] is JsonObject mcpObj)
        mcpServers = mcpObj.ToDictionary(kv => kv.Key, kv => (JsonNode?)kv.Value?.DeepClone());

    Dictionary<string, JsonNode?>? lspServers = null;
    if (obj.ContainsKey("lspServers") && obj["lspServers"] is JsonObject lspObj)
        lspServers = lspObj.ToDictionary(kv => kv.Key, kv => (JsonNode?)kv.Value?.DeepClone());

    return (mcpServers, lspServers);
}

void RunSkillsList(bool verbose, string? pluginFilter, string? skillFilter, string scope, string target)
{
    foreach (var (label, dir) in GetSkillsTargetDirs(scope, target))
        RunAssetList($"Skills [{label}]", scope, dir, GetSourceSkills(pluginFilter, skillFilter), pluginFilter,
            targetDir => Directory.Exists(targetDir)
                ? Directory.GetDirectories(targetDir)
                    .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
                    .Select(Path.GetFileName).ToList()!
                : []);
}

void RunAgentsList(bool verbose, string? pluginFilter, string scope, string target)
{
    foreach (var (label, dir) in GetAgentsTargetDirs(scope, target))
        RunAssetList($"Agents [{label}]", scope, dir, GetSourceAgents(pluginFilter), pluginFilter,
            targetDir => Directory.Exists(targetDir)
                ? Directory.GetFiles(targetDir, "*.agent.md")
                    .Select(Path.GetFileName).ToList()!
                : []);
}

void RunAssetList(string label, string scope, string targetDir,
    List<(string pluginName, string assetName, string assetPath)> source, string? pluginFilter,
    Func<string, List<string>> enumerateInstalled)
{
    PrintHeader($"{label} ({scope})");
    if (pluginFilter == null)
        source = ResolveDuplicateAssets(source);
    if (source.Count > 0)
    {
        Console.WriteLine($"  Repo {label.ToLowerInvariant()}:");
        foreach (var group in source.GroupBy(s => s.pluginName))
        {
            Console.WriteLine($"    [{group.Key}]");
            foreach (var s in group)
                Console.WriteLine($"      {s.assetName}");
        }
    }

    Console.WriteLine();
    var installed = enumerateInstalled(targetDir);
    if (installed.Count == 0)
    {
        PrintInfo($"  Installed: ({(Directory.Exists(targetDir) ? "none" : $"no {label.ToLowerInvariant()} directory found")})");
        return;
    }
    Console.WriteLine("  Installed:");
    foreach (var item in installed)
        Console.WriteLine($"    {item}");
}

void RunSkillsInstall(bool exact, bool force, bool dryRun, bool verbose, string? pluginFilter, string? skillFilter, string scope, string target)
{
    foreach (var (label, dir) in GetSkillsTargetDirs(scope, target))
        RunAssetInstall($"Skills [{label}]", dir, scope,
            GetSourceSkills(pluginFilter, skillFilter), pluginFilter,
            exact, force, dryRun, verbose,
            assetPath => Path.Combine(dir, Path.GetFileName(assetPath)),
            dst => Directory.Exists(dst),
            (dst, _) => BackupDirectory(dst, "skills", null),
            (src, dst) => CopyDirectoryRecursive(src, dst),
            targetDir => Directory.Exists(targetDir)
                ? Directory.GetDirectories(targetDir)
                    .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
                    .Select(Path.GetFileName).ToList()!
                : [],
            path => { BackupDirectory(path, "skills", null); Directory.Delete(path, true); });
}

void RunAgentsInstall(bool exact, bool force, bool dryRun, bool verbose, string? pluginFilter, string scope, string target)
{
    foreach (var (label, dir) in GetAgentsTargetDirs(scope, target))
        RunAssetInstall($"Agents [{label}]", dir, scope,
            GetSourceAgents(pluginFilter), pluginFilter,
            exact, force, dryRun, verbose,
            assetPath => Path.Combine(dir, Path.GetFileName(assetPath)),
            dst => File.Exists(dst),
            (dst, _) => BackupFile(dst, "agents", null),
            (src, dst) => File.Copy(src, dst, true),
            targetDir => Directory.Exists(targetDir)
                ? Directory.GetFiles(targetDir, "*.agent.md")
                    .Select(Path.GetFileName).ToList()!
                : [],
            path => { BackupFile(path, "agents", null); File.Delete(path); });
}

void RunAssetInstall(string label, string targetDir, string scope,
    List<(string pluginName, string assetName, string assetPath)> source, string? pluginFilter,
    bool exact, bool force, bool dryRun, bool verbose,
    Func<string, string> getDstPath,
    Func<string, bool> existsAtTarget,
    Action<string, string> backupTarget,
    Action<string, string> copyAsset,
    Func<string, List<string>> enumerateInstalled,
    Action<string> removeAsset)
{
    PrintHeader($"{label} install ({scope})");
    if (pluginFilter == null)
        source = ResolveDuplicateAssets(source);

    if (source.Count == 0)
    {
        PrintWarning(pluginFilter != null
            ? $"  No {label.ToLowerInvariant()} found for plugin '{pluginFilter}'"
            : $"  No {label.ToLowerInvariant()} found in repo");
        return;
    }

    if (!dryRun)
        Directory.CreateDirectory(targetDir);

    foreach (var group in source.GroupBy(s => s.pluginName))
    {
        if (verbose) PrintInfo($"  [{group.Key}]");
        foreach (var (_, assetName, assetPath) in group)
        {
            var dst = getDstPath(assetPath);
            var displayName = Path.GetFileName(dst);

            if (existsAtTarget(dst))
            {
                if (!force && !exact)
                {
                    if (verbose) PrintWarning($"    Skipped (exists): {displayName}");
                    continue;
                }
                if (!dryRun) backupTarget(dst, assetName);
            }

            if (dryRun)
                PrintInfo($"    Would install: {displayName}");
            else
            {
                copyAsset(assetPath, dst);
                PrintSuccess($"    Installed: {displayName}");
            }
        }
    }

    if (exact)
    {
        var sourceNames = source.Select(s => Path.GetFileName(getDstPath(s.assetPath)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var installed = enumerateInstalled(targetDir)
            .Where(n => !sourceNames.Contains(n))
            .ToList();

        foreach (var item in installed)
        {
            var path = Path.Combine(targetDir, item);
            if (dryRun)
                PrintInfo($"  Would remove (not in repo): {item}");
            else
            {
                removeAsset(path);
                PrintWarning($"  Removed (not in repo): {item}");
            }
        }
    }
}

void RunSkillsUninstall(bool dryRun, bool verbose, string? pluginFilter, string? skillFilter, string scope, string target)
{
    foreach (var (label, dir) in GetSkillsTargetDirs(scope, target))
        RunAssetUninstall($"Skills [{label}]", dir, scope,
            GetSourceSkills(pluginFilter, skillFilter), pluginFilter, dryRun, verbose,
            assetPath => Path.Combine(dir, Path.GetFileName(assetPath)),
            dst => Directory.Exists(dst),
            dst => { BackupDirectory(dst, "skills", null); Directory.Delete(dst, true); });
}

void RunAgentsUninstall(bool dryRun, bool verbose, string? pluginFilter, string scope, string target)
{
    foreach (var (label, dir) in GetAgentsTargetDirs(scope, target))
        RunAssetUninstall($"Agents [{label}]", dir, scope,
            GetSourceAgents(pluginFilter), pluginFilter, dryRun, verbose,
            assetPath => Path.Combine(dir, Path.GetFileName(assetPath)),
            dst => File.Exists(dst),
            dst => { BackupFile(dst, "agents", null); File.Delete(dst); });
}

void RunAssetUninstall(string label, string targetDir, string scope,
    List<(string pluginName, string assetName, string assetPath)> source, string? pluginFilter,
    bool dryRun, bool verbose,
    Func<string, string> getDstPath,
    Func<string, bool> existsAtTarget,
    Action<string> removeAsset)
{
    PrintHeader($"{label} uninstall ({scope})");
    if (pluginFilter == null)
        source = ResolveDuplicateAssets(source);

    foreach (var (_, assetName, assetPath) in source)
    {
        var dst = getDstPath(assetPath);
        var displayName = Path.GetFileName(dst);
        if (!existsAtTarget(dst))
        {
            if (verbose) PrintInfo($"  Not installed: {displayName}");
            continue;
        }
        if (dryRun)
            PrintInfo($"  Would remove: {displayName}");
        else
        {
            removeAsset(dst);
            PrintSuccess($"  Removed: {displayName}");
        }
    }
}

void RunSkillsDiff(bool verbose, string? pluginFilter, string? skillFilter, string scope, string target)
{
    foreach (var (label, dir) in GetSkillsTargetDirs(scope, target))
    {
        PrintHeader($"Skills diff [{label}] ({scope})");
        var source = GetSourceSkills(pluginFilter, skillFilter);
        if (pluginFilter == null)
            source = ResolveDuplicateAssets(source);

        var sourceMap = source.ToDictionary(s => s.assetName, s => s.assetPath, StringComparer.OrdinalIgnoreCase);
        var targetItems = Directory.Exists(dir)
            ? Directory.GetDirectories(dir)
                .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
                .Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        DiffSets(sourceMap.Keys, targetItems!, "  ",
            name => File.ReadAllText(Path.Combine(sourceMap[name], "SKILL.md")),
            name => File.ReadAllText(Path.Combine(dir, name, "SKILL.md")));
    }
}

void RunAgentsDiff(bool verbose, string? pluginFilter, string scope, string target)
{
    foreach (var (label, dir) in GetAgentsTargetDirs(scope, target))
    {
        PrintHeader($"Agents diff [{label}] ({scope})");
        var source = GetSourceAgents(pluginFilter);
        if (pluginFilter == null)
            source = ResolveDuplicateAssets(source);

        var sourceMap = source.ToDictionary(
            a => Path.GetFileName(a.assetPath), a => a.assetPath, StringComparer.OrdinalIgnoreCase);
        var targetItems = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.agent.md")
                .Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        DiffSets(sourceMap.Keys, targetItems!, "  ",
            name => File.ReadAllText(sourceMap[name]),
            name => File.ReadAllText(Path.Combine(dir, name)));
    }
}

// Shared set-based diff: +/-/~ comparison between source and target item sets
void DiffSets(ICollection<string> sourceNames, ICollection<string> targetNames, string indent,
    Func<string, string>? getSourceContent = null, Func<string, string>? getTargetContent = null)
{
    bool hasDiffs = false;
    foreach (var s in sourceNames.Where(s => !targetNames.Contains(s)))
    {
        Console.WriteLine($"{indent}+ {s} (in repo, not installed)");
        hasDiffs = true;
    }
    foreach (var s in targetNames.Where(s => !sourceNames.Contains(s)))
    {
        Console.WriteLine($"{indent}- {s} (installed, not in repo)");
        hasDiffs = true;
    }
    if (getSourceContent != null && getTargetContent != null)
    {
        foreach (var s in sourceNames.Where(s => targetNames.Contains(s)))
        {
            try
            {
                if (getSourceContent(s) != getTargetContent(s))
                {
                    Console.WriteLine($"{indent}~ {s} (modified)");
                    hasDiffs = true;
                }
            }
            catch { }
        }
    }
    if (!hasDiffs) PrintInfo($"{indent}(no differences)");
}

// ============================================================================
// File-Based Category Handlers (prompts — edition-dependent)
// ============================================================================

void RunFileCategoryList(string edition, string repoSubDir, string targetSubDir,
    string pattern, string label, bool verbose)
{
    PrintHeader(label);
    foreach (var ed in GetEditions(edition))
    {
        var targetDir = Path.Combine(GetVSCodeUserDir(ed), targetSubDir);
        Console.WriteLine($"  [{ed}] {targetDir}");
        if (!Directory.Exists(targetDir))
        {
            PrintInfo("    (directory not found)");
            continue;
        }
        var files = Directory.GetFiles(targetDir, pattern);
        if (files.Length == 0)
        {
            PrintInfo("    (none)");
            continue;
        }
        foreach (var file in files)
            Console.WriteLine($"    {Path.GetFileName(file)}");
    }
}

void RunFileCategoryInstall(string edition, string repoSubDir, string targetSubDir,
    string pattern, string label, bool exact, bool force, bool dryRun, bool verbose)
{
    PrintHeader($"{label} install");
    var sourceDir = Path.Combine(repoRoot, repoSubDir);

    if (!Directory.Exists(sourceDir))
    {
        PrintWarning($"  No {repoSubDir}/ directory in repo");
        return;
    }

    var sourceFiles = Directory.GetFiles(sourceDir, pattern);
    if (sourceFiles.Length == 0 && !exact)
    {
        PrintInfo($"  No {pattern} files to install");
        return;
    }

    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var targetDir = Path.Combine(GetVSCodeUserDir(ed), targetSubDir);
        if (!dryRun)
            Directory.CreateDirectory(targetDir);

        SyncFiles(sourceFiles, targetDir, label.ToLowerInvariant(), ed, exact, force, dryRun, verbose);

        if (exact)
            RemoveExtraFiles(sourceFiles, targetDir, pattern, label.ToLowerInvariant(), ed, dryRun, verbose);
    }
}

void RunFileCategoryUninstall(string edition, string repoSubDir, string targetSubDir,
    string pattern, string label, bool dryRun, bool verbose)
{
    PrintHeader($"{label} uninstall");
    var sourceDir = Path.Combine(repoRoot, repoSubDir);

    var sourceFiles = Directory.Exists(sourceDir)
        ? Directory.GetFiles(sourceDir, pattern)
        : [];

    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var targetDir = Path.Combine(GetVSCodeUserDir(ed), targetSubDir);
        foreach (var src in sourceFiles)
        {
            var fileName = Path.GetFileName(src);
            var target = Path.Combine(targetDir, fileName);
            if (!File.Exists(target))
            {
                if (verbose) PrintInfo($"    Not installed: {fileName}");
                continue;
            }
            if (!dryRun) BackupFile(target, label.ToLowerInvariant(), ed);
            if (dryRun)
                PrintInfo($"    Would remove: {fileName}");
            else
            {
                File.Delete(target);
                PrintSuccess($"    Removed: {fileName}");
            }
        }
    }
}

void RunFileCategoryDiff(string edition, string repoSubDir, string targetSubDir,
    string pattern, string label, bool verbose)
{
    PrintHeader($"{label} diff");
    var sourceDir = Path.Combine(repoRoot, repoSubDir);

    var sourceFileNames = Directory.Exists(sourceDir)
        ? Directory.GetFiles(sourceDir, pattern).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var targetDir = Path.Combine(GetVSCodeUserDir(ed), targetSubDir);
        var targetFileNames = Directory.Exists(targetDir)
            ? Directory.GetFiles(targetDir, pattern).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

        DiffSets(sourceFileNames!, targetFileNames!, "    ",
            name => File.ReadAllText(Path.Combine(sourceDir, name)),
            name => File.ReadAllText(Path.Combine(targetDir, name)));
    }
}

// ============================================================================
// Instructions Handlers (edition-independent target, but settings update is per-edition)
// ============================================================================

void RunInstructionsList(bool verbose)
{
    var targetDir = GetInstructionsTargetDir();
    PrintHeader("Instructions");
    Console.WriteLine($"  Profile: {targetDir}");
    if (!Directory.Exists(targetDir))
    {
        PrintInfo("    (directory not found)");
    }
    else
    {
        var files = Directory.GetFiles(targetDir, "*.md");
        if (files.Length == 0)
        {
            PrintInfo("    (none)");
        }
        else
        {
            foreach (var file in files)
                Console.WriteLine($"    {Path.GetFileName(file)}");
        }
    }

    // Copilot CLI local instructions (~/.copilot/copilot-instructions.md)
    var cliInstrPath = Path.Combine(GetCopilotCliDir(), "copilot-instructions.md");
    Console.WriteLine($"  CLI: {cliInstrPath}");
    if (File.Exists(cliInstrPath))
        Console.WriteLine("    copilot-instructions.md");
    else
        PrintInfo("    (file not found)");
}

void RunInstructionsInstall(string edition, bool exact, bool force, bool dryRun, bool verbose)
{
    PrintHeader("Instructions install");
    var sourceDir = Path.Combine(repoRoot, "instructions");
    var targetDir = GetInstructionsTargetDir();

    if (!Directory.Exists(sourceDir))
    {
        PrintWarning("  No instructions/ directory in repo");
        return;
    }

    var sourceFiles = Directory.GetFiles(sourceDir, "*.md");
    if (!dryRun)
        Directory.CreateDirectory(targetDir);

    Console.WriteLine($"  Target: {targetDir}");
    SyncFiles(sourceFiles, targetDir, "instructions", null, exact, force, dryRun, verbose);

    if (exact)
        RemoveExtraFiles(sourceFiles, targetDir, "*.md", "instructions", null, dryRun, verbose);

    // Ensure chat.instructionFilesLocations setting is present
    EnsureInstructionsSetting(edition, dryRun, verbose);

    // Copilot CLI local instructions (~/.copilot/copilot-instructions.md)
    InstallCliInstructions(sourceFiles, force, dryRun, verbose);
}

void RunInstructionsUninstall(bool dryRun, bool verbose)
{
    PrintHeader("Instructions uninstall");
    var sourceDir = Path.Combine(repoRoot, "instructions");
    var targetDir = GetInstructionsTargetDir();

    var sourceFiles = Directory.Exists(sourceDir)
        ? Directory.GetFiles(sourceDir, "*.md")
        : [];

    foreach (var src in sourceFiles)
    {
        var fileName = Path.GetFileName(src);
        var target = Path.Combine(targetDir, fileName);
        if (!File.Exists(target))
        {
            if (verbose) PrintInfo($"    Not installed: {fileName}");
            continue;
        }
        if (!dryRun) BackupFile(target, "instructions", null);
        if (dryRun)
            PrintInfo($"    Would remove: {fileName}");
        else
        {
            File.Delete(target);
            PrintSuccess($"    Removed: {fileName}");
        }
    }

    // Copilot CLI local instructions
    var cliInstrPath = Path.Combine(GetCopilotCliDir(), "copilot-instructions.md");
    if (File.Exists(cliInstrPath))
    {
        if (!dryRun) BackupFile(cliInstrPath, "instructions", "cli");
        if (dryRun)
            PrintInfo($"  Would remove CLI: copilot-instructions.md");
        else
        {
            File.Delete(cliInstrPath);
            PrintSuccess($"  Removed CLI: copilot-instructions.md");
        }
    }
    else if (verbose)
    {
        PrintInfo("  CLI copilot-instructions.md not found");
    }
}

void RunInstructionsDiff(bool verbose)
{
    PrintHeader("Instructions diff");
    var sourceDir = Path.Combine(repoRoot, "instructions");
    var targetDir = GetInstructionsTargetDir();

    var sourceFileNames = Directory.Exists(sourceDir)
        ? Directory.GetFiles(sourceDir, "*.md").Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string?>(StringComparer.OrdinalIgnoreCase);
    var targetFileNames = Directory.Exists(targetDir)
        ? Directory.GetFiles(targetDir, "*.md").Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

    DiffSets(sourceFileNames!, targetFileNames!, "  ",
        name => File.ReadAllText(Path.Combine(sourceDir, name)),
        name => File.ReadAllText(Path.Combine(targetDir, name)));

    // Copilot CLI local instructions diff
    var cliInstrPath = Path.Combine(GetCopilotCliDir(), "copilot-instructions.md");
    Console.WriteLine("  [cli]");
    if (!File.Exists(cliInstrPath))
    {
        if (sourceFileNames!.Count > 0)
            PrintInfo("    + copilot-instructions.md (not installed)");
        else
            PrintInfo("    (no differences)");
    }
    else
    {
        var hasSourceContent = Directory.Exists(sourceDir) && Directory.GetFiles(sourceDir, "*.md").Length > 0;
        if (!hasSourceContent)
            Console.WriteLine("    - copilot-instructions.md (installed, no source instructions)");
        else
            PrintInfo("    (installed)");
    }
}

// ============================================================================
// MCP Handlers (edition-dependent)
// ============================================================================

void RunMcpList(string edition, string target, bool verbose)
{
    PrintHeader("MCP Servers");
    foreach (var mcp in GetMcpTargetPaths(target, edition))
    {
        Console.WriteLine($"  [{mcp.Label}] {mcp.Path}");
        if (!File.Exists(mcp.Path))
        {
            PrintInfo("    (file not found)");
            continue;
        }
        var servers = ReadMcpServers(mcp.Path, mcp.WrapperKey);
        if (servers.Count == 0)
        {
            PrintInfo("    (no servers)");
            continue;
        }
        foreach (var (name, node) in servers)
        {
            var type = node?["type"]?.GetValue<string>() ?? "unknown";
            Console.WriteLine($"    {name} ({type})");
        }
    }

}

/// <summary>Merge servers from a source dictionary into a user config's Servers dictionary.</summary>
/// <returns>Number of servers added or updated.</returns>
int MergeServersIntoConfig(Dictionary<string, JsonNode?> userServers,
    Dictionary<string, JsonNode?> sourceServers, bool dryRun, bool verbose)
{
    int count = 0;
    foreach (var (name, config) in sourceServers)
    {
        var exists = userServers.ContainsKey(name);
        userServers[name] = config?.DeepClone();
        count++;
        if (dryRun)
            PrintInfo($"    Would {(exists ? "update" : "add")}: {name}");
        else if (verbose)
            PrintSuccess($"    {(exists ? "Updated" : "Added")}: {name}");
    }
    return count;
}

/// <summary>Install MCP servers into configs across detected targets, with backup tracking.</summary>
/// <returns>Total number of servers merged across all targets.</returns>
int InstallMcpServersToTargets(Dictionary<string, JsonNode?> servers, string target, string edition,
    bool dryRun, bool verbose, HashSet<string>? backedUp = null)
{
    int total = 0;
    foreach (var mcp in GetMcpTargetPaths(target, edition))
    {
        Console.WriteLine($"    [{mcp.Label}]");
        var userServers = ReadMcpServers(mcp.Path, mcp.WrapperKey);
        if (File.Exists(mcp.Path) && !dryRun && (backedUp == null || backedUp.Add(mcp.Path)))
            BackupFile(mcp.Path, "mcp", mcp.Label);

        total += MergeServersIntoConfig(userServers, servers, dryRun, verbose);

        if (!dryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mcp.Path)!);
            WriteMcpServers(mcp.Path, mcp.WrapperKey, userServers);
            PrintSuccess($"      Wrote: {mcp.Path}");
        }
    }
    return total;
}

void RunMcpInstall(string edition, string target, bool exact, bool dryRun, bool verbose)
{
    PrintHeader("MCP install");
    var templatePath = Path.Combine(repoRoot, "template.mcp.json");
    if (!File.Exists(templatePath))
    {
        PrintWarning("  No template.mcp.json in repo");
        return;
    }

    var template = ReadMcpConfig(templatePath);
    if (template?.Servers == null || template.Servers.Count == 0)
    {
        PrintInfo("  No servers in template");
        return;
    }

    foreach (var mcp in GetMcpTargetPaths(target, edition))
    {
        Console.WriteLine($"  [{mcp.Label}]");
        var userServers = ReadMcpServers(mcp.Path, mcp.WrapperKey);

        if (File.Exists(mcp.Path) && !dryRun) BackupFile(mcp.Path, "mcp", mcp.Label);

        MergeServersIntoConfig(userServers, template.Servers, dryRun, verbose);

        // Exact mode: remove servers not in template
        if (exact)
        {
            var templateNames = template.Servers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toRemove = userServers.Keys
                .Where(k => !templateNames.Contains(k))
                .ToList();
            foreach (var name in toRemove)
            {
                userServers.Remove(name);
                if (dryRun)
                    PrintInfo($"    Would remove (not in template): {name}");
                else
                    PrintWarning($"    Removed (not in template): {name}");
            }
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mcp.Path)!);
            WriteMcpServers(mcp.Path, mcp.WrapperKey, userServers);
            PrintSuccess($"    Wrote: {mcp.Path}");
        }
    }
}

void RunMcpUninstall(string edition, string target, bool dryRun, bool verbose)
{
    PrintHeader("MCP uninstall");
    var templatePath = Path.Combine(repoRoot, "template.mcp.json");
    if (!File.Exists(templatePath))
    {
        PrintWarning("  No template.mcp.json in repo");
        return;
    }

    var template = ReadMcpConfig(templatePath);
    if (template?.Servers == null) return;

    foreach (var mcp in GetMcpTargetPaths(target, edition))
    {
        Console.WriteLine($"  [{mcp.Label}]");
        if (!File.Exists(mcp.Path))
        {
            PrintInfo("    (file not found)");
            continue;
        }

        if (!dryRun) BackupFile(mcp.Path, "mcp", mcp.Label);
        var userServers = ReadMcpServers(mcp.Path, mcp.WrapperKey);
        if (userServers.Count == 0) continue;

        foreach (var name in template.Servers.Keys)
        {
            if (userServers.Remove(name))
            {
                if (dryRun)
                    PrintInfo($"    Would remove: {name}");
                else
                    PrintSuccess($"    Removed: {name}");
            }
            else if (verbose)
            {
                PrintInfo($"    Not present: {name}");
            }
        }

        if (!dryRun) WriteMcpServers(mcp.Path, mcp.WrapperKey, userServers);
    }
}

void RunMcpDiff(string edition, string target, bool verbose)
{
    PrintHeader("MCP diff");
    var templatePath = Path.Combine(repoRoot, "template.mcp.json");
    if (!File.Exists(templatePath))
    {
        PrintWarning("  No template.mcp.json in repo");
        return;
    }

    var template = ReadMcpConfig(templatePath);
    if (template?.Servers == null) return;

    foreach (var mcp in GetMcpTargetPaths(target, edition))
    {
        Console.WriteLine($"  [{mcp.Label}]");
        if (!File.Exists(mcp.Path))
        {
            PrintInfo("    (no config — all template servers would be added)");
            continue;
        }

        var userServers = ReadMcpServers(mcp.Path, mcp.WrapperKey);

        bool hasDiffs = false;
        foreach (var (name, config) in template.Servers)
        {
            if (!userServers.ContainsKey(name))
            {
                Console.WriteLine($"    + {name} (in template, not installed)");
                hasDiffs = true;
            }
            else
            {
                var templateJson = config?.ToJsonString() ?? "";
                var userJson = userServers[name]?.ToJsonString() ?? "";
                if (templateJson != userJson)
                {
                    Console.WriteLine($"    ~ {name} (config differs)");
                    hasDiffs = true;
                }
            }
        }
        foreach (var name in userServers.Keys.Where(k => !template.Servers.ContainsKey(k)))
        {
            Console.WriteLine($"    - {name} (installed, not in template)");
            hasDiffs = true;
        }
        if (!hasDiffs) PrintInfo("    (no differences)");
    }
}

// ============================================================================
// Plugin Handlers (unified plugin group operations)
// ============================================================================

void RunPluginInstall(string? pluginName, bool force, bool dryRun, bool verbose, string edition, string scope, string target = "auto")
{
    var plugins = GetPluginsOrWarn(pluginName);
    if (plugins.Count == 0) return;

    var backedUp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var name in plugins)
    {
        PrintHeader($"Plugin install: {name}");
        int skillCount = 0, agentCount = 0, mcpCount = 0;

        // Skills
        var skills = GetSourceSkills(name);
        if (skills.Count > 0)
        {
            RunSkillsInstall(false, force, dryRun, verbose, name, null, scope, target);
            skillCount = skills.Count;
        }

        // Agents
        var agents = GetSourceAgents(name);
        if (agents.Count > 0)
        {
            RunAgentsInstall(false, force, dryRun, verbose, name, scope, target);
            agentCount = agents.Count;
        }

        // MCP servers from plugin.json
        var (mcpServers, lspServers) = ReadPluginServers(name);
        if (mcpServers != null && mcpServers.Count > 0)
        {
            Console.WriteLine($"  MCP servers ({mcpServers.Count}):");
            mcpCount += InstallMcpServersToTargets(mcpServers, target, edition, dryRun, verbose, backedUp);
        }

        // LSP servers (reported but not installed — managed via plugin system)
        if (lspServers != null && lspServers.Count > 0)
        {
            Console.WriteLine($"  LSP servers ({lspServers.Count}):");
            foreach (var (lspName, _) in lspServers)
                PrintInfo($"    {lspName} (managed via plugin install, not this CLI)");
        }

        // Summary
        Console.WriteLine();
        var parts = new List<string>();
        if (skillCount > 0) parts.Add($"{skillCount} skill{(skillCount != 1 ? "s" : "")}");
        if (agentCount > 0) parts.Add($"{agentCount} agent{(agentCount != 1 ? "s" : "")}");
        if (mcpCount > 0) parts.Add($"{mcpCount} MCP server{(mcpCount != 1 ? "s" : "")}");
        if (lspServers != null && lspServers.Count > 0)
            parts.Add($"{lspServers.Count} LSP server{(lspServers.Count != 1 ? "s" : "")}");

        if (parts.Count > 0)
            PrintSuccess($"  {(dryRun ? "Would install" : "Installed")}: {string.Join(", ", parts)}");
        else
            PrintInfo($"  Plugin '{name}' has no installable assets");
    }
}

void RunPluginList(string? pluginName, bool verbose)
{
    var plugins = GetPluginsOrWarn(pluginName);
    if (plugins.Count == 0) return;

    PrintHeader("Plugins");
    foreach (var name in plugins)
    {
        Console.WriteLine($"  [{name}]");

        var skills = GetSourceSkills(name);
        if (skills.Count > 0)
        {
            Console.WriteLine($"    Skills ({skills.Count}):");
            foreach (var s in skills) Console.WriteLine($"      {s.assetName}");
        }

        var agents = GetSourceAgents(name);
        if (agents.Count > 0)
        {
            Console.WriteLine($"    Agents ({agents.Count}):");
            foreach (var a in agents) Console.WriteLine($"      {a.assetName}");
        }

        var (mcpServers, lspServers) = ReadPluginServers(name);
        if (mcpServers != null && mcpServers.Count > 0)
        {
            Console.WriteLine($"    MCP Servers ({mcpServers.Count}):");
            foreach (var (srvName, node) in mcpServers)
            {
                var type = node?["type"]?.GetValue<string>();
                var cmd = node?["command"]?.GetValue<string>();
                var detail = type == "http" ? node?["url"]?.GetValue<string>() ?? "http" : cmd ?? "unknown";
                Console.WriteLine($"      {srvName} ({detail})");
            }
        }

        if (lspServers != null && lspServers.Count > 0)
        {
            Console.WriteLine($"    LSP Servers ({lspServers.Count}):");
            foreach (var (srvName, node) in lspServers)
            {
                var cmd = node?["command"]?.GetValue<string>() ?? "unknown";
                Console.WriteLine($"      {srvName} ({cmd})");
            }
        }

        if (skills.Count == 0 && agents.Count == 0 &&
            (mcpServers == null || mcpServers.Count == 0) &&
            (lspServers == null || lspServers.Count == 0))
            PrintInfo("    (empty plugin)");
    }
}

void RunPluginDiff(string? pluginName, bool verbose, string edition, string scope, string target = "auto")
{
    var plugins = GetPluginsOrWarn(pluginName);
    if (plugins.Count == 0) return;

    foreach (var name in plugins)
    {
        PrintHeader($"Plugin diff: {name}");
        bool hasDiffs = false;

        // Skills diff
        var skills = GetSourceSkills(name);
        if (skills.Count > 0)
        {
            foreach (var (tlabel, tdir) in GetSkillsTargetDirs(scope, target))
            {
                Console.WriteLine($"  Skills [{tlabel}]:");
                foreach (var (_, assetName, assetPath) in skills)
                {
                    var dst = Path.Combine(tdir, Path.GetFileName(assetPath));
                    if (!Directory.Exists(dst))
                    {
                        Console.WriteLine($"    + {assetName} (not installed)");
                        hasDiffs = true;
                    }
                    else if (verbose)
                        PrintInfo($"    = {assetName}");
                }
            }
        }

        // Agents diff
        var agents = GetSourceAgents(name);
        if (agents.Count > 0)
        {
            foreach (var (tlabel, tdir) in GetAgentsTargetDirs(scope, target))
            {
                Console.WriteLine($"  Agents [{tlabel}]:");
                foreach (var (_, assetName, assetPath) in agents)
                {
                    var dst = Path.Combine(tdir, Path.GetFileName(assetPath));
                    if (!File.Exists(dst))
                    {
                        Console.WriteLine($"    + {assetName} (not installed)");
                        hasDiffs = true;
                    }
                    else if (verbose)
                        PrintInfo($"    = {assetName}");
                }
            }
        }

        // MCP servers diff
        var (mcpServers, lspServers) = ReadPluginServers(name);
        if (mcpServers != null && mcpServers.Count > 0)
        {
            Console.WriteLine($"  MCP Servers:");
            foreach (var mcp in GetMcpTargetPaths(target, edition))
            {
                Console.WriteLine($"    [{mcp.Label}]");
                if (!File.Exists(mcp.Path))
                {
                    foreach (var srvName in mcpServers.Keys)
                    {
                        Console.WriteLine($"      + {srvName} (not installed)");
                        hasDiffs = true;
                    }
                    continue;
                }
                var userServers = ReadMcpServers(mcp.Path, mcp.WrapperKey);
                foreach (var (srvName, config) in mcpServers)
                {
                    if (!userServers.ContainsKey(srvName))
                    {
                        Console.WriteLine($"      + {srvName} (not installed)");
                        hasDiffs = true;
                    }
                    else
                    {
                        var srcJson = config?.ToJsonString() ?? "";
                        var dstJson = userServers[srvName]?.ToJsonString() ?? "";
                        if (srcJson != dstJson)
                        {
                            Console.WriteLine($"      ~ {srvName} (config differs)");
                            hasDiffs = true;
                        }
                        else if (verbose)
                            PrintInfo($"      = {srvName}");
                    }
                }
            }
        }

        // LSP servers diff (informational — managed via plugin install)
        if (lspServers != null && lspServers.Count > 0)
        {
            Console.WriteLine($"  LSP Servers:");
            foreach (var srvName in lspServers.Keys)
                PrintInfo($"    {srvName}");
        }

        if (!hasDiffs) PrintInfo("  (no differences)");
    }
}

void RunPluginUninstall(string? pluginName, bool dryRun, bool verbose, string edition, string scope, string target = "auto")
{
    var plugins = GetPluginsOrWarn(pluginName);
    if (plugins.Count == 0) return;

    var backedUp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var name in plugins)
    {
        PrintHeader($"Plugin uninstall: {name}");

        // Skills
        var skills = GetSourceSkills(name);
        if (skills.Count > 0)
            RunSkillsUninstall(dryRun, verbose, name, null, scope, target);

        // Agents
        var agents = GetSourceAgents(name);
        if (agents.Count > 0)
            RunAgentsUninstall(dryRun, verbose, name, scope, target);

        // MCP servers from plugin.json
        var (mcpServers, _) = ReadPluginServers(name);
        if (mcpServers != null && mcpServers.Count > 0)
        {
            Console.WriteLine($"  MCP servers:");
            foreach (var mcp in GetMcpTargetPaths(target, edition))
            {
                Console.WriteLine($"    [{mcp.Label}]");
                if (!File.Exists(mcp.Path)) continue;

                if (!dryRun && backedUp.Add(mcp.Path)) BackupFile(mcp.Path, "mcp", mcp.Label);
                var userServers = ReadMcpServers(mcp.Path, mcp.WrapperKey);
                if (userServers.Count == 0) continue;

                foreach (var srvName in mcpServers.Keys)
                {
                    if (userServers.Remove(srvName))
                    {
                        if (dryRun)
                            PrintInfo($"      Would remove: {srvName}");
                        else
                            PrintSuccess($"      Removed: {srvName}");
                    }
                    else if (verbose)
                        PrintInfo($"      Not present: {srvName}");
                }

                if (!dryRun) WriteMcpServers(mcp.Path, mcp.WrapperKey, userServers);
            }
        }
    }
}

// ============================================================================
// Settings Handlers (edition-dependent)
// ============================================================================

void RunSettingsList(string edition, bool verbose)
{
    PrintHeader("Settings");
    foreach (var ed in GetEditions(edition))
    {
        var settingsPath = Path.Combine(GetVSCodeUserDir(ed), "settings.json");
        Console.WriteLine($"  [{ed}] {settingsPath}");
        if (!File.Exists(settingsPath))
        {
            PrintInfo("    (file not found)");
            continue;
        }

        var node = ReadJsoncNode(settingsPath);
        if (node is not JsonObject obj) continue;

        var chatSettings = obj
            .Where(kv => kv.Key.StartsWith("chat.", StringComparison.OrdinalIgnoreCase) ||
                         kv.Key.StartsWith("github.copilot.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key)
            .ToList();

        if (chatSettings.Count == 0)
        {
            PrintInfo("    (no chat/copilot settings found)");
            continue;
        }
        foreach (var (key, value) in chatSettings)
            Console.WriteLine($"    {key}: {value?.ToJsonString()}");
    }
}

void RunSettingsUpdate(string edition, bool dryRun, bool verbose)
{
    PrintHeader("Settings update");
    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var settingsPath = Path.Combine(GetVSCodeUserDir(ed), "settings.json");

        JsonNode? node;
        if (File.Exists(settingsPath))
        {
            if (!dryRun) BackupFile(settingsPath, "settings", ed);
            node = ReadJsoncNode(settingsPath);
        }
        else
        {
            node = new JsonObject();
        }

        if (node is not JsonObject obj) continue;

        bool changed = false;
        foreach (var (key, expectedValue) in requiredSettings)
        {
            if (obj.ContainsKey(key))
            {
                var currentJson = obj[key]?.ToJsonString() ?? "null";
                var expectedJson = expectedValue?.ToJsonString() ?? "null";
                if (currentJson == expectedJson)
                {
                    if (verbose) PrintInfo($"    Already set: {key}");
                    continue;
                }
            }

            if (dryRun)
            {
                PrintInfo($"    Would set: {key} = {expectedValue?.ToJsonString()}");
            }
            else
            {
                obj[key] = expectedValue?.DeepClone();
                PrintSuccess($"    Set: {key} = {expectedValue?.ToJsonString()}");
                changed = true;
            }
        }

        // Ensure repo marketplace is in chat.plugins.marketplaces array
        var marketplaceRef = GetMarketplaceRef();
        if (marketplaceRef != null)
        {
            const string mkey = "chat.plugins.marketplaces";
            var arr = obj[mkey] as JsonArray ?? new JsonArray();
            if (obj[mkey] is not JsonArray) obj[mkey] = arr;
            bool found = arr.Any(e => e?.GetValue<string>() == marketplaceRef);
            if (found)
            {
                if (verbose) PrintInfo($"    Already in {mkey}: {marketplaceRef}");
            }
            else if (dryRun)
            {
                PrintInfo($"    Would add to {mkey}: {marketplaceRef}");
            }
            else
            {
                arr.Add((JsonNode)JsonValue.Create(marketplaceRef)!);
                PrintSuccess($"    Added to {mkey}: {marketplaceRef}");
                changed = true;
            }
        }

        if (changed && !dryRun)
        {
            WriteJsonNode(settingsPath, node);
        }
    }
}

void RunSettingsDiff(string edition, bool verbose)
{
    PrintHeader("Settings diff");
    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var settingsPath = Path.Combine(GetVSCodeUserDir(ed), "settings.json");
        if (!File.Exists(settingsPath))
        {
            PrintInfo("    (file not found — all required settings missing)");
            continue;
        }

        var node = ReadJsoncNode(settingsPath);
        if (node is not JsonObject obj) continue;

        bool hasDiffs = false;
        foreach (var (key, expectedValue) in requiredSettings)
        {
            if (!obj.ContainsKey(key))
            {
                Console.WriteLine($"    + {key} (missing)");
                hasDiffs = true;
            }
            else
            {
                var currentJson = obj[key]?.ToJsonString() ?? "null";
                var expectedJson = expectedValue?.ToJsonString() ?? "null";
                if (currentJson != expectedJson)
                {
                    Console.WriteLine($"    ~ {key} (differs: current={currentJson}, expected={expectedJson})");
                    hasDiffs = true;
                }
            }
        }
        // Check marketplace entry
        var marketplaceRef = GetMarketplaceRef();
        if (marketplaceRef != null)
        {
            var arr = obj["chat.plugins.marketplaces"] as JsonArray;
            if (arr == null || !arr.Any(e => e?.GetValue<string>() == marketplaceRef))
            {
                Console.WriteLine($"    + chat.plugins.marketplaces: {marketplaceRef} (missing)");
                hasDiffs = true;
            }
        }
        if (!hasDiffs) PrintInfo("    (all required settings present)");
    }
}

// ============================================================================
// Marketplace (repo-scoped .github/copilot/settings.json)
// ============================================================================

void RunMarketplaceInstall(string? targetDir, string? mirrorName, string[]? pluginNames,
    bool dryRun, bool verbose)
{
    PrintHeader("Marketplace install (repo-scoped)");

    // Resolve target directory
    var dir = targetDir ?? Environment.CurrentDirectory;
    if (!Directory.Exists(dir))
    {
        PrintError($"  Target directory not found: {dir}");
        return;
    }

    // Resolve marketplace source (mirror or this repo)
    string marketplaceName;
    string targetRepo;
    List<string> availablePlugins;

    if (mirrorName != null)
    {
        // Use a mirror from public-mirrors.json
        var mirrorsPath = Path.Combine(repoRoot, "public-mirrors.json");
        if (!File.Exists(mirrorsPath))
        {
            PrintError($"  public-mirrors.json not found at {mirrorsPath}");
            return;
        }
        var mirrorsJson = JsonNode.Parse(File.ReadAllText(mirrorsPath));
        var mirrors = mirrorsJson?["mirrors"]?.AsArray();
        var mirror = mirrors?.FirstOrDefault(m => m?["name"]?.GetValue<string>() == mirrorName);
        if (mirror == null)
        {
            PrintError($"  Mirror '{mirrorName}' not found. Available: {string.Join(", ", mirrors?.Select(m => m?["name"]?.GetValue<string>()) ?? [])}");
            return;
        }
        marketplaceName = mirrorName;
        targetRepo = mirror["targetRepo"]!.GetValue<string>();
        // Collect all plugin names from the mirror's plugins object
        availablePlugins = new List<string>();
        if (mirror["plugins"] is JsonObject pluginsObj)
        {
            foreach (var kv in pluginsObj)
                availablePlugins.Add(kv.Key);
        }
    }
    else
    {
        // Use this repo's marketplace
        var mktRef = GetMarketplaceRef();
        if (mktRef == null)
        {
            PrintError("  Could not determine marketplace repo from git remote");
            return;
        }
        targetRepo = mktRef;
        var mktPath = Path.Combine(repoRoot, ".github", "plugin", "marketplace.json");
        if (!File.Exists(mktPath))
        {
            PrintError($"  marketplace.json not found at {mktPath}");
            return;
        }
        var mktJson = JsonNode.Parse(File.ReadAllText(mktPath));
        marketplaceName = mktJson?["name"]?.GetValue<string>() ?? "marketplace";
        availablePlugins = new List<string>();
        if (mktJson?["plugins"] is JsonArray pluginsArr)
        {
            foreach (var p in pluginsArr)
            {
                var name = p?["name"]?.GetValue<string>();
                if (name != null) availablePlugins.Add(name);
            }
        }
    }

    // Resolve which plugins to enable
    var enablePlugins = pluginNames?.Length > 0
        ? pluginNames.ToList()
        : availablePlugins;

    // Validate requested plugins exist
    foreach (var p in enablePlugins)
    {
        if (!availablePlugins.Contains(p))
            PrintWarning($"  Plugin '{p}' not found in {marketplaceName} — including anyway");
    }

    // Build .github/copilot/settings.json
    var settingsObj = new JsonObject
    {
        ["marketplaces"] = new JsonObject
        {
            [marketplaceName] = new JsonObject
            {
                ["source"] = new JsonObject
                {
                    ["source"] = "github",
                    ["repo"] = targetRepo
                }
            }
        }
    };

    var enabledObj = new JsonObject();
    foreach (var p in enablePlugins)
        enabledObj[$"{p}@{marketplaceName}"] = true;
    settingsObj["enabledPlugins"] = enabledObj;

    var copilotDir = Path.Combine(dir, ".github", "copilot");
    var settingsPath = Path.Combine(copilotDir, "settings.json");

    // Check for existing file
    if (File.Exists(settingsPath))
    {
        var existing = File.ReadAllText(settingsPath);
        if (verbose) Console.WriteLine($"  Existing {settingsPath}:\n{existing}");

        // Merge: add marketplace and plugins to existing settings
        var existingNode = JsonNode.Parse(existing);
        if (existingNode is JsonObject existingObj)
        {
            // Add/update marketplace entry
            if (existingObj["marketplaces"] is not JsonObject existingMkt)
            {
                existingMkt = new JsonObject();
                existingObj["marketplaces"] = existingMkt;
            }
            existingMkt[marketplaceName] = new JsonObject
            {
                ["source"] = new JsonObject
                {
                    ["source"] = "github",
                    ["repo"] = targetRepo
                }
            };

            // Add/update enabled plugins
            if (existingObj["enabledPlugins"] is not JsonObject existingEnabled)
            {
                existingEnabled = new JsonObject();
                existingObj["enabledPlugins"] = existingEnabled;
            }
            foreach (var p in enablePlugins)
                existingEnabled[$"{p}@{marketplaceName}"] = true;

            settingsObj = existingObj;
        }
    }

    var output = settingsObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    Console.WriteLine($"\n  Target: {settingsPath}");
    Console.WriteLine($"  Marketplace: {marketplaceName} → {targetRepo}");
    Console.WriteLine($"  Plugins: {string.Join(", ", enablePlugins)}");
    Console.WriteLine();
    Console.WriteLine(output);

    if (dryRun)
    {
        PrintInfo("\n  (dry run — no files written)");
        return;
    }

    Directory.CreateDirectory(copilotDir);
    File.WriteAllText(settingsPath, output + "\n");
    PrintSuccess($"\n  Wrote {settingsPath}");
    PrintInfo("  Commit this file so anyone cloning the repo gets the marketplace plugins automatically.");
}

// ============================================================================
// Upstream Diff & Sync (PR-based bidirectional sync with external repos)
// ============================================================================

List<UpstreamConfig> LoadUpstreams(string? filterName)
{
    var mirrorsPath = Path.Combine(repoRoot, "public-mirrors.json");
    if (!File.Exists(mirrorsPath))
    {
        PrintError("public-mirrors.json not found");
        return new();
    }
    var json = JsonNode.Parse(File.ReadAllText(mirrorsPath));
    var upstreams = json?["upstreams"]?.AsArray();
    if (upstreams == null || upstreams.Count == 0)
    {
        PrintError("No upstreams configured in public-mirrors.json");
        return new();
    }

    var result = new List<UpstreamConfig>();
    foreach (var u in upstreams)
    {
        var name = u?["name"]?.GetValue<string>() ?? "";
        if (filterName != null && name != filterName) continue;

        var targetRepo = u?["targetRepo"]?.GetValue<string>() ?? "";
        var targetBranch = u?["targetBranch"]?.GetValue<string>() ?? "main";
        var plugins = new List<UpstreamPluginMapping>();

        if (u?["plugins"] is JsonObject pluginsObj)
        {
            foreach (var kv in pluginsObj)
            {
                var localPlugin = kv.Key;
                var skills = new List<string>();
                string? targetPlugin = null;

                if (kv.Value is JsonObject pObj)
                {
                    if (pObj["skills"] is JsonArray skillsArr)
                        skills.AddRange(skillsArr.Select(s => s!.GetValue<string>()));
                    targetPlugin = pObj["targetPlugin"]?.GetValue<string>();
                }
                else if (kv.Value is JsonArray arr)
                {
                    skills.AddRange(arr.Select(s => s!.GetValue<string>()));
                }

                plugins.Add(new UpstreamPluginMapping(localPlugin, targetPlugin ?? localPlugin, skills));
            }
        }

        result.Add(new UpstreamConfig(name, targetRepo, targetBranch, u?["description"]?.GetValue<string>() ?? "", plugins));
    }

    if (filterName != null && result.Count == 0)
        PrintError($"Upstream '{filterName}' not found");

    return result;
}

// (types defined at end of file with other records)

/// <summary>Gets the git tree for a remote repo via gh api.</summary>
Dictionary<string, string>? GetRemoteTree(string repo, string branch)
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "gh",
            Arguments = $"api repos/{repo}/git/trees/{branch}?recursive=1 --jq \".tree[] | select(.type == \\\"blob\\\") | [.path, .sha] | @tsv\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) return null;

        var tree = new Dictionary<string, string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2) tree[parts[0]] = parts[1];
        }
        return tree;
    }
    catch { return null; }
}

/// <summary>Gets the git blob SHA for a local file (same algorithm as git).</summary>
string? GetLocalBlobSha(string filePath)
{
    if (!File.Exists(filePath)) return null;
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"hash-object \"{filePath}\"",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 ? output : null;
    }
    catch { return null; }
}

/// <summary>Computes drift for a single skill between local and remote.</summary>
SkillDrift ComputeSkillDrift(string skillName, string localPlugin, string targetPlugin,
    Dictionary<string, string> remoteTree)
{
    var localSkillDir = Path.Combine(repoRoot, "plugins", localPlugin, "skills", skillName);
    var remotePrefix = $"plugins/{targetPlugin}/skills/{skillName}/";
    var drifts = new List<FileDrift>();

    // Syncable subdirs within a skill
    var syncableDirs = new[] { "", "references", "scripts", "assets" };

    // Collect local files
    var localFiles = new Dictionary<string, string>(); // relative path → full path
    if (Directory.Exists(localSkillDir))
    {
        foreach (var dir in syncableDirs)
        {
            var fullDir = dir == "" ? localSkillDir : Path.Combine(localSkillDir, dir);
            if (!Directory.Exists(fullDir)) continue;
            foreach (var file in Directory.GetFiles(fullDir))
            {
                var relPath = Path.GetRelativePath(localSkillDir, file).Replace('\\', '/');
                localFiles[relPath] = file;
            }
        }
    }

    // Collect remote files under the skill prefix
    var remoteFiles = new Dictionary<string, string>(); // relative path → sha
    foreach (var kv in remoteTree)
    {
        if (kv.Key.StartsWith(remotePrefix) && !kv.Key.EndsWith("/"))
        {
            var relPath = kv.Key[remotePrefix.Length..];
            // Only include syncable subdirs
            var topDir = relPath.Contains('/') ? relPath[..relPath.IndexOf('/')] : "";
            if (syncableDirs.Contains(topDir) || !relPath.Contains('/'))
                remoteFiles[relPath] = kv.Value;
        }
    }

    // Compare
    var allPaths = localFiles.Keys.Union(remoteFiles.Keys).OrderBy(p => p);
    foreach (var path in allPaths)
    {
        var hasLocal = localFiles.ContainsKey(path);
        var hasRemote = remoteFiles.ContainsKey(path);

        if (hasLocal && hasRemote)
        {
            var localSha = GetLocalBlobSha(localFiles[path]);
            var remoteSha = remoteFiles[path];
            if (localSha == remoteSha)
                drifts.Add(new FileDrift(path, DriftStatus.InSync, localSha, remoteSha));
            else
                drifts.Add(new FileDrift(path, DriftStatus.Diverged, localSha, remoteSha));
        }
        else if (hasLocal)
        {
            drifts.Add(new FileDrift(path, DriftStatus.LocalOnly, GetLocalBlobSha(localFiles[path]), null));
        }
        else
        {
            drifts.Add(new FileDrift(path, DriftStatus.RemoteOnly, null, remoteFiles[path]));
        }
    }

    return new SkillDrift(skillName, localPlugin, targetPlugin, drifts);
}

void RunUpstreamDiff(string? upstreamName, string? pluginFilter, string? skillFilter,
    bool jsonOutput, bool verbose)
{
    PrintHeader("Upstream diff (bidirectional)");
    var upstreams = LoadUpstreams(upstreamName);
    if (upstreams.Count == 0) return;

    var allResults = new Dictionary<string, List<SkillDrift>>();

    foreach (var upstream in upstreams)
    {
        Console.WriteLine($"\n  {upstream.Name} → {upstream.TargetRepo} ({upstream.TargetBranch})");
        var remoteTree = GetRemoteTree(upstream.TargetRepo, upstream.TargetBranch);
        if (remoteTree == null)
        {
            PrintError($"    Could not fetch remote tree for {upstream.TargetRepo}");
            continue;
        }

        var skillDrifts = new List<SkillDrift>();

        foreach (var pluginMapping in upstream.Plugins)
        {
            if (pluginFilter != null && pluginMapping.LocalPlugin != pluginFilter) continue;

            foreach (var skill in pluginMapping.Skills)
            {
                if (skillFilter != null && skill != skillFilter) continue;

                var drift = ComputeSkillDrift(skill, pluginMapping.LocalPlugin,
                    pluginMapping.TargetPlugin, remoteTree);
                skillDrifts.Add(drift);

                if (!jsonOutput)
                {
                    var icon = drift.OverallStatus switch
                    {
                        DriftStatus.InSync => "✅",
                        DriftStatus.LocalAhead => "📤",
                        DriftStatus.RemoteAhead => "📥",
                        DriftStatus.Diverged => "⚡",
                        DriftStatus.LocalOnly => "➕",
                        DriftStatus.RemoteOnly => "➖",
                        _ => "❓"
                    };
                    var pluginNote = pluginMapping.LocalPlugin != pluginMapping.TargetPlugin
                        ? $" ({pluginMapping.LocalPlugin} → {pluginMapping.TargetPlugin})"
                        : "";
                    Console.WriteLine($"    {icon} {skill}{pluginNote}");

                    if (verbose || drift.OverallStatus != DriftStatus.InSync)
                    {
                        foreach (var f in drift.Files.Where(f => f.Status != DriftStatus.InSync || verbose))
                        {
                            var fIcon = f.Status switch
                            {
                                DriftStatus.InSync => "  ✅",
                                DriftStatus.LocalOnly => "  ➕",
                                DriftStatus.RemoteOnly => "  ➖",
                                DriftStatus.Diverged => "  ⚡",
                                _ => "  ❓"
                            };
                            Console.WriteLine($"      {fIcon} {f.RelativePath}");
                        }
                    }
                }
            }
        }

        // Also check for agents in remote that map to our agents
        foreach (var pluginMapping in upstream.Plugins)
        {
            if (pluginFilter != null && pluginMapping.LocalPlugin != pluginFilter) continue;

            var localAgentDir = Path.Combine(repoRoot, "plugins", pluginMapping.LocalPlugin, "agents");
            var remoteAgentPrefix = $"plugins/{pluginMapping.TargetPlugin}/agents/";

            var localAgents = Directory.Exists(localAgentDir)
                ? Directory.GetFiles(localAgentDir, "*.agent.md").Select(Path.GetFileName).ToList()
                : new List<string?>();

            var remoteAgents = remoteTree.Keys
                .Where(k => k.StartsWith(remoteAgentPrefix) && k.EndsWith(".agent.md"))
                .Select(k => k[remoteAgentPrefix.Length..])
                .ToList();

            var allAgents = localAgents.Union(remoteAgents).Where(a => a != null).Distinct().OrderBy(a => a);
            foreach (var agent in allAgents)
            {
                var localPath = Path.Combine(localAgentDir, agent!);
                var remotePath = remoteAgentPrefix + agent;
                var hasLocal = File.Exists(localPath);
                var hasRemote = remoteTree.ContainsKey(remotePath);

                DriftStatus status;
                if (hasLocal && hasRemote)
                {
                    var localSha = GetLocalBlobSha(localPath);
                    status = localSha == remoteTree[remotePath] ? DriftStatus.InSync : DriftStatus.Diverged;
                }
                else if (hasLocal) status = DriftStatus.LocalOnly;
                else status = DriftStatus.RemoteOnly;

                if (!jsonOutput && (status != DriftStatus.InSync || verbose))
                {
                    var icon = status switch
                    {
                        DriftStatus.InSync => "✅", DriftStatus.Diverged => "⚡",
                        DriftStatus.LocalOnly => "➕", DriftStatus.RemoteOnly => "➖", _ => "❓"
                    };
                    Console.WriteLine($"    {icon} agent: {agent}");
                }
            }
        }

        allResults[upstream.Name] = skillDrifts;
    }

    if (jsonOutput)
    {
        var jsonObj = new JsonObject();
        foreach (var (name, drifts) in allResults)
        {
            var arr = new JsonArray();
            foreach (var d in drifts)
            {
                var filesArr = new JsonArray();
                foreach (var f in d.Files)
                    filesArr.Add(new JsonObject
                    {
                        ["path"] = f.RelativePath,
                        ["status"] = f.Status.ToString(),
                        ["localSha"] = f.LocalSha,
                        ["remoteSha"] = f.RemoteSha
                    });
                arr.Add(new JsonObject
                {
                    ["skill"] = d.SkillName,
                    ["localPlugin"] = d.LocalPlugin,
                    ["targetPlugin"] = d.TargetPlugin,
                    ["status"] = d.OverallStatus.ToString(),
                    ["files"] = filesArr
                });
            }
            jsonObj[name] = arr;
        }
        Console.WriteLine(jsonObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    // Summary
    if (!jsonOutput)
    {
        Console.WriteLine();
        var total = allResults.Values.SelectMany(d => d).ToList();
        var synced = total.Count(d => d.OverallStatus == DriftStatus.InSync);
        var outbound = total.Count(d => d.OverallStatus == DriftStatus.LocalAhead);
        var inbound = total.Count(d => d.OverallStatus == DriftStatus.RemoteAhead);
        var diverged = total.Count(d => d.OverallStatus == DriftStatus.Diverged);
        PrintInfo($"  Summary: {synced} in-sync, {outbound} local-ahead, {inbound} remote-ahead, {diverged} diverged");
    }
}

void RunUpstreamSync(string? upstreamName, string? pluginFilter, string? skillFilter,
    bool dryRun, bool verbose)
{
    PrintHeader("Upstream sync (create PRs)");
    var upstreams = LoadUpstreams(upstreamName);
    if (upstreams.Count == 0) return;

    foreach (var upstream in upstreams)
    {
        Console.WriteLine($"\n  {upstream.Name} → {upstream.TargetRepo} ({upstream.TargetBranch})");
        var remoteTree = GetRemoteTree(upstream.TargetRepo, upstream.TargetBranch);
        if (remoteTree == null)
        {
            PrintError($"    Could not fetch remote tree for {upstream.TargetRepo}");
            continue;
        }

        // Find skills with outbound drift
        var toSync = new List<SkillDrift>();
        foreach (var pluginMapping in upstream.Plugins)
        {
            if (pluginFilter != null && pluginMapping.LocalPlugin != pluginFilter) continue;
            foreach (var skill in pluginMapping.Skills)
            {
                if (skillFilter != null && skill != skillFilter) continue;
                var drift = ComputeSkillDrift(skill, pluginMapping.LocalPlugin,
                    pluginMapping.TargetPlugin, remoteTree);
                if (drift.OverallStatus != DriftStatus.InSync)
                    toSync.Add(drift);
            }
        }

        if (toSync.Count == 0)
        {
            PrintSuccess($"    All configured skills are in sync");
            continue;
        }

        foreach (var drift in toSync)
        {
            var icon = drift.OverallStatus switch
            {
                DriftStatus.LocalAhead => "📤", DriftStatus.Diverged => "⚡",
                DriftStatus.RemoteAhead => "📥", _ => "📝"
            };
            Console.WriteLine($"    {icon} {drift.SkillName}: {drift.OverallStatus}");

            var changedFiles = drift.Files.Where(f => f.Status != DriftStatus.InSync).ToList();
            foreach (var f in changedFiles)
                Console.WriteLine($"        {f.Status}: {f.RelativePath}");

            if (drift.OverallStatus == DriftStatus.RemoteAhead)
            {
                PrintInfo($"      ↑ Remote has changes we don't — consider merging back");
                continue;
            }
        }

        if (dryRun)
        {
            PrintInfo($"\n    (dry run — {toSync.Count} skill(s) would be synced via PR)");
            continue;
        }

        // Clone, branch, copy, PR
        var outboundSkills = toSync.Where(d =>
            d.OverallStatus is DriftStatus.LocalAhead or DriftStatus.Diverged or DriftStatus.LocalOnly).ToList();

        if (outboundSkills.Count == 0)
        {
            PrintInfo("    No outbound changes to push");
            continue;
        }

        var tmpDir = Path.Combine(Path.GetTempPath(), $"upstream-sync-{upstream.Name}-{DateTime.Now:yyyyMMdd-HHmmss}");
        try
        {
            Console.WriteLine($"    Cloning {upstream.TargetRepo}...");
            var cloneResult = RunProcess("gh", $"repo clone {upstream.TargetRepo} \"{tmpDir}\" -- --depth=1 --branch={upstream.TargetBranch}");
            if (cloneResult != 0)
            {
                PrintError($"    Failed to clone {upstream.TargetRepo}");
                continue;
            }

            var skillNames = string.Join("-", outboundSkills.Select(s => s.SkillName));
            var branchName = $"update/{skillNames}";
            RunProcess("git", $"-C \"{tmpDir}\" checkout -b {branchName}");

            // Copy files
            foreach (var drift in outboundSkills)
            {
                var localSkillDir = Path.Combine(repoRoot, "plugins", drift.LocalPlugin, "skills", drift.SkillName);
                var targetSkillDir = Path.Combine(tmpDir, "plugins", drift.TargetPlugin, "skills", drift.SkillName);

                foreach (var f in drift.Files.Where(f => f.Status is DriftStatus.LocalOnly or DriftStatus.Diverged))
                {
                    var src = Path.Combine(localSkillDir, f.RelativePath);
                    var dst = Path.Combine(targetSkillDir, f.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, true);
                    if (verbose) Console.WriteLine($"      Copied {f.RelativePath}");
                }
            }

            // Sync mcpServers from local plugin.json to target plugin.json
            var syncedPlugins = outboundSkills.Select(s => (s.LocalPlugin, s.TargetPlugin)).Distinct().ToList();
            foreach (var (localPlugin, targetPlugin) in syncedPlugins)
            {
                var localPjPath = Path.Combine(repoRoot, "plugins", localPlugin, "plugin.json");
                var targetPjPath = Path.Combine(tmpDir, "plugins", targetPlugin, "plugin.json");
                if (!File.Exists(localPjPath) || !File.Exists(targetPjPath)) continue;

                var localPj = JsonNode.Parse(File.ReadAllText(localPjPath));
                var targetPj = JsonNode.Parse(File.ReadAllText(targetPjPath));
                if (localPj?["mcpServers"] is JsonObject localMcp && targetPj is JsonObject targetObj)
                {
                    targetObj["mcpServers"] = localMcp.DeepClone();
                    File.WriteAllText(targetPjPath, targetObj.ToJsonString(
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
                    if (verbose) Console.WriteLine($"      Synced mcpServers to {targetPlugin}/plugin.json");
                }
            }

            // Commit
            RunProcess("git", $"-C \"{tmpDir}\" add -A");
            var commitMsg = outboundSkills.Count == 1
                ? $"Update {outboundSkills[0].SkillName} skill"
                : $"Update {outboundSkills.Count} skills: {skillNames}";
            RunProcess("git", $"-C \"{tmpDir}\" commit -m \"{commitMsg}\" -m \"Synced from copilot-skills\"");

            // Push and create PR
            RunProcess("git", $"-C \"{tmpDir}\" push origin {branchName}");

            var prBody = BuildPrBody(outboundSkills, upstream);
            var prTitle = commitMsg;

            // Write PR body to temp file to avoid shell escaping issues
            var prBodyFile = Path.Combine(tmpDir, ".pr-body.md");
            File.WriteAllText(prBodyFile, prBody);

            var prResult = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "gh",
                Arguments = $"pr create --repo {upstream.TargetRepo} --base {upstream.TargetBranch} --head {branchName} --title \"{prTitle}\" --body-file \"{prBodyFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var prProcess = System.Diagnostics.Process.Start(prResult)!;
            var prOutput = prProcess.StandardOutput.ReadToEnd().Trim();
            var prError = prProcess.StandardError.ReadToEnd().Trim();
            prProcess.WaitForExit();

            if (prProcess.ExitCode == 0)
                PrintSuccess($"    Created PR: {prOutput}");
            else
                PrintError($"    PR creation failed: {prError}");
        }
        finally
        {
            if (Directory.Exists(tmpDir))
            {
                try { Directory.Delete(tmpDir, true); }
                catch { PrintWarning($"    Could not clean up {tmpDir}"); }
            }
        }
    }
}

string BuildPrBody(List<SkillDrift> skills, UpstreamConfig upstream)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("## Skill sync from copilot-skills");
    sb.AppendLine();
    sb.AppendLine("This PR syncs skill content from the copilot-skills marketplace.");
    sb.AppendLine();
    sb.AppendLine("### Changes");
    sb.AppendLine();

    foreach (var skill in skills)
    {
        sb.AppendLine($"#### `{skill.SkillName}`");
        var pluginNote = skill.LocalPlugin != skill.TargetPlugin
            ? $" (source: `{skill.LocalPlugin}`)"
            : "";
        sb.AppendLine($"Plugin: `{skill.TargetPlugin}`{pluginNote}");
        sb.AppendLine();

        foreach (var f in skill.Files.Where(f => f.Status != DriftStatus.InSync))
        {
            var label = f.Status switch
            {
                DriftStatus.LocalOnly => "new",
                DriftStatus.Diverged => "modified",
                DriftStatus.RemoteOnly => "remote-only (not touched)",
                _ => f.Status.ToString()
            };
            sb.AppendLine($"- `{f.RelativePath}` ({label})");
        }
        sb.AppendLine();
    }

    sb.AppendLine("### Notes");
    sb.AppendLine();
    sb.AppendLine("- Only skill content files are synced (SKILL.md, references/, scripts/, assets/)");
    sb.AppendLine("- plugin.json, marketplace.json, CODEOWNERS, and tests/ are NOT modified");
    sb.AppendLine("- Please review and update any target-repo-specific files as needed");

    return sb.ToString();
}

void RunUpstreamPull(string? upstreamName, string? pluginFilter, string? skillFilter,
    bool dryRun, bool force, bool verbose)
{
    PrintHeader("Upstream pull (import remote changes)");
    var upstreams = LoadUpstreams(upstreamName);
    if (upstreams.Count == 0) return;

    var totalPulled = 0;

    foreach (var upstream in upstreams)
    {
        Console.WriteLine($"\n  {upstream.Name} ← {upstream.TargetRepo} ({upstream.TargetBranch})");
        var remoteTree = GetRemoteTree(upstream.TargetRepo, upstream.TargetBranch);
        if (remoteTree == null)
        {
            PrintError($"    Could not fetch remote tree for {upstream.TargetRepo}");
            continue;
        }

        var toPull = new List<SkillDrift>();
        foreach (var pluginMapping in upstream.Plugins)
        {
            if (pluginFilter != null && pluginMapping.LocalPlugin != pluginFilter) continue;
            foreach (var skill in pluginMapping.Skills)
            {
                if (skillFilter != null && skill != skillFilter) continue;
                var drift = ComputeSkillDrift(skill, pluginMapping.LocalPlugin,
                    pluginMapping.TargetPlugin, remoteTree);
                if (drift.OverallStatus is DriftStatus.RemoteAhead or DriftStatus.Diverged)
                    toPull.Add(drift);
            }
        }

        if (toPull.Count == 0)
        {
            PrintSuccess($"    All configured skills are up to date");
            continue;
        }

        foreach (var drift in toPull)
        {
            var icon = drift.OverallStatus == DriftStatus.RemoteAhead ? "📥" : "⚡";
            Console.WriteLine($"    {icon} {drift.SkillName}: {drift.OverallStatus}");

            var inboundFiles = drift.Files
                .Where(f => f.Status is DriftStatus.RemoteAhead or DriftStatus.Diverged or DriftStatus.RemoteOnly)
                .ToList();

            foreach (var f in inboundFiles)
                Console.WriteLine($"        {f.Status}: {f.RelativePath}");

            if (drift.OverallStatus == DriftStatus.Diverged && !force)
            {
                PrintWarning($"      ⚠ Skill has diverged — use --force to overwrite local with remote");
                continue;
            }

            if (dryRun)
            {
                PrintInfo($"      (dry run — {inboundFiles.Count} file(s) would be pulled)");
                continue;
            }

            // Fetch and write each remote file
            var localSkillDir = Path.Combine(repoRoot, "plugins", drift.LocalPlugin, "skills", drift.SkillName);
            var remotePrefix = $"plugins/{drift.TargetPlugin}/skills/{drift.SkillName}/";
            var pulled = 0;

            foreach (var f in inboundFiles)
            {
                var remotePath = remotePrefix + f.RelativePath;
                var localPath = Path.Combine(localSkillDir, f.RelativePath);

                // Fetch file content from remote
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "gh",
                    Arguments = $"api repos/{upstream.TargetRepo}/contents/{remotePath} -H \"Accept: application/vnd.github.raw\" --method GET -F ref={upstream.TargetBranch}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = System.Diagnostics.Process.Start(psi)!;
                var content = proc.StandardOutput.ReadToEnd();
                proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    PrintError($"      Failed to fetch {f.RelativePath}");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                File.WriteAllText(localPath, content);
                pulled++;
                if (verbose) PrintSuccess($"      ✅ {f.RelativePath}");
            }

            totalPulled += pulled;
            PrintSuccess($"      Pulled {pulled} file(s) for {drift.SkillName}");
        }
    }

    if (!dryRun && totalPulled > 0)
    {
        Console.WriteLine();
        PrintInfo($"  Pulled {totalPulled} file(s) total. Review changes with 'git diff' and commit when ready.");
    }
}


void SyncFiles(string[] sourceFiles, string targetDir, string category,
    string? edition, bool exact, bool force, bool dryRun, bool verbose)
{
    foreach (var src in sourceFiles)
    {
        var fileName = Path.GetFileName(src);
        var target = Path.Combine(targetDir, fileName);

        if (File.Exists(target))
        {
            if (!force && !exact)
            {
                if (verbose) PrintWarning($"    Skipped (exists): {fileName}");
                continue;
            }
            if (!dryRun) BackupFile(target, category, edition);
        }

        if (dryRun)
            PrintInfo($"    Would install: {fileName}");
        else
        {
            File.Copy(src, target, true);
            PrintSuccess($"    Installed: {fileName}");
        }
    }
}

void RemoveExtraFiles(string[] sourceFiles, string targetDir, string pattern,
    string category, string? edition, bool dryRun, bool verbose)
{
    if (!Directory.Exists(targetDir)) return;

    var sourceNames = sourceFiles.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var targetFiles = Directory.GetFiles(targetDir, pattern);

    foreach (var target in targetFiles)
    {
        var fileName = Path.GetFileName(target);
        if (sourceNames.Contains(fileName)) continue;

        if (!dryRun) BackupFile(target, category, edition);
        if (dryRun)
            PrintInfo($"    Would remove (not in repo): {fileName}");
        else
        {
            File.Delete(target);
            PrintWarning($"    Removed (not in repo): {fileName}");
        }
    }
}

// ============================================================================
// Instructions Settings Helper
// ============================================================================

void EnsureInstructionsSetting(string edition, bool dryRun, bool verbose)
{
    var instrPath = "$HOME/.copilot-instructions/instructions";
    var key = "chat.instructionFilesLocations";

    foreach (var ed in GetEditions(edition))
    {
        var settingsPath = Path.Combine(GetVSCodeUserDir(ed), "settings.json");

        JsonNode? node;
        if (File.Exists(settingsPath))
        {
            node = ReadJsoncNode(settingsPath);
        }
        else
        {
            node = new JsonObject();
        }

        if (node is not JsonObject obj) continue;

        if (obj[key] is JsonObject locations)
        {
            if (locations[instrPath]?.GetValue<bool>() == true)
            {
                if (verbose) PrintInfo($"    {key} already set in {ed}");
                continue;
            }
            locations[instrPath] = true;
        }
        else
        {
            obj[key] = new JsonObject { [instrPath] = true };
        }

        if (dryRun)
        {
            PrintInfo($"    Would update {key} in {ed}");
        }
        else
        {
            if (!File.Exists(settingsPath))
                BackupFile(settingsPath, "settings", ed);
            WriteJsonNode(settingsPath, node);
            PrintSuccess($"    Updated {key} in {ed}");
        }
    }
}

void InstallCliInstructions(string[] sourceFiles, bool force, bool dryRun, bool verbose)
{
    if (sourceFiles.Length == 0)
    {
        if (verbose) PrintInfo("  CLI: No instruction files to install");
        return;
    }

    var cliInstrPath = Path.Combine(GetCopilotCliDir(), "copilot-instructions.md");
    Console.WriteLine($"  CLI: {cliInstrPath}");

    if (File.Exists(cliInstrPath) && !force)
    {
        if (verbose) PrintWarning("    Skipped (exists): copilot-instructions.md");
        return;
    }

    if (File.Exists(cliInstrPath) && !dryRun)
        BackupFile(cliInstrPath, "instructions", "cli");

    if (dryRun)
    {
        PrintInfo("    Would install: copilot-instructions.md (concatenated from repo instructions)");
        return;
    }

    // Concatenate all instruction files into a single CLI instructions file
    Directory.CreateDirectory(Path.GetDirectoryName(cliInstrPath)!);
    using var writer = new StreamWriter(cliInstrPath);
    writer.WriteLine("<!-- Auto-generated by blazor-ai.cs from repo instructions/ -->");
    writer.WriteLine();
    for (int i = 0; i < sourceFiles.Length; i++)
    {
        if (i > 0)
        {
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
        }
        writer.Write(File.ReadAllText(sourceFiles[i]));
    }
    PrintSuccess("    Installed: copilot-instructions.md");
}

// ============================================================================
// Path Resolution
// ============================================================================

string GetRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string? callerPath = null)
{
    // CallerFilePath is resolved at compile time to the absolute path of this source file.
    // The script lives in <repo-root>/scripts/<name>.cs, so go up one level.
    var scriptsDir = Path.GetDirectoryName(callerPath)
        ?? throw new InvalidOperationException("Could not determine script directory from CallerFilePath.");
    return Path.GetDirectoryName(scriptsDir)
        ?? throw new InvalidOperationException("Could not determine repo root from script directory.");
}

string[] GetEditions(string edition) => edition switch
{
    "insiders" => ["insiders"],
    "stable" => ["stable"],
    _ => ["insiders", "stable"]
};

string GetVSCodeUserDir(string edition)
{
    var folder = edition == "insiders" ? "Code - Insiders" : "Code";
    if (OperatingSystem.IsWindows())
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folder, "User");
    if (OperatingSystem.IsMacOS())
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", folder, "User");
    // Linux
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", folder, "User");
}

string FindGitRoot()
{
    var dir = Environment.CurrentDirectory;
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir, ".git")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return Environment.CurrentDirectory;
}

string GetSkillsTargetDir(string scope = "personal")
{
    if (scope == "project")
        return Path.Combine(FindGitRoot(), ".github", "skills");
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot", "skills");
}

// Copilot CLI base config directory (~/.copilot/)
// Ref: https://docs.github.com/en/copilot/how-tos/copilot-cli/use-copilot-cli
//   User-level agents:      ~/.copilot/agents/
//   User-level skills:      ~/.copilot/skills/
//   Local instructions:     ~/.copilot/copilot-instructions.md
//   MCP server config:      ~/.copilot/mcp-config.json
//   General config:         ~/.copilot/config.json
string GetCopilotCliDir()
{
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot");
}

string GetAgentsTargetDir(string scope = "personal")
{
    if (scope == "project")
        return Path.Combine(FindGitRoot(), ".github", "agents");
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot", "agents");
}

// ============================================================================
// Multi-tool target detection
// ============================================================================

List<ToolTarget> DetectTargets(string target)
{
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var copilotDir = Path.Combine(home, ".copilot");
    var claudeDir = Path.Combine(home, ".claude");

    if (target == "copilot") return [new("Copilot CLI", copilotDir)];
    if (target == "claude") return [new("Claude Code", claudeDir)];
    // VS Code reads ~/.copilot/skills/ for skills and ~/.copilot/instructions/ for instructions,
    // so route vscode target through the Copilot CLI directory for those asset types.
    if (target == "vscode") return [new("VS Code (via Copilot)", copilotDir)];
    if (target == "all")
        return [new("Copilot CLI", copilotDir), new("Claude Code", claudeDir)];

    // auto: detect which tools are installed
    var targets = new List<ToolTarget>();
    if (Directory.Exists(copilotDir))
        targets.Add(new("Copilot CLI", copilotDir));
    if (Directory.Exists(claudeDir))
        targets.Add(new("Claude Code", claudeDir));
    if (targets.Count == 0)
        targets.Add(new("Copilot CLI", copilotDir)); // fallback
    return targets;
}

bool TargetIncludesVSCode(string target)
{
    if (target is "vscode" or "all") return true;
    if (target is "copilot" or "claude") return false;
    // auto: check if any VS Code user dir exists
    try { GetVSCodeUserDir("stable"); return true; }
    catch { }
    try { GetVSCodeUserDir("insiders"); return true; }
    catch { }
    return false;
}

List<(string label, string dir)> GetSkillsTargetDirs(string scope, string target)
{
    if (scope == "project")
    {
        var root = FindGitRoot();
        // Claude Code only reads .claude/skills/, not .github/skills/
        if (target == "claude")
            return [("project (.claude)", Path.Combine(root, ".claude", "skills"))];
        if (target == "all")
            return [("project (.github)", Path.Combine(root, ".github", "skills")),
                    ("project (.claude)", Path.Combine(root, ".claude", "skills"))];
        return [("project", Path.Combine(root, ".github", "skills"))];
    }
    return DetectTargets(target)
        .Select(t => (t.Name, Path.Combine(t.RootDir, "skills")))
        .ToList();
}

List<(string label, string dir)> GetAgentsTargetDirs(string scope, string target)
{
    if (scope == "project")
    {
        var root = FindGitRoot();
        // Claude Code only reads .claude/agents/, not .github/agents/
        if (target == "claude")
            return [("project (.claude)", Path.Combine(root, ".claude", "agents"))];
        if (target == "all")
            return [("project (.github)", Path.Combine(root, ".github", "agents")),
                    ("project (.claude)", Path.Combine(root, ".claude", "agents"))];
        return [("project", Path.Combine(root, ".github", "agents"))];
    }
    // Only Copilot CLI supports personal agents; VS Code and Claude use project-level only
    var dirs = DetectTargets(target)
        .Where(t => t.Name == "Copilot CLI")
        .Select(t => (t.Name, Path.Combine(t.RootDir, "agents")))
        .ToList();
    if (dirs.Count == 0 && target == "vscode")
        Console.WriteLine("  ℹ VS Code does not support personal agents — use --scope project to install to .github/agents/");
    return dirs;
}

List<McpTarget> GetMcpTargetPaths(string target, string edition)
{
    var result = new List<McpTarget>();
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    bool includeCopilot = target is "copilot" or "all" ||
        (target == "auto" && Directory.Exists(Path.Combine(home, ".copilot")));
    bool includeClaude = target is "claude" or "all" ||
        (target == "auto" && Directory.Exists(Path.Combine(home, ".claude")));
    bool includeVSCode = TargetIncludesVSCode(target);

    if (includeCopilot)
        result.Add(new("Copilot CLI", Path.Combine(home, ".copilot", "mcp-config.json"), "mcpServers"));
    if (includeClaude)
        result.Add(new("Claude Code", Path.Combine(home, ".claude", "settings.local.json"), "mcpServers"));
    if (includeVSCode)
    {
        foreach (var ed in GetEditions(edition))
            result.Add(new($"VS Code ({ed})", Path.Combine(GetVSCodeUserDir(ed), "mcp.json"), "servers"));
    }

    // Fallback: if nothing detected, default to Copilot CLI
    if (result.Count == 0)
        result.Add(new("Copilot CLI", Path.Combine(home, ".copilot", "mcp-config.json"), "mcpServers"));

    return result;
}

// Parse git remote URL to owner/repo shorthand for VS Code marketplace setting
string? GetMarketplaceRef()
{
    var url = GetGitRemoteUrl();
    if (url == null) return null;
    // https://github.com/owner/repo.git or git@github.com:owner/repo.git
    var m = System.Text.RegularExpressions.Regex.Match(url, @"github\.com[:/]([^/]+)/([^/]+?)(?:\.git)?$");
    return m.Success ? $"{m.Groups[1].Value}/{m.Groups[2].Value}" : null;
}

string? GetGitRemoteUrl()
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "remote get-url origin",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            return output;
    }
    catch { }
    return null;
}

string? GetGitHubUser()
{
    if (_cachedGitHubUser != null) return _cachedGitHubUser;
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "gh",
            Arguments = "api user --jq .login",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
        {
            _cachedGitHubUser = output;
            return output;
        }
    }
    catch { }
    return null;
}

List<(string pluginName, string assetName, string assetPath)> ResolveDuplicateAssets(
    List<(string pluginName, string assetName, string assetPath)> assets)
{
    var groups = assets.GroupBy(s => s.assetName, StringComparer.OrdinalIgnoreCase).ToList();
    var noDupes = groups.Where(g => g.Count() == 1).SelectMany(g => g).ToList();
    var dupeGroups = groups.Where(g => g.Count() > 1).ToList();

    if (dupeGroups.Count == 0) return assets;

    var user = GetGitHubUser();

    foreach (var group in dupeGroups)
    {
        var match = user != null
            ? group.FirstOrDefault(s => s.pluginName.Equals(user, StringComparison.OrdinalIgnoreCase))
            : default;

        if (match != default)
        {
            noDupes.Add(match);
            var skipped = group.Where(s => s != match).Select(s => s.pluginName);
            PrintWarning($"  Duplicate '{group.Key}' — using personal plugin '{match.pluginName}'" +
                $" (skipping: {string.Join(", ", skipped)})");
        }
        else
        {
            var plugins = string.Join(", ", group.Select(s => s.pluginName));
            PrintWarning($"  Duplicate '{group.Key}' in plugins: {plugins} — skipping." +
                $" Use --plugin to select one.");
        }
    }

    return noDupes;
}

string GetInstructionsTargetDir()
{
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot-instructions", "instructions");
}

// ============================================================================
// Backup Helpers
// ============================================================================

string EnsureBackupDir(string category, string? edition)
{
    _backupTimestamp ??= DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
    var baseDir = _customBackupPath ?? Path.Combine(repoRoot, "backup");
    var parts = new List<string> { baseDir, _backupTimestamp };
    if (edition != null) parts.Add(edition);
    parts.Add(category);
    var dir = Path.Combine(parts.ToArray());
    Directory.CreateDirectory(dir);
    return dir;
}

void BackupFile(string filePath, string category, string? edition)
{
    if (!File.Exists(filePath)) return;
    var backupDir = EnsureBackupDir(category, edition);
    var dest = Path.Combine(backupDir, Path.GetFileName(filePath));
    File.Copy(filePath, dest, true);
}

void BackupDirectory(string dirPath, string category, string? edition)
{
    if (!Directory.Exists(dirPath)) return;
    var backupDir = EnsureBackupDir(category, edition);
    var dest = Path.Combine(backupDir, Path.GetFileName(dirPath));
    CopyDirectoryRecursive(dirPath, dest);
}

void CopyDirectoryRecursive(string source, string target)
{
    Directory.CreateDirectory(target);
    foreach (var file in Directory.GetFiles(source))
        File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
    foreach (var dir in Directory.GetDirectories(source))
        CopyDirectoryRecursive(dir, Path.Combine(target, Path.GetFileName(dir)));
}

// ============================================================================
// JSON Helpers
// ============================================================================

JsonNode? ReadJsoncNode(string path)
{
    var text = File.ReadAllText(path);
    return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    });
}

void WriteJsonNode(string path, JsonNode node)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(path, node.ToJsonString(options));
}

McpConfig? ReadMcpConfig(string path)
{
    var text = File.ReadAllText(path);
    using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    });
    return JsonSerializer.Deserialize(doc, AppJsonContext.Default.McpConfig);
}

void WriteMcpConfig(string path, McpConfig config)
{
    var json = JsonSerializer.Serialize(config, AppJsonContext.Default.McpConfig);
    File.WriteAllText(path, json);
}

/// <summary>Read MCP servers from a JSON file using the specified wrapper key.</summary>
Dictionary<string, JsonNode?> ReadMcpServers(string path, string wrapperKey)
{
    if (!File.Exists(path)) return new();
    var root = ReadJsoncNode(path) as JsonObject;
    if (root == null) return new();
    var serversNode = root[wrapperKey] as JsonObject;
    if (serversNode == null) return new();
    var result = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
    foreach (var prop in serversNode)
        result[prop.Key] = prop.Value?.DeepClone();
    return result;
}

/// <summary>Write MCP servers to a JSON file under the specified wrapper key, preserving other content.</summary>
void WriteMcpServers(string path, string wrapperKey, Dictionary<string, JsonNode?> servers)
{
    JsonObject root;
    if (File.Exists(path))
    {
        root = (ReadJsoncNode(path) as JsonObject) ?? new JsonObject();
    }
    else
    {
        root = new JsonObject();
    }
    var serversObj = new JsonObject();
    foreach (var (name, config) in servers)
        serversObj[name] = config?.DeepClone();
    root[wrapperKey] = serversObj;
    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(path, root.ToJsonString(options));
}


// ============================================================================
// Process Helpers
// ============================================================================

int RunProcess(string fileName, string arguments)
{
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = false
    };
    using var process = System.Diagnostics.Process.Start(psi)!;
    process.WaitForExit();
    return process.ExitCode;
}

// ============================================================================
// Console Output Helpers
// ============================================================================

void PrintHeader(string text)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n=== {text} ===");
    Console.ResetColor();
}

void PrintSuccess(string text)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(text);
    Console.ResetColor();
}

void PrintWarning(string text)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(text);
    Console.ResetColor();
}

void PrintError(string text)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(text);
    Console.ResetColor();
}

#pragma warning restore CS8604
#pragma warning restore CS8619

void PrintInfo(string text)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(text);
    Console.ResetColor();
}

// ============================================================================
// JSON Models & Source Generator Context
// ============================================================================

record ToolTarget(string Name, string RootDir);
record McpTarget(string Label, string Path, string WrapperKey);

record UpstreamConfig(string Name, string TargetRepo, string TargetBranch, string Description, List<UpstreamPluginMapping> Plugins);
record UpstreamPluginMapping(string LocalPlugin, string TargetPlugin, List<string> Skills);

enum DriftStatus { InSync, LocalAhead, RemoteAhead, Diverged, LocalOnly, RemoteOnly }

record FileDrift(string RelativePath, DriftStatus Status, string? LocalSha, string? RemoteSha);
record SkillDrift(string SkillName, string LocalPlugin, string TargetPlugin, List<FileDrift> Files)
{
    public DriftStatus OverallStatus => Files.All(f => f.Status == DriftStatus.InSync) ? DriftStatus.InSync
        : Files.Any(f => f.Status == DriftStatus.Diverged) ? DriftStatus.Diverged
        : Files.All(f => f.Status is DriftStatus.LocalAhead or DriftStatus.LocalOnly or DriftStatus.InSync) ? DriftStatus.LocalAhead
        : Files.All(f => f.Status is DriftStatus.RemoteAhead or DriftStatus.RemoteOnly or DriftStatus.InSync) ? DriftStatus.RemoteAhead
        : DriftStatus.Diverged;
}

/// <summary>Represents the structure of an mcp.json configuration file.</summary>
class McpConfig
{
    [JsonPropertyName("inputs")]
    public JsonArray? Inputs { get; set; }

    [JsonPropertyName("servers")]
    public Dictionary<string, JsonNode?>? Servers { get; set; }
}

/// <summary>
/// Source-generated JSON serializer context for AOT-friendly, efficient serialization.
/// Handles McpConfig with indented output and null-value suppression.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(McpConfig))]
internal partial class AppJsonContext : JsonSerializerContext { }
