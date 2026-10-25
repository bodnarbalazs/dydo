namespace DynaDocs.Utils;

using System.Reflection;

/// <summary>
/// One source for what `dydo version` prints and a fresh init records, so the two cannot drift.
/// </summary>
public static class DydoVersion
{
    public static string Current { get; } = Read(typeof(DydoVersion).Assembly);

    private static string Read(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString(3)
        ?? "1.0.0";
}
