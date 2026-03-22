namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class WorktreeCommand
{
    internal static Action<string, string>? RunProcessOverride;
    internal static Func<string, string, int>? RunProcessWithExitCodeOverride;

    public static Command Create()
    {
        var command = new Command("worktree", "Manage git worktrees for agent dispatch");

        var cleanupCommand = new Command("cleanup", "Remove agent's worktree markers and clean up worktree if last reference");
        var worktreeIdArg = new Argument<string>("worktree-id")
        {
            Description = "The worktree ID to clean up"
        };
        var agentOption = new Option<string>("--agent")
        {
            Description = "The agent name performing cleanup",
            Required = true
        };
        cleanupCommand.Arguments.Add(worktreeIdArg);
        cleanupCommand.Options.Add(agentOption);
        cleanupCommand.SetAction((result) =>
        {
            var id = result.GetValue(worktreeIdArg)!;
            var agent = result.GetValue(agentOption)!;
            return ExecuteCleanup(id, agent);
        });

        command.Subcommands.Add(cleanupCommand);

        var mergeCommand = new Command("merge", "Merge worktree branch back into base branch");
        var finalizeOption = new Option<bool>("--finalize")
        {
            Description = "Finalize a merge after resolving conflicts"
        };
        mergeCommand.Options.Add(finalizeOption);
        mergeCommand.SetAction((result) =>
        {
            var finalize = result.GetValue(finalizeOption);
            return ExecuteMerge(finalize);
        });
        command.Subcommands.Add(mergeCommand);

        var initSettingsCommand = new Command("init-settings", "Copy settings.local.json to worktree with Read permission for main repo path");
        var mainRootOption = new Option<string>("--main-root")
        {
            Description = "Absolute path to the main project root",
            Required = true
        };
        initSettingsCommand.Options.Add(mainRootOption);
        initSettingsCommand.SetAction((result) =>
        {
            var mainRoot = result.GetValue(mainRootOption)!;
            return ExecuteInitSettings(mainRoot);
        });
        command.Subcommands.Add(initSettingsCommand);

        return command;
    }

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    internal static int ExecuteInitSettings(string mainRoot)
    {
        var sourcePath = Path.Combine(mainRoot, ".claude", "settings.local.json");

        JsonNode settings;
        if (File.Exists(sourcePath))
        {
            try
            {
                settings = JsonNode.Parse(File.ReadAllText(sourcePath)) ?? new JsonObject();
            }
            catch
            {
                settings = new JsonObject();
            }
        }
        else
        {
            // No source settings — nothing to copy
            return ExitCodes.Success;
        }

        var permissions = settings["permissions"]?.AsObject() ?? new JsonObject();
        settings["permissions"] = permissions;

        var allow = permissions["allow"]?.AsArray() ?? new JsonArray();
        permissions["allow"] = allow;

        var normalizedRoot = mainRoot.TrimEnd('/', '\\');
        var readAbsoluteEntry = $"Read({normalizedRoot}/**)";
        var readWildcardEntry = "Read(**)";
        var readTildeEntry = "Read(~/**)";

        var hasAbsolute = false;
        var hasWildcard = false;
        var hasTilde = false;
        foreach (var item in allow)
        {
            var value = item?.GetValue<string>();
            if (value == readAbsoluteEntry) hasAbsolute = true;
            if (value == readWildcardEntry) hasWildcard = true;
            if (value == readTildeEntry) hasTilde = true;
        }

        if (!hasAbsolute)
            allow.Add((JsonNode)readAbsoluteEntry);
        if (!hasWildcard)
            allow.Add((JsonNode)readWildcardEntry);
        if (!hasTilde)
            allow.Add((JsonNode)readTildeEntry);

        var claudeDir = Path.Combine(Directory.GetCurrentDirectory(), ".claude");
        Directory.CreateDirectory(claudeDir);

        var targetPath = Path.Combine(claudeDir, "settings.local.json");
        File.WriteAllText(targetPath, settings.ToJsonString(WriteOptions));

        return ExitCodes.Success;
    }

    internal static int ExecuteCleanup(string worktreeId, string agentName)
    {
        var registry = new AgentRegistry();
        return ExecuteCleanup(worktreeId, agentName, registry);
    }

    internal static int ExecuteCleanup(string worktreeId, string agentName, AgentRegistry registry)
    {
        var childCount = CountChildWorktrees(registry, worktreeId);
        if (childCount > 0)
        {
            ConsoleOutput.WriteError($"Cannot clean up worktree '{worktreeId}': {childCount} child worktree(s) still active. Clean up children first.");
            return ExitCodes.ToolError;
        }

        var workspace = registry.GetAgentWorkspace(agentName);
        RemoveMarkers(workspace);

        var remainingRefs = CountWorktreeReferences(registry, worktreeId);
        if (remainingRefs > 0)
        {
            Console.WriteLine($"Worktree {worktreeId}: {remainingRefs} agent(s) still referencing — skipping removal.");
            return ExitCodes.Success;
        }

        var worktreePath = ResolveWorktreePath(registry, worktreeId);
        if (worktreePath == null)
        {
            Console.WriteLine($"Worktree {worktreeId}: no path found, skipping removal.");
            return ExitCodes.Success;
        }

        RemoveJunction(Path.Combine(worktreePath, "dydo", "agents"));
        RemoveJunction(Path.Combine(worktreePath, "dydo", "_system", "roles"));
        RemoveGitWorktree(worktreePath);
        DeleteWorktreeBranch(worktreeId);

        Console.WriteLine($"Worktree {worktreeId}: cleaned up.");
        return ExitCodes.Success;
    }

    internal static void RemoveMarkers(string workspace)
    {
        foreach (var name in new[] { ".worktree", ".worktree-path", ".worktree-base", ".merge-source", ".worktree-hold", ".worktree-root" })
        {
            var marker = Path.Combine(workspace, name);
            if (File.Exists(marker)) File.Delete(marker);
        }
    }

    internal static int CountWorktreeReferences(AgentRegistry registry, string worktreeId)
    {
        var count = 0;
        var childPrefix = $"{worktreeId}/";
        foreach (var agent in registry.GetAllAgentStates())
        {
            foreach (var markerName in new[] { ".worktree", ".worktree-hold" })
            {
                var marker = Path.Combine(registry.GetAgentWorkspace(agent.Name), markerName);
                if (!File.Exists(marker)) continue;
                var value = File.ReadAllText(marker).Trim();
                if (value == worktreeId || value.StartsWith(childPrefix))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    internal static int CountChildWorktrees(AgentRegistry registry, string worktreeId)
    {
        var count = 0;
        var childPrefix = $"{worktreeId}/";
        foreach (var agent in registry.GetAllAgentStates())
        {
            foreach (var markerName in new[] { ".worktree", ".worktree-hold" })
            {
                var marker = Path.Combine(registry.GetAgentWorkspace(agent.Name), markerName);
                if (!File.Exists(marker)) continue;
                if (File.ReadAllText(marker).Trim().StartsWith(childPrefix))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    internal static string? ResolveWorktreePath(AgentRegistry registry, string worktreeId)
    {
        // Check all agents for a .worktree-path (may still exist in released agents' workspaces)
        foreach (var agent in registry.GetAllAgentStates())
        {
            var pathMarker = Path.Combine(registry.GetAgentWorkspace(agent.Name), ".worktree-path");
            if (File.Exists(pathMarker))
            {
                var path = File.ReadAllText(pathMarker).Trim();
                // Hierarchical IDs (e.g., "parent/child") won't match Path.GetFileName;
                // check if path ends with the worktree ID
                var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
                var normalizedId = worktreeId.Replace('\\', '/');
                if (normalizedPath.EndsWith("/" + normalizedId) || normalizedPath == normalizedId)
                    return path;
            }
        }

        // Fall back to convention
        var projectRoot = PathUtils.FindProjectRoot();
        if (projectRoot == null) return null;
        var conventionPath = Path.GetFullPath(Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", worktreeId));
        return Directory.Exists(conventionPath) ? conventionPath : null;
    }

    private static void RunProcess(string fileName, string arguments)
    {
        if (RunProcessOverride != null)
        {
            RunProcessOverride(fileName, arguments);
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        var proc = Process.Start(psi);
        proc?.StandardInput.Close();
        proc?.WaitForExit();
    }

    internal static int RunProcessWithExitCode(string fileName, string arguments)
    {
        if (RunProcessWithExitCodeOverride != null)
            return RunProcessWithExitCodeOverride(fileName, arguments);

        if (RunProcessOverride != null)
        {
            RunProcessOverride(fileName, arguments);
            return 0;
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        var p = Process.Start(psi);
        p?.StandardInput.Close();
        p?.WaitForExit();
        return p?.ExitCode ?? 1;
    }

    internal static void RemoveJunction(string junctionPath)
    {
        if (!Path.Exists(junctionPath)) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Junctions on Windows: rmdir without /s to remove only the junction, not contents
                RunProcess("cmd", $"/c rmdir \"{junctionPath}\"");
            }
            else
            {
                File.Delete(junctionPath); // rm -f for symlinks
            }
        }
        catch
        {
            // Best-effort — git worktree remove --force will handle leftovers
        }
    }

    internal static void RemoveGitWorktree(string worktreePath)
    {
        try
        {
            RunProcess("git", $"worktree remove \"{worktreePath}\" --force");
        }
        catch
        {
            // Harmless if worktree already removed
        }
    }

    private static void DeleteWorktreeBranch(string worktreeId)
    {
        try
        {
            RunProcess("git", $"branch -D worktree/{TerminalLauncher.WorktreeIdToBranchSuffix(worktreeId)}");
        }
        catch
        {
            // Harmless if branch doesn't exist
        }
    }

    internal static int ExecuteMerge(bool finalize)
    {
        var registry = new AgentRegistry();
        return ExecuteMerge(finalize, registry);
    }

    internal static int ExecuteMerge(bool finalize, AgentRegistry registry)
    {
        var sessionId = registry.GetSessionContext();
        var agent = registry.GetCurrentAgent(sessionId);
        if (agent == null)
        {
            ConsoleOutput.WriteError("No agent claimed for this session.");
            return ExitCodes.ToolError;
        }

        var workspace = registry.GetAgentWorkspace(agent.Name);

        var mergeSourcePath = Path.Combine(workspace, ".merge-source");
        if (!File.Exists(mergeSourcePath))
        {
            ConsoleOutput.WriteError("No .merge-source marker found. Nothing to merge.");
            return ExitCodes.ToolError;
        }
        var mergeSource = File.ReadAllText(mergeSourcePath).Trim();

        // Block merge while child worktrees are still active
        var mergeBranchSuffix = mergeSource.StartsWith("worktree/")
            ? mergeSource["worktree/".Length..]
            : mergeSource;
        var mergeWorktreeId = TerminalLauncher.BranchSuffixToWorktreeId(mergeBranchSuffix);
        var mergeChildCount = CountChildWorktrees(registry, mergeWorktreeId);
        if (mergeChildCount > 0)
        {
            ConsoleOutput.WriteError($"Cannot merge: {mergeChildCount} child worktree(s) still active. Merge children first.");
            return ExitCodes.ToolError;
        }

        var basePath = Path.Combine(workspace, ".worktree-base");
        if (!File.Exists(basePath))
        {
            ConsoleOutput.WriteError("No .worktree-base marker found. Cannot determine target branch.");
            return ExitCodes.ToolError;
        }
        var baseBranch = File.ReadAllText(basePath).Trim();

        // Merge must run from the main repo via git -C, not from inside a worktree.
        var worktreeRootPath = Path.Combine(workspace, ".worktree-root");
        var mainRoot = File.Exists(worktreeRootPath)
            ? File.ReadAllText(worktreeRootPath).Trim()
            : PathUtils.FindProjectRoot();
        if (mainRoot == null)
        {
            ConsoleOutput.WriteError("Cannot determine main project root. No .worktree-root marker and FindProjectRoot failed.");
            return ExitCodes.ToolError;
        }

        if (finalize)
            return FinalizeMerge(registry, agent.Name, workspace, mergeSource, mainRoot);

        Console.WriteLine($"Merging worktree branch {mergeSource} into {baseBranch}...");

        var exitCode = RunProcessWithExitCode("git", $"-C \"{mainRoot}\" merge {mergeSource} --no-edit");
        if (exitCode != 0)
        {
            Console.WriteLine("Merge conflicts detected. Resolve them, commit, then run:");
            Console.WriteLine("  dydo worktree merge --finalize");
            return ExitCodes.ValidationErrors;
        }

        return FinalizeMerge(registry, agent.Name, workspace, mergeSource, mainRoot);
    }

    internal static int FinalizeMerge(AgentRegistry registry, string agentName, string workspace, string mergeSource, string mainRoot)
    {
        // Extract worktreeId from merge-source (e.g. "worktree/domain-A.+.auth" -> "domain-A/auth")
        var branchSuffix = mergeSource.StartsWith("worktree/")
            ? mergeSource["worktree/".Length..]
            : mergeSource;
        var worktreeId = TerminalLauncher.BranchSuffixToWorktreeId(branchSuffix);

        RunProcess("git", $"-C \"{mainRoot}\" branch -D {mergeSource}");

        var worktreePath = ResolveWorktreePath(registry, worktreeId);
        if (worktreePath != null)
        {
            RemoveJunction(Path.Combine(worktreePath, "dydo", "agents"));
            RemoveJunction(Path.Combine(worktreePath, "dydo", "_system", "roles"));
            RemoveGitWorktree(worktreePath);
        }

        RemoveMarkers(workspace);

        Console.WriteLine($"Merge finalized. Worktree {worktreeId} cleaned up.");
        return ExitCodes.Success;
    }
}
