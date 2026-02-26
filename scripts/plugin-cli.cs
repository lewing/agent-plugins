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
    editionOption, exactOption, forceOption, dryRunOption, verboseOption, backupPathOption
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
    Description = "Install target: personal (~/.copilot/skills/) or project (.github/skills/)",
    DefaultValueFactory = _ => "personal",
    Recursive = true
};
scopeOption.AcceptOnlyFromAmong("personal", "project");
var skillsCmd = new Command("skills", "Manage agent skills (SKILL.md folders)");
skillsCmd.Options.Add(pluginOption);
skillsCmd.Options.Add(skillOption);
skillsCmd.Options.Add(scopeOption);
skillsCmd.Subcommands.Add(Action("list", "List installed skills", pr =>
    RunSkillsList(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption))));
skillsCmd.Subcommands.Add(Action("install", "Install skills from repo", pr =>
    RunSkillsInstall(pr.GetValue(exactOption), pr.GetValue(forceOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption))));
skillsCmd.Subcommands.Add(Action("uninstall", "Remove repo-managed skills", pr =>
    RunSkillsUninstall(pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption))));
skillsCmd.Subcommands.Add(Action("diff", "Compare repo vs installed skills", pr =>
    RunSkillsDiff(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(skillOption), pr.GetValue(scopeOption))));
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
    RunAgentsList(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption))));
agentsCmd.Subcommands.Add(Action("install", "Install agents from repo", pr =>
    RunAgentsInstall(pr.GetValue(exactOption), pr.GetValue(forceOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption))));
agentsCmd.Subcommands.Add(Action("uninstall", "Remove repo-managed agents", pr =>
    RunAgentsUninstall(pr.GetValue(dryRunOption), pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption))));
agentsCmd.Subcommands.Add(Action("diff", "Compare repo vs installed agents", pr =>
    RunAgentsDiff(pr.GetValue(verboseOption), pr.GetValue(pluginOption), pr.GetValue(scopeOption))));
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
        pr.GetValue(editionOption), pr.GetValue(scopeOption)));
    pluginCmd.Subcommands.Add(installCmd);

    var listNameArg = new Argument<string?>("name") { Description = nameArg.Description, Arity = ArgumentArity.ZeroOrOne };
    var listCmd = new Command("list", "Show plugins and their contents") { listNameArg };
    listCmd.SetAction(pr => RunPluginList(pr.GetValue(listNameArg), pr.GetValue(verboseOption)));
    pluginCmd.Subcommands.Add(listCmd);

    var diffNameArg = new Argument<string?>("name") { Description = nameArg.Description, Arity = ArgumentArity.ZeroOrOne };
    var diffCmd = new Command("diff", "Compare plugin assets: repo vs installed") { diffNameArg };
    diffCmd.SetAction(pr => RunPluginDiff(pr.GetValue(diffNameArg), pr.GetValue(verboseOption),
        pr.GetValue(editionOption), pr.GetValue(scopeOption)));
    pluginCmd.Subcommands.Add(diffCmd);

    var uninstallNameArg = new Argument<string?>("name") { Description = nameArg.Description, Arity = ArgumentArity.ZeroOrOne };
    var uninstallCmd = new Command("uninstall", "Remove a plugin's installed assets") { uninstallNameArg };
    uninstallCmd.SetAction(pr => RunPluginUninstall(pr.GetValue(uninstallNameArg),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption),
        pr.GetValue(editionOption), pr.GetValue(scopeOption)));
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
    RunMcpList(pr.GetValue(editionOption), pr.GetValue(verboseOption))));
