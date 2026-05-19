namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Rules;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Runs documentation validation rules for CheckCommand.
/// </summary>
internal static class CheckDocValidator
{
    public static ValidationResult Validate(string basePath, string? reportScope = null)
    {
        var parser = new MarkdownParser();
        var scanner = new DocScanner(parser);
        var linkResolver = new LinkResolver();
        var typesService = new FrontmatterTypesService(basePath);

        var allDocs = scanner.ScanDirectory(basePath)
            .Where(d => !PathUtils.NormalizePath(d.RelativePath)
                .StartsWith("agents/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var allFolders = scanner.GetAllFolders(basePath)
            .Where(f =>
            {
                var rel = PathUtils.NormalizePath(Path.GetRelativePath(basePath, f));
                return !rel.StartsWith("agents", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var docsToValidate = reportScope == null
            ? allDocs
            : allDocs.Where(d => IsUnderScope(d.FilePath, reportScope)).ToList();
        var foldersToValidate = reportScope == null
            ? allFolders
            : allFolders.Where(f => IsUnderScope(f, reportScope)).ToList();

        var rules = CreateRules(linkResolver, typesService);
        var result = new ValidationResult { TotalFilesChecked = docsToValidate.Count };

        foreach (var doc in docsToValidate)
        {
            foreach (var rule in rules)
            {
                result.AddRange(rule.Validate(doc, allDocs, basePath));
            }
        }

        foreach (var folder in foldersToValidate)
        {
            foreach (var rule in rules)
            {
                result.AddRange(rule.ValidateFolder(folder, allDocs, basePath));
            }
        }

        return result;
    }

    internal static bool IsUnderScope(string fullPath, string scopePath)
    {
        var normPath = PathUtils.NormalizeForKey(fullPath);
        var normScope = PathUtils.NormalizeForKey(scopePath).TrimEnd('/');
        if (normScope.Length == 0)
            return true;
        return normPath == normScope || normPath.StartsWith(normScope + "/", StringComparison.Ordinal);
    }

    private static List<IRule> CreateRules(ILinkResolver linkResolver, IFrontmatterTypesService typesService)
    {
        return
        [
            new NamingRule(),
            new RelativeLinksRule(),
            new FrontmatterRule(typesService),
            new SummaryRule(),
            new BrokenLinksRule(linkResolver),
            new HubFilesRule(),
            new FolderMetaFilesRule(),
            new OrphanDocsRule(),
            new OffLimitsRule(),
            new UncustomizedDocsRule()
        ];
    }
}
