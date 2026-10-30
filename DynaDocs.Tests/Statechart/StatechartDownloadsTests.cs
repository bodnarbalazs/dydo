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
        Assert.Equal("https://nodejs.org/dist/v22.23.2/node-v22.23.2-win-x64.zip", downloads[0].Url);
        Assert.Equal("1177b4137ba5adaa56354ae40f1080c7450e8ae09cecb47da459d1c52ac99f97", downloads[0].Integrity);
        Assert.Equal(128L * 1024 * 1024, downloads[0].Limit);
        Assert.Equal(96L * 1024 * 1024, downloads[1].Limit);
        Assert.StartsWith("sha512-", downloads[3].Integrity);
        Assert.Throws<IOException>(() => StatechartDownloads.Select(assets, "win-arm64"));
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
