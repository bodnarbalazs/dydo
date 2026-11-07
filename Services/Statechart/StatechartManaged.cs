namespace DynaDocs.Services.Statechart;

using System.Text.Json;
using System.Text.Json.Nodes;

// Readiness keys on the lock entry, the verified content and the RID. Release and payload only record
// the last verification, so an update that keeps the lock re-stamps instead of reinstalling.
internal sealed class StatechartManaged(StatechartHome home, StatechartAssets assets, string rid, StatechartDownload[] downloads)
{
    private readonly StatechartDownload[] _downloads = downloads;
    private StatechartManagedState? _state;
    public string Root { get; } = Path.Combine(home.Dependencies, assets.LockDigest, rid);

    public StatechartManaged(StatechartHome home, StatechartAssets assets, string rid)
        : this(home, assets, rid, StatechartDownloads.Select(assets, rid)) { }

    public StatechartDependencies Read() => Inspect().Dependencies;

    public StatechartManagedState Inspect() => _state ??= Scan();

    public async Task<StatechartDependencies> InstallAsync(StatechartDependencies selected, string payload, CancellationToken token)
    {
        var parent = Path.GetDirectoryName(Root)!;
        StatechartPaths.EnsureDirectory(parent);
        using (await Lock(parent, token))
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            SweepStages(parent);
            var state = Scan();
            selected = Combine(selected, state.Dependencies);
            var missing = Missing(selected);
            if (missing.Length == 0) return selected;
            var stage = Path.Combine(parent, ".install-" + rid + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                if (!Directory.Exists(Root)) CreateRoot(stage);
                ValidateRoot();
                var installed = await FetchAsync(missing, stage, deadline.Token);
                var candidate = Combine(new StatechartDependencies(installed.GetValueOrDefault("node"),
                    installed.GetValueOrDefault("java"), installed.GetValueOrDefault("tlc"), installed.GetValueOrDefault("pele")), selected);
                var readiness = await StatechartReady.VerifyAsync(candidate, payload, home, assets.CheckerDigest, full: true, deadline.Token);
                foreach (var item in _downloads.Where(item => installed.ContainsKey(item.Name)))
                    Place(item, Path.Combine(stage, item.Name), stage, readiness);
                foreach (var name in Exercised(state, candidate)) Restamp(Path.Combine(Root, name), parent, readiness);
            }
            finally
            {
                _state = null;
                RemoveStage(stage);
            }
        }
        return Combine(selected, Read());
    }

    // An update that kept the lock: prove the selection with this payload, then re-stamp the stale components it ran.
    // False when there are none, such as after waiting for another run's re-stamp or under an explicit override.
    public async Task<bool> RestampAsync(StatechartDependencies selected, string payload, CancellationToken token)
    {
        if (Exercised(Inspect(), selected).Length == 0) return false;
        var parent = Path.GetDirectoryName(Root)!;
        using (await Lock(parent, token))
        {
            SweepStages(parent);
            _state = null;
            var state = Inspect();
            selected = Combine(selected, state.Dependencies);
            var stale = Exercised(state, selected);
            if (stale.Length == 0) return false;
            var readiness = await StatechartReady.VerifyAsync(selected, payload, home, assets.CheckerDigest, full: true, token);
            foreach (var name in stale) Restamp(Path.Combine(Root, name), parent, readiness);
            _state = null;
            return true;
        }
    }

    internal static string[] Missing(StatechartDependencies dependencies) =>
        Components(dependencies).Where(item => item.Path is null).Select(item => item.Name).ToArray();

    private static (string Name, string? Path)[] Components(StatechartDependencies dependencies) =>
        [("node", dependencies.Node), ("java", dependencies.Java), ("tlc", dependencies.Tlc), ("pele", dependencies.Renderer)];

    // Stale components the selection runs from this store; a readiness proof says nothing about one an override replaced.
    private static string[] Exercised(StatechartManagedState state, StatechartDependencies selected)
    {
        var managed = Components(state.Dependencies);
        return Components(selected).Where((item, index) => state.Stale.Contains(item.Name) && item.Path == managed[index].Path)
            .Select(item => item.Name).ToArray();
    }

    internal static StatechartDependencies Combine(StatechartDependencies selected, StatechartDependencies managed) =>
        new(selected.Node ?? managed.Node, selected.Java ?? managed.Java, selected.Tlc ?? managed.Tlc, selected.Renderer ?? managed.Renderer);

    private StatechartManagedState Scan()
    {
        var paths = _downloads.ToDictionary(item => item.Name, _ => (string?)null);
        if (!Directory.Exists(Root))
        {
            if (new DirectoryInfo(Root).LinkTarget is not null) throw Foreign("is a link");
            return new(new(null, null, null, null), [], []);
        }
        ValidateRoot();
        var stale = new List<string>();
        var damaged = new List<string>();
        foreach (var item in _downloads.Where(item => Directory.Exists(Path.Combine(Root, item.Name))))
        {
            var verified = Verified(item, Path.Combine(Root, item.Name));
            if (verified is not { } component) { damaged.Add(item.Name); continue; }
            if (component.Identity["release"]?.GetValue<string>() != assets.Release ||
                component.Identity["payload"]?.GetValue<string>() != assets.Digest)
                stale.Add(item.Name);
            paths[item.Name] = component.Executable;
        }
        return new(new(paths["node"], paths["java"], paths["tlc"], paths["pele"]), stale.ToArray(), damaged.ToArray());
    }

    // The receipt and executable while the lock entry, RID and content still match the receipt; otherwise null.
    private (JsonObject Identity, string Executable)? Verified(StatechartDownload item, string directory)
    {
        try
        {
            StatechartPaths.RequirePlainDirectory(directory);
            var receipt = new FileInfo(Path.Combine(directory, ".ready.json"));
            if (!receipt.Exists || (receipt.Attributes & FileAttributes.ReparsePoint) != 0 || receipt.Length > 8 * 1024 * 1024 ||
                JsonNode.Parse(File.ReadAllBytes(receipt.FullName)) is not JsonObject identity ||
                identity["schema"]?.GetValue<int>() != 1 || identity["protocol"]?.GetValue<int>() != 1 ||
                identity["rid"]?.GetValue<string>() != rid || identity["lock"]?.GetValue<string>() != assets.LockDigest ||
                identity["integrity"]?.GetValue<string>() != item.Integrity ||
                !JsonNode.DeepEquals(identity["files"], StatechartInventory.Capture(directory, ".ready.json")))
                return null;
            StatechartDownloads.Verify(item, Archive(directory, item));
            return (identity, Executable(directory, item.Name));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<Dictionary<string, string>> FetchAsync(string[] missing, string stage, CancellationToken token)
    {
        var installed = new Dictionary<string, string>();
        foreach (var item in _downloads.Where(item => missing.Contains(item.Name, StringComparer.Ordinal)))
        {
            var component = Path.Combine(stage, item.Name);
            Directory.CreateDirectory(component);
            var archive = Archive(component, item);
            await StatechartDownloads.FetchAsync(item, archive, token);
            StatechartDownloads.Verify(item, archive);
            if (item.Name != "tlc") StatechartArchive.Extract(archive, Path.Combine(component, "content"), token);
            installed[item.Name] = Executable(component, item.Name);
        }
        return installed;
    }

    // Moves a verified component into the store; a component that failed verification moves into the stage it is deleted with.
    private void Place(StatechartDownload item, string component, string stage, JsonObject readiness)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = assets.Release, ["rid"] = rid,
            ["payload"] = assets.Digest, ["lock"] = assets.LockDigest, ["integrity"] = item.Integrity,
            ["url"] = item.Url, ["setupUtc"] = now, ["verifiedUtc"] = now,
            ["readiness"] = readiness.DeepClone(), ["files"] = StatechartInventory.Capture(component) };
        using (var stream = new FileStream(Path.Combine(component, ".ready.json"), FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(System.Text.Encoding.UTF8.GetBytes(receipt.ToJsonString()));
            stream.Flush(flushToDisk: true);
        }
        var target = Path.Combine(Root, item.Name);
        if (Directory.Exists(target)) Directory.Move(target, Path.Combine(stage, item.Name + ".replaced"));
        Directory.Move(component, target);
    }

    private void Restamp(string component, string parent, JsonObject readiness)
    {
        var path = Path.Combine(component, ".ready.json");
        var identity = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        identity["release"] = assets.Release;
        identity["payload"] = assets.Digest;
        identity["verifiedUtc"] = DateTimeOffset.UtcNow.ToString("O");
        identity["readiness"] = readiness.DeepClone();
        var temp = Path.Combine(parent, ".receipt-" + rid + "-" + Guid.NewGuid().ToString("N"));
        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(System.Text.Encoding.UTF8.GetBytes(identity.ToJsonString()));
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }

    private Task<FileStream> Lock(string parent, CancellationToken token) =>
        StatechartLock.AcquireAsync(Path.Combine(parent, rid + ".install.lock"), token);

    // Runs under the install lock, so no other run for this RID still uses these stages or receipt files.
    private void SweepStages(string parent)
    {
        foreach (var stage in Directory.EnumerateDirectories(parent, ".install-" + rid + "-*"))
            if ((File.GetAttributes(stage) & FileAttributes.ReparsePoint) == 0) Directory.Delete(stage, recursive: true);
        foreach (var receipt in Directory.EnumerateFiles(parent, ".receipt-" + rid + "-*"))
            if ((File.GetAttributes(receipt) & FileAttributes.ReparsePoint) == 0) File.Delete(receipt);
    }

    // The root appears with its marker in one rename, so no crash leaves a root that every later run refuses.
    private void CreateRoot(string stage)
    {
        var fresh = Path.Combine(stage, "store");
        Directory.CreateDirectory(fresh);
        File.WriteAllText(Path.Combine(fresh, ".statechart-store"), assets.LockDigest);
        Directory.Move(fresh, Root);
    }

    private static void RemoveStage(string stage)
    {
        try { Directory.Delete(stage, recursive: true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file still in use keeps this stage until the next install sweeps it.
        }
    }

    private static string Archive(string component, StatechartDownload item) =>
        Path.Combine(component, Path.GetFileName(new Uri(item.Url).AbsolutePath));

    private string Executable(string component, string name)
    {
        if (name == "tlc") return Path.Combine(component, "tla2tools.jar");
        if (name == "pele") return Path.Combine(component, "content/package/dist/pele.js");
        var content = Path.Combine(component, "content");
        var top = Directory.GetDirectories(content).Single();
        var relative = name == "node" ? "bin/node" : "bin/java";
        if (rid == "win-x64") relative = name == "node" ? "node.exe" : "bin/java.exe";
        else if (name == "java" && rid.StartsWith("osx-", StringComparison.Ordinal)) relative = "Contents/Home/bin/java";
        var target = Path.Combine(top, relative);
        if (!File.Exists(target)) throw new IOException("Locked " + name + " archive has no expected executable: " + relative);
        // Receipts record content hashes, not modes, so a runtime that lost its execute bit still matches one.
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(target) & UnixFileMode.UserExecute) == 0)
            throw new IOException("Managed " + name + " cannot execute: " + relative);
        return target;
    }

    private void ValidateRoot()
    {
        StatechartPaths.RequirePlainDirectory(Root);
        var marker = new FileInfo(Path.Combine(Root, ".statechart-store"));
        if (!marker.Exists || (marker.Attributes & FileAttributes.ReparsePoint) != 0 || marker.Length > 1024 ||
            File.ReadAllText(marker.FullName) != assets.LockDigest)
            throw Foreign("has no store marker for this dependency lock");
        var allowed = _downloads.Select(item => item.Name).Append(".statechart-store").ToHashSet(StringComparer.Ordinal);
        var foreign = Directory.EnumerateFileSystemEntries(Root).Where(path => !allowed.Contains(Path.GetFileName(path)) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                Path.GetFileName(path) != ".statechart-store" && !Directory.Exists(path))
            .Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        if (foreign.Length != 0) throw Foreign("holds entries dydo did not install: " + string.Join(", ", foreign));
    }

    private IOException Foreign(string problem) => new($"The statechart dependency store {Root} {problem}; dydo leaves it untouched. " +
        "Move it aside, then run the chart again for a fresh locked install plan.");
}
