namespace DynaDocs.Services;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Serialization;

public partial class ConfigService : IConfigService
{
    public const string ConfigFileName = "dydo.json";
    public const string DefaultRoot = "dydo";

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
            ValidateSwitchboard(document.RootElement);
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
        => SaveConfig(
            config,
            path,
            TemporarySiblingPath,
            CreateNewSibling,
            (stream, bytes) => stream.Write(bytes),
            stream => stream.Flush(flushToDisk: true),
            stream => stream.Dispose(),
            (temporary, target) => File.Move(temporary, target, overwrite: true));

    // Beside the target on purpose: File.Move is an atomic rename only within one filesystem.
    internal static string TemporarySiblingPath(string target) => $"{target}.{Guid.NewGuid():N}.tmp";

    // CreateNew refuses an existing name, so a colliding sibling is never opened or overwritten.
    internal static FileStream CreateNewSibling(string path) =>
        new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

    internal void SaveConfig(
        DydoConfig config,
        string path,
        Func<string, string> chooseTemporaryPath,
        Func<string, FileStream> createNew,
        Action<FileStream, byte[]> writeAll,
        Action<FileStream> durableFlush,
        Action<FileStream> close,
        Action<string, string> replace)
    {
        config.Skills = config.Skills
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var skill in config.Skills.Values)
        {
            if (skill.Resources != null)
                skill.Resources = skill.Resources.OrderBy(name => name, StringComparer.Ordinal).ToList();
        }
        var json = JsonSerializer.Serialize(config, DydoConfigJsonContext.Default.DydoConfig);
        var bytes = Encoding.UTF8.GetBytes(json);
        var temporaryPath = chooseTemporaryPath(path);
        FileStream? stream = null;
        // Only a sibling this invocation created is ours to delete: a collision owns nothing,
        // and a successful rename has already consumed it.
        var ownsTemporary = false;
        try
        {
            stream = createNew(temporaryPath);
            ownsTemporary = true;
            writeAll(stream, bytes);
            durableFlush(stream);
            close(stream);
            stream = null;
            replace(temporaryPath, path);
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

    internal static bool IsValidSlug(string value) =>
        value.Length is >= 1 and <= 64
        && !value.Contains("-resource-", StringComparison.Ordinal)
        && SlugRegex().IsMatch(value);

    private static void ValidateSwitchboard(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("dydo.json root must be an object.");
        if (!root.TryGetProperty("skills", out var skills))
            return;
        if (skills.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("dydo.json skills must be an object.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in skills.EnumerateObject())
        {
            if (!IsValidSlug(entry.Name))
                throw new InvalidDataException($"dydo.json skill switch '{entry.Name}' is not a 1-64 character lowercase kebab-case name or contains the protected -resource- delimiter.");
            if (!names.Add(entry.Name))
                throw new InvalidDataException($"dydo.json skill switch '{entry.Name}' collides ordinal-ignore-case with another key.");
            ValidateSwitch(entry.Name, entry.Value);
        }
    }

    private static void ValidateSwitch(string name, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"dydo.json skill switch '{name}' must be an object.");

        var allowed = new HashSet<string>(["enabled", "origin", "emitAgent", "codexMetadata", "resources"], StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                throw new InvalidDataException($"dydo.json skill switch '{name}' has unknown property '{property.Name}'.");
        }

        if (!value.TryGetProperty("enabled", out var enabled) || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"dydo.json skill switch '{name}' requires boolean enabled.");
        if (value.TryGetProperty("origin", out var origin)
            && (origin.ValueKind != JsonValueKind.String || origin.GetString() is not ("shipped" or "custom")))
            throw new InvalidDataException($"dydo.json skill switch '{name}' origin must be 'shipped' or 'custom'.");
        ValidateOptionalBoolean(value, name, "emitAgent");
        ValidateOptionalBoolean(value, name, "codexMetadata");

        if (!value.TryGetProperty("resources", out var resources))
            return;
        if (resources.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"dydo.json skill switch '{name}' resources must be an array.");
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in resources.EnumerateArray())
        {
            if (resource.ValueKind != JsonValueKind.String
                || !IsValidSlug(resource.GetString()!)
                || !slugs.Add(resource.GetString()!))
                throw new InvalidDataException($"dydo.json skill switch '{name}' resources must contain unique lowercase kebab-case strings.");
        }
    }

    private static void ValidateOptionalBoolean(JsonElement value, string name, string property)
    {
        if (value.TryGetProperty(property, out var field)
            && field.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"dydo.json skill switch '{name}' {property} must be boolean.");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

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
