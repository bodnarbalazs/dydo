namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public class FrontmatterRule : RuleBase
{
    private readonly IFrontmatterTypesService? _typesService;

    public override string Name => "Frontmatter";
    public override string Description => "Every doc must have valid YAML frontmatter with required fields";

    public FrontmatterRule(IFrontmatterTypesService? typesService = null)
    {
        _typesService = typesService;
    }

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        var normalized = PathUtils.NormalizePath(doc.RelativePath);

        // Skip template files and template additions
        if (RuleSkipPaths.IsTemplateOrAddition(normalized))
        {
            yield break;
        }

        // Skip files-off-limits.md which uses type: config
        if (doc.FileName.Equals("files-off-limits.md", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        // Skip task files - they use task-specific frontmatter (name/status/created/assigned)
        // Meta files (_tasks.md, _index.md) still go through normal validation
        if (normalized.Contains("/tasks/", StringComparison.OrdinalIgnoreCase) &&
            !doc.FileName.StartsWith("_"))
        {
            if (!doc.HasFrontmatter)
            {
                yield return CreateError(doc, "Missing frontmatter");
                yield break;
            }
            var taskFm = doc.Frontmatter!;
            if (string.IsNullOrEmpty(taskFm.Area))
                yield return CreateError(doc, "Missing required frontmatter field: area");
            else if (!Frontmatter.ValidAreas.Contains(taskFm.Area))
                yield return CreateError(doc, $"Invalid area value '{taskFm.Area}'. Must be one of: {string.Join(", ", Frontmatter.ValidAreas)}");
            yield break;
        }

        if (!doc.HasFrontmatter)
        {
            yield return CreateError(doc, "Missing frontmatter");
            yield break;
        }

        var fm = doc.Frontmatter!;

        if (string.IsNullOrEmpty(fm.Area))
        {
            yield return CreateError(doc, "Missing required frontmatter field: area");
        }
        else if (!Frontmatter.ValidAreas.Contains(fm.Area))
        {
            yield return CreateError(doc, $"Invalid area value '{fm.Area}'. Must be one of: {string.Join(", ", Frontmatter.ValidAreas)}");
        }

        var validTypes = _typesService?.GetValidTypes() ?? Frontmatter.ValidTypes;
        if (string.IsNullOrEmpty(fm.Type))
        {
            yield return CreateError(doc, "Missing required frontmatter field: type");
        }
        else if (!validTypes.Contains(fm.Type))
        {
            yield return CreateError(doc, $"Invalid type value '{fm.Type}'. Must be one of: {string.Join(", ", validTypes)}");
        }

        if (fm.Type == "decision")
        {
            if (string.IsNullOrEmpty(fm.Status))
            {
                yield return CreateError(doc, "Decision documents require 'status' field");
            }
            else if (!Frontmatter.ValidStatuses.Contains(fm.Status))
            {
                yield return CreateError(doc, $"Invalid status value '{fm.Status}'. Must be one of: {string.Join(", ", Frontmatter.ValidStatuses)}");
            }

            if (string.IsNullOrEmpty(fm.Date))
            {
                yield return CreateError(doc, "Decision documents require 'date' field (YYYY-MM-DD)");
            }
        }

        if (fm.Type == "changelog" && string.IsNullOrEmpty(fm.Date))
        {
            yield return CreateError(doc, "Changelog documents require 'date' field (YYYY-MM-DD)");
        }
    }
}
