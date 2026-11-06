namespace DynaDocs.Tests.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using DynaDocs.Services.Statechart;

public class StatechartLifecycleTests
{
    private static ProcessStartInfo Binary()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows Job Object lifetime runs only on Windows.");
        return StatechartTestBinary.Start();
    }

    // The cleanup helper's protocol and scratch handling are portable; only its native owner, the Job Object, is Windows-only.
    private static Process Cleanup(string nonce, StatechartTestDirectory scope, string? stageParent = null)
    {
        var start = StatechartTestBinary.Start();
        start.ArgumentList.Add("__statechart-cleanup");
        foreach (var argument in new[] { nonce, stageParent ?? scope.Path, scope.Path, scope.Path }) start.ArgumentList.Add(argument);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        return scope.Own(Process.Start(start)!);
    }

    // Reserves and registers through a helper, returning the scratch it created.
    private static async Task<(string Stage, string Temp)> Register(Process helper, string nonce)
    {
        Assert.Equal("ready:1:" + nonce, await StatechartCleanup.Read(helper.StandardOutput));
        await StatechartCleanup.Say(helper.StandardInput, "reserve:" + nonce);
        var created = (await StatechartCleanup.Read(helper.StandardOutput))!.Split(':');
        Assert.Equal("created", created[0]);
        await StatechartCleanup.Say(helper.StandardInput, "register:" + nonce);
        Assert.Equal("registered:" + nonce, await StatechartCleanup.Read(helper.StandardOutput));
        return (StatechartCleanup.Decode(created[1]), StatechartCleanup.Decode(created[2]));
    }

    [SkippableFact]
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

    [SkippableFact]
    public async Task OwnerEofRetainsRegisteredScratchWithDurableUncertaintyReason()
    {
        using var scope = new StatechartTestDirectory();
        var lifetime = await WindowsStatechartLifetime.StartAsync(Binary(), scope.Path, scope.Path, scope.Path);
        string stage;
        // The owner's EOF is the subject, so it is disposed here, and also when registration throws, before the scope.
        try
        {
            await lifetime.RegisterAsync();
            stage = lifetime.Stage;
        }
        finally { await lifetime.DisposeAsync(); }
        Assert.True(Directory.Exists(stage));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            Assert.Contains("drain uncertain", File.ReadAllText(file)));
    }

    // A helper that answers anything but the cleanup protocol is released, and nothing is reserved.
    [SkippableFact]
    public async Task AHelperThatDoesNotSpeakTheCleanupProtocolIsReleasedAndRejected()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Windows Job Object lifetime runs only on Windows.");
        using var scope = new StatechartTestDirectory();
        var impostor = new ProcessStartInfo("cmd.exe");
        foreach (var argument in new[] { "/d", "/q", "/c", "echo", "impostor" }) impostor.ArgumentList.Add(argument);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WindowsStatechartLifetime.StartAsync(impostor, scope.Path, scope.Path, scope.Path));
        Assert.StartsWith("Statechart cleanup protocol lost: ready:1:", error.Message);
        await Assert.ThrowsAsync<Win32Exception>(() => WindowsStatechartLifetime.StartAsync(
            new ProcessStartInfo(Path.Combine(scope.Path, "missing-helper.exe")), scope.Path, scope.Path, scope.Path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(scope.Path));
    }

    // Created through the platform dispatch, the lifetime refuses out-of-order use, owns an attached check until it
    // exits, and reports a lost helper.
    [SkippableFact]
    public async Task ALifetimeOwnsAttachedChecksInOrderAndReportsALostHelper()
    {
        using var scope = new StatechartTestDirectory();
        await using var lifetime = await StatechartLifetime.CreateAsync(Binary(), scope.Path, scope.Path, scope.Path);
        Assert.IsType<WindowsStatechartLifetime>(lifetime);
        await Assert.ThrowsAsync<InvalidOperationException>(lifetime.DrainAsync);
        await Assert.ThrowsAsync<InvalidOperationException>(lifetime.CompleteAsync);
        await lifetime.RegisterAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(lifetime.RegisterAsync);

        using (var checking = Process.Start(Waiting())!)
        {
            lifetime.Attach(checking.Id);
            checking.StandardInput.Close();
            await lifetime.WaitAsync(checking, CancellationToken.None);
            Assert.True(checking.HasExited);
        }
        Assert.True(await lifetime.CompleteAsync());
        Assert.True(await lifetime.DrainAsync());
        Assert.False(Directory.Exists(lifetime.Stage));
        Assert.Throws<InvalidOperationException>(() => lifetime.Attach(Environment.ProcessId));
        await Assert.ThrowsAsync<InvalidOperationException>(lifetime.CompleteAsync);

        using var orphan = Process.Start(Waiting())!;
        var lost = await Assert.ThrowsAsync<IOException>(() => lifetime.WaitAsync(orphan, CancellationToken.None));
        Assert.Equal("Statechart cleanup owner was lost; checking stopped and scratch retained.", lost.Message);
        orphan.Kill();
        await orphan.WaitForExitAsync();
    }

    // A command interpreter that lives until its input closes.
    private static ProcessStartInfo Waiting() => new("cmd.exe")
    {
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptionBeforeOrDuringRegistrationNeverDeletesAmbiguousDirectories(bool reserve)
    {
        using var scope = new StatechartTestDirectory();
        var nonce = Guid.NewGuid().ToString("N");
        var helper = Cleanup(nonce, scope);
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
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            Assert.Contains("owner loss before registration; drain uncertain", File.ReadAllText(file)));
    }

    [Fact]
    public async Task TheDrainedFrameAloneLetsTheHelperDeleteRegisteredScratch()
    {
        using var scope = new StatechartTestDirectory();
        var nonce = Guid.NewGuid().ToString("N");
        var helper = Cleanup(nonce, scope);
        var (stage, temp) = await Register(helper, nonce);
        File.WriteAllText(Path.Combine(stage, "owned-file"), "checking");
        Directory.CreateDirectory(Path.Combine(temp, "owned-directory"));
        await StatechartCleanup.Say(helper.StandardInput, "drained:" + nonce);
        Assert.Equal("cleaned:" + nonce, await StatechartCleanup.Read(helper.StandardOutput));
        await helper.WaitForExitAsync();
        Assert.Equal(0, helper.ExitCode);
        Assert.False(Directory.Exists(stage));
        Assert.False(Directory.Exists(temp));
        Assert.Equal(2, Directory.GetFiles(scope.Path, "scratch-*.journal").Length);
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            Assert.EndsWith("registered\ndeleted-after-authoritative-drain\n", File.ReadAllText(file)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithoutDrainProvenanceTheHelperRetainsRegisteredScratch(bool forged)
    {
        using var scope = new StatechartTestDirectory();
        var nonce = Guid.NewGuid().ToString("N");
        var helper = Cleanup(nonce, scope);
        var (stage, temp) = await Register(helper, nonce);
        if (forged) await StatechartCleanup.Say(helper.StandardInput, "drained:" + Guid.NewGuid().ToString("N"));
        helper.StandardInput.Close();
        await helper.WaitForExitAsync();
        Assert.Equal(2, helper.ExitCode);
        Assert.True(Directory.Exists(stage));
        Assert.True(Directory.Exists(temp));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            Assert.Contains("owner loss or invalid drain provenance; drain uncertain", File.ReadAllText(file)));
    }

    [Fact]
    public async Task AReplacedScratchPathIsRetainedEvenAfterTheDrain()
    {
        using var scope = new StatechartTestDirectory();
        var nonce = Guid.NewGuid().ToString("N");
        if (OperatingSystem.IsWindows())
        {
            await ARegisteredScratchPathCannotBeReplacedOnWindows(scope, nonce);
            return;
        }
        var helper = Cleanup(nonce, scope);
        var (stage, _) = await Register(helper, nonce);
        Directory.Move(stage, stage + "-registered");
        Directory.CreateDirectory(stage);
        File.WriteAllText(Path.Combine(stage, "foreign"), "keep");
        await StatechartCleanup.Say(helper.StandardInput, "drained:" + nonce);
        Assert.Null(await StatechartCleanup.Read(helper.StandardOutput));
        await helper.WaitForExitAsync();
        Assert.Equal(2, helper.ExitCode);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(stage, "foreign")));
        Assert.True(Directory.Exists(stage + "-registered"));
        Assert.Contains(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            File.ReadAllText(file).Contains("retained: directory path identity changed; foreign replacement retained"));
    }

    // Windows refuses the replacement itself: the registered handle denies delete sharing, and a directory above an
    // open handle cannot be renamed. So nothing foreign can take the path, and only the drain deletes the scratch.
    private static async Task ARegisteredScratchPathCannotBeReplacedOnWindows(StatechartTestDirectory scope, string nonce)
    {
        const int sharingViolation = unchecked((int)0x80070020), accessDenied = unchecked((int)0x80070005);
        var parent = Directory.CreateDirectory(Path.Combine(scope.Path, "parent")).FullName;
        var helper = Cleanup(nonce, scope, parent);
        var (stage, temp) = await Register(helper, nonce);
        Assert.Equal(sharingViolation, Assert.Throws<IOException>(() => Directory.Move(stage, stage + "-registered")).HResult);
        Assert.Equal(sharingViolation, Assert.Throws<IOException>(() => Directory.Delete(stage)).HResult);
        Assert.Equal(accessDenied, Assert.Throws<IOException>(() => Directory.Move(parent, parent + "-moved")).HResult);
        Assert.True(Directory.Exists(stage));
        await StatechartCleanup.Say(helper.StandardInput, "drained:" + nonce);
        Assert.Equal("cleaned:" + nonce, await StatechartCleanup.Read(helper.StandardOutput));
        await helper.WaitForExitAsync();
        Assert.Equal(0, helper.ExitCode);
        Assert.False(Directory.Exists(stage));
        Assert.False(Directory.Exists(temp));
        Assert.False(Directory.Exists(stage + "-registered"));
        Assert.Equal(2, Directory.GetFiles(scope.Path, "scratch-*.journal").Length);
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), file =>
            Assert.EndsWith("registered\ndeleted-after-authoritative-drain\n", File.ReadAllText(file)));
    }

    [Fact]
    public async Task AHelperWithoutAValidChannelNonceReservesNothing()
    {
        using var scope = new StatechartTestDirectory();
        var helper = Cleanup("not-a-nonce", scope);
        var errors = helper.StandardError.ReadToEndAsync();
        Assert.Null(await StatechartCleanup.Read(helper.StandardOutput));
        await helper.WaitForExitAsync();
        Assert.Equal(2, helper.ExitCode);
        Assert.Contains("Invalid statechart cleanup channel.", await errors);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scope.Path));
    }

    // Only the POSIX lifetime routes a launch through its owned group; without native gating the target starts as given.
    [Fact]
    public void ALifetimeWithoutNativeGatingStartsTheTargetAsGiven()
    {
        var target = new ProcessStartInfo("/runtime", ["--version"]);
        Assert.Same(target, new UngatedLifetime().Gate(target));
    }

    private sealed class UngatedLifetime : StatechartLifetime
    {
        public override string Stage => throw new NotSupportedException();
        public override string Temp => throw new NotSupportedException();
        public override Task RegisterAsync() => throw new NotSupportedException();
        public override void Attach(int pid) => throw new NotSupportedException();
        public override Task<bool> DrainAsync() => throw new NotSupportedException();
        public override Task<bool> CompleteAsync() => throw new NotSupportedException();
        public override Task WaitAsync(Process checking, CancellationToken token) => throw new NotSupportedException();
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("created:c3RhZ2U=")]
    [InlineData("reserved:c3RhZ2U=:dGVtcA==")]
    [InlineData("created:c3RhZ2U=:dGVtcA==:extra")]
    public void OnlyACreatedFrameNamingStageAndTempReservesScratch(string? frame)
    {
        Assert.Equal(("/stage", "/temp"), StatechartCleanup.Created(
            $"created:{StatechartCleanup.Encode("/stage")}:{StatechartCleanup.Encode("/temp")}", "unused"));
        Assert.Equal("no reservation", Assert.Throws<InvalidDataException>(() => StatechartCleanup.Created(frame, "no reservation")).Message);
    }

    [Fact]
    public async Task ControlFramesEndAtLineFeedsStayBoundedAndStopAtEof()
    {
        using var frames = new StringReader("first\r\nsecond\nunterminated");
        Assert.Equal("first", await StatechartCleanup.Read(frames));
        Assert.Equal("second", await StatechartCleanup.Read(frames));
        Assert.Null(await StatechartCleanup.Read(frames));
        using var largest = new StringReader(new string('x', 16384) + "\n");
        Assert.Equal(16384, (await StatechartCleanup.Read(largest))!.Length);
        using var oversize = new StringReader(new string('x', 16385) + "\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => StatechartCleanup.Read(oversize));
    }
}
