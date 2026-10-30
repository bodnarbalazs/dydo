namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DynaDocs.Services.Statechart;

public class StatechartLifecycleTests
{
    private static ProcessStartInfo Binary()
    {
        var binary = Environment.GetEnvironmentVariable("STATECHART_TEST_BINARY");
        Assert.True(!string.IsNullOrEmpty(binary) && File.Exists(binary),
            "STATECHART_TEST_BINARY must name the pinned real NativeAOT binary; no source fallback.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(binary + ".proof.json"));
        Assert.True(manifest.RootElement.GetProperty("nativeAot").GetBoolean());
        Assert.Equal("win-x64", manifest.RootElement.GetProperty("rid").GetString());
        Assert.Equal(manifest.RootElement.GetProperty("sha256").GetString(),
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(binary!))));
        return new ProcessStartInfo(binary!);
    }

    [Fact]
    public async Task IndependentCleanupDeletesRegisteredScratchOnlyAfterNativeOwnerSealsAndDrains()
    {
        using var scope = new StatechartTestDirectory();
        await using var lifetime = await WindowsStatechartLifetime.StartAsync(Binary(), scope.Path, scope.Path, scope.Path);
        Assert.Throws<InvalidOperationException>(() => lifetime.Attach(Environment.ProcessId));
        await lifetime.RegisterAsync();
        File.WriteAllText(Path.Combine(lifetime.Stage, "checking-file"), "owned");
        Assert.True(await lifetime.CompleteAsync());
        Assert.False(Directory.Exists(lifetime.Stage));
        Assert.False(Directory.Exists(lifetime.Temp));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(file)));
    }

    [Fact]
    public async Task OwnerEofRetainsRegisteredScratchWithDurableUncertaintyReason()
    {
        using var scope = new StatechartTestDirectory();
        var lifetime = await WindowsStatechartLifetime.StartAsync(Binary(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var stage = lifetime.Stage;
        await lifetime.DisposeAsync();
        Assert.True(Directory.Exists(stage));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            Assert.Contains("drain uncertain", File.ReadAllText(file)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptionBeforeOrDuringRegistrationNeverDeletesAmbiguousDirectories(bool reserve)
    {
        using var scope = new StatechartTestDirectory();
        var nonce = Guid.NewGuid().ToString("N");
        var start = Binary();
        start.ArgumentList.Add("__statechart-cleanup");
        foreach (var argument in new[] { nonce, scope.Path, scope.Path, scope.Path }) start.ArgumentList.Add(argument);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        using var helper = Process.Start(start)!;
        Assert.Equal("ready:1:" + nonce, await StatechartCleanup.Read(helper.StandardOutput));
        if (reserve)
        {
            await StatechartCleanup.Say(helper.StandardInput, "reserve:" + nonce);
            Assert.StartsWith("created:", await StatechartCleanup.Read(helper.StandardOutput));
        }
        helper.StandardInput.Close();
        await helper.WaitForExitAsync();
        Assert.Equal(2, helper.ExitCode);
        Assert.Equal(reserve ? 2 : 0, Directory.GetDirectories(scope.Path).Length);
    }
}
