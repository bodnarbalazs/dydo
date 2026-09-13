namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Validates dydo.json invariants for CheckCommand. Today's only invariant
/// is the scan-exclude list — every entry in
/// <see cref="ConfigFactory.DydoInternalScanExclude"/> must be present in
/// <c>config.ScanExclude</c>. <c>dydo fix</c> restores missing entries.
/// </summary>
internal static class CheckConfigValidator
{
    public static List<string> Validate(DydoConfig config)
    {
        var errors = new List<string>();

        // A pre-source-layer project remains valid until `template update` migrates it, so only
        // the source directory may be absent. The established internal exclusions stay required.
        var missing = ConfigFactory.FindMissingScanExcludeInvariants(config);
        if (config.Skills.Count == 0)
            missing.RemoveAll(entry => entry.Equals("_system/templates/", StringComparison.OrdinalIgnoreCase));
        foreach (var entry in missing)
        {
            errors.Add(
                $"dydo.json scanExclude is missing required entry '{entry}'. Run 'dydo fix' to restore it.");
        }

        return errors;
    }
}
