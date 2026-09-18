namespace DynaDocs.Services;

using System.Reflection;

/// <summary>
/// The project skeleton `dydo init` copies out. Every file under `Scaffold/` in the repository is
/// embedded in the assembly under its own repository-relative path, so init walks the tree instead
/// of naming each file; adding a file to `Scaffold/` is all it takes to ship it.
/// </summary>
public static class ScaffoldTree
{
    private const string Root = "Scaffold/";
    private const string DydoRoot = "Scaffold/dydo/";

    private static readonly Assembly Assembly = typeof(ScaffoldTree).Assembly;

    /// <summary>Every embedded scaffold file, named by its repository-relative path.</summary>
    public static IEnumerable<string> ResourceNames =>
        Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Root, StringComparison.Ordinal));

    /// <summary>
    /// Writes the `dydo/` documentation tree under <paramref name="dydoRoot"/>, leaving any file
    /// the project already has exactly as it found it.
    /// </summary>
    public static void WriteDydoTree(string dydoRoot)
    {
        foreach (var name in ResourceNames.Where(n => n.StartsWith(DydoRoot, StringComparison.Ordinal)))
        {
            var path = Path.Combine(dydoRoot,
                name[DydoRoot.Length..].Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(path))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Read(name));
        }
    }

    /// <summary>
    /// The runtime entry-point file at the project root — materialized as CLAUDE.md (Claude Code)
    /// and AGENTS.md (Codex) from one runtime-neutral source, `Scaffold/entry-point.md`.
    /// </summary>
    public static string EntryPoint(string projectName) =>
        Read($"{Root}entry-point.md")
            .Replace("{{PROJECT_NAME}}", projectName)
            .TrimEnd('\r', '\n');

    /// <summary>Reads one scaffold file by its repository-relative path.</summary>
    public static string Read(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Scaffold file not found: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
