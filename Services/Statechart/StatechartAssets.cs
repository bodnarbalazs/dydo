namespace DynaDocs.Services.Statechart;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed class StatechartAssets
{
    private readonly Assembly _assembly = typeof(StatechartAssets).Assembly;
    private readonly byte[] _manifest;
    public string Digest { get; }
    public string LockDigest { get; }

    public StatechartAssets()
    {
        _manifest = ReadResource("Statechart/manifest.json");
        Digest = Hash(_manifest);
        using var json = JsonDocument.Parse(_manifest);
        if (json.RootElement.GetProperty("schema").GetInt32() != 1 ||
            json.RootElement.GetProperty("protocol").GetInt32() != 1)
            throw new InvalidDataException("Unsupported statechart payload protocol.");
        LockDigest = json.RootElement.GetProperty("dependencyLockSha256").GetString()!;
        if (Hash(ReadResource("Statechart/lock.json")) != LockDigest)
            throw new InvalidDataException("Embedded statechart dependency lock does not match its payload.");
        ValidateEmbedded();
    }

    public string Extract(string root)
    {
        var versionRoot = Path.Combine(root, "3.5.1");
        StatechartPaths.EnsureDirectory(versionRoot);
        var target = Path.Combine(versionRoot, Digest);
        if (Directory.Exists(target))
        {
            Verify(target);
            return target;
        }
        var staging = Path.Combine(versionRoot, ".extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        using var json = JsonDocument.Parse(_manifest);
        foreach (var row in json.RootElement.GetProperty("files").EnumerateArray())
        {
            var name = row.GetProperty("path").GetString()!;
            var destination = Contained(staging, name);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            output.Write(ReadResource(row.GetProperty("resource").GetString()!));
        }
        File.WriteAllText(Path.Combine(staging, ".statechart-receipt"), Digest);
        Verify(staging);
        try { Directory.Move(staging, target); }
        catch (IOException) when (Directory.Exists(target))
        {
            Verify(target);
            // Only this attempt's fully verified staging can be removed.
            Directory.Delete(staging, recursive: true);
        }
        return target;
    }

    public void Verify(string directory)
    {
        StatechartPaths.RequirePlainDirectory(directory);
        if (File.ReadAllText(Path.Combine(directory, ".statechart-receipt")) != Digest)
            throw new IOException("Foreign or incompatible statechart asset directory: " + directory);
        using var json = JsonDocument.Parse(_manifest);
        var expected = json.RootElement.GetProperty("files").EnumerateArray()
            .ToDictionary(row => row.GetProperty("path").GetString()!, row => row.GetProperty("sha256").GetString()!);
        expected.Add(".statechart-receipt", Hash(System.Text.Encoding.UTF8.GetBytes(Digest)));
        VerifyDirectory(directory, directory, expected);
        if (expected.Count != 0) throw new IOException("Missing statechart assets: " + string.Join(", ", expected.Keys));
    }

    public byte[] DependencyLock() => ReadResource("Statechart/lock.json");

    public void VerifySkill(string directory)
    {
        StatechartPaths.RequirePlainDirectory(directory);
        const string prefix = "skills/engineering/statechart/";
        using var json = JsonDocument.Parse(_manifest);
        var expected = json.RootElement.GetProperty("files").EnumerateArray()
            .Where(row => row.GetProperty("path").GetString()!.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(row => row.GetProperty("path").GetString()![prefix.Length..], row => row.GetProperty("sha256").GetString()!);
        VerifyDirectory(directory, directory, expected, ".statechart-skill.json");
        if (expected.Count != 0) throw new IOException("Missing installed statechart skill files: " + string.Join(", ", expected.Keys));
    }

    private static void VerifyDirectory(string root, string directory, Dictionary<string, string> expected, string? excluded = null)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked statechart asset: " + path);
            if ((attributes & FileAttributes.Directory) != 0) VerifyDirectory(root, path, expected, excluded);
            else
            {
                var name = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (name == excluded) continue;
                if (!expected.Remove(name, out var digest) || Hash(File.ReadAllBytes(path)) != digest)
                    throw new IOException("Foreign or changed statechart asset: " + name);
            }
        }
    }

    private void ValidateEmbedded()
    {
        using var json = JsonDocument.Parse(_manifest);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in json.RootElement.GetProperty("files").EnumerateArray())
        {
            var name = row.GetProperty("path").GetString()!;
            _ = Contained(Path.GetTempPath(), name);
            if (!names.Add(name)) throw new InvalidDataException("Duplicate statechart resource path: " + name);
            var content = ReadResource(row.GetProperty("resource").GetString()!);
            if (content.Length != row.GetProperty("bytes").GetInt32() || Hash(content) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("Embedded statechart payload mismatch: " + name);
        }
    }

    private byte[] ReadResource(string name)
    {
        using var stream = _assembly.GetManifestResourceStream(name) ??
            throw new InvalidDataException("Missing installed statechart resource: " + name);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static string Contained(string root, string name)
    {
        if (name.Split('/').Any(part => part is "" or "." or "..") || name.Contains('\\') || name.Contains(':'))
            throw new InvalidDataException("Invalid statechart payload path: " + name);
        return Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
