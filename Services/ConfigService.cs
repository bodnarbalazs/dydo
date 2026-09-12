namespace DynaDocs.Services;

using System.Text;
using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

public class ConfigService : IConfigService
{
    public const string ConfigFileName = ConfigFileLocator.FileName;
    public const string DefaultRoot = ConfigFileLocator.DefaultRoot;

    // Cache keyed by startPath to avoid repeated directory walks within the same instance
    private readonly Dictionary<string, string?> _configFileCache = new();

    /// <summary>
    /// Find dydo.json by walking up the directory tree
    /// </summary>
    public string? FindConfigFile(string? startPath = null)
    {
        var cacheKey = startPath ?? Environment.CurrentDirectory;
        if (_configFileCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var result = ConfigFileLocator.WalkUpForFile(cacheKey, ConfigFileName);
        _configFileCache[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// Load configuration from dydo.json
    /// </summary>
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
    /// Save configuration to dydo.json. The bytes go to a flushed sibling that is then renamed
    /// over the target, so a failure at any step leaves the original file untouched.
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
        var json = JsonSerializer.Serialize(config, DydoConfigJsonContext.Default.DydoConfig);
        var bytes = Encoding.UTF8.GetBytes(json);
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

    /// <summary>
    /// Get the project root directory (where dydo.json lives)
    /// </summary>
    public string? GetProjectRoot(string? startPath = null)
    {
        var configPath = FindConfigFile(startPath);
        if (configPath == null)
            return null;

        return Path.GetDirectoryName(configPath);
    }

    /// <summary>
    /// Get the dydo root folder path (e.g., /project/dydo/)
    /// </summary>
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

    /// <summary>
    /// Get the audit folder path (dydo/_system/audit/)
    /// </summary>
    public string GetAuditPath(string? startPath = null)
    {
        return Path.Combine(GetDydoRoot(startPath), "_system", "audit");
    }

    /// <summary>
    /// Get the changelog folder path (dydo/project/changelog/)
    /// </summary>
    public string GetChangelogPath(string? startPath = null)
    {
        return Path.Combine(GetDydoRoot(startPath), "project", "changelog");
    }
}
