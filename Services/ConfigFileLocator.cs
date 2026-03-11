namespace DynaDocs.Services;

internal static class ConfigFileLocator
{
    public static string? WalkUpForFile(string startDir, string fileName)
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
}
