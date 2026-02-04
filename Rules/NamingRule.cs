namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Utils;

public class NamingRule : RuleBase
{
    public override string Name => "NamingConvention";
    public override string Description => "All file and folder names must be kebab-case";
    public override bool CanAutoFix => true;

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        var normalized = PathUtils.NormalizePath(doc.RelativePath);

        // Skip template files - they use .template.md naming by design
        if (normalized.StartsWith("_system/templates/", StringComparison.OrdinalIgnoreCase))
            yield break;

        // Skip agent workspace files - agent names are PascalCase identities by design
        if (normalized.StartsWith("agents/", StringComparison.OrdinalIgnoreCase))
            yield break;

        if (!PathUtils.IsKebabCase(doc.FileName))
        {
            var suggested = PathUtils.ToKebabCase(Path.GetFileNameWithoutExtension(doc.FileName)) + ".md";
            yield return CreateError(doc, $"Filename should be kebab-case: {suggested}", suggestedFix: suggested);
        }

        var pathParts = doc.RelativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in pathParts.SkipLast(1))
        {
            if (!PathUtils.IsKebabCase(part))
            {
                var suggested = PathUtils.ToKebabCase(part);
                yield return CreateError(doc, $"Folder name should be kebab-case: {part} -> {suggested}");
            }
        }
    }
}
