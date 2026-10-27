namespace DynaDocs.Services;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

public class ConfigService : IConfigService
{
    public const string ConfigFileName = ConfigFileLocator.FileName;
    public const string DefaultRoot = ConfigFileLocator.DefaultRoot;

    private readonly Dictionary<string, string?> _configFileCache = new();

    public string? FindConfigFile(string? startPath = null)
    {
        var cacheKey = startPath ?? Environment.CurrentDirectory;
        if (_configFileCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var result = ConfigFileLocator.WalkUpForFile(cacheKey, ConfigFileName);
        _configFileCache[cacheKey] = result;
        return result;
    }

    public DydoConfig? LoadConfig(string? startPath = null)
    {
        var configPath = FindConfigFile(startPath);
        if (configPath == null)
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig);
        }
        catch
        {
            return null;
        }
    }

    public DydoConfig? LoadConfigStrict(string? startPath = null)
    {
        var configPath = FindConfigFile(startPath);
        if (configPath == null)
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            using var document = JsonDocument.Parse(json);
            ValidateTesting(document.RootElement);
            return JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig)
                ?? throw new InvalidDataException("dydo.json could not be deserialized.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"dydo.json has Invalid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Save configuration to dydo.json as an edit of the existing text: only members the config
    /// changed are rewritten, and nothing is written when none did. The bytes go to a flushed
    /// sibling that is then renamed over the target, so a failure at any step leaves the original
    /// file untouched.
    /// </summary>
    public void SaveConfig(DydoConfig config, string path)
        => SaveConfig(config, path, ConfigSaveOperations.Default);

    // Beside the target on purpose: File.Move is an atomic rename only within one filesystem.
    internal static string TemporarySiblingPath(string target) => $"{target}.{Guid.NewGuid():N}.tmp";

    // CreateNew refuses an existing name, so a colliding sibling is never opened or overwritten.
    internal static FileStream CreateNewSibling(string path) =>
        new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

    internal void SaveConfig(DydoConfig config, string path, ConfigSaveOperations operations)
    {
        var original = JsonText.Read(path);
        var json = JsonText.Render(original?.Text, Patch(original?.Text, config));
        if (json == original?.Text)
            return;
        var bytes = JsonText.Encode(original, json);
        var temporaryPath = operations.ChooseTemporaryPath(path);
        FileStream? stream = null;
        // Only a sibling this invocation created is ours to delete: a collision owns nothing,
        // and a successful rename has already consumed it.
        var ownsTemporary = false;
        try
        {
            stream = operations.CreateNew(temporaryPath);
            ownsTemporary = true;
            operations.WriteAll(stream, bytes);
            operations.DurableFlush(stream);
            operations.Close(stream);
            stream = null;
            operations.Replace(temporaryPath, path);
            ownsTemporary = false;
        }
        finally
        {
            if (stream != null)
            {
                try { stream.Dispose(); }
                catch { }
            }

            if (ownsTemporary)
            {
                try { File.Delete(temporaryPath); }
                catch { }
            }
        }
    }

    // dydo.json is the user's file: only what changed between the model of the original and the
    // config being saved touches it, so unknown keys and members the model would default stay out.
    private static JsonNode Patch(string? original, DydoConfig config)
    {
        var updated = JsonSerializer.SerializeToNode(config, DydoConfigJsonContext.Default.DydoConfig)!;
        if (original == null)
            return updated;
        try
        {
            var current = JsonText.Parse(original);
            var model = JsonSerializer.Deserialize(original.TrimStart('﻿'), DydoConfigJsonContext.Default.DydoConfig);
            if (current is not JsonObject || model == null)
                return updated;
            var baseline = JsonSerializer.SerializeToNode(model, DydoConfigJsonContext.Default.DydoConfig);
            return JsonText.Merge(current, baseline, updated)!;
        }
        catch (JsonException)
        {
            return updated;
        }
    }

    private static void ValidateTesting(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("dydo.json root must be an object.");
        if (!root.TryGetProperty("testing", out var testing))
            return;
        if (testing.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("dydo.json testing.runner requires testing to be an object.");
        if (!testing.TryGetProperty("runner", out var runner))
            throw new InvalidDataException("dydo.json testing.runner is required.");
        if (runner.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("dydo.json testing.runner must be a nonempty array of strings.");

        var values = runner.EnumerateArray().ToList();
        if (values.Count == 0
            || values.Any(value => value.ValueKind != JsonValueKind.String
                || value.GetString()!.Contains('\0')))
            throw new InvalidDataException("dydo.json testing.runner must be a nonempty array of strings.");
        if (string.IsNullOrWhiteSpace(values[0].GetString()))
            throw new InvalidDataException("dydo.json testing.runner executable must not be blank.");
    }

    public string? GetProjectRoot(string? startPath = null)
    {
        var configPath = FindConfigFile(startPath);
        if (configPath == null)
            return null;

        return Path.GetDirectoryName(configPath);
    }

    public string GetDydoRoot(string? startPath = null)
    {
        var baseDir = GetProjectRoot(startPath) ?? startPath ?? Environment.CurrentDirectory;
        var rootFolder = LoadConfig(startPath)?.Structure.Root ?? DefaultRoot;
        return Path.Combine(baseDir, rootFolder);
    }

    /// <summary>
    /// Get the docs folder path (dydo root itself contains docs)
    /// </summary>
    public string GetDocsPath(string? startPath = null)
    {
        return GetDydoRoot(startPath);
    }

    public string GetChangelogPath(string? startPath = null)
    {
        return Path.Combine(GetDydoRoot(startPath), "project", "changelog");
    }
}
