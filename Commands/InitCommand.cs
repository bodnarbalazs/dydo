namespace DynaDocs.Commands;

using System.CommandLine;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsToml;
using CsToml.Error;
using CsToml.Values;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class InitCommand
{
    public static Command Create()
    {
        var integrationArgument = new Argument<string>("integration")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "Integration to configure (claude, codex, all, none)"
        };

        var joinOption = new Option<bool>("--join")
        {
            Description = "Join an existing DynaDocs project as a new team member"
        };

        var command = new Command("init", "Initialize DynaDocs with specified integration");
        command.Arguments.Add(integrationArgument);
        command.Options.Add(joinOption);

        command.SetAction(parseResult =>
        {
            var integration = parseResult.GetValue(integrationArgument)!;
            var join = parseResult.GetValue(joinOption);

            return join
                ? ExecuteJoin(integration)
                : ExecuteInit(integration);
        });

        return command;
    }

    private static int ExecuteInit(string integration)
    {
        if (!IsValidIntegration(integration))
            return IntegrationError(integration);
        integration = integration.ToLowerInvariant();

        var configService = new ConfigService();
        var existingConfig = configService.FindConfigFile();
        if (existingConfig != null)
        {
            ConsoleOutput.WriteError($"DynaDocs already initialized. Config found at: {existingConfig}");
            Console.WriteLine("Use 'dydo init <integration> --join' to add an integration to this project or wire up this machine.");
            return ExitCodes.ToolError;
        }

        Console.WriteLine();
        Console.WriteLine("DynaDocs Setup");
        Console.WriteLine(new string('─', 14));
        Console.WriteLine();
        Console.WriteLine("Creating...");

        try
        {
            var projectRoot = Environment.CurrentDirectory;
            var config = ConfigFactory.CreateDefault();
            config.DydoVersion = DydoVersion.Current;
            var integrations = ExpandIntegrations(integration);
            foreach (var name in integrations)
                config.Integrations[name] = true;

            var hostSettings = PrepareHostSettings(projectRoot, integrations);

            var configPath = Path.Combine(projectRoot, ConfigService.ConfigFileName);
            configService.SaveConfig(config, configPath);
            Console.WriteLine($"  ✓ {ConfigService.ConfigFileName}");

            ScaffoldProject(config, projectRoot, integrations);
            WriteHostSettings(hostSettings);
            PrintInitSummary(integrations);

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static string[] ExpandIntegrations(string integration) =>
        integration == "all" ? ["claude", "codex"] : [integration];

    private static void ScaffoldProject(DydoConfig config,
        string projectRoot, string[] integrations)
    {
        WriteEntryPoint(projectRoot);
        if (integrations.Contains("claude"))
            ReconcileClaudeEntryFiles(projectRoot);

        var dydoRoot = Path.Combine(projectRoot, config.Structure.Root);
        Directory.CreateDirectory(dydoRoot);

        var scaffolder = new FolderScaffolder();
        scaffolder.Scaffold(dydoRoot);
        Console.WriteLine($"  ✓ {config.Structure.Root}/ documentation structure");

        WriteIfNotExists(
            Path.Combine(dydoRoot, "files-off-limits.md"),
            () => ScaffoldTree.Read("Scaffold/dydo/files-off-limits.md"),
            "files-off-limits.md (security config)");

        UpdateGitignore(projectRoot, config.Structure.Root, integrations);
        Console.WriteLine($"  ✓ Updated .gitignore (agents/, local state)");

        if (integrations.Contains("claude"))
        {
            ConfigureClaudeHooks(projectRoot);
            Console.WriteLine("  ✓ Claude Code hooks configured");
        }
        if (integrations.Contains("codex"))
        {
            ConfigureCodexHooks(projectRoot);
            Console.WriteLine("  - Codex hooks configured");
        }
    }

    private static void PrintInitSummary(string[] integrations)
    {
        Console.WriteLine();
        Console.WriteLine("Documentation funnel created:");
        Console.WriteLine("  AGENTS.md -> dydo/index.md (orientation) -> the docs");
        if (integrations.Contains("claude"))
            Console.WriteLine("  Claude Code 2.1.281 or later reads AGENTS.md; earlier versions may read only CLAUDE.md.");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  1. Customize dydo/understand/architecture.md for your project");
        Console.WriteLine("  2. Customize dydo/guides/coding-standards.md");
        Console.WriteLine();

        var completionResult = ShellCompletionInstaller.Install();
        if (completionResult != null)
            Console.WriteLine($"  {completionResult}");
    }

    // Join wires this machine's integration into an already-initialized project (a fresh clone,
    // or a second integration) without re-scaffolding or overwriting the tree.
    internal static int ExecuteJoin(string integration, Action? beforeConfigCommit = null)
    {
        if (!IsValidIntegration(integration))
            return IntegrationError(integration);
        integration = integration.ToLowerInvariant();

        var configService = new ConfigService();
        var configPath = configService.FindConfigFile();
        if (configPath == null)
        {
            ConsoleOutput.WriteError("No DynaDocs project found. Run 'dydo init <integration>' first to create one.");
            return ExitCodes.ToolError;
        }

        Console.WriteLine();
        Console.WriteLine("Joining DynaDocs project...");
        Console.WriteLine();

        try
        {
            var projectRoot = Path.GetDirectoryName(configPath)!;
            var integrations = ExpandIntegrations(integration);
            var config = configService.LoadConfig(projectRoot);
            var hostSettings = PrepareHostSettings(projectRoot, integrations);

            if (integrations.Contains("claude") || integrations.Contains("codex"))
                WriteEntryPoint(projectRoot);

            if (integrations.Contains("claude"))
            {
                ReconcileClaudeEntryFiles(projectRoot);
                ConfigureClaudeHooks(projectRoot);
                Console.WriteLine("  ✓ Claude Code hooks configured");
            }

            if (integrations.Contains("codex"))
            {
                ConfigureCodexHooks(projectRoot);
                Console.WriteLine("  - Codex hooks configured");
            }

            UpdateGitignore(projectRoot, config?.Structure.Root ?? "dydo", integrations);

            // Joining wires this machine, but the integration set is project state: record it
            // so dydo.json reflects every integration the project actually uses (issue 0300).
            var pendingConfig = config != null && integrations.Any(name => !config.Integrations.GetValueOrDefault(name))
                ? config
                : null;
            if (pendingConfig != null)
            {
                foreach (var name in integrations)
                    pendingConfig.Integrations[name] = true;
                Console.WriteLine($"  ✓ Recorded integration(s) in {ConfigService.ConfigFileName}: {string.Join(", ", integrations)}");
            }

            var completionResult = ShellCompletionInstaller.Install();
            if (completionResult != null)
            {
                Console.WriteLine();
                Console.WriteLine($"  {completionResult}");
            }

            if (pendingConfig != null)
            {
                beforeConfigCommit?.Invoke();
                configService.SaveConfig(pendingConfig, configPath);
            }

            WriteHostSettings(hostSettings);

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }


    private static int IntegrationError(string integration)
    {
        ConsoleOutput.WriteError($"Unknown integration: {integration}. Valid options: claude, codex, all, none");
        return ExitCodes.ToolError;
    }

    private static bool IsValidIntegration(string integration)
    {
        return integration.ToLowerInvariant() switch
        {
            "claude" => true,
            "codex" => true,
            "all" => true,
            "none" => true,
            _ => false
        };
    }

    // Content is the file's bytes in its own encoding; Notice is the one line init and join print
    // when a setting overrode the user's value.
    private sealed record HostSetting(string Path, byte[] Content, string? Notice);

    private static List<HostSetting> PrepareHostSettings(string projectRoot, string[] integrations)
    {
        var prepared = new List<HostSetting>();
        if (integrations.Contains("claude"))
        {
            var setting = PrepareClaudeSettings(projectRoot);
            if (setting != null)
                prepared.Add(setting);
        }
        if (integrations.Contains("codex"))
        {
            var setting = PrepareCodexSettings(projectRoot);
            if (setting != null)
                prepared.Add(setting);
        }
        return prepared;
    }

    private static void WriteHostSettings(List<HostSetting> prepared)
    {
        foreach (var (path, content, notice) in prepared)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
            if (notice != null)
                Console.WriteLine(notice);
        }
    }

    private static string MemoryOffNotice(string relativePath, string found) =>
        $"  ✓ Turned native memory off: {relativePath} had {found}, now false (DR 055)";

    private static HostSetting? PrepareClaudeSettings(string projectRoot)
    {
        const string relativePath = ".claude/settings.json";
        const string key = "env.CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH";
        var path = Path.Combine(projectRoot, ".claude", "settings.json");
        var original = JsonText.Read(path);
        JsonObject root;
        if (original != null)
        {
            try
            {
                root = JsonText.Parse(original.Text) as JsonObject
                    ?? throw HostSettingError(relativePath, "JSON object root", "non-object root");
            }
            catch (JsonException ex)
            {
                throw HostSettingError(relativePath, "valid JSON object root", $"malformed JSON ({ex.Message})");
            }
        }
        else
        {
            root = new JsonObject();
        }

        JsonObject env;
        if (!root.TryGetPropertyValue("env", out var envNode))
        {
            env = new JsonObject();
            root["env"] = env;
        }
        else if (envNode is JsonObject existingEnv)
        {
            env = existingEnv;
        }
        else
        {
            throw HostSettingError(relativePath, "object at env", DescribeJson(envNode));
        }

        if (!env.TryGetPropertyValue("CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH", out var depthNode))
        {
            env["CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH"] = "3";
        }
        else if (depthNode is not JsonValue value ||
                 !value.TryGetValue<string>(out var depth) || !IsCanonicalDepth(depth, 3))
        {
            throw HostSettingError(relativePath, $"{key} JSON string canonical unsigned integer >= 3",
                DescribeJson(depthNode));
        }

        // DR 055: native memory is off; a user's true is overridden and reported, not refused.
        string? notice = null;
        var hasMemory = root.TryGetPropertyValue("autoMemoryEnabled", out var memoryNode);
        if (!hasMemory || memoryNode is not JsonValue memory || !memory.TryGetValue<bool>(out var on) || on)
        {
            if (hasMemory)
                notice = MemoryOffNotice(relativePath, $"autoMemoryEnabled = {memoryNode?.ToJsonString() ?? "null"}");
            root["autoMemoryEnabled"] = false;
        }

        // The guard is project wiring every clone needs, so it rides the committed file.
        ConfigureGuardHook(root, GuardMatcher, ClaudeGuardHookCommand);
        RemoveDydoHooks(root, "Stop", DydoStopHookCommand);

        var content = JsonText.Render(original?.Text, root);
        return content == original?.Text ? null : new HostSetting(path, JsonText.Encode(original, content), notice);
    }

    private static HostSetting? PrepareCodexSettings(string projectRoot)
    {
        const string relativePath = ".codex/config.toml";
        var path = Path.Combine(projectRoot, ".codex", "config.toml");
        var originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
        var document = ReadTomlDocument(originalBytes, relativePath);
        var agents = document.RootNode["agents"u8];
        var hasAgentsTable = ValidateAgentsTable(agents, relativePath);
        var original = Encoding.UTF8.GetString(originalBytes);
        var (agentsStart, agentsEnd) = FindTable(original, "agents", relativePath);
        if (hasAgentsTable && agentsStart < 0)
            throw HostSettingError(relativePath, "plain [agents] table", "quoted or otherwise ambiguous table header");

        var additions = MissingAgentSettings(agents, hasAgentsTable);
        var updated = additions.Count == 0
            ? original
            : InsertTableSettings(original, "agents", agentsStart, agentsEnd, additions);
        var (content, notice) = TurnCodexMemoriesOff(document, updated, relativePath);
        return content == original ? null : new HostSetting(path, Encoding.UTF8.GetBytes(content), notice);
    }

    /// <summary>
    /// DR 055: Codex memories run only when [features] memories is true, so dydo writes false and
    /// overrides a true. The edited text is parsed again, so a features form the line edit cannot
    /// reach (inline table, dotted or quoted key) is refused rather than written broken.
    /// </summary>
    private static (string Content, string? Notice) TurnCodexMemoriesOff(
        TomlDocument document, string text, string relativePath)
    {
        if (IsCodexMemoriesOff(document))
            return (text, null);

        var (start, end) = FindTable(text, "features", relativePath);
        var section = start < 0 ? "" : text[start..end];
        var line = Regex.Match(section,
            @"^[ \t]*memories[ \t]*=[ \t]*(?<value>""(?:[^""\\\r\n]|\\.)*""|'[^'\r\n]*'|[^\s#]+)[^\r\n]*",
            RegexOptions.Multiline);
        string content;
        string? notice = null;
        if (!line.Success)
        {
            content = InsertTableSettings(text, "features", start, end, ["memories = false"]);
        }
        else
        {
            // Only the value changes: indentation, spacing and a trailing comment stay the user's.
            var value = line.Groups["value"];
            content = text[..start] + section[..value.Index] + "false" + section[(value.Index + value.Length)..]
                + text[end..];
            notice = MemoryOffNotice(relativePath, line.Value.Trim());
        }

        if (!IsCodexMemoriesOff(ReadTomlDocument(Encoding.UTF8.GetBytes(content), relativePath)))
            throw HostSettingError(relativePath, "features.memories in a plain [features] table", "another form");
        return (content, notice);
    }

    private static bool IsCodexMemoriesOff(TomlDocument document)
    {
        var features = document.RootNode["features"u8];
        if (!features.HasValue)
            return false;
        var memories = features["memories"u8];
        return memories.HasValue && memories.ValueType == TomlValueType.Boolean
            && memories.TryGetBool(out var on) && !on;
    }

    private static TomlDocument ReadTomlDocument(byte[] bytes, string relativePath)
    {
        try
        {
            return CsTomlSerializer.Deserialize<TomlDocument>(bytes);
        }
        catch (CsTomlSerializeException ex)
        {
            var parse = ex.ParseExceptions?.FirstOrDefault();
            var detail = parse == null
                ? ex.Message
                : $"line {parse.LineNumber}: {parse.InnerException?.Message ?? parse.Message}";
            throw HostSettingError(relativePath, "valid TOML document", $"malformed TOML ({detail})");
        }
    }

    private static bool ValidateAgentsTable(TomlDocumentNode agents, string relativePath)
    {
        var hasAgentsTable = agents.HasValue && agents.HasNodeOnly && agents.IsTableHeader;
        if (agents.HasValue && !hasAgentsTable)
            throw HostSettingError(relativePath, "explicit [agents] table", "inline, scalar, or array agents value");
        if (hasAgentsTable)
        {
            ValidateTomlInteger(relativePath, agents["max_depth"u8], "max_depth", 3);
            ValidateTomlInteger(relativePath, agents["max_concurrent_threads_per_session"u8], "max_concurrent_threads_per_session", 16);
            ValidateTomlEnabled(relativePath, agents["enabled"u8]);
        }
        return hasAgentsTable;
    }

    private static (int Start, int End) FindTable(string original, string name, string relativePath)
    {
        var lines = Regex.Matches(original, @"[^\r\n]*(?:\r\n|\r|\n|$)");
        var header = new Regex($@"^\[{Regex.Escape(name)}\](?:\s*#.*)?$");
        var tableStart = -1;
        var tableEnd = original.Length;
        foreach (Match lineMatch in lines)
        {
            if (lineMatch.Length == 0)
                continue;
            // A BOM is not whitespace to Trim, yet TOML readers skip it: the first header follows it.
            var line = lineMatch.Value.TrimEnd('\r', '\n').TrimStart('﻿');
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            if (header.IsMatch(trimmed))
            {
                if (tableStart >= 0)
                    throw HostSettingError(relativePath, $"one unambiguous [{name}] table", $"duplicate [{name}] table");
                tableStart = lineMatch.Index;
                continue;
            }
            if (tableStart >= 0 && tableEnd == original.Length && trimmed.StartsWith('['))
                tableEnd = lineMatch.Index;
        }
        return (tableStart, tableEnd);
    }

    private static List<string> MissingAgentSettings(TomlDocumentNode agents, bool hasAgentsTable)
    {
        var additions = new List<string>();
        if (!hasAgentsTable || !agents["max_depth"u8].HasValue)
            additions.Add("max_depth = 3");
        if (!hasAgentsTable || !agents["max_concurrent_threads_per_session"u8].HasValue)
            additions.Add("max_concurrent_threads_per_session = 16");
        return additions;
    }

    private static string InsertTableSettings(string original, string table, int tableStart, int tableEnd,
        List<string> additions)
    {
        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (tableStart < 0)
        {
            var separator = "";
            if (original.Length > 0)
                separator = original.EndsWith("\n", StringComparison.Ordinal) ||
                            original.EndsWith("\r", StringComparison.Ordinal) ? newline : newline + newline;
            return original + separator + $"[{table}]" + newline + string.Join(newline, additions) + newline;
        }
        // After the table's last line, so blank lines before the next table stay below the additions.
        var contentEnd = original[..tableEnd].TrimEnd('\r', '\n').Length;
        var lineEnd = original.AsSpan(contentEnd, tableEnd - contentEnd) switch
        {
            ['\r', '\n', ..] => contentEnd + 2,
            ['\n', ..] or ['\r', ..] => contentEnd + 1,
            _ => contentEnd
        };
        var prefix = original[..lineEnd];
        var suffix = original[lineEnd..];
        var joiner = prefix.EndsWith("\n", StringComparison.Ordinal) ||
                     prefix.EndsWith("\r", StringComparison.Ordinal) ? "" : newline;
        return prefix + joiner + string.Join(newline, additions) + newline + suffix;
    }

    private static void ValidateTomlInteger(string path, TomlDocumentNode node, string key, int minimum)
    {
        if (!node.HasValue)
            return;
        if (node.ValueType != TomlValueType.Integer || !node.TryGetInt64(out var value) || value < minimum)
            throw HostSettingError(path, $"agents.{key} integer >= {minimum}", $"agents.{key} has incompatible or lower value");
    }

    private static void ValidateTomlEnabled(string path, TomlDocumentNode node)
    {
        if (!node.HasValue)
            return;
        if (node.ValueType != TomlValueType.Boolean || !node.TryGetBool(out var enabled) || !enabled)
            throw HostSettingError(path, "agents.enabled = true or absent", "agents.enabled has incompatible or false value");
    }

    private static bool IsCanonicalDepth(string value, int minimum) =>
        Regex.IsMatch(value, @"^(0|[1-9][0-9]*)$") &&
        BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum;

    private static ArgumentException HostSettingError(string path, string required, string found) =>
        new($"Invalid {path}: required {required}; found {found}.");

    private static string DescribeJson(JsonNode? value) => value switch
    {
        null => "null",
        JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => $"string \"{text}\"",
        JsonObject => "object",
        JsonArray => "array",
        _ => value.ToJsonString()
    };

    // The dydo clean and dydo merge entries are narrow on purpose: Claude Code's auto mode drops blanket
    // shell rules but keeps narrow ones, so these commands (safe by design) run without a prompt there too.
    private static readonly string[] DydoAllowEntries =
    {
        "Bash(dydo:*)",
        "PowerShell(dydo:*)",
        "Bash(dydo clean:*)",
        "PowerShell(dydo clean:*)",
        "Bash(dydo merge:*)",
        "PowerShell(dydo merge:*)"
    };

    // The Claude guard rides the committed .claude/settings.json (PrepareClaudeSettings). This
    // machine's settings.local.json keeps its allow list and own hooks; a guard an older dydo wired here goes,
    // or every tool call would run the guard twice.
    private static void ConfigureClaudeHooks(string projectRoot)
    {
        var claudeDir = Path.Combine(projectRoot, ".claude");
        Directory.CreateDirectory(claudeDir);

        var settingsPath = Path.Combine(claudeDir, "settings.local.json");
        var original = JsonText.Read(settingsPath);
        var settings = LoadJsonSettings(original?.Text);

        RemoveDydoHooks(settings, "PreToolUse", GuardHookCommand, ClaudeGuardHookCommand);
        RemoveDydoHooks(settings, "Stop", DydoStopHookCommand);
        ConfigureAllowList(settings);

        JsonText.Write(settingsPath, original, settings);
    }

    private const string GuardMatcher =
        "Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit";

    // Codex emits shell execution under several tool names depending on mode (shell_command
    // interactive/exec, plus the code-mode aliases exec, local_shell, unified_exec). All must be in
    // the matcher or the hook never binds on Codex's shell lane, silently (issue 0295). Keep them in
    // lockstep with GuardCommand.ShellTools, which routes them to the shell analyzer.
    internal const string CodexShellTools = "shell_command|exec|local_shell|unified_exec";

    // The documented Codex names come first (shell and unified exec arrive as Bash, apply_patch
    // also as Edit/Write, spawn_agent as Agent). The shell names above stay too: the documented
    // reading is unproven against the installed Codex (issue 0295). Claude-only names (Read, Glob,
    // Grep, PowerShell, plan mode) are left out because Codex never emits them.
    private const string CodexGuardMatcher = "Bash|apply_patch|Edit|Write|Agent|" + CodexShellTools;

    // The Claude Code hook alone carries the host marker: it is the guard's only proof that the
    // host can answer an ask (the mode-set rerun). The Codex hook stays unmarked, so it is denied.
    private const string GuardHookCommand = "dydo guard";
    private const string ClaudeGuardHookCommand = GuardHookCommand + " --host " + GuardCommand.ClaudeHost;

    // dydo installs no Stop hook, and `dydo guard --stop` is not a guard option, so a Stop entry
    // running it would fail every turn end.
    private const string DydoStopHookCommand = "dydo guard --stop";

    // Exactly one dydo guard entry, standing where dydo's first one stood, so a rerun changes
    // nothing and foreign entries keep their place.
    private static void ConfigureGuardHook(JsonNode settings, string matcher, string guardCommand = GuardHookCommand)
    {
        var hooks = settings["hooks"]?.AsObject() ?? new JsonObject();
        settings["hooks"] = hooks;

        var preToolUse = hooks["PreToolUse"]?.AsArray() ?? new JsonArray();
        hooks["PreToolUse"] = preToolUse;

        var first = preToolUse.ToList().FindIndex(entry => entry is JsonObject candidate &&
            candidate["hooks"] is JsonArray entryHooks &&
            entryHooks.Any(hook => IsCommand(hook, GuardHookCommand, ClaudeGuardHookCommand)));
        // Every entry before the first holds no dydo command and survives, so first stays in range.
        RemoveDydoGuardEntries(preToolUse, GuardHookCommand, ClaudeGuardHookCommand);

        var hookCommand = new JsonObject
        {
            ["type"] = "command",
            ["command"] = guardCommand
        };
        var hooksArray = new JsonArray();
        hooksArray.Add((JsonNode)hookCommand);

        var guardEntry = new JsonObject
        {
            ["matcher"] = matcher,
            ["hooks"] = hooksArray
        };
        preToolUse.Insert(first < 0 ? preToolUse.Count : first, guardEntry);
    }

    // An event array dydo empties goes with it, and so does a hooks object it empties; one that
    // was already empty is the user's and stays.
    private static void RemoveDydoHooks(JsonObject settings, string eventName, params string[] commands)
    {
        if (settings["hooks"] is not JsonObject hooks || hooks[eventName] is not JsonArray entries || entries.Count == 0)
            return;

        RemoveDydoGuardEntries(entries, commands);
        if (entries.Count > 0)
            return;
        hooks.Remove(eventName);
        if (hooks.Count == 0)
            settings.Remove("hooks");
    }

    private static void ConfigureAllowList(JsonNode settings)
    {
        var permissions = settings["permissions"]?.AsObject() ?? new JsonObject();
        settings["permissions"] = permissions;

        var allow = permissions["allow"]?.AsArray() ?? new JsonArray();
        permissions["allow"] = allow;

        foreach (var entry in DydoAllowEntries)
        {
            var hasEntry = allow.Any(existing => existing?.GetValue<string>() == entry);
            if (!hasEntry)
                allow.Add((JsonNode)entry);
        }
    }

    // A missing, malformed or non-object file starts fresh.
    private static JsonObject LoadJsonSettings(string? original)
    {
        if (original == null)
            return new JsonObject();
        try
        {
            return JsonText.Parse(original) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    // Written as an edit of the committed text: Codex trusts its hooks by hash, so a rewrite that
    // reflowed or re-escaped the project's own hooks would ask every machine to trust them again.
    internal static void ConfigureCodexHooks(string projectRoot)
    {
        var codexDir = Path.Combine(projectRoot, ".codex");
        Directory.CreateDirectory(codexDir);

        var hooksPath = Path.Combine(codexDir, "hooks.json");
        var original = JsonText.Read(hooksPath);
        var settings = LoadJsonSettings(original?.Text);

        ConfigureGuardHook(settings, CodexGuardMatcher);
        RemoveDydoHooks(settings, "Stop", DydoStopHookCommand);

        JsonText.Write(hooksPath, original, settings);

        var rulesPath = Path.Combine(codexDir, "rules", "dydo.rules");
        Directory.CreateDirectory(Path.GetDirectoryName(rulesPath)!);
        var existing = File.Exists(rulesPath) ? File.ReadAllText(rulesPath) : "";
        if (existing.ReplaceLineEndings("\n") != CodexRules)
            File.WriteAllText(rulesPath, existing.Contains("\r\n", StringComparison.Ordinal) ? CodexRules.ReplaceLineEndings("\r\n") : CodexRules);
    }

    // Codex's exec-policy allow rules for dydo clean and dydo merge, committed beside .codex/hooks.json.
    // Codex loads project rules only when the project's .codex/ layer is trusted, as it does the hooks.
    // dydo owns the file, preserves matching bytes and restores stale content in the checkout's line endings.
    // The merge rule also names dydo.exe: Codex matches the argv's first word as written,
    // and Windows sessions write either.
    internal const string CodexRules =
        "# Written by dydo init codex; dydo rewrites this file on every init and --join.\n"
        + "# dydo clean archives uncommitted work and keeps unmerged branches before it removes a\n"
        + "# worktree, so it runs without an approval prompt.\n"
        + "prefix_rule(\n"
        + "    pattern = [\"dydo\", \"clean\"],\n"
        + "    decision = \"allow\",\n"
        + "    justification = \"dydo clean archives uncommitted work and keeps unmerged branches before it removes a worktree\",\n"
        + "    match = [\"dydo clean --dry-run\", \"dydo clean --all\"],\n"
        + "    not_match = [\"dydo init codex\"],\n"
        + ")\n"
        + "# dydo merge refuses in code everything but a reviewed, green PR into a feature/* branch, so it\n"
        + "# runs without an approval prompt.\n"
        + "prefix_rule(\n"
        + "    pattern = [[\"dydo\", \"dydo.exe\"], \"merge\"],\n"
        + "    decision = \"allow\",\n"
        + "    justification = \"dydo merge merges a reviewed PR into a feature/* branch only; master, main and the default branch are refused in code\",\n"
        + "    match = [\"dydo merge 12 --match-head-commit 0123456789abcdef0123456789abcdef01234567\", \"dydo.exe merge 12 --match-head-commit 0123456789abcdef0123456789abcdef01234567\"],\n"
        + "    not_match = [\"dydo init codex\"],\n"
        + ")\n";

    internal static JsonNode BuildCodexHooks()
    {
        var settings = new JsonObject();
        ConfigureGuardHook(settings, CodexGuardMatcher);
        return settings;
    }

    // Removes only exact dydo-managed hook commands, leaving custom hooks in mixed entries intact.
    // An entry goes only when that empties it; one already empty is the user's.
    private static void RemoveDydoGuardEntries(JsonArray entries, params string[] managedCommands)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] is not JsonObject entry ||
                entry["hooks"] is not JsonArray entryHooks ||
                entryHooks.Count == 0)
                continue;

            for (int hookIndex = entryHooks.Count - 1; hookIndex >= 0; hookIndex--)
            {
                if (IsCommand(entryHooks[hookIndex], managedCommands))
                    entryHooks.RemoveAt(hookIndex);
            }

            if (entryHooks.Count == 0)
                entries.RemoveAt(i);
        }
    }

    private static bool IsCommand(JsonNode? hook, params string[] commands) =>
        hook is JsonObject entry &&
        entry["command"] is JsonValue commandNode &&
        commandNode.TryGetValue<string>(out var command) &&
        commands.Contains(command);

    // The guard, in both hosts, is committed wiring that works from a fresh clone: Claude's in
    // .claude/settings.json, Codex's in .codex/hooks.json. settings.local.json stays machine-local
    // for the allow list init writes there (ruling 2026-10-27, reversing issue 0303).
    private const string ClaudeSettingsEntry = ".claude/settings.local.json";

    // setup-skills.mjs always projects the authored /skills tree into BOTH host-local discovery
    // directories, regardless of which host is wired; neither is source, so both must stay out of
    // every project's commits in every init mode, including 'none'.
    private const string ClaudeSkillsEntry = "/.claude/skills/";
    private const string CodexSkillsEntry = "/.agents/skills/";

    private static void UpdateGitignore(string projectRoot, string dydoRoot, string[] integrations)
    {
        var gitignorePath = Path.Combine(projectRoot, ".gitignore");
        var sections = new List<(string Comment, string Entry)>
        {
            ("# DynaDocs agent workspaces (local state)", $"{dydoRoot}/agents/"),
            ("# DynaDocs runtime state", $"{dydoRoot}/_system/.local/"),
            ("# Claude Code skill projection (machine-local, wired by setup-skills.mjs)", ClaudeSkillsEntry),
            ("# Codex skill projection (machine-local, wired by setup-skills.mjs)", CodexSkillsEntry),
        };
        if (integrations.Contains("claude"))
            sections.Add(("# Claude Code personal settings (machine-local, wired by 'dydo init claude --join')", ClaudeSettingsEntry));

        var content = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";
        var modified = !File.Exists(gitignorePath);

        foreach (var (comment, entry) in sections)
        {
            if (content.Contains(entry))
                continue;

            if (content.Length > 0 && !content.EndsWith('\n'))
                content += '\n';
            if (content.Length > 0)
                content += '\n';

            content += $"{comment}\n{entry}\n";
            modified = true;
        }

        if (modified)
            File.WriteAllText(gitignorePath, content);
    }

    private static void WriteIfNotExists(string path, Func<string> contentFactory, string label)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, contentFactory());
            Console.WriteLine($"  ✓ {label}");
        }
    }

    private static void WriteEntryPoint(string projectRoot) =>
        WriteIfNotExists(
            Path.Combine(projectRoot, "AGENTS.md"),
            () => ScaffoldTree.EntryPoint(Path.GetFileName(projectRoot)),
            "AGENTS.md (entry point)");

    // Claude Code reads AGENTS.md only when no CLAUDE.md, CLAUDE.local.md or .claude/CLAUDE.md
    // is present. A CLAUDE.md that is dydo's own copy goes; anything the user wrote stays and warns,
    // unless it already imports AGENTS.md first.
    private static void ReconcileClaudeEntryFiles(string projectRoot)
    {
        var claudeMd = Path.Combine(projectRoot, "CLAUDE.md");
        if (File.Exists(claudeMd))
        {
            var content = NormalizeEntry(File.ReadAllText(claudeMd));
            var isDydoCopy =
                content == NormalizeEntry(File.ReadAllText(Path.Combine(projectRoot, "AGENTS.md"))) ||
                content == NormalizeEntry(ScaffoldTree.EntryPoint(Path.GetFileName(projectRoot))) ||
                ReleasedEntryPoints.Generated(content);
            if (isDydoCopy)
            {
                File.Delete(claudeMd);
                Console.WriteLine("  ✓ Removed CLAUDE.md, a copy of the entry point: Claude Code reads AGENTS.md");
            }
            else if (!ImportsFirst(claudeMd, "@AGENTS.md"))
            {
                ConsoleOutput.WriteWarning("  ! CLAUDE.md hides AGENTS.md from Claude Code. Move its content into AGENTS.md, or put @AGENTS.md at its top.");
            }
        }

        var claudeLocalMd = Path.Combine(projectRoot, "CLAUDE.local.md");
        if (File.Exists(claudeLocalMd) && !ImportsFirst(claudeLocalMd, "@AGENTS.md"))
            ConsoleOutput.WriteWarning("  ! CLAUDE.local.md stops Claude Code reading AGENTS.md. Put @AGENTS.md at its top, or set Project instructions to claude-md-and-agents-md.");
        var dotClaudeMd = Path.Combine(projectRoot, ".claude", "CLAUDE.md");
        if (File.Exists(dotClaudeMd) && !ImportsFirst(dotClaudeMd, "@../AGENTS.md"))
            ConsoleOutput.WriteWarning("  ! .claude/CLAUDE.md stops Claude Code reading AGENTS.md. Put @../AGENTS.md at its top.");
    }

    // File.ReadLines drops a BOM; the first non-blank line, trimmed, must be the import.
    private static bool ImportsFirst(string path, string import) =>
        File.ReadLines(path).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() == import;

    private static string NormalizeEntry(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

}
