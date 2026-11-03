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
