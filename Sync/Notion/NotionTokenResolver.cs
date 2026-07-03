namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;

/// <summary>
/// Resolves the Notion integration token (Decision 027 §2 — never logged, never written to a synced
/// file). Three tiers, in precedence order: (a) the local secret store (<see cref="NotionTokenStore"/>),
/// (b) the project-namespaced env var <c>DYDO_&lt;SLUG&gt;_NOTION_TOKEN</c> (for CI), then (c) the generic
/// <c>DYDO_NOTION_TOKEN</c> — with the historical Windows User-registry fallback preserved on the generic
/// tier so a persistently set token works without restarting the shell. The slug comes from
/// <see cref="DydoConfig.Name"/> when set, else the sanitized project-root directory name.
/// </summary>
public static class NotionTokenResolver
{
    public const string TokenEnvVar = "DYDO_NOTION_TOKEN";

    public static string? Resolve(DydoConfig? config, string? projectRoot, string dydoRoot)
    {
        var local = NotionTokenStore.Read(NotionTokenStore.PathFor(dydoRoot));
        if (!string.IsNullOrWhiteSpace(local))
            return local;

        var slug = SlugFor(config, projectRoot);
        if (slug.Length > 0)
        {
            var namespaced = Environment.GetEnvironmentVariable($"DYDO_{slug}_NOTION_TOKEN");
            if (!string.IsNullOrWhiteSpace(namespaced))
                return namespaced;
        }

        var token = Environment.GetEnvironmentVariable(TokenEnvVar);
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        // Windows convenience: a token set via `setx`/System Properties lands in the User registry hive,
        // which the current process's environment block does not see until a new shell starts.
        if (OperatingSystem.IsWindows())
        {
            var userToken = Environment.GetEnvironmentVariable(TokenEnvVar, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userToken))
                return userToken;
        }

        return null;
    }

    /// <summary>The env-var slug: <see cref="DydoConfig.Name"/> if set, else the project-root directory name,
    /// sanitized (uppercased; every non-alphanumeric character becomes <c>_</c>).</summary>
    public static string SlugFor(DydoConfig? config, string? projectRoot)
    {
        var raw = config?.Name;
        if (string.IsNullOrWhiteSpace(raw) && !string.IsNullOrWhiteSpace(projectRoot))
            raw = Path.GetFileName(projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return Slugify(raw);
    }

    public static string Slugify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // ASCII-only: a Unicode letter (e.g. 'é') would survive char.IsLetterOrDigit and produce an
        // env-var name a CI operator can't reliably type. Every non-ASCII-alphanumeric becomes '_'.
        return new string(name.Select(c => IsAsciiAlphanumeric(c) ? char.ToUpperInvariant(c) : '_').ToArray());
    }

    private static bool IsAsciiAlphanumeric(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