mcpCmd.Subcommands.Add(Action("install", "Merge template MCP servers into config", pr =>
    RunMcpInstall(pr.GetValue(editionOption), pr.GetValue(exactOption),
        pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
mcpCmd.Subcommands.Add(Action("uninstall", "Remove template MCP servers from config", pr =>
    RunMcpUninstall(pr.GetValue(editionOption), pr.GetValue(dryRunOption), pr.GetValue(verboseOption))));
mcpCmd.Subcommands.Add(Action("diff", "Compare template vs installed MCP servers", pr =>
    RunMcpDiff(pr.GetValue(editionOption), pr.GetValue(verboseOption))));
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

// ----- All -----
var allCmd = new Command("all", "Bulk operations across all categories");
allCmd.Subcommands.Add(Action("list", "List all asset types", pr =>
{
    var edition = pr.GetValue(editionOption);
    var verbose = pr.GetValue(verboseOption);
    RunPluginList(null, verbose);
    RunFileCategoryList(edition, "prompts", "prompts", "*.prompt.md", "Prompts", verbose);
    RunInstructionsList(verbose);
    RunMcpList(edition, verbose);
    RunSettingsList(edition, verbose);
}));
allCmd.Subcommands.Add(Action("install", "Install everything from repo", pr =>
{
    var edition = pr.GetValue(editionOption);
    var exact = pr.GetValue(exactOption);
    var force = pr.GetValue(forceOption);
    var dryRun = pr.GetValue(dryRunOption);
    var verbose = pr.GetValue(verboseOption);
    var scope = pr.GetValue(scopeOption);
    RunPluginInstall(null, force, dryRun, verbose, edition, scope);
    RunFileCategoryInstall(edition, "prompts", "prompts", "*.prompt.md", "Prompts", exact, force, dryRun, verbose);
    RunInstructionsInstall(edition, exact, force, dryRun, verbose);
    RunMcpInstall(edition, exact, dryRun, verbose);
    RunSettingsUpdate(edition, dryRun, verbose);
}));
allCmd.Subcommands.Add(Action("uninstall", "Uninstall everything", pr =>
{
    var edition = pr.GetValue(editionOption);
    var dryRun = pr.GetValue(dryRunOption);
    var verbose = pr.GetValue(verboseOption);
    var scope = pr.GetValue(scopeOption);
    RunPluginUninstall(null, dryRun, verbose, edition, scope);
    RunFileCategoryUninstall(edition, "prompts", "prompts", "*.prompt.md", "Prompts", dryRun, verbose);
    RunInstructionsUninstall(dryRun, verbose);
    RunMcpUninstall(edition, dryRun, verbose);
}));
allCmd.Subcommands.Add(Action("diff", "Diff all asset types", pr =>
{
    var edition = pr.GetValue(editionOption);
    var verbose = pr.GetValue(verboseOption);
    var scope = pr.GetValue(scopeOption);
    RunPluginDiff(null, verbose, edition, scope);
    RunFileCategoryDiff(edition, "prompts", "prompts", "*.prompt.md", "Prompts", verbose);
    RunInstructionsDiff(verbose);
    RunMcpDiff(edition, verbose);
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
    RunSkillsInstall(false, true, dryRun, verbose, null, null, "personal");
    RunAgentsInstall(false, true, dryRun, verbose, null, "personal");
    RunFileCategoryInstall(edition, "prompts", "prompts", "*.prompt.md", "Prompts", false, true, dryRun, verbose);
    RunInstructionsInstall(edition, false, true, dryRun, verbose);
    RunMcpInstall(edition, false, dryRun, verbose);
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

void RunSkillsList(bool verbose, string? pluginFilter, string? skillFilter, string scope)
    => RunAssetList("Skills", scope, GetSkillsTargetDir(scope), GetSourceSkills(pluginFilter, skillFilter), pluginFilter,
        targetDir => Directory.Exists(targetDir)
            ? Directory.GetDirectories(targetDir)
                .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
                .Select(Path.GetFileName).ToList()!
            : []);

void RunAgentsList(bool verbose, string? pluginFilter, string scope)
    => RunAssetList("Agents", scope, GetAgentsTargetDir(scope), GetSourceAgents(pluginFilter), pluginFilter,
        targetDir => Directory.Exists(targetDir)
            ? Directory.GetFiles(targetDir, "*.agent.md")
                .Select(Path.GetFileName).ToList()!
            : []);

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

void RunSkillsInstall(bool exact, bool force, bool dryRun, bool verbose, string? pluginFilter, string? skillFilter, string scope)
    => RunAssetInstall("Skills", GetSkillsTargetDir(scope), scope,
        GetSourceSkills(pluginFilter, skillFilter), pluginFilter,
        exact, force, dryRun, verbose,
        assetPath => Path.Combine(GetSkillsTargetDir(scope), Path.GetFileName(assetPath)),
        dst => Directory.Exists(dst),
        (dst, _) => BackupDirectory(dst, "skills", null),
        (src, dst) => CopyDirectoryRecursive(src, dst),
        targetDir => Directory.Exists(targetDir)
            ? Directory.GetDirectories(targetDir)
                .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
                .Select(Path.GetFileName).ToList()!
            : [],
        path => { BackupDirectory(path, "skills", null); Directory.Delete(path, true); });

void RunAgentsInstall(bool exact, bool force, bool dryRun, bool verbose, string? pluginFilter, string scope)
    => RunAssetInstall("Agents", GetAgentsTargetDir(scope), scope,
        GetSourceAgents(pluginFilter), pluginFilter,
        exact, force, dryRun, verbose,
        assetPath => Path.Combine(GetAgentsTargetDir(scope), Path.GetFileName(assetPath)),
        dst => File.Exists(dst),
        (dst, _) => BackupFile(dst, "agents", null),
        (src, dst) => File.Copy(src, dst, true),
        targetDir => Directory.Exists(targetDir)
            ? Directory.GetFiles(targetDir, "*.agent.md")
                .Select(Path.GetFileName).ToList()!
            : [],
        path => { BackupFile(path, "agents", null); File.Delete(path); });

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

void RunSkillsUninstall(bool dryRun, bool verbose, string? pluginFilter, string? skillFilter, string scope)
    => RunAssetUninstall("Skills", GetSkillsTargetDir(scope), scope,
        GetSourceSkills(pluginFilter, skillFilter), pluginFilter, dryRun, verbose,
        assetPath => Path.Combine(GetSkillsTargetDir(scope), Path.GetFileName(assetPath)),
        dst => Directory.Exists(dst),
        dst => { BackupDirectory(dst, "skills", null); Directory.Delete(dst, true); });

void RunAgentsUninstall(bool dryRun, bool verbose, string? pluginFilter, string scope)
    => RunAssetUninstall("Agents", GetAgentsTargetDir(scope), scope,
        GetSourceAgents(pluginFilter), pluginFilter, dryRun, verbose,
        assetPath => Path.Combine(GetAgentsTargetDir(scope), Path.GetFileName(assetPath)),
        dst => File.Exists(dst),
        dst => { BackupFile(dst, "agents", null); File.Delete(dst); });

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

void RunSkillsDiff(bool verbose, string? pluginFilter, string? skillFilter, string scope)
{
    PrintHeader($"Skills diff ({scope})");
    var targetDir = GetSkillsTargetDir(scope);
    var source = GetSourceSkills(pluginFilter, skillFilter);
    if (pluginFilter == null)
        source = ResolveDuplicateAssets(source);

    var sourceMap = source.ToDictionary(s => s.assetName, s => s.assetPath, StringComparer.OrdinalIgnoreCase);
    var targetItems = Directory.Exists(targetDir)
        ? Directory.GetDirectories(targetDir)
            .Where(d => File.Exists(Path.Combine(d, "SKILL.md")))
            .Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    DiffSets(sourceMap.Keys, targetItems!, "  ",
        name => File.ReadAllText(Path.Combine(sourceMap[name], "SKILL.md")),
        name => File.ReadAllText(Path.Combine(targetDir, name, "SKILL.md")));
}

void RunAgentsDiff(bool verbose, string? pluginFilter, string scope)
{
    PrintHeader($"Agents diff ({scope})");
    var targetDir = GetAgentsTargetDir(scope);
    var source = GetSourceAgents(pluginFilter);
    if (pluginFilter == null)
        source = ResolveDuplicateAssets(source);

    var sourceMap = source.ToDictionary(
        a => Path.GetFileName(a.assetPath), a => a.assetPath, StringComparer.OrdinalIgnoreCase);
    var targetItems = Directory.Exists(targetDir)
        ? Directory.GetFiles(targetDir, "*.agent.md")
            .Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    DiffSets(sourceMap.Keys, targetItems!, "  ",
        name => File.ReadAllText(sourceMap[name]),
        name => File.ReadAllText(Path.Combine(targetDir, name)));
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
        return;
    }
    var files = Directory.GetFiles(targetDir, "*.md");
    if (files.Length == 0)
    {
        PrintInfo("    (none)");
        return;
    }
    foreach (var file in files)
        Console.WriteLine($"    {Path.GetFileName(file)}");
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
}

// ============================================================================
// MCP Handlers (edition-dependent)
// ============================================================================

void RunMcpList(string edition, bool verbose)
{
    PrintHeader("MCP Servers");
    foreach (var ed in GetEditions(edition))
    {
        var mcpPath = Path.Combine(GetVSCodeUserDir(ed), "mcp.json");
        Console.WriteLine($"  [{ed}] {mcpPath}");
        if (!File.Exists(mcpPath))
        {
            PrintInfo("    (file not found)");
            continue;
        }
        var config = ReadMcpConfig(mcpPath);
        if (config?.Servers == null || config.Servers.Count == 0)
        {
            PrintInfo("    (no servers)");
            continue;
        }
        foreach (var (name, node) in config.Servers)
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

/// <summary>Install MCP servers into user mcp.json across editions, with backup tracking.</summary>
/// <returns>Total number of servers merged across all editions.</returns>
int InstallMcpServersToEditions(Dictionary<string, JsonNode?> servers, string edition,
    bool dryRun, bool verbose, HashSet<string>? backedUp = null)
{
    int total = 0;
    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"    [{ed}]");
        var mcpPath = Path.Combine(GetVSCodeUserDir(ed), "mcp.json");

        McpConfig? userConfig = null;
        if (File.Exists(mcpPath))
        {
            if (!dryRun && (backedUp == null || backedUp.Add(mcpPath)))
                BackupFile(mcpPath, "mcp", ed);
            userConfig = ReadMcpConfig(mcpPath);
        }
        userConfig ??= new McpConfig();
        userConfig.Servers ??= new Dictionary<string, JsonNode?>();

        total += MergeServersIntoConfig(userConfig.Servers, servers, dryRun, verbose);

        if (!dryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mcpPath)!);
            WriteMcpConfig(mcpPath, userConfig);
            PrintSuccess($"      Wrote: {mcpPath}");
        }
    }
    return total;
}

void RunMcpInstall(string edition, bool exact, bool dryRun, bool verbose)
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

    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var mcpPath = Path.Combine(GetVSCodeUserDir(ed), "mcp.json");

        McpConfig? userConfig = null;
        if (File.Exists(mcpPath))
        {
            if (!dryRun) BackupFile(mcpPath, "mcp", ed);
            userConfig = ReadMcpConfig(mcpPath);
        }
        userConfig ??= new McpConfig();
        userConfig.Servers ??= new Dictionary<string, JsonNode?>();

        MergeServersIntoConfig(userConfig.Servers, template.Servers, dryRun, verbose);

        // Exact mode: remove servers not in template
        if (exact)
        {
            var templateNames = template.Servers.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toRemove = userConfig.Servers.Keys
                .Where(k => !templateNames.Contains(k))
                .ToList();
            foreach (var name in toRemove)
            {
                userConfig.Servers.Remove(name);
                if (dryRun)
                    PrintInfo($"    Would remove (not in template): {name}");
                else
                    PrintWarning($"    Removed (not in template): {name}");
            }
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mcpPath)!);
            WriteMcpConfig(mcpPath, userConfig);
            PrintSuccess($"    Wrote: {mcpPath}");
        }
    }
}

