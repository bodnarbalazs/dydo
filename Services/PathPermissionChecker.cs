namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public class PathPermissionChecker
{
    private readonly string _basePath;
    private readonly IConfigService _configService;
    private readonly Dictionary<string, RoleDefinition> _roleDefinitions;

    public PathPermissionChecker(string basePath, IConfigService configService,
        Dictionary<string, RoleDefinition> roleDefinitions)
    {
        _basePath = basePath;
        _configService = configService;
        _roleDefinitions = roleDefinitions;
    }

    public bool IsPathAllowed(AgentState agent, string path, string action, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrEmpty(agent.Role))
        {
            error = $"Agent {agent.Name} has no role set. Run 'dydo agent role <role>' first.";
            return false;
        }

        var relativePath = GetRelativePath(path);

        foreach (var pattern in agent.ReadOnlyPaths)
        {
            if (pattern == "**" || MatchesGlob(relativePath, pattern))
            {
                var isAllowed = agent.WritablePaths.Any(ap => MatchesGlob(relativePath, ap));
                if (!isAllowed)
                {
                    error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}. {GetRoleRestrictionMessage(agent.Role, relativePath)}";
                    return false;
                }
            }
        }

        if (agent.WritablePaths.Count == 0)
        {
            error = $"Agent {agent.Name} ({agent.Role}) has no write permissions.";
            return false;
        }

        var allowed = agent.WritablePaths.Any(pattern => MatchesGlob(relativePath, pattern));
        if (!allowed)
        {
            error = $"Agent {agent.Name} ({agent.Role}) cannot {action} {relativePath}. {GetRoleRestrictionMessage(agent.Role, relativePath)}";
            return false;
        }

        return true;
    }

    private string GetRoleRestrictionMessage(string role, string? relativePath = null)
    {
        var pathNudge = relativePath != null ? GetPathSpecificNudge(relativePath) : null;
        if (pathNudge != null)
            return pathNudge;

        return _roleDefinitions.TryGetValue(role, out var def) ? def.DenialHint ?? "" : "";
    }

    /// <summary>
    /// Returns a targeted nudge for known "wrong destination" paths, or null if no special case applies.
    /// </summary>
    private static string? GetPathSpecificNudge(string relativePath)
    {
        if (relativePath.StartsWith(".claude/plans/", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith(".claude\\plans\\", StringComparison.OrdinalIgnoreCase))
        {
            return "Dydo agents don't use Claude Code's built-in plans. "
                 + "Switch to planner mode ('dydo agent role planner --task <name>') "
                 + "and write your plan to your workspace (dydo/agents/<you>/plan-<task>.md).";
        }

        return null;
    }

    private string GetRelativePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            var projectRoot = _configService.GetProjectRoot(_basePath) ?? _basePath;
            // In worktrees, use the main project root so paths resolve correctly
            var mainRoot = PathUtils.GetMainProjectRoot(_basePath);
            if (mainRoot != null)
                projectRoot = mainRoot;
            var relative = Path.GetRelativePath(projectRoot, path);
            return PathUtils.NormalizePath(relative);
        }
        return PathUtils.NormalizePath(path);
    }

    public static bool MatchesGlob(string path, string pattern)
    {
        return GlobMatcher.IsMatch(path, pattern);
    }
}
