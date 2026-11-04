namespace DynaDocs.Services.Statechart;

using System.Security.Cryptography;
using System.Text.Json;

internal sealed class StatechartChecker
{
    internal const string Digest = "936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88";
    private readonly string _source;
    private readonly string _jar;

    private StatechartChecker(string source, string jar) { _source = source; _jar = jar; }

    internal static string Input(string path)
    {
        var full = Path.GetFullPath(path);
        RejectCompanion(full);
        var resolved = Resolve(full, 0);
        RejectCompanion(resolved);
        RequireDigest(resolved);
        return resolved;
    }

    private static string Resolve(string path, int links)
    {
        if (links > 40) throw new IOException("Too many linked checker input components: " + path);
        var root = Path.GetPathRoot(path)!;
        var parts = path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (var i = 0; i < parts.Length; i++)
        {
            current = Path.Combine(current, parts[i]);
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) == 0) continue;
            FileSystemInfo entry = (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(current) : new FileInfo(current);
            var target = entry.ResolveLinkTarget(returnFinalTarget: true) ??
                throw new IOException("Cannot resolve checker input link: " + current);
            return Resolve(Path.Combine([target.FullName, .. parts.Skip(i + 1)]), links + 1);
        }
        return current;
    }

    private static void RejectCompanion(string jar)
    {
        var parent = Path.GetDirectoryName(jar)!;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (Directory.EnumerateFileSystemEntries(parent).Any(entry =>
                string.Equals(Path.GetFileName(entry), "CommunityModules.jar", comparison)))
            throw new IOException("Unsupported unpinned dependency CommunityModules.jar beside checker input: " + jar);
    }

    private static void RequireDigest(string jar)
    {
        if (!File.Exists(jar) || (File.GetAttributes(jar) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(jar))) != Digest)
            throw new IOException("Statechart requires the pinned TLC1.7.4 jar: " + jar);
    }

    internal static StatechartChecker Prepare(string source, string owned)
    {
        var input = Input(source);
        StatechartPaths.RequirePlainDirectory(owned);
        var directory = Path.Combine(owned, "checker");
        if (OperatingSystem.IsWindows()) WindowsStatechartFiles.CreateExclusive(directory);
        else PosixStatechartFiles.CreateExclusive(directory);
        var jar = Path.Combine(directory, "tla2tools.jar");
        using (var output = new FileStream(jar, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var original = File.OpenRead(input)) original.CopyTo(output);
        var checker = new StatechartChecker(source, jar);
        checker.Verify();
        return checker;
    }

    internal string Verify()
    {
        Input(_source);
        var directory = Path.GetDirectoryName(_jar)!;
        StatechartPaths.RequirePlainDirectory(directory);
        if (!Directory.EnumerateFileSystemEntries(directory).SequenceEqual([_jar]))
            throw new IOException("Isolated checker directory must contain only tla2tools.jar: " + directory);
        RequireDigest(_jar);
        return _jar;
    }

    internal string[] TlcArguments(string payload)
    {
        var jar = Verify();
        var root = Path.Combine(payload, "skills/engineering/statechart/scripts/java");
        StatechartPaths.RequirePlainDirectory(root);
        var helper = Path.Combine(root, "statechart-tlc-bootstrap.jar");
        var lockPath = Path.Combine(root, "build-lock.json");
        if ((File.GetAttributes(lockPath) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked statechart bootstrap build lock; no raw TLC fallback.");
        using var buildLock = JsonDocument.Parse(File.ReadAllBytes(lockPath));
        var artifact = buildLock.RootElement.GetProperty("artifact");
        if (buildLock.RootElement.GetProperty("schema").GetInt32() != 1 ||
            artifact.GetProperty("state").GetString() != "sealed-build" ||
            !File.Exists(helper) || (File.GetAttributes(helper) & FileAttributes.ReparsePoint) != 0 ||
            artifact.GetProperty("sha256").GetString() != Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))))
            throw new IOException("Statechart TLC bootstrap is missing or changed; no raw TLC fallback.");
        return ["-cp", helper + Path.PathSeparator + jar, "dydo.statechart.LoopbackTlc"];
    }
}
