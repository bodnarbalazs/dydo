namespace DynaDocs.Commands;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Recognises a CLAUDE.md that a released dydo generated, so `dydo init --join` can remove it
/// after the entry template has moved on (DYD-372).
/// </summary>
internal static class ReleasedEntryPoints
{
    // SHA-256 of everything after the title line, LF endings, trailing newlines trimmed.
    private static readonly HashSet<string> BodyHashes =
    [
        "99a04b3b17d244a40b81ae37fc2e47f1a944a416c35f22e53f38d19f6574a0ad", // v1.1.0 .. v2.0.15, TemplateGenerator.GenerateClaudeMd
        "f09d631af5bec0300254ee7e6c75d29200d7e5e6dcbdf9efb704dcd4ae243fd3", // v2.1.0 .. v2.2.3, entry-point.template.md b9d23af4
        "6f66bac16fd49884d8a623a4771180aa0c9f5a14f2e6b4f5fcb084cad5fc0d3d", // v2.2.4 .. v2.2.5, entry-point.template.md 547fa082
        "f141627e38aa83a4a8636ec0eaafc6e8ff094a84860fd4c4aedc460725920394", // v2.2.6 .. v2.2.9, entry-point.template.md 91747359
        "b17458d76c7f689fbd81d47076df48d6ebd0bb4fcd89e7b8a76b341ced7c34f8", // v3.0.0 .. v3.4.1, Scaffold/entry-point.md 93c0ddb0
        "a233eecbc3703fa7259dd49a43940c2adf50e110eaa1f8247cf28abaf3f9be9b", // 1629c8a0 (3.5 base tree), Scaffold/entry-point.md f904f57f
    ];

    /// <param name="normalized">CLAUDE.md with LF endings and trailing newlines trimmed.</param>
    public static bool Generated(string normalized)
    {
        // Any "# " title passes: it held the folder name at generation time, and worktrees and
        // renamed clones change that folder, so only the body proves dydo wrote the file.
        var newline = normalized.IndexOf('\n');
        if (newline < 0 || !normalized.StartsWith("# ", StringComparison.Ordinal))
            return false;

        var body = normalized[(newline + 1)..];
        return BodyHashes.Contains(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))));
    }
}
