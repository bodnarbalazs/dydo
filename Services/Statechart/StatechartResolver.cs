namespace DynaDocs.Services.Statechart;

internal static class StatechartResolver
{
    public static async Task<StatechartDependencies> ResolveAsync(StatechartRequest request, StatechartManaged managed,
        StatechartHome home, CancellationToken token)
    {
        var retained = managed.Read();
        var path = Environment.GetEnvironmentVariable("PATH");
        var node = retained.Node ?? StatechartPrerequisites.Find("node", path);
        var java = request.Java is not null ? StatechartPrerequisites.Java(request.Caller, request.Java, path) :
            retained.Java ?? StatechartPrerequisites.Java(request.Caller, null, path) ?? IdeJava();
        var tlc = request.Tlc is not null ? StatechartPrerequisites.Tlc(request.Caller, request.Tlc) : retained.Tlc;
        StatechartPaths.EnsureDirectory(home.State);
        await using var owner = await StatechartLifetime.CreateAsync(StatechartExecutable.Current(),
            home.State, home.State, Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        await owner.RegisterAsync();
        try
        {
            if (node is not null && !StatechartPrerequisites.Compatible("node",
                    await StatechartProbe.CaptureAsync(node, ["--version"], owner, request.Caller, token))) node = null;
            if (java is not null && !StatechartPrerequisites.Compatible("java",
                    await StatechartProbe.CaptureAsync(java, ["-version"], owner, request.Caller, token)))
            {
                if (request.Java is not null) throw new IOException("Explicit JAVA must execute Java11 or later.");
                java = null;
            }
            return new(node, java, tlc, retained.Renderer);
        }
        finally
        {
            if (!await owner.CompleteAsync()) throw new IOException("Dependency discovery drain uncertain; scratch retained.");
        }
    }

    // Only Windows has Program Files; elsewhere the folder path is empty and would name the caller's directory.
    private static string? IdeJava()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var jetbrains = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains");
        if (!Directory.Exists(jetbrains)) return null;
        return Directory.EnumerateDirectories(jetbrains).Order(StringComparer.Ordinal)
            .Select(directory => Path.Combine(directory, "jbr", "bin", "java.exe")).FirstOrDefault(File.Exists);
    }
}
