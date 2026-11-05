namespace DynaDocs.Tests.Statechart;

using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DynaDocs.Services.Statechart;

// Dependency-free stand-ins for the managed runtimes, the checker jar and the dependency lock. Each one still faces the
// product's own checks: archives are fetched, verified and extracted, and the jar's identity is whatever lock pins it.
internal static class StatechartStandIns
{
    // Upstream TLC recordings from the statechart corpus: a clean run and an invariant violation.
    public static string CleanRun => Recording("CounterStop/verdict-1.out");
    public static string ViolatedRun => Recording("CounterAction/verdict-1.out");

    // A jar the checker hashes but never runs.
    public static byte[] Jar()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("META-INF/MANIFEST.MF");
            entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var writer = new StreamWriter(entry.Open());
            writer.Write("Manifest-Version: 1.0\nCreated-By: dydo checker stand-in\n");
        }
        return buffer.ToArray();
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    // Answers the version probe and the native bootstrap's startup gate, then exits with the given render status.
    public static string Node(string version = "v26.13.0", int render = 0) =>
        $"#!/bin/sh\nif [ \"$1\" = --version ]; then echo {version}; exit 0; fi\n" +
        $"echo \"ready:1:$3\"\nread -r go\n[ \"$go\" = \"go:1:$3\" ] || exit 9\nexit {render}\n";

    // Answers the version probe, SANY and TLC the way readiness calls them; TLC replays a recorded run. Every call is logged.
    public static string Java(string log, string tlc, string version = "21.0.8", int sany = 0) =>
        $"#!/bin/sh\necho \"$*\" >> '{log}'\ncase \"$*\" in\n" +
        $"  -version) echo 'openjdk version \"{version}\" 2025-07-15 LTS' >&2 ;;\n" +
        $"  *tla2sany.SANY*) echo 'SANY finished.'; exit {sany} ;;\n" +
        $"  *dydo.statechart.LoopbackTlc*) /bin/cat '{tlc}' ;;\n" +
        "  *) exit 64 ;;\nesac\n";

    [UnsupportedOSPlatform("windows")]
    public static string Executable(string path, string script)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, script);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    // A gzip tar of the given files, in the layout the locked runtime archives use.
    public static byte[] TarGz(params (string Path, string Content)[] files)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
                };
                tar.WriteEntry(entry);
            }
        }
        return buffer.ToArray();
    }

    // The real embedded payload under a test-built lock: the manifest names the lock's digest, so every product check holds.
    public static StatechartAssets Assets(JsonObject dependencyLock)
    {
        var lockBytes = Encoding.UTF8.GetBytes(dependencyLock.ToJsonString());
        using var stream = typeof(StatechartAssets).Assembly.GetManifestResourceStream("Statechart/manifest.json")!;
        var manifest = JsonNode.Parse(stream)!.AsObject();
        manifest["dependencyLockSha256"] = Sha256(lockBytes);
        return new StatechartAssets(new Dictionary<string, byte[]>
        {
            ["Statechart/manifest.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString()),
            ["Statechart/lock.json"] = lockBytes
        });
    }

    // A lock for this platform naming the served archives; only the checker row matters when nothing is downloaded.
    public static JsonObject Lock(string root, IReadOnlyDictionary<string, byte[]> served, string checker) => new()
    {
        ["schema"] = 1, ["protocol"] = 1,
        ["platforms"] = new JsonArray(new JsonObject
        {
            ["rid"] = RuntimeInformation.RuntimeIdentifier,
            ["node"] = Row(root, "/node/node.tar.gz", served),
            ["java"] = Row(root, "/java/java.tar.gz", served)
        }),
        ["tlc"] = new JsonObject { ["version"] = "1.7.4", ["url"] = root + "/tlc/tla2tools.jar", ["sha256"] = checker },
        ["pele"] = new JsonObject { ["version"] = "1.9.0", ["url"] = root + "/pele/pele.tgz", ["entry"] = "dist/pele.js",
            ["integrity"] = served.TryGetValue("/pele/pele.tgz", out var pele) ? "sha512-" + Convert.ToBase64String(SHA512.HashData(pele)) : "sha512-none" }
    };

    // The four locked components as archives of stand-ins: Node and Java scripts, the jar, and a renderer module.
    public static Dictionary<string, byte[]> Components(string log, string tlc) => new()
    {
        ["/node/node.tar.gz"] = TarGz(("node-v26.13.0-linux/bin/node", Node())),
        ["/java/java.tar.gz"] = TarGz(("jdk-21.0.8+9-jre/bin/java", Java(log, tlc))),
        ["/tlc/tla2tools.jar"] = Jar(),
        ["/pele/pele.tgz"] = TarGz(("package/dist/pele.js", "export const render = () => '';\n"))
    };

    // Serves the given paths on loopback while the work runs; each request gets its own connection.
    public static async Task Serve(IReadOnlyDictionary<string, byte[]> files, Func<string, Task> work)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var stop = new CancellationTokenSource();
        var serving = Task.Run(async () =>
        {
            while (true)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(stop.Token); }
                catch (OperationCanceledException) { return; }
                using (client) await Respond(client, files);
            }
        });
        try { await work($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}"); }
        finally
        {
            await stop.CancelAsync();
            await serving;
        }
    }

    // Runs in-process work without the caller's runtimes: JAVA and TLA2TOOLS would become explicit overrides, and PATH
    // could offer a Node or Java of its own.
    public static async Task WithoutCallerRuntimes(Func<Task> work)
    {
        var saved = new[] { "JAVA", "TLA2TOOLS", "PATH" }.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        foreach (var name in saved.Keys) Environment.SetEnvironmentVariable(name, null);
        try { await work(); }
        finally
        {
            foreach (var (name, value) in saved) Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static JsonObject Row(string root, string path, IReadOnlyDictionary<string, byte[]> served) => new()
    {
        ["version"] = "stand-in", ["url"] = root + path,
        ["sha256"] = served.TryGetValue(path, out var bytes) ? Sha256(bytes) : new string('0', 64)
    };

    private static async Task Respond(TcpClient client, IReadOnlyDictionary<string, byte[]> files)
    {
        await using var stream = client.GetStream();
        var request = "";
        var buffer = new byte[4096];
        int read;
        while (!request.Contains("\r\n\r\n", StringComparison.Ordinal) && (read = await stream.ReadAsync(buffer)) > 0)
            request += Encoding.ASCII.GetString(buffer, 0, read);
        var fields = request.Split(' ');
        var body = fields.Length > 1 && files.TryGetValue(fields[1], out var found) ? found : null;
        var head = body is null ? "HTTP/1.1 404 Not Found\r\nContent-Length: 0" : $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head + "\r\nConnection: close\r\n\r\n").Concat(body ?? []).ToArray());
    }

    private static string Recording(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "skills", "engineering", "statechart", "scripts", "test", "corpus", name);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Missing upstream statechart corpus recording: " + name);
    }
}
