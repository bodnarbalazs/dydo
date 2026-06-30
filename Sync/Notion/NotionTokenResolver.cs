namespace DynaDocs.Sync.Notion;

using System.Runtime.InteropServices;

/// <summary>
/// Resolves the Notion integration token (Decision 025 §6 — never logged, never written to a synced
/// file). Reads <c>DYDO_NOTION_TOKEN</c> from the process environment; on Windows, falls back to the
/// User-scoped environment variable so a persistently set token works without restarting the shell.
/// </summary>
public static class NotionTokenResolver
{
    public const string TokenEnvVar = "DYDO_NOTION_TOKEN";

    public static string? Resolve()
    {
        var token = Environment.GetEnvironmentVariable(TokenEnvVar);
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        // Windows convenience: a token set via `setx`/System Properties lands in the User registry
        // hive, which the current process's environment block does not see until a new shell starts.
        // Reading the User target picks it up immediately.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var userToken = Environment.GetEnvironmentVariable(TokenEnvVar, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userToken))
                return userToken;
        }

        return null;
    }
}
