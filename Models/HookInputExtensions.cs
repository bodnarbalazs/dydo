namespace DynaDocs.Models;

/// <summary>
/// Helper methods for working with hook input
/// </summary>
public static class HookInputExtensions
{
    private static readonly Dictionary<string, string> ActionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["write"] = "write",
        ["edit"] = "edit",
        ["bash"] = "execute",
        ["read"] = "read",
        ["glob"] = "read",
        ["grep"] = "read",
    };

    private static readonly HashSet<string> WriteTools = new(StringComparer.OrdinalIgnoreCase) { "write", "edit" };
    private static readonly HashSet<string> ReadTools = new(StringComparer.OrdinalIgnoreCase) { "read", "glob", "grep" };
    private static readonly HashSet<string> FileTools = new(StringComparer.OrdinalIgnoreCase) { "edit", "write", "read", "bash", "glob", "grep" };

    public static string GetAction(this HookInput input)
    {
        if (input.ToolName != null && ActionMap.TryGetValue(input.ToolName, out var action))
            return action;
        return "unknown";
    }

    public static string? GetFilePath(this HookInput input)
    {
        return input.ToolInput?.FilePath;
    }

    public static string? GetSearchPath(this HookInput input)
    {
        return input.ToolInput?.Path;
    }

    public static bool IsWriteOperation(this HookInput input)
    {
        return input.ToolName != null && WriteTools.Contains(input.ToolName);
    }

    public static bool IsReadOperation(this HookInput input)
    {
        return input.ToolName != null && ReadTools.Contains(input.ToolName);
    }

    public static bool IsBashTool(this HookInput input)
    {
        return input.ToolName?.Equals("bash", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public static string? GetCommand(this HookInput input)
    {
        return input.ToolInput?.Command;
    }

    public static bool IsFileOperation(this HookInput input)
    {
        return input.ToolName != null && FileTools.Contains(input.ToolName);
    }
}
