namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Utils;

/// <summary>
/// Reads the temporary dydo 3.0 disposition manifest that closes the v2 PM corpus
/// while its records are still present in the repository.
/// </summary>
public sealed class LegacyPmManifestService
{
    public const string ManifestRelativePath = "project/migrations/3.0-pm-records.json";

    private static readonly string[] RetainedNonRecordPathValues =
    [
        ProjectPath("campaigns", "_index.md"),
        ProjectPath("sprints", "_index.md"),
        ProjectPath("slices", "_index.md"),
        ProjectPath("tasks", "_index.md"),
        ProjectPath("issues", "_index.md"),
        ProjectPath("backlog", "_index.md"),
        ProjectPath("campaigns", "_campaigns.md"),
        ProjectPath("sprints", "_sprints.md"),
        ProjectPath("slices", "_slices.md"),
        ProjectPath("tasks", "_tasks.md"),
        ProjectPath("issues", "_issues.md"),
        ProjectPath("backlog", "_backlog.md")
    ];

    private readonly string _dydoRoot;
    private readonly HashSet<string> _pendingRecordPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _manifestRecordPaths = new(StringComparer.Ordinal);
    private HashSet<string>? _allowedPaths;
    private bool _loaded;

    public LegacyPmManifestService(string dydoRoot)
    {
        _dydoRoot = dydoRoot;
    }

    public bool IsActive => File.Exists(Path.Combine(_dydoRoot, ManifestRelativePath));

    public IReadOnlySet<string> GetPendingRecordPaths()
    {
        EnsureLoaded();
        return _pendingRecordPaths;
    }

    public IReadOnlySet<string> GetManifestRecordPaths()
    {
        EnsureLoaded();
        return _manifestRecordPaths;
    }

    public IReadOnlySet<string> GetAllowedPaths()
    {
        if (_allowedPaths != null)
            return _allowedPaths;

        _allowedPaths = new HashSet<string>(GetPendingRecordPaths(), StringComparer.Ordinal);
        _allowedPaths.UnionWith(RetainedNonRecordPathValues.Select(NormalizeRepoPath));
        return _allowedPaths;
    }

    public static IReadOnlySet<string> GetRetainedNonRecordPaths()
    {
        return new HashSet<string>(
            RetainedNonRecordPathValues.Select(NormalizeRepoPath),
            StringComparer.Ordinal);
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

    public static bool IsLegacyTaskPath(string dydoRelativePath)
    {
        var normalized = PathUtils.NormalizeForKey(dydoRelativePath);
        var taskRoot = string.Join('/', "project", "tasks");
        return normalized == taskRoot || normalized.StartsWith(taskRoot + "/", StringComparison.Ordinal);
    }

    private static string ProjectPath(string folder, string fileName)
    {
        return string.Join('/', "dydo", "project", folder, fileName);
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

        var (pendingRecordPaths, manifestRecordPaths) = ReadManifest(manifestPath);
        _manifestRecordPaths.UnionWith(manifestRecordPaths);
        _pendingRecordPaths.UnionWith(pendingRecordPaths);
        _loaded = true;
    }

    private static (HashSet<string> Pending, HashSet<string> All) ReadManifest(string manifestPath)
    {
        try
        {
            var pendingRecordPaths = new HashSet<string>(StringComparer.Ordinal);
            var manifestRecordPaths = new HashSet<string>(StringComparer.Ordinal);
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var records = GetRecords(document);

            foreach (var record in records.EnumerateArray())
            {
                var (path, pending) = ReadRecord(record);
                if (!manifestRecordPaths.Add(path))
                    throw new InvalidDataException($"Duplicate legacy PM manifest path: {path}");
                if (pending)
                    pendingRecordPaths.Add(path);
            }

            return (pendingRecordPaths, manifestRecordPaths);
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

    private static (string Path, bool Pending) ReadRecord(JsonElement record)
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
            throw new InvalidDataException("Every pending legacy PM record requires a path.");
        }

        var path = NormalizeRepoPath(pathElement.GetString()!);
        if (!path.StartsWith("dydo/", StringComparison.Ordinal))
            throw new InvalidDataException($"Legacy PM manifest path must be under dydo/: {path}");
        return (path, state.GetString() == "pending");
    }
}
