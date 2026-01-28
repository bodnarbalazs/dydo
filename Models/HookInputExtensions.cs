namespace DynaDocs.Models;

/// <summary>
/// Helper methods for working with hook input
/// </summary>
public static class HookInputExtensions
{
    /// <summary>
    /// Get the action type from the tool name
    /// </summary>
    public static string GetAction(this HookInput input)
    {
        return input.ToolName?.ToLowerInvariant() switch
        {
            "write" => "write",
            "edit" => "edit",
            "bash" => "execute",
            "read" => "read",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Get the file path being operated on
    /// </summary>
    public static string? GetFilePath(this HookInput input)
    {
        return input.ToolInput?.FilePath;
    }

    /// <summary>
    /// Check if this is a write operation (Write or Edit tool)
    /// </summary>
    public static bool IsWriteOperation(this HookInput input)
    {
        var toolName = input.ToolName?.ToLowerInvariant();
        return toolName == "write" || toolName == "edit";
    }

    /// <summary>
    /// Check if this is a read operation (Read tool)
    /// </summary>
    public static bool IsReadOperation(this HookInput input)
    {
        var toolName = input.ToolName?.ToLowerInvariant();
        return toolName == "read" || toolName == "glob" || toolName == "grep";
    }

    /// <summary>
    /// Check if this is a Bash command
    /// </summary>
    public static bool IsBashTool(this HookInput input)
    {
        return input.ToolName?.Equals("bash", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Get the command string for Bash tool
    /// </summary>
    public static string? GetCommand(this HookInput input)
    {
        return input.ToolInput?.Command;
    }

    /// <summary>
    /// Check if this tool operates on files (Edit, Write, Read, Bash)
    /// </summary>
    public static bool IsFileOperation(this HookInput input)
    {
        var toolName = input.ToolName?.ToLowerInvariant();
        return toolName is "edit" or "write" or "read" or "bash" or "glob" or "grep";
    }
}
