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
    public static ValidationResult Validate(string basePath)
    {
        var parser = new MarkdownParser();
        var scanner = new DocScanner(parser);
        var linkResolver = new LinkResolver();

        var docs = scanner.ScanDirectory(basePath)
            .Where(d => !PathUtils.NormalizePath(d.RelativePath)
                .StartsWith("agents/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var folders = scanner.GetAllFolders(basePath)
            .Where(f =>
            {
                var rel = PathUtils.NormalizePath(Path.GetRelativePath(basePath, f));
                return !rel.StartsWith("agents", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        var rules = CreateRules(linkResolver);
        var result = new ValidationResult { TotalFilesChecked = docs.Count };

        foreach (var doc in docs)
        {
            foreach (var rule in rules)
            {
                result.AddRange(rule.Validate(doc, docs, basePath));
            }
        }

        foreach (var folder in folders)
        {
            foreach (var rule in rules)
            {
                result.AddRange(rule.ValidateFolder(folder, docs, basePath));
            }
        }

        return result;
    }

    private static List<IRule> CreateRules(ILinkResolver linkResolver)
    {
        return
        [
            new NamingRule(),
            new RelativeLinksRule(),
            new FrontmatterRule(),
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
