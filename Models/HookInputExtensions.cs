namespace DynaDocs.Models;

/// <summary>
/// How a call touches a write target: a Claude write tool's own path, or one header of a Codex
/// apply_patch.
/// </summary>
public enum WriteTargetKind { ToolPath, Add, Update, Delete, MoveTo }

public readonly record struct WriteTarget(string Path, WriteTargetKind Kind)
{
    /// <summary>Whether the write can bring a new file into being; Update and Delete never do.</summary>
    public bool MayCreate => Kind is not (WriteTargetKind.Update or WriteTargetKind.Delete);

    public bool FromPatch => Kind != WriteTargetKind.ToolPath;
}

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
    // MultiEdit is not in dydo's Claude hook matcher today; named so a MultiEdit call that
    // does arrive is read by its file_path rather than ignored.
    private static readonly HashSet<string> PathWriteTools = new(StringComparer.OrdinalIgnoreCase) { "write", "edit", "multiedit", "notebookedit" };
    private static readonly (string Header, WriteTargetKind Kind)[] PatchTargetHeaders =
    [
        ("*** Add File: ", WriteTargetKind.Add),
        ("*** Update File: ", WriteTargetKind.Update),
        ("*** Delete File: ", WriteTargetKind.Delete),
        ("*** Move to: ", WriteTargetKind.MoveTo),
    ];
    private static readonly HashSet<string> ReadTools = new(StringComparer.OrdinalIgnoreCase) { "read", "glob", "grep" };
    private static readonly HashSet<string> FileTools = new(StringComparer.OrdinalIgnoreCase) { "edit", "write", "notebookedit", "read", "bash", "glob", "grep" };

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

    /// <summary>
    /// Paths this call will write or delete, in call order: the file_path / notebook_path of a
    /// Claude write tool, or every Add File / Update File / Delete File / Move to path of a Codex
    /// apply_patch, whose payload carries the whole patch in tool_input.command and no file_path.
    /// Patch paths come back as written; a relative one is relative to the hook's cwd.
    /// </summary>
    public static IReadOnlyList<WriteTarget> GetWriteTargets(this HookInput input)
    {
        if (string.Equals(input.ToolName, "apply_patch", StringComparison.OrdinalIgnoreCase))
            return ParsePatchTargets(input.ToolInput?.Command);

        var path = input.GetFilePath();
        return path != null && input.ToolName != null && PathWriteTools.Contains(input.ToolName)
            ? [new WriteTarget(path, WriteTargetKind.ToolPath)]
            : [];
    }

    // The file section a patch line sits in decides whether it can be a header at all.
    private enum PatchSection { None, AddFile, UpdateFile }

    private static List<WriteTarget> ParsePatchTargets(string? patch)
    {
        var targets = new List<WriteTarget>();
        var section = PatchSection.None;
        foreach (var line in (patch ?? "").Split('\n'))
        {
            if (IsHunkBody(line, section))
                continue;

            var trimmed = line.Trim();
            foreach (var (header, kind) in PatchTargetHeaders)
            {
                if (!trimmed.StartsWith(header, StringComparison.Ordinal))
                    continue;
                targets.Add(new WriteTarget(trimmed[header.Length..].Trim(), kind));
                section = SectionAfter(kind);
            }
        }
        return targets;
    }

    // Codex's grammar: an Add File body line starts with '+'; an Update File hunk line starts with
    // ' ', '+' or '-' (or is an @@ / End of File marker, which no header matches). Body is never a
    // header, so a file quoting patch syntax names no target.
    private static bool IsHunkBody(string line, PatchSection section) => section switch
    {
        PatchSection.AddFile => line.StartsWith('+'),
        PatchSection.UpdateFile => line.Length > 0 && line[0] is ' ' or '+' or '-',
        _ => false,
    };

    private static PatchSection SectionAfter(WriteTargetKind kind) => kind switch
    {
        WriteTargetKind.Add => PatchSection.AddFile,
        WriteTargetKind.Update or WriteTargetKind.MoveTo => PatchSection.UpdateFile,
        _ => PatchSection.None,
    };

    public static string? GetCommand(this HookInput input)
    {
        return input.ToolInput?.Command;
    }

    public static bool IsFileOperation(this HookInput input)
    {
        return input.ToolName != null && FileTools.Contains(input.ToolName);
    }
}
