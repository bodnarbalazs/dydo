namespace DynaDocs.Services.Statechart;

internal static class StatechartPaths
{
    public static void EnsureDirectory(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new IOException("Statechart root must be absolute.");
        if (!Directory.Exists(path))
        {
            EnsureDirectory(Path.GetDirectoryName(path) ?? throw new IOException("Invalid statechart root."));
            Directory.CreateDirectory(path);
        }
        RequirePlainDirectory(path);
    }
    public static void RequirePlainDirectory(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path))
            throw new IOException("Statechart roots must be absolute: " + path);
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Statechart root has a missing or linked ancestor: " + current.FullName);
        }
    }

    public static string Root(string variable, string fallback)
    {
        var path = Environment.GetEnvironmentVariable(variable) ?? fallback;
        if (!Path.IsPathFullyQualified(path)) throw new IOException(variable + " must be absolute.");
        return Path.GetFullPath(path);
    }
}
