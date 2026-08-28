namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Utils;

/// <summary>
/// Reads the dydo 3.0 disposition manifest used to prevent retired v2 PM records
/// from being reintroduced.
/// </summary>
public sealed class LegacyPmManifestService
{
    public const string ManifestRelativePath = "project/migrations/3.0-pm-records.json";

    private readonly string _dydoRoot;
    private readonly HashSet<string> _manifestRecordPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _retainedPaths = new(StringComparer.Ordinal);
    private HashSet<string>? _allowedPaths;
    private bool _loaded;

    public LegacyPmManifestService(string dydoRoot)
    {
        _dydoRoot = dydoRoot;
    }

    public bool IsActive => File.Exists(Path.Combine(_dydoRoot, ManifestRelativePath));

    public IReadOnlySet<string> GetManifestRecordPaths()
    {
        EnsureLoaded();
        return _manifestRecordPaths;
    }

    public IReadOnlySet<string> GetAllowedPaths()
    {
        if (_allowedPaths != null)
            return _allowedPaths;

        EnsureLoaded();
        _allowedPaths = new HashSet<string>(_retainedPaths, StringComparer.Ordinal);
        return _allowedPaths;
    }

    public static string NormalizeRepoPath(string path)
    {
        var normalized = PathUtils.NormalizeForKey(PathUtils.CollapseRelativeSegments(path));
        if (Path.IsPathRooted(path) || normalized.Split('/', 2)[0] == "..")
            throw new InvalidDataException($"Legacy PM manifest path escapes the repository: {path}");
        return normalized;
    }

    public static string ToRepoPath(string dydoRelativePath)
    {
        return NormalizeRepoPath($"dydo/{dydoRelativePath}");
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        var manifestPath = Path.Combine(_dydoRoot, ManifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            _loaded = true;
            return;
        }

        var (manifestRecordPaths, retainedPaths) = ReadManifest(manifestPath);
        _manifestRecordPaths.UnionWith(manifestRecordPaths);
        _retainedPaths.UnionWith(retainedPaths);
        _loaded = true;
    }

    private static (HashSet<string> All, HashSet<string> Retained) ReadManifest(string manifestPath)
    {
        try
        {
            var manifestRecordPaths = new HashSet<string>(StringComparer.Ordinal);
            var retainedPaths = new HashSet<string>(StringComparer.Ordinal);
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var records = GetRecords(document);

            foreach (var record in records.EnumerateArray())
            {
                var (path, retained) = ReadRecord(record);
                if (!manifestRecordPaths.Add(path))
                    throw new InvalidDataException($"Duplicate legacy PM manifest path: {path}");
                if (retained)
                    retainedPaths.Add(path);
            }

            return (manifestRecordPaths, retainedPaths);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Legacy PM manifest is malformed: {ex.Message}", ex);
        }
    }

    private static JsonElement GetRecords(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("records", out var records) ||
            records.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Legacy PM manifest must contain a records array.");
        }

        return records;
    }

    private static (string Path, bool Retained) ReadRecord(JsonElement record)
    {
        if (!record.TryGetProperty("executionState", out var state) ||
            state.ValueKind != JsonValueKind.String ||
            state.GetString() is not ("pending" or "applied"))
        {
            throw new InvalidDataException("Every legacy PM record requires a valid executionState.");
        }

        if (!record.TryGetProperty("path", out var pathElement) ||
            pathElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(pathElement.GetString()))
        {
            throw new InvalidDataException("Every legacy PM record requires a path.");
        }

        var path = NormalizeRepoPath(pathElement.GetString()!);
        if (!path.StartsWith("dydo/", StringComparison.Ordinal))
            throw new InvalidDataException($"Legacy PM manifest path must be under dydo/: {path}");

        if (!record.TryGetProperty("finalDisposition", out var disposition) ||
            disposition.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Every legacy PM record requires a finalDisposition.");
        }

        var retained = disposition.GetString() is "retain" or "retain-normalize" &&
                       (state.GetString() == "applied" || disposition.GetString() == "retain-normalize");
        if (!retained)
            return (path, false);

        if (!record.TryGetProperty("target", out var target) ||
            target.ValueKind != JsonValueKind.Object ||
            !target.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String || kind.GetString() != "retained-path" ||
            !target.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String ||
            NormalizeRepoPath(value.GetString()!) != path)
        {
            throw new InvalidDataException("Retained legacy PM records require a matching retained-path target.");
        }

        return (path, true);
    }
}
