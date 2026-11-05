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
        var stored = JsonText.Read(Path.Combine(projectRoot, ".claude", "settings.json"));
        if (stored == null)
            return Unguarded("the file is missing");

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(stored.Text.TrimStart('\uFEFF'), documentOptions: NoDuplicateKeys);
        }
        catch (JsonException)
        {
            return UnguardedUntilFixed("the file is not valid JSON");
        }

        if (root is not JsonObject settings)
            return UnguardedUntilFixed("the file is not a JSON object");
        return RunsGuard(settings) ? null : Unguarded($"it has no PreToolUse hook running '{HookCommand}'");
    }

    private static bool RunsGuard(JsonObject settings) =>
        settings["hooks"] is JsonObject hooks &&
        hooks["PreToolUse"] is JsonArray entries &&
        entries.OfType<JsonObject>()
            .Select(entry => entry["hooks"])
            .OfType<JsonArray>()
            .SelectMany(entryHooks => entryHooks.OfType<JsonObject>())
            .Any(hook => hook["command"] is JsonValue command &&
                         command.TryGetValue<string>(out var text) && text == HookCommand);

    private static ValidationIssue Unguarded(string reason) =>
        Warning($"{reason}. Run 'dydo init claude --join'.");

    // init --join refuses a file it cannot parse rather than repair it.
    private static ValidationIssue UnguardedUntilFixed(string reason) =>
        Warning($"{reason}. Fix the JSON, then run 'dydo init claude --join'.");

    private static ValidationIssue Warning(string detail) => new()
    {
        Severity = "warning",
        File = SettingsPath,
        Message = $"Claude agents run without the dydo guard: {detail}"
    };
}
