namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Prevents resurrection of the retired v2 PM corpus.
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
    public override string Description => "The retired v2 PM corpus may not be recreated";

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

    private static string RepoProjectDirectory(string folder)
    {
        return string.Join('/', "dydo", "project", folder);
    }
}
