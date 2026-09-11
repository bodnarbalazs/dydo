namespace DynaDocs.Models;

using System.Text.RegularExpressions;

/// <summary>
/// Helper methods for working with hook input
/// </summary>
public static class HookInputExtensions
{
    private static readonly Dictionary<string, string> ActionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["write"] = "write",
        ["edit"] = "edit",
        ["notebookedit"] = "edit",
        ["bash"] = "execute",
        ["powershell"] = "execute",
        ["read"] = "read",
        ["glob"] = "read",
        ["grep"] = "read",
    };

    private static readonly HashSet<string> WriteTools = new(StringComparer.OrdinalIgnoreCase) { "write", "edit", "notebookedit" };
    private static readonly HashSet<string> ReadTools = new(StringComparer.OrdinalIgnoreCase) { "read", "glob", "grep" };
    private static readonly HashSet<string> FileTools = new(StringComparer.OrdinalIgnoreCase) { "edit", "write", "notebookedit", "read", "bash", "glob", "grep" };

    // apply_patch marker lines (OpenCode/OpenAI patch format). Every Add/Update/Delete File
    // names a mutated target; Move to names the destination of a rename. A patch may carry
    // several of each, so every match yields a path for the guard to check.
    private static readonly Regex PatchPathRegex = new(
        @"^\*\*\*[ \t]+(?:(?:Add|Update|Delete)[ \t]+File|Move[ \t]+to):[ \t]*(?<path>.+?)[ \t\r]*$",
        RegexOptions.Multiline);

    public static string GetAction(this HookInput input)
    {
        if (input.ToolName != null && ActionMap.TryGetValue(input.ToolName, out var action))
            return action;
        return "unknown";
    }

    public static string? GetFilePath(this HookInput input)
    {
        return input.ToolInput?.FilePath ?? input.ToolInput?.NotebookPath;
    }

    /// <summary>
    /// Every path a hook call names: the direct file_path / notebook_path, plus each target
    /// parsed from an apply_patch patch_text. A call with no path yields an empty list.
    /// </summary>
    public static IReadOnlyList<string> GetFilePaths(this HookInput input)
    {
        var toolInput = input.ToolInput;
        if (toolInput == null)
            return Array.Empty<string>();

        var paths = new List<string>();
        AddPath(paths, toolInput.FilePath);
        AddPath(paths, toolInput.NotebookPath);

        if (!string.IsNullOrWhiteSpace(toolInput.PatchText))
        {
            foreach (Match match in PatchPathRegex.Matches(toolInput.PatchText))
                AddPath(paths, match.Groups["path"].Value);
        }

        return paths;
    }

    private static void AddPath(List<string> paths, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            paths.Add(path);
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
