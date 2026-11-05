namespace DynaDocs.Tests.Statechart;

using System.Security.Cryptography;
using DynaDocs.Services.Statechart;

public class StatechartDownloadsTests
{
    [Fact]
    public void SelectUsesTheCommissionedLockAndRejectsUnsupportedRidsBeforeDownloading()
    {
        var assets = new StatechartAssets();
        var downloads = StatechartDownloads.Select(assets, "win-x64");
        Assert.Equal(["node", "java", "tlc", "pele"], downloads.Select(item => item.Name));
        Assert.Equal(128L * 1024 * 1024, downloads[0].Limit);
        Assert.Equal(96L * 1024 * 1024, downloads[1].Limit);
        Assert.StartsWith("sha512-", downloads[3].Integrity);
        Assert.Throws<IOException>(() => StatechartDownloads.Select(assets, "win-arm64"));
    }

    // Official nodejs.org SHASUMS256.txt for v26.13.0, signed by a Node.js release key.
    [Theory]
    [InlineData("win-x64", "node-v26.13.0-win-x64.zip", "dcc60d59a0746999f99064a01c020445d54d84228c691cdbf25b49ac3b59150a")]
    [InlineData("linux-x64", "node-v26.13.0-linux-x64.tar.gz", "5848553868b402d3fabb5b09270649977780a390f832155721025a08b5104455")]
    [InlineData("linux-arm64", "node-v26.13.0-linux-arm64.tar.gz", "439e9c438acdf6a6ffe47418da709153d66c4bed812e30a473faacb107dd2791")]
    [InlineData("osx-x64", "node-v26.13.0-darwin-x64.tar.gz", "525c9b29bb0263d687de60a4cec0e2a6db876a8eb032369e5795eca71e7c358e")]
    [InlineData("osx-arm64", "node-v26.13.0-darwin-arm64.tar.gz", "d38bd5e5a2ddce6bbb5ce45e46e208337da4d51fffe611e567fd32073d6439f6")]
    public void EveryPlatformPinsManagedNode26(string rid, string file, string sha256)
    {
        var node = StatechartDownloads.Select(new StatechartAssets(), rid)[0];
        Assert.Equal("node", node.Name);
        Assert.Equal("https://nodejs.org/dist/v26.13.0/" + file, node.Url);
        Assert.Equal(sha256, node.Integrity);
    }

    [Fact]
    public async Task StreamingBoundRejectsExcessWithoutWritingIt()
    {
        using var source = new MemoryStream(new byte[32]);
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<IOException>(() => StatechartDownloads.CopyBoundedAsync(source, output, 16, CancellationToken.None));
        Assert.Empty(output.ToArray());
    }

    [Fact]
    public async Task ADownloadWritesItsBoundedBodyToANewArchive()
    {
        using var scope = new StatechartTestDirectory();
        var target = Path.Combine(scope.Path, "archive");
        byte[] body = [1, 2, 3];
        await Serve("200 OK", "Content-Length: 3", body, url => StatechartDownloads.FetchAsync(Item(url, body, 3), target, CancellationToken.None));
        Assert.Equal(body, File.ReadAllBytes(target));
        StatechartDownloads.Verify(Item(new Uri("https://example.invalid/archive"), body, 3), target);
        await Serve("200 OK", "Content-Length: 3", body, async url =>
            await Assert.ThrowsAsync<IOException>(() => StatechartDownloads.FetchAsync(Item(url, body, 3), target, CancellationToken.None)));
        Assert.Equal(body, File.ReadAllBytes(target));
    }

    [Theory]
    [InlineData("Content-Length: 5")]
    [InlineData("Connection: close")]
    public async Task ABodyOverItsBoundIsRefusedWhetherDeclaredOrStreamed(string header)
    {
        using var scope = new StatechartTestDirectory();
        var target = Path.Combine(scope.Path, "archive");
        byte[] body = [1, 2, 3, 4, 5];
        await Serve("200 OK", header, body, async url =>
            await Assert.ThrowsAsync<IOException>(() => StatechartDownloads.FetchAsync(Item(url, body, 4), target, CancellationToken.None)));
        Assert.Equal(header.StartsWith("Content-Length", StringComparison.Ordinal), !File.Exists(target));
        if (File.Exists(target)) Assert.Empty(File.ReadAllBytes(target));
    }

    [Fact]
    public async Task AFailedResponseIsAnErrorAndWritesNothing()
    {
        using var scope = new StatechartTestDirectory();
        var target = Path.Combine(scope.Path, "archive");
        await Serve("404 Not Found", "Content-Length: 0", [], async url =>
            await Assert.ThrowsAsync<HttpRequestException>(() => StatechartDownloads.FetchAsync(Item(url, [], 4), target, CancellationToken.None)));
        Assert.False(File.Exists(target));
    }

    private static StatechartDownload Item(Uri url, byte[] body, long limit) =>
        new("fixture", url.ToString(), Convert.ToHexStringLower(SHA256.HashData(body)), limit);

    // A one-shot loopback responder: the download's only network peer.
    private static async Task Serve(string status, string header, byte[] body, Func<Uri, Task> fetch)
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var serving = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var request = "";
            var buffer = new byte[1024];
            int read;
            while (!request.Contains("\r\n\r\n", StringComparison.Ordinal) && (read = await stream.ReadAsync(buffer)) > 0)
                request += System.Text.Encoding.ASCII.GetString(buffer, 0, read);
            // One write: the client may close as soon as it has read the headers.
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n{header}\r\nConnection: close\r\n\r\n")
                .Concat(body).ToArray());
        });
        try { await fetch(new Uri($"http://127.0.0.1:{port}/archive.tar.gz")); }
        finally { listener.Stop(); }
        await serving;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IntegrityRejectsChangedOrOversizedArchivesWithoutRemovingThem(bool sha512)
    {
        using var scope = new StatechartTestDirectory();
        var path = Path.Combine(scope.Path, "archive");
        byte[] bytes = [1, 2, 3];
        File.WriteAllBytes(path, bytes);
        var hash = sha512 ? "sha512-" + Convert.ToBase64String(SHA512.HashData(bytes)) : Convert.ToHexStringLower(SHA256.HashData(bytes));
        var item = new StatechartDownload("fixture", "https://example.invalid/archive", hash, 4);
        StatechartDownloads.Verify(item, path);
        File.WriteAllBytes(path, [1, 2, 4]);
        Assert.Throws<IOException>(() => StatechartDownloads.Verify(item, path));
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
        Assert.Throws<IOException>(() => StatechartDownloads.Verify(item, path));
        Assert.True(File.Exists(path));
    }
}
