namespace DynaDocs.Services.Statechart;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using DynaDocs.Utils;

internal sealed class StatechartAssets
{
    private readonly Assembly _assembly = typeof(StatechartAssets).Assembly;
    private readonly IReadOnlyDictionary<string, byte[]> _replaced;
    private readonly byte[] _manifest;
    public string Digest { get; }
    public string LockDigest { get; }
    public string Release { get; }
    // The pinned tla2tools.jar every checker input must match, from this payload's dependency lock.
    public string CheckerDigest { get; }

    public StatechartAssets() : this(new Dictionary<string, byte[]>()) { }

    // A test-built asset set: these resources replace the embedded ones and face the same checks, so a manifest must
    // name its lock's digest and every file it lists.
    internal StatechartAssets(IReadOnlyDictionary<string, byte[]> replaced)
    {
        _replaced = replaced;
        _manifest = ReadResource("Statechart/manifest.json");
        Digest = Hash(_manifest);
        using var json = JsonDocument.Parse(_manifest);
        if (json.RootElement.GetProperty("schema").GetInt32() != 1 ||
            json.RootElement.GetProperty("protocol").GetInt32() != 1)
            throw new InvalidDataException("Unsupported statechart payload protocol.");
        Release = json.RootElement.GetProperty("release").GetString()!;
        if (Release != DydoVersion.Current)
            throw new InvalidDataException($"Statechart payload release {Release} differs from dydo {DydoVersion.Current}.");
        LockDigest = json.RootElement.GetProperty("dependencyLockSha256").GetString()!;
        if (Hash(ReadResource("Statechart/lock.json")) != LockDigest)
            throw new InvalidDataException("Embedded statechart dependency lock does not match its payload.");
        using var dependencyLock = JsonDocument.Parse(DependencyLock());
        CheckerDigest = dependencyLock.RootElement.GetProperty("tlc").GetProperty("sha256").GetString()!;
        ValidateEmbedded();
    }

    // This payload's verified extraction. Under this payload's receipt, damaged content is extracted again offline
    // from the embedded bytes; a directory without that receipt is not dydo's and stays untouched.
    public async Task<string> ExtractAsync(string root, CancellationToken token)
    {
        var versionRoot = Path.Combine(root, Release);
        StatechartPaths.EnsureDirectory(versionRoot);
        var target = Path.Combine(versionRoot, Digest);
        await using var held = await StatechartLock.AcquireAsync(Path.Combine(versionRoot, ".extract.lock"), token);
        // Every extraction runs under this lock, so a staging directory found here was left by a crash.
        foreach (var stale in Directory.EnumerateDirectories(versionRoot, ".extract-*")
                     .Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0))
            Directory.Delete(stale, recursive: true);
        if (Directory.Exists(target) && Intact(target)) return target;
        var staging = Path.Combine(versionRoot, ".extract-" + Guid.NewGuid().ToString("N"));
        var damaged = staging + "-damaged";
        try
        {
            Write(staging);
            Verify(staging);
            if (Directory.Exists(target)) Directory.Move(target, damaged);
            Directory.Move(staging, target);
        }
        finally
        {
            foreach (var path in new[] { staging, damaged }.Where(Directory.Exists)) Directory.Delete(path, recursive: true);
        }
        return target;
    }

    // False when this payload's receipt marks the directory as dydo's own extraction but its content fails verification.
    private bool Intact(string directory)
    {
        var receipt = new FileInfo(Path.Combine(directory, ".statechart-receipt"));
        StatechartPaths.RequirePlainDirectory(directory);
        if (!receipt.Exists || (receipt.Attributes & FileAttributes.ReparsePoint) != 0 || receipt.Length > 1024 ||
            File.ReadAllText(receipt.FullName) != Digest)
            throw new IOException($"Foreign statechart asset directory retained: {directory}. Move it aside; dydo then extracts its payload there again.");
        try
        {
            Verify(directory);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void Write(string staging)
    {
        Directory.CreateDirectory(staging);
        using var json = JsonDocument.Parse(_manifest);
        foreach (var row in json.RootElement.GetProperty("files").EnumerateArray())
        {
            var destination = Contained(staging, row.GetProperty("path").GetString()!);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            output.Write(ReadResource(row.GetProperty("resource").GetString()!));
        }
        File.WriteAllText(Path.Combine(staging, ".statechart-receipt"), Digest);
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
        if (_replaced.TryGetValue(name, out var replaced)) return replaced;
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
