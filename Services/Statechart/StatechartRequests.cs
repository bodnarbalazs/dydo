namespace DynaDocs.Services.Statechart;

using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class StatechartRequests
{
    private readonly string _root;
    private readonly StatechartAssets _assets;
    private readonly string _rid;
    private readonly string _dependencies;

    public StatechartRequests(StatechartHome home, StatechartAssets assets, string rid)
    {
        _root = Path.Combine(home.State, "requests");
        StatechartPaths.EnsureDirectory(_root);
        _assets = assets;
        _rid = rid;
        _dependencies = Path.Combine(home.Dependencies, assets.LockDigest, rid);
    }

    public string Save(StatechartRequest request)
    {
        StatechartRequest.Validate(request, _assets, _rid);
        var staging = Path.Combine(_root, ".pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        Restrict(staging);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, StatechartRequestJson.Default.StatechartRequest);
        Write(Path.Combine(staging, "request.json"), bytes);
        var provenance = new JsonObject { ["schema"] = 1, ["requestDigest"] = Hash(bytes), ["dependencies"] = _dependencies };
        Write(Path.Combine(staging, "identity.json"), System.Text.Encoding.UTF8.GetBytes(provenance.ToJsonString()));
        var target = Path.Combine(_root, request.Id);
        Directory.Move(staging, target);
        return Path.Combine(target, "request.json");
    }

    public StatechartRequest Load(string path, bool validateInputs = true)
    {
        var directory = DirectoryOf(path);
        var bytes = ReadBounded(path);
        var identity = JsonNode.Parse(ReadBounded(Path.Combine(directory, "identity.json")))!.AsObject();
        if (identity["schema"]?.GetValue<int>() != 1 || identity["requestDigest"]?.GetValue<string>() != Hash(bytes) ||
            identity["dependencies"]?.GetValue<string>() != _dependencies)
            throw new IOException("Saved statechart request or permitted dependency destination changed; prepare a fresh plan.");
        var request = JsonSerializer.Deserialize(bytes, StatechartRequestJson.Default.StatechartRequest)
            ?? throw new IOException("Empty statechart request.");
        if (request.Id != Path.GetFileName(directory)) throw new IOException("Saved statechart request identity mismatch.");
        StatechartRequest.ValidateIdentity(request, _assets, _rid);
        if (validateInputs) StatechartRequest.Validate(request, _assets, _rid);
        return request;
    }

    public FileStream Lock(string path) => new(Path.Combine(DirectoryOf(path), "execution.lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    public void Failure(string path, string reason)
    {
        var file = Path.Combine(DirectoryOf(path), "failure-" + Guid.NewGuid().ToString("N") + ".json");
        Write(file, System.Text.Encoding.UTF8.GetBytes(new JsonObject { ["state"] = "failed", ["reason"] = reason }.ToJsonString()));
    }

    public void Complete(string path, StatechartRequest request, int verdict)
    {
        if (verdict is not (0 or 1)) throw new InvalidOperationException("Only actual complete checker verdicts can complete a request.");
        if (!File.Exists(Path.Combine(request.Output, "report.md"))) throw new IOException("Missing completed statechart report.");
        var output = StatechartInventory.Capture(request.Output);
        var receipt = new JsonObject { ["schema"] = 1, ["verdict"] = verdict,
            ["requestDigest"] = Hash(ReadBounded(path)), ["output"] = output };
        var directory = DirectoryOf(path);
        var temp = Path.Combine(directory, ".completed-" + Guid.NewGuid().ToString("N"));
        Write(temp, System.Text.Encoding.UTF8.GetBytes(receipt.ToJsonString()));
        File.Move(temp, Path.Combine(directory, "completed.json"));
    }

    public int? Completed(string path, StatechartRequest request)
    {
        var file = Path.Combine(DirectoryOf(path), "completed.json");
        if (!File.Exists(file)) return null;
        var receipt = JsonNode.Parse(ReadBounded(file))!.AsObject();
        var verdict = receipt["verdict"]!.GetValue<int>();
        if (receipt["schema"]?.GetValue<int>() != 1 || verdict is not (0 or 1) ||
            receipt["requestDigest"]?.GetValue<string>() != Hash(ReadBounded(path)) ||
            !JsonNode.DeepEquals(receipt["output"], StatechartInventory.Capture(request.Output)))
            throw new IOException("Completed statechart output is stale, missing or changed; no replay success.");
        return verdict;
    }

    private string DirectoryOf(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new IOException("Resume requires an absolute request path.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var id = Path.GetFileName(directory);
        var expected = Path.Combine(_root, id, "request.json");
        if (!Guid.TryParseExact(id, "N", out _) || !string.Equals(Path.GetFullPath(path), expected,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Resume request is outside the configured statechart request store.");
        StatechartPaths.RequirePlainDirectory(directory);
        return directory;
    }

    private static byte[] ReadBounded(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > 8 * 1024 * 1024)
            throw new IOException("Linked or oversized statechart request/receipt.");
        return File.ReadAllBytes(path);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void Write(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void Restrict(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            using var user = WindowsIdentity.GetCurrent();
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(security);
        }
        else File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
