namespace DynaDocs.Tests.Statechart;

using System.Text.RegularExpressions;
using DynaDocs.Services.Statechart;
using DynaDocs.Utils;

public class StatechartAssetsTests
{
    [Fact]
    public async Task InstalledPayloadExtractsOfflineAndReusesExactBytes()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var first = await assets.ExtractAsync(scope.Path, CancellationToken.None);
        Assert.Equal(first, await assets.ExtractAsync(scope.Path, CancellationToken.None));
        Assert.True(File.Exists(Path.Combine(first, "skills/engineering/statechart/scripts/worker.ts")));
        Assert.True(File.Exists(Path.Combine(first, "setup-skills.mjs")));
        Assert.True(File.Exists(Path.Combine(first, "skills/engineering/statechart/scripts/java/statechart-tlc-bootstrap.jar")),
            "The sealed Java11 helper must be in the verified payload; no raw-TLC fallback.");
        Assert.Equal("3ad231072bddc04588d0f2cf1b61a3e62fc38b4ff400ccea5c3b5d41409575ab", assets.LockDigest);
    }

    // The receipt shows the directory is this payload's own extraction, so the embedded bytes restore it offline.
    [Theory]
    [InlineData("changed")]
    [InlineData("deleted")]
    [InlineData("added")]
    [InlineData("changed and deleted")]
    public async Task DamagedContentUnderThisPayloadsReceiptIsExtractedAgainFromTheEmbeddedBytes(string damage)
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var folder = await assets.ExtractAsync(scope.Path, CancellationToken.None);
        var helper = Path.Combine(folder, "skills/engineering/statechart/scripts/java/statechart-tlc-bootstrap.jar");
        var setup = Path.Combine(folder, "setup-skills.mjs");
        var (helperBytes, setupBytes) = (File.ReadAllBytes(helper), File.ReadAllBytes(setup));
        if (damage.Contains("changed")) File.AppendAllText(setup, "changed");
        if (damage.Contains("deleted")) File.Delete(helper);
        if (damage == "added") File.WriteAllText(Path.Combine(folder, "foreign.txt"), "added");
        Assert.Equal(folder, await assets.ExtractAsync(scope.Path, CancellationToken.None));
        assets.Verify(folder);
        Assert.Equal(helperBytes, File.ReadAllBytes(helper));
        Assert.Equal(setupBytes, File.ReadAllBytes(setup));
        Assert.Equal([".extract.lock", assets.Digest],
            Directory.GetFileSystemEntries(Path.GetDirectoryName(folder)!).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADirectoryWithoutThisPayloadsReceiptIsRetainedAndRejected(bool otherReceipt)
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var target = Path.Combine(scope.Path, assets.Release, assets.Digest);
        Directory.CreateDirectory(target);
        var sentinel = Path.Combine(target, "setup-skills.mjs");
        File.WriteAllText(sentinel, "foreign sentinel");
        if (otherReceipt) File.WriteAllText(Path.Combine(target, ".statechart-receipt"), "another payload");
        var error = await Assert.ThrowsAsync<IOException>(() => assets.ExtractAsync(scope.Path, CancellationToken.None));
        Assert.Contains(target, error.Message);
        Assert.Equal("foreign sentinel", File.ReadAllText(sentinel));
    }

    [Fact]
    public async Task AnExtractionACrashLeftBehindIsSwept()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var stale = Path.Combine(scope.Path, assets.Release, ".extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "partial"), "interrupted");
        await assets.ExtractAsync(scope.Path, CancellationToken.None);
        Assert.False(Directory.Exists(stale));
    }

    [Fact]
    public void ThePayloadReleaseIsTheProductVersion()
    {
        Assert.Equal(DydoVersion.Current, new StatechartAssets().Release);
    }

    // Requests, receipts and the asset root take the release from the assets, never from a literal of their own.
    [Fact]
    public void NoStatechartSourceCarriesItsOwnReleaseLiteral()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "DynaDocs.csproj"))) root = root.Parent!;
        var literals = Directory.GetFiles(Path.Combine(root.FullName, "Services", "Statechart"), "*.cs")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "\"\\d+\\.\\d+\\.\\d+\"")
                .Select(match => Path.GetFileName(file) + ": " + match.Value))
            .ToArray();
        Assert.Empty(literals);
    }
}
