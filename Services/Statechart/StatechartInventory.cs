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
                var link = new FileInfo(path);
                var target = link.ResolveLinkTarget(returnFinalTarget: true) ?? throw new IOException("Broken installed statechart link.");
                var relative = Path.GetRelativePath(root, target.FullName);
                if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new IOException("Installed statechart link escapes its root.");
                row["link"] = link.LinkTarget;
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
}
