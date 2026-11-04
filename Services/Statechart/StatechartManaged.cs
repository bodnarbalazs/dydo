namespace DynaDocs.Services.Statechart;

using System.Text.Json.Nodes;

internal sealed class StatechartManaged(StatechartHome home, StatechartAssets assets, string rid)
{
    private readonly StatechartDownload[] _downloads = StatechartDownloads.Select(assets, rid);
    public string Root { get; } = Path.Combine(home.Dependencies, assets.LockDigest, rid);

    public StatechartDependencies Read()
    {
        if (!Directory.Exists(Root))
        {
            if (new DirectoryInfo(Root).LinkTarget is not null) throw new IOException("Linked managed statechart root.");
            return new(null, null, null, null);
        }
        ValidateRoot();
        var paths = new Dictionary<string, string?>();
        foreach (var item in _downloads)
        {
            var directory = Path.Combine(Root, item.Name);
            if (!Directory.Exists(directory)) { paths[item.Name] = null; continue; }
            StatechartPaths.RequirePlainDirectory(directory);
            var receipt = Path.Combine(directory, ".ready.json");
            if ((File.GetAttributes(receipt) & FileAttributes.ReparsePoint) != 0 || new FileInfo(receipt).Length > 8 * 1024 * 1024)
                throw new IOException("Linked or oversized managed statechart receipt.");
            var identity = JsonNode.Parse(File.ReadAllBytes(receipt))!.AsObject();
            if (identity["schema"]?.GetValue<int>() != 1 || identity["protocol"]?.GetValue<int>() != 1 ||
                identity["release"]?.GetValue<string>() != assets.Release ||
                identity["rid"]?.GetValue<string>() != rid || identity["payload"]?.GetValue<string>() != assets.Digest ||
                identity["lock"]?.GetValue<string>() != assets.LockDigest || identity["integrity"]?.GetValue<string>() != item.Integrity ||
                !JsonNode.DeepEquals(identity["files"], StatechartInventory.Capture(directory, ".ready.json")))
                throw new IOException("Managed statechart content changed; an explicit repair plan is required.");
            StatechartDownloads.Verify(item, Archive(directory, item));
            paths[item.Name] = Executable(directory, item.Name);
        }
        return new(paths["node"], paths["java"], paths["tlc"], paths["pele"]);
    }

    public async Task<StatechartDependencies> InstallAsync(StatechartDependencies selected, string payload, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(10));
        var parent = Path.GetDirectoryName(Root)!;
        StatechartPaths.EnsureDirectory(parent);
        using var installationLock = new FileStream(Path.Combine(parent, rid + ".install.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var retained = Read();
        selected = Combine(selected, retained);
        var missing = Missing(selected);
        if (missing.Length == 0) return selected;
        if (!Directory.Exists(Root))
        {
            Directory.CreateDirectory(Root);
            using var marker = new FileStream(Path.Combine(Root, ".statechart-store"), FileMode.CreateNew, FileAccess.Write);
            marker.Write(System.Text.Encoding.UTF8.GetBytes(assets.LockDigest));
        }
        ValidateRoot();
        var stage = Path.Combine(parent, ".install-" + rid + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var installed = new Dictionary<string, string>();
        foreach (var item in _downloads.Where(item => missing.Contains(item.Name, StringComparer.Ordinal)))
        {
            var component = Path.Combine(stage, item.Name);
            Directory.CreateDirectory(component);
            var archive = Archive(component, item);
            await StatechartDownloads.FetchAsync(item, archive, deadline.Token);
            StatechartDownloads.Verify(item, archive);
            if (item.Name != "tlc") StatechartArchive.Extract(archive, Path.Combine(component, "content"), deadline.Token);
            installed[item.Name] = Executable(component, item.Name);
        }
        var candidate = new StatechartDependencies(installed.GetValueOrDefault("node") ?? selected.Node,
            installed.GetValueOrDefault("java") ?? selected.Java, installed.GetValueOrDefault("tlc") ?? selected.Tlc,
            installed.GetValueOrDefault("pele") ?? selected.Renderer);
        var readiness = await StatechartReady.VerifyAsync(candidate, payload, home, full: true, deadline.Token);
        foreach (var item in _downloads.Where(item => installed.ContainsKey(item.Name)))
        {
            var component = Path.Combine(stage, item.Name);
            var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = assets.Release, ["rid"] = rid,
                ["payload"] = assets.Digest, ["lock"] = assets.LockDigest, ["integrity"] = item.Integrity,
                ["url"] = item.Url, ["setupUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["readiness"] = readiness.DeepClone(), ["files"] = StatechartInventory.Capture(component) };
            using (var stream = new FileStream(Path.Combine(component, ".ready.json"), FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(System.Text.Encoding.UTF8.GetBytes(receipt.ToJsonString()));
                stream.Flush(flushToDisk: true);
            }
            Directory.Move(component, Path.Combine(Root, item.Name));
        }
        Directory.Delete(stage, recursive: false);
        return Combine(selected, Read());
    }

    internal static string[] Missing(StatechartDependencies dependencies) =>
        new[] { ("node", dependencies.Node), ("java", dependencies.Java), ("tlc", dependencies.Tlc), ("pele", dependencies.Renderer) }
            .Where(item => item.Item2 is null).Select(item => item.Item1).ToArray();

    internal static StatechartDependencies Combine(StatechartDependencies selected, StatechartDependencies managed) =>
        new(selected.Node ?? managed.Node, selected.Java ?? managed.Java, selected.Tlc ?? managed.Tlc, selected.Renderer ?? managed.Renderer);

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
        return target;
    }

    private void ValidateRoot()
    {
        StatechartPaths.RequirePlainDirectory(Root);
        if ((File.GetAttributes(Path.Combine(Root, ".statechart-store")) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked statechart store marker retained.");
        if (File.ReadAllText(Path.Combine(Root, ".statechart-store")) != assets.LockDigest)
            throw new IOException("Foreign statechart dependency destination retained.");
        var allowed = _downloads.Select(item => item.Name).Append(".statechart-store").ToHashSet(StringComparer.Ordinal);
        if (Directory.EnumerateFileSystemEntries(Root).Any(path => !allowed.Contains(Path.GetFileName(path)) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                Path.GetFileName(path) != ".statechart-store" && !Directory.Exists(path)))
            throw new IOException("Foreign content in statechart dependency destination retained.");
    }
}
