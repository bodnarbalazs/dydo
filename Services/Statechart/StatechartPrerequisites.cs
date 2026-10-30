namespace DynaDocs.Services.Statechart;

using System.Security.Cryptography;
using System.Text.RegularExpressions;

internal static class StatechartPrerequisites
{
    public static bool Compatible(string runtime, string version)
    {
        var match = Regex.Match(version, @"(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        var major = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value,
            System.Globalization.CultureInfo.InvariantCulture) : 0;
        return runtime switch
        {
            "node" => major == 22 && minor >= 18 || major == 23 && minor >= 6 || major > 23,
            "bun" => major > 1 || major == 1 && (minor > 1 || minor == 1 && patch >= 5),
            "deno" => major >= 2,
            "java" => major >= 11,
            _ => false
        };
    }

    public static string? Find(string executable, string? path)
    {
        foreach (var directory in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Path.IsPathFullyQualified(directory)) continue;
            var candidate = Path.Combine(directory, executable + (OperatingSystem.IsWindows() ? ".exe" : ""));
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static string? Java(string cwd, string? value, string? path)
    {
        if (value is null) return Find("java", path);
        var resolved = value.IndexOfAny(['/', '\\']) >= 0 ? Path.GetFullPath(value, cwd) : Find(value, path);
        if (resolved is null || !File.Exists(resolved)) throw new IOException("Invalid explicit JAVA override: " + value);
        return resolved;
    }

    public static string? Tlc(string cwd, string? value)
    {
        if (value is null) return null;
        var file = Path.GetFullPath(value, cwd);
        if (!File.Exists(file) || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))) !=
            "936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88")
            throw new IOException("Explicit TLA2TOOLS must be the locked TLC1.7.4 jar: " + file);
        return file;
    }
}
