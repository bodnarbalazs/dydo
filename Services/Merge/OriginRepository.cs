namespace DynaDocs.Services.Merge;

using System.Text.RegularExpressions;

/// <summary>The GitHub repository an origin URL names: HTTPS, <c>ssh://</c> or scp-like <c>[user@]host:owner/name</c>.</summary>
internal sealed partial record OriginRepository(string Host, string Owner, string Name, bool IsSsh)
{
    public string FullName => $"{Owner}/{Name}";

    public string ApiPath => $"repos/{Owner}/{Name}";

    // An SSH config alias (Host work-gh) resolves only through ssh; gh knows hosts by their real name,
    // which has a dot.
    public bool IsSshAlias => IsSsh && !Host.Contains('.');

    public string RealHostHint => $"point origin at the real host, such as git@github.com:{FullName}.git";

    public override string ToString() => $"{Host}/{Owner}/{Name}";

    public static OriginRepository? Parse(string url)
    {
        var match = OriginRegex().Match(url);
        if (!match.Success)
            return null;
        var ssh = match.Groups["scheme"].Value is "" or "ssh";
        return new OriginRepository(match.Groups["host"].Value.ToLowerInvariant(), match.Groups["owner"].Value,
            match.Groups["name"].Value, ssh);
    }

    // Credentials and ports are matched and dropped; a URL never reaches output.
    [GeneratedRegex(@"\A(?:(?<scheme>https?|ssh)://(?:[^@/]+@)?(?<host>[^/:@]+)(?::\d+)?/|(?:[^@/:]+@)?(?<host>[^/:@]+):)"
        + @"(?<owner>[A-Za-z0-9_.-]+)/(?<name>[A-Za-z0-9_.-]+?)(?:\.git)?/?\z")]
    private static partial Regex OriginRegex();
}