void RunMcpUninstall(string edition, bool dryRun, bool verbose)
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

    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var mcpPath = Path.Combine(GetVSCodeUserDir(ed), "mcp.json");
        if (!File.Exists(mcpPath))
        {
            PrintInfo("    (no mcp.json)");
            continue;
        }

        if (!dryRun) BackupFile(mcpPath, "mcp", ed);
        var userConfig = ReadMcpConfig(mcpPath);
        if (userConfig?.Servers == null) continue;

        foreach (var name in template.Servers.Keys)
        {
            if (userConfig.Servers.Remove(name))
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

        if (!dryRun) WriteMcpConfig(mcpPath, userConfig);
    }
}

void RunMcpDiff(string edition, bool verbose)
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

    foreach (var ed in GetEditions(edition))
    {
        Console.WriteLine($"  [{ed}]");
        var mcpPath = Path.Combine(GetVSCodeUserDir(ed), "mcp.json");
        if (!File.Exists(mcpPath))
        {
            PrintInfo("    (no mcp.json — all template servers would be added)");
            continue;
        }

        var userConfig = ReadMcpConfig(mcpPath);
        var userServers = userConfig?.Servers ?? new Dictionary<string, JsonNode?>();

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

void RunPluginInstall(string? pluginName, bool force, bool dryRun, bool verbose, string edition, string scope)
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
            RunSkillsInstall(false, force, dryRun, verbose, name, null, scope);
            skillCount = skills.Count;
        }

        // Agents
        var agents = GetSourceAgents(name);
        if (agents.Count > 0)
        {
            RunAgentsInstall(false, force, dryRun, verbose, name, scope);
            agentCount = agents.Count;
        }

        // MCP servers from plugin.json
        var (mcpServers, lspServers) = ReadPluginServers(name);
        if (mcpServers != null && mcpServers.Count > 0)
        {
            Console.WriteLine($"  MCP servers ({mcpServers.Count}):");
            mcpCount += InstallMcpServersToEditions(mcpServers, edition, dryRun, verbose, backedUp);
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

void RunPluginDiff(string? pluginName, bool verbose, string edition, string scope)
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
            var targetDir = GetSkillsTargetDir(scope);
            Console.WriteLine($"  Skills:");
            foreach (var (_, assetName, assetPath) in skills)
            {
                var dst = Path.Combine(targetDir, Path.GetFileName(assetPath));
                if (!Directory.Exists(dst))
                {
                    Console.WriteLine($"    + {assetName} (not installed)");
                    hasDiffs = true;
                }
                else if (verbose)
                    PrintInfo($"    = {assetName}");
            }
        }

        // Agents diff
        var agents = GetSourceAgents(name);
        if (agents.Count > 0)
        {
            Console.WriteLine($"  Agents:");
            var targetDir = GetAgentsTargetDir(scope);
            foreach (var (_, assetName, assetPath) in agents)
            {
                var dst = Path.Combine(targetDir, Path.GetFileName(assetPath));
                if (!File.Exists(dst))
                {
                    Console.WriteLine($"    + {assetName} (not installed)");
                    hasDiffs = true;
                }
                else if (verbose)
                    PrintInfo($"    = {assetName}");
            }
        }

        // MCP servers diff
        var (mcpServers, lspServers) = ReadPluginServers(name);
        if (mcpServers != null && mcpServers.Count > 0)
        {
            Console.WriteLine($"  MCP Servers:");
            foreach (var ed in GetEditions(edition))
            {
                Console.WriteLine($"    [{ed}]");
                var mcpPath = Path.Combine(GetVSCodeUserDir(ed), "mcp.json");
                if (!File.Exists(mcpPath))
                {
                    foreach (var srvName in mcpServers.Keys)
                    {
                        Console.WriteLine($"      + {srvName} (not installed)");
                        hasDiffs = true;
                    }
                    continue;
                }
                var userConfig = ReadMcpConfig(mcpPath);
                var userServers = userConfig?.Servers ?? new Dictionary<string, JsonNode?>();
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

void RunPluginUninstall(string? pluginName, bool dryRun, bool verbose, string edition, string scope)
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
            RunSkillsUninstall(dryRun, verbose, name, null, scope);

        // Agents
        var agents = GetSourceAgents(name);
        if (agents.Count > 0)
            RunAgentsUninstall(dryRun, verbose, name, scope);

        // MCP servers from plugin.json
        var (mcpServers, _) = ReadPluginServers(name);
        if (mcpServers != null && mcpServers.Count > 0)
        {
            Console.WriteLine($"  MCP servers:");
            foreach (var ed in GetEditions(edition))
            {
                Console.WriteLine($"    [{ed}]");
                var mcpPath = Path.Combine(GetVSCodeUserDir(ed), "mcp.json");
                if (!File.Exists(mcpPath)) continue;

                if (!dryRun && backedUp.Add(mcpPath)) BackupFile(mcpPath, "mcp", ed);
                var userConfig = ReadMcpConfig(mcpPath);
                if (userConfig?.Servers == null) continue;

                foreach (var srvName in mcpServers.Keys)
                {
                    if (userConfig.Servers.Remove(srvName))
                    {
                        if (dryRun)
                            PrintInfo($"      Would remove: {srvName}");
                        else
                            PrintSuccess($"      Removed: {srvName}");
                    }
                    else if (verbose)
                        PrintInfo($"      Not present: {srvName}");
                }

                if (!dryRun) WriteMcpConfig(mcpPath, userConfig);
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
        if (!hasDiffs) PrintInfo("    (all required settings present)");
    }
}

// ============================================================================
// Shared File Sync Helpers
// ============================================================================

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

string GetAgentsTargetDir(string scope = "personal")
{
    if (scope == "project")
        return Path.Combine(FindGitRoot(), ".github", "agents");
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot", "agents");
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
