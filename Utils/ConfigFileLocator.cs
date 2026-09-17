namespace DynaDocs.Utils;

using System.Text.Json;

internal static class ConfigFileLocator
{
    internal const string FileName = "dydo.json";
    internal const string DefaultRoot = "dydo";

    internal static string? WalkUpForFile(string startDir, string fileName)
    {
        var dir = startDir;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;

            dir = parent.FullName;
        }
        return null;
    }

    internal static string? FindProjectRoot(string? startPath = null)
    {
        var config = WalkUpForFile(startPath ?? Environment.CurrentDirectory, FileName);
        return config == null ? null : Path.GetDirectoryName(config);
    }

    internal static string GetDydoRoot(string projectRoot)
    {
        var root = DefaultRoot;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, FileName)));
            if (document.RootElement.TryGetProperty("structure", out var structure)
                && structure.TryGetProperty("root", out var configured)
                && configured.GetString() is { Length: > 0 } value)
                root = value;
        }
        catch (JsonException) { }
        catch (IOException) { }
        catch (InvalidOperationException) { }
        catch (UnauthorizedAccessException) { }
        return Path.Combine(projectRoot, root);
    }
}
