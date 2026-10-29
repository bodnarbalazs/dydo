namespace DynaDocs.Services.Merge;

using DynaDocs.Utils;

/// <summary>Ends a <c>dydo merge</c> run before anything merges: a refusal (exit 1) or a tool error (exit 2), with the stderr lines it prints.</summary>
internal sealed class MergeStop(int exitCode, IReadOnlyList<string> lines) : Exception(lines[0])
{
    public int ExitCode => exitCode;

    public IReadOnlyList<string> Lines => lines;

    public static MergeStop Refused(string reason, IEnumerable<string>? details = null) =>
        new(ExitCodes.ValidationErrors, [$"refused: {reason}", .. details ?? []]);

    public static MergeStop ToolError(string reason) => new(ExitCodes.ToolError, [$"error: {reason}"]);
}
