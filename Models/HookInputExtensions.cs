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
}
