namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Rule for validating the files-off-limits.md configuration.
/// Checks that literal paths (without wildcards) in the off-limits file actually exist.
/// </summary>
public class OffLimitsRule : RuleBase
{
    public override string Name => "OffLimitsValidation";
    public override string Description => "Validates that literal paths in files-off-limits.md exist";

    private readonly IOffLimitsService _offLimitsService;

    public OffLimitsRule(IOffLimitsService? offLimitsService = null)
    {
        _offLimitsService = offLimitsService ?? new OffLimitsService();
    }

    public override IEnumerable<Violation> ValidateFolder(string folderPath, List<DocFile> allDocs, string basePath)
    {
        // Only validate at the dydo root level (not in subfolders)
        var configService = new ConfigService();
        var dydoRoot = configService.GetDydoRoot(basePath);

        // Normalize paths for comparison
        var normalizedFolderPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedDydoRoot = Path.GetFullPath(dydoRoot).TrimEnd(Path.DirectorySeparatorChar);

        if (!normalizedFolderPath.Equals(normalizedDydoRoot, StringComparison.OrdinalIgnoreCase))
            yield break;

        // Check if off-limits file exists
        if (!_offLimitsService.OffLimitsFileExists(basePath))
        {
            yield return new Violation(
                "dydo/files-off-limits.md",
                Name,
                "files-off-limits.md not found. Run 'dydo init claude' to create it.",
                ViolationSeverity.Warning
            );
            yield break;
        }

        // Load patterns and validate literal paths
        _offLimitsService.LoadPatterns(basePath);

        var projectRoot = configService.GetProjectRoot(basePath) ?? basePath;
        var missingPaths = _offLimitsService.ValidateLiteralPaths(projectRoot);

        foreach (var path in missingPaths)
        {
            yield return new Violation(
                "dydo/files-off-limits.md",
                Name,
                $"Literal path in off-limits file does not exist: {path}",
                ViolationSeverity.Warning
            );
        }
    }
}
