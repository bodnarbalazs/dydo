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

        var missing = ConfigFactory.FindMissingScanExcludeInvariants(config);
        foreach (var entry in missing)
        {
            errors.Add(
                $"dydo.json scanExclude is missing required entry '{entry}'. Run 'dydo fix' to restore it.");
        }

        return errors;
    }
}
