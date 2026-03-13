namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class WorktreeCommand
{
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
        return command;
    }

    internal static int ExecuteCleanup(string worktreeId, string agentName)
    {
        var registry = new AgentRegistry();
        return ExecuteCleanup(worktreeId, agentName, registry);
    }

    internal static int ExecuteCleanup(string worktreeId, string agentName, AgentRegistry registry)
    {
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

        RemoveAgentsJunction(worktreePath);
        RemoveGitWorktree(worktreePath);
        DeleteWorktreeBranch(worktreeId);

        Console.WriteLine($"Worktree {worktreeId}: cleaned up.");
        return ExitCodes.Success;
    }

    private static void RemoveMarkers(string workspace)
    {
        foreach (var name in new[] { ".worktree", ".worktree-path", ".worktree-base", ".merge-source" })
        {
            var marker = Path.Combine(workspace, name);
            if (File.Exists(marker)) File.Delete(marker);
        }
    }

    private static int CountWorktreeReferences(AgentRegistry registry, string worktreeId)
    {
        var count = 0;
        foreach (var agent in registry.GetAllAgentStates())
        {
            var marker = Path.Combine(registry.GetAgentWorkspace(agent.Name), ".worktree");
            if (!File.Exists(marker)) continue;
            if (File.ReadAllText(marker).Trim() == worktreeId)
                count++;
        }
        return count;
    }

    private static string? ResolveWorktreePath(AgentRegistry registry, string worktreeId)
    {
        // Check all agents for a .worktree-path (may still exist in released agents' workspaces)
        foreach (var agent in registry.GetAllAgentStates())
        {
            var pathMarker = Path.Combine(registry.GetAgentWorkspace(agent.Name), ".worktree-path");
            if (File.Exists(pathMarker))
            {
                var path = File.ReadAllText(pathMarker).Trim();
                if (Path.GetFileName(path) == worktreeId)
                    return path;
            }
        }

        // Fall back to convention
        var projectRoot = PathUtils.FindProjectRoot();
        if (projectRoot == null) return null;
        var conventionPath = Path.GetFullPath(Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", worktreeId));
        return Directory.Exists(conventionPath) ? conventionPath : null;
    }

    private static void RemoveAgentsJunction(string worktreePath)
    {
        var junctionPath = Path.Combine(worktreePath, "dydo", "agents");
        if (!Path.Exists(junctionPath)) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Junctions on Windows: rmdir without /s to remove only the junction, not contents
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c rmdir \"{junctionPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                })?.WaitForExit();
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

    private static void RemoveGitWorktree(string worktreePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"worktree remove \"{worktreePath}\" --force",
                UseShellExecute = false,
                CreateNoWindow = true
            })?.WaitForExit();
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
            Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"branch -D worktree/{worktreeId}",
                UseShellExecute = false,
                CreateNoWindow = true
            })?.WaitForExit();
        }
        catch
        {
            // Harmless if branch doesn't exist
        }
    }
}
