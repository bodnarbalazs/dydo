namespace DynaDocs.Services.Statechart;

using System.Security.Cryptography;
using System.Text.Json.Nodes;

internal static class StatechartInventory
{
    public static JsonArray Capture(string root, string? excluded = null)
    {
        StatechartPaths.RequirePlainDirectory(root);
        var entries = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal);
        Visit(root, root, excluded, entries);
        return new JsonArray(entries.Values.Select(value => (JsonNode)value).ToArray());
    }

    private static void Visit(string root, string directory, string? excluded, SortedDictionary<string, JsonObject> entries)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var name = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (name == excluded) continue;
            var attributes = File.GetAttributes(path);
            var row = new JsonObject { ["path"] = name };
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                var link = new FileInfo(path).LinkTarget ?? throw new IOException("Unreadable installed statechart link: " + name);
                StatechartLinks.Resolve(StatechartLinks.Parent(name), Portable(link),
                    candidate => LinkAt(root, candidate), candidate => DirectoryAt(root, candidate));
                row["link"] = link;
            }
            else if ((attributes & FileAttributes.Directory) != 0)
            {
                row["directory"] = true;
                Visit(root, path, excluded, entries);
            }
            else
            {
                using var stream = File.OpenRead(path);
                row["sha256"] = Convert.ToHexStringLower(SHA256.HashData(stream));
            }
            entries.Add(name, row);
        }
    }

    // A missing entry is no link, so the walk reports the link broken.
    private static string? LinkAt(string root, string candidate)
    {
        try { return new FileInfo(Path.Combine(root, candidate)).LinkTarget is { } link ? Portable(link) : null; }
        catch (IOException) { return null; }
    }

    private static bool? DirectoryAt(string root, string candidate)
    {
        var path = Path.Combine(root, candidate);
        if (Directory.Exists(path)) return true;
        return File.Exists(path) ? false : null;
    }

    private static string Portable(string link) => link.Replace(Path.DirectorySeparatorChar, '/');
}
