namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// The Claude Code guard hook <c>dydo init claude</c> commits to <c>.claude/settings.json</c>, and the
/// check that a project still runs it (DYD-501). Init and validation share these strings, so the check
/// accepts exactly what init writes.
/// </summary>
public static class ClaudeGuardWiring
{
    /// <summary>The <c>--host</c> value in the Claude Code hook command dydo init writes.</summary>
    public const string Host = "claude";

    public const string HookCommand = "dydo guard --host " + Host;

    private const string SettingsPath = ".claude/settings.json";

    // A repeated key parses lazily and throws on first read, so it is refused here as invalid JSON.
    private static readonly JsonDocumentOptions NoDuplicateKeys = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// A warning when the project's committed Claude settings run no PreToolUse hook with exactly
    /// <see cref="HookCommand"/>, whatever its matcher; null when they do.
    /// </summary>
    public static ValidationIssue? Check(string projectRoot)
    {
        StoredText? stored;
        try
        {
            stored = JsonText.Read(Path.Combine(projectRoot, ".claude", "settings.json"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Warning("the file cannot be read. Make it readable, then run 'dydo init claude --join'.");
        }

        if (stored == null)
            return Unguarded("the file is missing");

        try
        {
            return Inspect(JsonNode.Parse(stored.Text.TrimStart('\uFEFF'), documentOptions: NoDuplicateKeys));
        }
        // A lone surrogate escape parses, then throws InvalidOperationException once its string is read.
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return UnguardedUntilFixed("the file is not valid JSON");
        }
    }

    // init --join reads a present hooks as an object and its PreToolUse as an array, and fails on any other shape.
    private static ValidationIssue? Inspect(JsonNode? root)
    {
        if (root is not JsonObject settings)
            return UnguardedUntilFixed("the file is not a JSON object");
        var hooks = settings["hooks"];
        if (hooks is not (null or JsonObject))
            return UnguardedUntilFixed("its 'hooks' is not a JSON object");
        var entries = hooks?["PreToolUse"];
        if (entries is not (null or JsonArray))
            return UnguardedUntilFixed("its 'hooks.PreToolUse' is not a JSON array");
        return entries is JsonArray array && RunsGuard(array)
            ? null
            : Unguarded($"it has no PreToolUse hook running '{HookCommand}'");
    }

    private static bool RunsGuard(JsonArray entries) =>
        entries.OfType<JsonObject>()
            .Select(entry => entry["hooks"])
            .OfType<JsonArray>()
            .SelectMany(entryHooks => entryHooks.OfType<JsonObject>())
            .Any(hook => hook["command"] is JsonValue command &&
                         command.TryGetValue<string>(out var text) && text == HookCommand);

    private static ValidationIssue Unguarded(string reason) =>
        Warning($"{reason}. Run 'dydo init claude --join'.");

    // init --join refuses a file it cannot parse, or whose hooks it cannot read, rather than repair it.
    private static ValidationIssue UnguardedUntilFixed(string reason) =>
        Warning($"{reason}. Fix the JSON, then run 'dydo init claude --join'.");

    private static ValidationIssue Warning(string detail) => new()
    {
        Severity = "warning",
        File = SettingsPath,
        Message = $"Claude agents run without the dydo guard: {detail}"
    };
}
