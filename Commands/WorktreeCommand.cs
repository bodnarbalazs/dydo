namespace DynaDocs.Commands;

using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class WorktreeCommand
{
    internal static Action<string, string>? RunProcessOverride;
    internal static Func<string, string, int>? RunProcessWithExitCodeOverride;
    internal static Func<string, string, (int ExitCode, string Stdout)>? RunProcessCaptureOverride;

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
        var forceOption = new Option<bool>("--force")
        {
            Description = "Bypass the pre-merge safety check (source branch not advanced / dirty worktree). Destroys uncommitted work — use only when you genuinely want a no-op cleanup."
        };
        mergeCommand.Options.Add(finalizeOption);
        mergeCommand.Options.Add(forceOption);
        mergeCommand.SetAction((result) =>
        {
            var finalize = result.GetValue(finalizeOption);
            var force = result.GetValue(forceOption);
            return ExecuteMerge(finalize, force);
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

        var pruneCommand = new Command("prune", "Remove orphaned worktree directories and stale markers");
        pruneCommand.SetAction((result) => ExecutePrune());
        command.Subcommands.Add(pruneCommand);

        var statusCommand = new Command("status", "Show classified merge-safety view of the current worktree");
        var allOption = new Option<bool>("--all")
        {
            Description = "Include junk (ignored generated artifacts) in the output"
        };
        statusCommand.Options.Add(allOption);
        statusCommand.SetAction((result) =>
        {
            var all = result.GetValue(allOption);
            return ExecuteStatus(all);
        });
        command.Subcommands.Add(statusCommand);

        return command;
    }

    internal static int ExecuteStatus(bool all)
    {
        var registry = new AgentRegistry();
        return ExecuteStatus(all, registry);
    }

    internal static int ExecuteStatus(bool all, AgentRegistry registry)
    {
        var worktreePath = ResolveCurrentWorktreePath(registry);
        if (worktreePath == null)
        {
            ConsoleOutput.WriteError("Not inside a dydo worktree. Run this from an agent's worktree directory.");
            return ExitCodes.ToolError;
        }

        var (statusExit, statusStdout) = RunProcessCapture("git", $"-C \"{worktreePath}\" status --porcelain");
        if (statusExit != 0)
        {
            ConsoleOutput.WriteError($"git status failed (exit {statusExit}) in {worktreePath}.");
            return ExitCodes.ToolError;
        }

        var config = new ConfigService().LoadConfig(worktreePath) ?? new DydoConfig();
        var result = WorktreeMergeSafety.Classify(statusStdout, config);

        if (result.Suspicious.Count == 0 && result.Junk.Count == 0)
        {
            Console.WriteLine("Worktree is clean — no uncommitted files.");
            return ExitCodes.Success;
        }

        if (result.Suspicious.Count == 0)
            Console.WriteLine("No suspicious files.");
        else
        {
            Console.WriteLine($"Suspicious ({result.Suspicious.Count}):");
            foreach (var group in result.Suspicious.GroupBy(f => f.Category))
            {
                Console.WriteLine($"  [{group.Key}]");
                foreach (var file in group)
                    Console.WriteLine($"    {file.GitStatusLine}");
            }
        }

        if (all)
        {
            Console.WriteLine();
            Console.WriteLine($"Junk ({result.Junk.Count}):");
            foreach (var file in result.Junk)
                Console.WriteLine($"  {file.GitStatusLine}    [matched {file.MatchedPattern}]");
        }
        else if (result.Junk.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"{result.Junk.Count} generated artifact{(result.Junk.Count == 1 ? "" : "s")} ignored — use --all to see them.");
        }

        return ExitCodes.Success;
    }

    private static string? ResolveCurrentWorktreePath(AgentRegistry registry)
    {
        var sessionId = registry.GetSessionContext();
        var agent = registry.GetCurrentAgent(sessionId);
        if (agent != null)
        {
            var workspace = registry.GetAgentWorkspace(agent.Name);
            var pathMarker = Path.Combine(workspace, ".worktree-path");
            if (File.Exists(pathMarker))
            {
                var path = File.ReadAllText(pathMarker).Trim();
                if (Directory.Exists(path))
                    return path;
            }
        }

        var cwd = Directory.GetCurrentDirectory();
        if (PathUtils.IsInsideWorktree(cwd))
            return cwd;

        return null;
    }

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    internal static int ExecuteInitSettings(string mainRoot, string? worktreePath = null)
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

        var entries = BuildPermissionEntries(mainRoot);
        AddMissingEntries(allow, entries);

        var targetRoot = worktreePath ?? Directory.GetCurrentDirectory();
        var claudeDir = Path.Combine(targetRoot, ".claude");
        Directory.CreateDirectory(claudeDir);

        var targetPath = Path.Combine(claudeDir, "settings.local.json");
        File.WriteAllText(targetPath, settings.ToJsonString(WriteOptions));

        // Also update main repo settings — Claude Code may load from there instead of worktree
        try
        {
            var mainSettings = JsonNode.Parse(File.ReadAllText(sourcePath)) ?? new JsonObject();
            var mainPerms = mainSettings["permissions"]?.AsObject() ?? new JsonObject();
            mainSettings["permissions"] = mainPerms;
            var mainAllow = mainPerms["allow"]?.AsArray() ?? new JsonArray();
            mainPerms["allow"] = mainAllow;

            AddMissingEntries(mainAllow, entries);
            File.WriteAllText(sourcePath, mainSettings.ToJsonString(WriteOptions));
        }
        catch { /* best-effort */ }

        return ExitCodes.Success;
    }

    internal static string[] BuildPermissionEntries(string mainRoot)
    {
        var normalizedRoot = mainRoot.Replace('\\', '/').TrimEnd('/');
        var backslashRoot = mainRoot.Replace('/', '\\').TrimEnd('\\');

        var pathPatterns = new List<string>
        {
            $"{normalizedRoot}/**",
            $"{backslashRoot}/**",
            "**",
            "~/**"
        };

        // MSYS/Git Bash format: C:/Users/... -> /c/Users/...
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && normalizedRoot.Length >= 2 && char.IsLetter(normalizedRoot[0]) && normalizedRoot[1] == ':')
        {
            var msysRoot = "/" + char.ToLowerInvariant(normalizedRoot[0]) + normalizedRoot[2..];
            pathPatterns.Add($"{msysRoot}/**");
        }

        var entries = new List<string>(pathPatterns.Count * 2);
        foreach (var pattern in pathPatterns)
        {
            entries.Add($"Read({pattern})");
            entries.Add($"Write({pattern})");
        }

        return entries.ToArray();
    }

    private static void AddMissingEntries(JsonArray allow, params string[] entries)
    {
        var existing = new HashSet<string>();
        foreach (var item in allow)
        {
            var value = item?.GetValue<string>();
            if (value != null) existing.Add(value);
        }

        foreach (var entry in entries)
        {
            if (!existing.Contains(entry))
                allow.Add((JsonNode)entry);
        }
    }

    internal static int ExecuteCleanup(string worktreeId, string agentName)
    {
        var registry = new AgentRegistry();
        return ExecuteCleanup(worktreeId, agentName, registry);
    }

    internal static int ExecuteCleanup(string worktreeId, string agentName, AgentRegistry registry)
    {
        try
        {
            TerminalLauncher.ValidateWorktreeId(worktreeId);
        }
        catch (ArgumentException ex)
        {
            ConsoleOutput.WriteError(ex.Message);
            return ExitCodes.ToolError;
        }

        var childCount = CountChildWorktrees(registry, worktreeId);
        if (childCount > 0)
        {
            ConsoleOutput.WriteError($"Cannot clean up worktree '{worktreeId}': {childCount} child worktree(s) still active. Clean up children first.");
            return ExitCodes.ToolError;
        }

        var workspace = registry.GetAgentWorkspace(agentName);

        // Read mainRoot before markers are removed so git commands use -C consistently
        var worktreeRootMarker = Path.Combine(workspace, ".worktree-root");
        var mainRoot = File.Exists(worktreeRootMarker) ? File.ReadAllText(worktreeRootMarker).Trim() : null;

        RemoveAllMarkers(workspace);

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

        TeardownWorktree(worktreePath, mainRoot);
        DeleteWorktreeBranch(worktreeId, mainRoot);

        Console.WriteLine($"Worktree {worktreeId}: cleaned up.");
        return ExitCodes.Success;
    }

    internal static void PreserveAuditFiles(string worktreePath)
    {
        var wtAuditDir = Path.Combine(worktreePath, "dydo", "_system", "audit");
        if (!Directory.Exists(wtAuditDir))
            return;

        var mainRoot = PathUtils.GetMainProjectRoot(worktreePath)
                       ?? PathUtils.FindProjectRoot();
        if (mainRoot == null)
            return;

        var mainAuditDir = Path.Combine(mainRoot, "dydo", "_system", "audit");
        var copied = 0;

        try
        {
            foreach (var yearDir in Directory.GetDirectories(wtAuditDir))
            {
                var yearName = Path.GetFileName(yearDir);
                var targetYearDir = Path.Combine(mainAuditDir, yearName);

                foreach (var file in Directory.GetFiles(yearDir, "*.json"))
                {
                    if (file.EndsWith(".tmp")) continue;

                    Directory.CreateDirectory(targetYearDir);
                    var targetFile = Path.Combine(targetYearDir, Path.GetFileName(file));
                    File.Copy(file, targetFile, overwrite: true);
                    copied++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WARNING: Failed to preserve some audit files: {ex.Message}");
        }

        if (copied > 0)
            Console.WriteLine($"  Preserved {copied} audit file(s) from worktree.");
    }

    internal static void RemoveWorktreeMarkers(string workspace)
    {
        foreach (var name in new[] { ".worktree", ".worktree-path", ".worktree-base", ".worktree-hold", ".worktree-root" })
        {
            var marker = Path.Combine(workspace, name);
            if (File.Exists(marker)) File.Delete(marker);
        }
    }

    internal static void RemoveAllMarkers(string workspace)
    {
        RemoveWorktreeMarkers(workspace);
        var mergeSource = Path.Combine(workspace, ".merge-source");
        if (File.Exists(mergeSource)) File.Delete(mergeSource);
    }

    internal static int CountWorktreeReferences(AgentRegistry registry, string worktreeId, bool includeHolds = true)
    {
        var count = 0;
        var childPrefix = $"{worktreeId}/";
        var markerNames = includeHolds ? new[] { ".worktree", ".worktree-hold" } : new[] { ".worktree" };
        foreach (var agent in registry.GetAllAgentStates())
        {
            foreach (var markerName in markerNames)
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

    /// <summary>
    /// Pre-merge safety check: refuses the merge if the source branch has not advanced beyond
    /// the base, or if the source worktree has uncommitted changes classified as suspicious
    /// (anything not matched by <c>worktree.mergeSafety.ignore</c>). Both symptoms mean a
    /// merge + cleanup would silently destroy the code-writer's work.
    /// Returns null if safe; otherwise an agent-oriented error message explaining how to recover.
    /// </summary>
    internal static string? CheckMergeSafety(string mainRoot, string baseBranch, string mergeSource, string? sourceWorktreePath, DydoConfig config)
    {
        string? aheadIssue = null;
        string? statusError = null;
        ClassificationResult? classification = null;

        var (revExit, revStdout) = RunProcessCapture("git", $"-C \"{mainRoot}\" rev-list --count {baseBranch}..{mergeSource}");
        if (revExit != 0)
        {
            aheadIssue = $"Could not count commits on {mergeSource} ahead of {baseBranch} (git rev-list exit {revExit}). Cannot verify the merge is safe.";
        }
        else if (int.TryParse(revStdout.Trim(), out var aheadCount) && aheadCount == 0)
        {
            aheadIssue = $"Branch {mergeSource} has 0 commits ahead of {baseBranch} — there is nothing to merge. This usually means your changes were never committed.";
        }

        if (sourceWorktreePath != null && Directory.Exists(sourceWorktreePath))
        {
            var (statusExit, statusStdout) = RunProcessCapture("git", $"-C \"{sourceWorktreePath}\" status --porcelain");
            if (statusExit != 0)
                statusError = $"Could not check for uncommitted changes in {sourceWorktreePath} (git status exit {statusExit}). Cannot verify the merge is safe.";
            else
                classification = WorktreeMergeSafety.Classify(statusStdout, config);
        }

        var hasSuspicious = classification != null && classification.Suspicious.Count > 0;
        if (aheadIssue == null && statusError == null && !hasSuspicious)
            return null;

        return BuildSafetyError(baseBranch, mergeSource, sourceWorktreePath, aheadIssue, statusError, classification);
    }

    private static string BuildSafetyError(
        string baseBranch,
        string mergeSource,
        string? sourceWorktreePath,
        string? aheadIssue,
        string? statusError,
        ClassificationResult? classification)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Refusing to merge {mergeSource} into {baseBranch}:");

        if (aheadIssue != null)
        {
            sb.AppendLine();
            sb.AppendLine($"  {aheadIssue}");
        }

        if (statusError != null)
        {
            sb.AppendLine();
            sb.AppendLine($"  {statusError}");
        }

        var suspicious = classification?.Suspicious ?? Array.Empty<SuspiciousFile>();
        var junk = classification?.Junk ?? Array.Empty<JunkFile>();

        if (suspicious.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Source worktree has uncommitted files. Commit them before merging:");
            sb.AppendLine();
            foreach (var file in suspicious)
            {
                var annotation = file.Category == SuspiciousCategory.TaskFile ? "    ← task file — commit it" : "";
                sb.AppendLine($"      {file.GitStatusLine}{annotation}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("  Recommended:");
        if (sourceWorktreePath != null)
            sb.AppendLine($"      cd \"{sourceWorktreePath}\"");
        sb.AppendLine(suspicious.Count > 0
            ? $"      git add -- {BuildAddArgs(suspicious)}"
            : "      git add -A");
        sb.AppendLine("      git commit -m \"<message>\"");
        sb.AppendLine("      dydo worktree merge");

        if (junk.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"  ({junk.Count} generated artifact{(junk.Count == 1 ? "" : "s")} ignored by worktree.mergeSafety.ignore.");
            sb.AppendLine("   Run `dydo worktree status --all` to see them.)");
        }

        sb.AppendLine();
        sb.Append("  --force bypasses this check and destroys uncommitted files. Use only if the listed files are truly throwaway.");
        return sb.ToString();
    }

    private static string BuildAddArgs(IReadOnlyList<SuspiciousFile> suspicious)
    {
        var parts = new List<string>(suspicious.Count);
        foreach (var file in suspicious)
        {
            var path = file.Path;
            parts.Add(path.Contains(' ') ? $"\"{path}\"" : path);
        }
        return string.Join(' ', parts);
    }

    internal const int ProcessTimeoutMs = 30_000;

    private static void RunProcess(string fileName, string arguments)
    {
        if (RunProcessOverride != null)
        {
            RunProcessOverride(fileName, arguments);
            return;
        }
        RunProcessWithExitCode(fileName, arguments);
    }

    internal static int RunProcessWithExitCode(string fileName, string arguments)
    {
        if (RunProcessWithExitCodeOverride != null)
            return RunProcessWithExitCodeOverride(fileName, arguments);

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
        if (p != null && !p.WaitForExit(ProcessTimeoutMs))
        {
            try { p.Kill(); } catch { }
            return 1;
        }
        return p?.ExitCode ?? 1;
    }

    internal static (int ExitCode, string Stdout) RunProcessCapture(string fileName, string arguments)
    {
        if (RunProcessCaptureOverride != null)
            return RunProcessCaptureOverride(fileName, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        var p = Process.Start(psi);
        if (p == null) return (1, string.Empty);
        p.StandardInput.Close();
        var stdout = p.StandardOutput.ReadToEnd();
        if (!p.WaitForExit(ProcessTimeoutMs))
        {
            try { p.Kill(); } catch { }
            return (1, stdout);
        }
        return (p.ExitCode, stdout);
    }

    internal static readonly string[] JunctionSubpaths =
    [
        Path.Combine("dydo", "agents"),
        Path.Combine("dydo", "_system", "roles"),
        Path.Combine("dydo", "project", "issues"),
        Path.Combine("dydo", "project", "inquisitions"),
    ];

    /// <summary>
    /// Generate bash symlink setup commands for all junction subpaths.
    /// <paramref name="rootExpr"/> is the target root — either a literal path (single-quoted)
    /// or a shell variable reference (double-quoted).
    /// </summary>
    internal static string GenerateBashJunctionScript(string rootExpr, bool isVariable)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var sub in JunctionSubpaths)
        {
            var fwd = sub.Replace('\\', '/');
            var target = isVariable ? $"\"{rootExpr}/{fwd}\"" : $"'{rootExpr}/{fwd}'";
            sb.Append($"mkdir -p {target} && ");
            sb.Append($"(if [ -L {fwd} ]; then rm {fwd}; elif [ -e {fwd} ]; then rm -rf {fwd}; fi) && ln -s {target} {fwd} && ");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Generate PowerShell junction setup commands for all junction subpaths.
    /// <paramref name="rootExpr"/> is either a literal root path or a PS variable like <c>$_wt_root.Path</c>.
    /// </summary>
    internal static string GeneratePsJunctionScript(string rootExpr, bool isVariable)
    {
        var sb = new System.Text.StringBuilder();
        int idx = 0;
        foreach (var sub in JunctionSubpaths)
        {
            var fwd = sub.Replace('\\', '/');
            string targetExpr;
            if (isVariable)
            {
                var varName = $"$_jt{idx}";
                sb.Append($"{varName} = Join-Path {rootExpr} '{fwd}'; ");
                sb.Append($"if (-not (Test-Path {varName})) {{ New-Item -ItemType Directory -Path {varName} -Force; }} ");
                targetExpr = varName;
            }
            else
            {
                targetExpr = $"'{rootExpr}/{fwd}'";
                sb.Append($"if (-not (Test-Path {targetExpr})) {{ New-Item -ItemType Directory -Path {targetExpr} -Force; }} ");
            }
            sb.Append($"if (Test-Path '{fwd}') {{ if ((Get-Item '{fwd}' -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {{ cmd /c rmdir (Resolve-Path '{fwd}').Path }} else {{ Remove-Item -Recurse -Force (Resolve-Path '{fwd}').Path }} }} ");
            sb.Append($"New-Item -ItemType Junction -Path '{fwd}' -Target {targetExpr}; ");
            idx++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Shared teardown: preserve audit files, remove junctions, safely delete the worktree,
    /// then clean up git's bookkeeping. When mainRoot is provided, git commands run via
    /// -C mainRoot (needed when executing from a worktree context). Branch deletion is
    /// intentionally excluded — callers handle it since FinalizeMerge uses mergeSource directly.
    ///
    /// The junction-safe delete runs BEFORE git worktree remove: issue #104 showed that
    /// git's --force removal on Windows can follow reparse points into main-repo junction
    /// targets (destroying agent workspaces). By wiping the directory ourselves first,
    /// git's remove only has to tidy up metadata for a missing working tree.
    /// </summary>
    internal static void TeardownWorktree(string worktreePath, string? mainRoot = null)
    {
        PreserveAuditFiles(worktreePath);
        foreach (var sub in JunctionSubpaths)
            RemoveJunction(Path.Combine(worktreePath, sub));
        RemoveZombieDirectory(worktreePath);
        RemoveGitWorktree(worktreePath, mainRoot);
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

    /// <summary>
    /// Recursively deletes a directory, safely handling junctions/symlinks at any depth.
    /// Junctions are detected via <see cref="FileAttributes.ReparsePoint"/> and unlinked
    /// via <see cref="Directory.Delete(string, bool)"/> with recursive=false — the Win32
    /// RemoveDirectory call removes a directory junction without touching its target.
    /// This path does not shell out to cmd, so it is reliable even when cmd isn't on PATH.
    /// </summary>
    internal static void DeleteDirectoryJunctionSafe(string path)
    {
        if (!Directory.Exists(path)) return;

        foreach (var subDir in Directory.GetDirectories(path))
        {
            if ((File.GetAttributes(subDir) & FileAttributes.ReparsePoint) != 0)
            {
                try { Directory.Delete(subDir, recursive: false); }
                catch { RemoveJunction(subDir); }
            }
            else
                DeleteDirectoryJunctionSafe(subDir);
        }

        foreach (var file in Directory.GetFiles(path))
            File.Delete(file);

        Directory.Delete(path);
    }

    internal static void RemoveGitWorktree(string worktreePath, string? mainRoot = null)
    {
        try
        {
            var gitPrefix = mainRoot != null ? $"-C \"{mainRoot}\" " : "";
            RunProcess("git", $"{gitPrefix}worktree remove --force -- \"{worktreePath}\"");
        }
        catch
        {
            // Harmless if worktree already removed
        }
    }

    internal static void RemoveZombieDirectory(string worktreePath)
    {
        // git worktree remove only works for registered worktrees.
        // Zombie directories (unregistered) need direct deletion.
        // Uses junction-safe deletion to avoid following junctions into the main repo.
        if (!Directory.Exists(worktreePath)) return;
        try
        {
            DeleteDirectoryJunctionSafe(worktreePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WARNING: Could not remove directory {worktreePath}: {ex.Message}");
        }
    }

    internal static void DeleteWorktreeBranch(string worktreeId, string? mainRoot = null)
    {
        try
        {
            var gitPrefix = mainRoot != null ? $"-C \"{mainRoot}\" " : "";
            RunProcess("git", $"{gitPrefix}branch -D -- worktree/{TerminalLauncher.WorktreeIdToBranchSuffix(worktreeId)}");
        }
        catch
        {
            // Harmless if branch doesn't exist
        }
    }

    internal static int ExecuteMerge(bool finalize, bool force = false)
    {
        var registry = new AgentRegistry();
        return ExecuteMerge(finalize, force, registry);
    }

    internal static int ExecuteMerge(bool finalize, AgentRegistry registry)
        => ExecuteMerge(finalize, false, registry);

    internal static int ExecuteMerge(bool finalize, bool force, AgentRegistry registry)
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

        if (!force)
        {
            var sourceWorktreePath = ResolveWorktreePath(registry, mergeWorktreeId);
            var config = new ConfigService().LoadConfig(mainRoot) ?? new DydoConfig();
            var safetyError = CheckMergeSafety(mainRoot, baseBranch, mergeSource, sourceWorktreePath, config);
            if (safetyError != null)
            {
                ConsoleOutput.WriteError(safetyError);
                return ExitCodes.ValidationErrors;
            }
        }

        Console.WriteLine($"Merging worktree branch {mergeSource} into {baseBranch}...");

        var exitCode = RunProcessWithExitCode("git", $"-C \"{mainRoot}\" merge --no-edit -- {mergeSource}");
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

        // Clear merger's own markers first so its .worktree-hold is not counted
        // as a reference below.
        RemoveAllMarkers(workspace);

        var worktreePath = ResolveWorktreePath(registry, worktreeId);
        if (worktreePath != null)
        {
            var remainingRefs = CountWorktreeReferences(registry, worktreeId);
            if (remainingRefs == 0)
            {
                TeardownWorktree(worktreePath, mainRoot);
            }
            else
            {
                Console.WriteLine($"Worktree {worktreeId}: {remainingRefs} agent(s) still referencing — directory kept; the last cleanup will remove it.");
            }
        }

        // Prune stale worktree references (no-op if the directory is still present).
        try { RunProcess("git", $"-C \"{mainRoot}\" worktree prune"); }
        catch { /* best-effort */ }

        try { RunProcess("git", $"-C \"{mainRoot}\" branch -D -- {mergeSource}"); }
        catch { Console.Error.WriteLine($"WARNING: Failed to delete branch {mergeSource}"); }

        Console.WriteLine($"Merge finalized. Worktree {worktreeId} branch deleted.");
        return ExitCodes.Success;
    }

    internal static int ExecutePrune()
    {
        var registry = new AgentRegistry();
        return ExecutePrune(registry);
    }

    internal static int ExecutePrune(AgentRegistry registry)
    {
        var worktreesDir = Path.GetFullPath(Path.Combine(registry.WorkspacePath, "..", "_system", ".local", "worktrees"));
        // Derive mainRoot from the known directory structure for consistent -C usage
        var mainRoot = Path.GetFullPath(Path.Combine(registry.WorkspacePath, "..", ".."));

        var orphansRemoved = 0;

        if (Directory.Exists(worktreesDir))
        {
            foreach (var dir in Directory.GetDirectories(worktreesDir))
            {
                var worktreeId = Path.GetFileName(dir);
                var refs = CountWorktreeReferences(registry, worktreeId);
                if (refs > 0)
                {
                    Console.WriteLine($"Worktree {worktreeId}: {refs} reference(s), skipping.");
                    continue;
                }

                Console.WriteLine($"Pruning orphaned worktree: {worktreeId}");
                ReportStrandedWatchdogPid(dir);
                TeardownWorktree(dir, mainRoot);
                DeleteWorktreeBranch(worktreeId, mainRoot);
                orphansRemoved++;
            }
        }

        var staleMarkersRemoved = CleanStaleMarkers(registry);

        Console.WriteLine($"Pruned {orphansRemoved} orphaned worktree(s), cleaned {staleMarkersRemoved} stale marker(s).");
        return ExitCodes.Success;
    }

    private static int CleanStaleMarkers(AgentRegistry registry)
    {
        var cleaned = 0;
        foreach (var agent in registry.GetAllAgentStates())
        {
            var workspace = registry.GetAgentWorkspace(agent.Name);

            var holdPath = Path.Combine(workspace, ".worktree-hold");
            if (File.Exists(holdPath))
            {
                var holdId = File.ReadAllText(holdPath).Trim();
                if (CountWorktreeReferences(registry, holdId, includeHolds: false) == 0)
                {
                    File.Delete(holdPath);
                    Console.WriteLine($"  Removed stale .worktree-hold from {agent.Name} (referenced {holdId})");
                    cleaned++;
                }
            }

            var mergePath = Path.Combine(workspace, ".merge-source");
            if (File.Exists(mergePath))
            {
                var mergeSource = File.ReadAllText(mergePath).Trim();
                var branchSuffix = mergeSource.StartsWith("worktree/")
                    ? mergeSource["worktree/".Length..]
                    : mergeSource;
                var wtId = TerminalLauncher.BranchSuffixToWorktreeId(branchSuffix);
                if (CountWorktreeReferences(registry, wtId, includeHolds: false) == 0)
                {
                    File.Delete(mergePath);
                    Console.WriteLine($"  Removed stale .merge-source from {agent.Name} (referenced {mergeSource})");
                    cleaned++;
                }
            }
        }
        return cleaned;
    }

    /// <summary>
    /// Emits a warning when an orphan worktree still contains a watchdog.pid file.
    /// Newer worktrees don't create this file inside the worktree — a stranded one
    /// means a legacy or abnormally-exited watchdog. A live PID is surfaced to stderr
    /// so the user can intervene; a dead PID is reported on stdout and swept by the
    /// subsequent teardown.
    /// </summary>
    private static void ReportStrandedWatchdogPid(string worktreePath)
    {
        var pidFile = Path.Combine(worktreePath, "dydo", "_system", ".local", "watchdog.pid");
        if (!File.Exists(pidFile)) return;

        var pidStr = File.ReadAllText(pidFile).Trim();
        if (int.TryParse(pidStr, out var pid) && ProcessUtils.IsProcessRunning(pid))
            Console.Error.WriteLine($"WARNING: Stranded watchdog.pid at {pidFile} (pid {pid} still ALIVE — investigate before relying on prune)");
        else
            Console.WriteLine($"  Stranded watchdog.pid at {pidFile} (pid {pidStr}, dead — sweeping)");
    }
}
