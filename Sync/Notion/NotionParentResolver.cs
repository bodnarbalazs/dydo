namespace DynaDocs.Sync.Notion;

using System.Runtime.InteropServices;

/// <summary>
/// Resolves the Notion parent page under which the PM spine databases are provisioned (slice brief §2).
/// Precedence: <c>notion.parentPageId</c> in dydo.json, else <c>DYDO_NOTION_PARENT_PAGE</c> from the
/// process environment; on Windows, falls back to the User-scoped variable (mirroring
/// <see cref="NotionTokenResolver"/>) so a persistently set value works without restarting the shell.
/// </summary>
public static class NotionParentResolver
{
    public const string ParentPageEnvVar = "DYDO_NOTION_PARENT_PAGE";

    public static string? Resolve(string? configuredParentPageId)
    {
        if (!string.IsNullOrWhiteSpace(configuredParentPageId))
            return configuredParentPageId;

        var env = Environment.GetEnvironmentVariable(ParentPageEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var userValue = Environment.GetEnvironmentVariable(ParentPageEnvVar, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userValue))
                return userValue;
        }

        return null;
    }
}
