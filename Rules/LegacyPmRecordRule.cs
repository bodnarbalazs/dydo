namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Prevents the frozen v2 PM corpus from growing while Project 3 applies its
/// human-ratified disposition manifest.
/// </summary>
public sealed class LegacyPmRecordRule : RuleBase
{
    private static readonly string[] CanonicalLegacyDirectories =
    [
        RepoProjectDirectory("campaigns"),
        RepoProjectDirectory("sprints"),
        RepoProjectDirectory("slices"),
        RepoProjectDirectory("tasks"),
        RepoProjectDirectory("issues"),
        RepoProjectDirectory("backlog")
    ];

    private static readonly HashSet<string> LegacyTypes = new(
        ["campaign", "sprint", "slice", "task", "issue", "backlog", "release", "future-feature"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> LegacyFields = new(
        ["seq", "assigned", "sprint", "slice", "task"],
        StringComparer.OrdinalIgnoreCase);

    private readonly LegacyPmManifestService _manifest;

    public LegacyPmRecordRule(LegacyPmManifestService manifest)
    {
        _manifest = manifest;
    }

    public override string Name => "LegacyPmRecord";
    public override string Description => "The frozen v2 PM corpus may contain only manifest-backed records and retained hubs";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        if (!_manifest.IsActive)
            yield break;

        var repoPath = LegacyPmManifestService.ToRepoPath(doc.RelativePath);
        if (!_manifest.GetManifestRecordPaths().Contains(repoPath) && !IsCandidate(doc, repoPath))
            yield break;

        if (!_manifest.GetAllowedPaths().Contains(repoPath))
            yield return CreateError(doc, "Repository PM record is outside the frozen v2 manifest allow-set");
    }

    public override IEnumerable<Violation> ValidateFolder(string folderPath, List<DocFile> allDocs, string basePath)
    {
        if (!_manifest.IsActive || !PathsEqual(folderPath, basePath))
            yield break;

        var existingPaths = allDocs
            .Select(doc => LegacyPmManifestService.ToRepoPath(doc.RelativePath))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var missingPath in _manifest.GetPendingRecordPaths()
                     .Where(path => !existingPaths.Contains(path))
                     .Order(StringComparer.Ordinal))
        {
            yield return CreateFolderError(missingPath, "Pending legacy PM manifest path does not resolve");
        }
    }

    private static bool IsCandidate(DocFile doc, string repoPath)
    {
        if (IsManifestDirectoryDirectChild(repoPath) || HasLegacyFrontmatterSignature(doc.Content))
            return true;

        return false;
    }

    private static bool IsManifestDirectoryDirectChild(string repoPath)
    {
        var directory = PathUtils.NormalizeForKey(Path.GetDirectoryName(repoPath) ?? "");
        return CanonicalLegacyDirectories.Contains(directory, StringComparer.Ordinal);
    }

    private static bool HasLegacyFrontmatterSignature(string content)
    {
        var fields = FrontmatterParser.ParseFields(content);
        if (fields == null)
            return false;

        foreach (var (key, value) in fields)
        {
            if (key.Equals("type", StringComparison.OrdinalIgnoreCase) &&
                LegacyTypes.Contains(value.Trim('"', '\'')))
            {
                return true;
            }
            if (LegacyFields.Contains(key))
                return true;
        }

        return false;
    }

    private static bool PathsEqual(string left, string right)
    {
        return PathUtils.NormalizeForKey(Path.GetFullPath(left)).TrimEnd('/') ==
               PathUtils.NormalizeForKey(Path.GetFullPath(right)).TrimEnd('/');
    }

    private static string RepoProjectDirectory(string folder)
    {
        return string.Join('/', "dydo", "project", folder);
    }
}
