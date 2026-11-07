namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Runtime.InteropServices;
using DynaDocs.Services.Statechart;

public class StatechartPosixLifecycleTests
{
    [Fact]
    public async Task RegisteredPosixLifetimeGatesWorkAndCleansOnlyAfterIndependentDrain()
    {
        using var scope = new StatechartTestDirectory();
        if (!OperatingSystem.IsLinux())
        {
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
                PosixStatechartLifetime.StartAsync(new ProcessStartInfo("missing"), scope.Path, scope.Path, scope.Path));
            return; // Platform refusal is not Linux execution evidence.
        }
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        Assert.Throws<InvalidOperationException>(() => lifetime.Gate(new ProcessStartInfo("/missing-before-registration")));
        await lifetime.RegisterAsync();
        File.WriteAllText(Path.Combine(lifetime.Stage, "owned-file"), "checking");
        var stage = lifetime.Stage;
        var temp = lifetime.Temp;
        Assert.True(await lifetime.CompleteAsync());
        Assert.False(Directory.Exists(stage));
        Assert.False(Directory.Exists(temp));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    // The socket path only serves the first connection; the connected pair no longer needs it.
    [SkippableFact]
    public async Task AConnectedChannelLeavesNoSocketFileBehind()
    {
        Skip.If(OperatingSystem.IsWindows(), "Windows pipes leave no socket file.");
        var name = "statechart-" + Guid.NewGuid().ToString("N");
        var socket = Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + name);
        using var server = PosixStatechartChannel.Listen(name);
        var connecting = PosixStatechartChannel.ConnectAsync(name);
        await server.AcceptAsync();
        using var client = await connecting;
        Assert.False(File.Exists(socket));
        await client.SendAsync("still connected");
        Assert.Equal("still connected", await server.ReadAsync());
    }

    [Fact]
    public async Task PosixControlEofDrainsOutsideThePublicProcessBeforeRegisteredCleanup()
    {
        using var scope = new StatechartTestDirectory();
        if (!OperatingSystem.IsLinux())
        {
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
                PosixStatechartLifetime.StartAsync(new ProcessStartInfo("missing"), scope.Path, scope.Path, scope.Path));
            return;
        }
        var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var stage = lifetime.Stage;
        var temp = lifetime.Temp;
        File.WriteAllText(Path.Combine(stage, "owned-file"), "checking");
        await lifetime.DisposeAsync();
        Assert.False(Directory.Exists(stage));
        Assert.False(Directory.Exists(temp));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    private const string LinuxOnly = "POSIX ownership runs on Linux.";

    [SkippableFact]
    public async Task ARegisteredLifetimeRegistersDrainsAndCompletesOnlyOnce()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(lifetime.RegisterAsync);
        Assert.True(await lifetime.DrainAsync());
        Assert.True(await lifetime.DrainAsync());
        Assert.Throws<InvalidOperationException>(() => lifetime.Gate(new ProcessStartInfo("/after-drain")));
        Assert.Throws<InvalidOperationException>(() => lifetime.Attach(Environment.ProcessId));
        Assert.True(await lifetime.CompleteAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(lifetime.CompleteAsync);
        Assert.False(Directory.Exists(lifetime.Stage));
    }

    // The exec fails here, so the handoff returns its error and this process keeps the owner to drain and clean up.
    [SkippableFact]
    public async Task AFailedHandoffLeavesTheOwnerToDrainAndCleanUp()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var target = new ProcessStartInfo(Path.Combine(scope.Path, "missing-runtime")) { WorkingDirectory = Environment.CurrentDirectory };
        var error = await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => lifetime.HandoffAsync(target, null));
        Assert.Equal("Statechart exec handoff failed.", error.Message);
        Assert.True(await lifetime.CompleteAsync());
        Assert.False(Directory.Exists(lifetime.Stage));
        Assert.False(Directory.Exists(lifetime.Temp));
    }

    // The owner accepts a continued request from its own store; the execution lock then survives exec for the runtime.
    [SkippableFact]
    public async Task AContinuedHandoffHandsTheRuntimeItsExecutionLock()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        using var roots = new ScopedRoots(scope.Path);
        var (store, request, path) = SavedRequest(scope.Path);
        using var executionLock = store.Lock(path);
        var descriptor = executionLock.SafeFileHandle.DangerousGetHandle().ToInt32();
        Assert.Equal(1, await ChildSees(descriptor));
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var error = await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => lifetime.HandoffAsync(
            new ProcessStartInfo(Path.Combine(scope.Path, "missing-runtime")), new StatechartContinuation(path, executionLock)));
        Assert.Equal("Statechart exec handoff failed.", error.Message);
        Assert.Equal(0, await ChildSees(descriptor));
        Assert.True(await lifetime.CompleteAsync());
        Assert.Null(store.Completed(path, request));
    }

    // The owner validates a continued request before it acknowledges the handoff; one outside its store stops it.
    [SkippableFact]
    public async Task AContinuationOutsideTheOwnersStoreIsRefusedBeforeExec()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        using var roots = new ScopedRoots(scope.Path);
        var foreign = Path.Combine(scope.Path, "request.json");
        File.WriteAllText(foreign, "{}");
        using var executionLock = new FileStream(Path.Combine(scope.Path, "execution.lock"), FileMode.CreateNew);
        await using var lifetime = await PosixStatechartLifetime.StartAsync(StatechartTestBinary.Start(), scope.Path, scope.Path, scope.Path);
        await lifetime.RegisterAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => lifetime.HandoffAsync(
            new ProcessStartInfo(Path.Combine(scope.Path, "missing-runtime")), new StatechartContinuation(foreign, executionLock)));
        Assert.StartsWith("Statechart POSIX channel lost: handoff:", error.Message);
        Assert.False(Directory.Exists(lifetime.Stage));
        Assert.False(Directory.Exists(lifetime.Temp));
    }

    private static async Task<int> ChildSees(int descriptor)
    {
        using var child = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", $"test -e /proc/self/fd/{descriptor}"]))!;
        await child.WaitForExitAsync();
        return child.ExitCode;
    }

    // Owners a lifetime starts inherit this process's environment; these roots keep their request store in the test.
    private sealed class ScopedRoots : IDisposable
    {
        private static readonly string[] Names = ["DYDO_STATECHART_ASSET_ROOT", "DYDO_STATECHART_DEPENDENCY_ROOT", "DYDO_STATECHART_STATE_ROOT"];
        private readonly string?[] _saved = Names.Select(Environment.GetEnvironmentVariable).ToArray();

        public ScopedRoots(string root)
        {
            foreach (var (name, folder) in Names.Zip(["assets", "dependencies", "state"]))
                Environment.SetEnvironmentVariable(name, Path.Combine(root, folder));
        }

        public void Dispose()
        {
            for (var index = 0; index < Names.Length; index++) Environment.SetEnvironmentVariable(Names[index], _saved[index]);
        }
    }

    // The group is the spawned child's own: an acknowledgment from the process a wrapper forked is refused with its own error.
    [SkippableFact]
    public async Task AnAnchorAcknowledgedByAnotherProcessIsRefused()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        var forwarding = new ProcessStartInfo("/bin/sh");
        foreach (var argument in new[] { "-c", "\"$0\" \"$@\"; exit $?", StatechartTestBinary.Start().FileName })
            forwarding.ArgumentList.Add(argument);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => PosixStatechartAnchor.StartAsync(forwarding));
        Assert.StartsWith("Statechart POSIX channel lost: anchor:1:", error.Message);
    }

    [SkippableFact]
    public void DirectoryIdentityRefusesMissingExistingOrNonDirectoryTargets()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        var directory = Path.Combine(scope.Path, "owned");
        PosixStatechartFiles.CreateExclusive(directory);
        Assert.Throws<System.ComponentModel.Win32Exception>(() => PosixStatechartFiles.CreateExclusive(directory));
        Assert.Throws<System.ComponentModel.Win32Exception>(() => PosixStatechartFiles.Open(Path.Combine(scope.Path, "missing")));
        var file = Path.Combine(scope.Path, "file");
        File.WriteAllText(file, "not a directory");
        using (var plain = File.OpenHandle(file))
            Assert.Throws<IOException>(() => PosixStatechartFiles.Identity(plain));
        using var held = PosixStatechartFiles.Open(directory);
        Assert.True(PosixStatechartFiles.Matches(directory, held));
        Directory.Delete(directory);
        Assert.False(PosixStatechartFiles.Matches(directory, held));
    }

    [SkippableFact]
    public async Task AHandedOffRequestRecordsItsVerdictOnlyAfterTheGroupDrains()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        var (store, request, path) = SavedRequest(scope.Path);
        await using var session = await OwnerSession.StartAsync(scope.Path);
        var nonce = session.Nonce;
        await session.RegisterAsync();
        Assert.Equal("handoff:" + nonce, await session.SendAsync($"handoff:{nonce}:{StatechartCleanup.Encode(path)}"));
        Assert.Equal("drained:" + nonce, await session.SendAsync("drain:" + nonce));
        Directory.CreateDirectory(request.Output);
        File.WriteAllText(Path.Combine(request.Output, "report.md"), "Bound breaks");
        Assert.Null(store.Completed(path, request));
        Assert.Equal("result:" + nonce, await session.SendAsync($"result:{nonce}:1"));
        Assert.Equal(1, store.Completed(path, request));
        Assert.Equal("cleaned:" + nonce, await session.SendAsync("complete:" + nonce));
        Assert.Equal((0, ""), await session.ExitAsync());
        Assert.False(Directory.Exists(session.Stage));
        Assert.False(Directory.Exists(session.Temp));
    }

    [SkippableTheory]
    [InlineData("out-of-range", "Invalid statechart completed-request frame.")]
    [InlineData("changed", "Saved statechart request changed during POSIX checking.")]
    [InlineData("anonymous", "Invalid statechart completed-request frame.")]
    [InlineData("before-drain", "")]
    public async Task AnUnprovenCompletedRequestFrameRecordsNoVerdict(string kind, string error)
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        var (store, request, path) = SavedRequest(scope.Path);
        await using var session = await OwnerSession.StartAsync(scope.Path);
        var nonce = session.Nonce;
        await session.RegisterAsync();
        var handed = kind == "anonymous" ? "-" : StatechartCleanup.Encode(path);
        Assert.Equal("handoff:" + nonce, await session.SendAsync($"handoff:{nonce}:{handed}"));
        if (kind != "before-drain") Assert.Equal("drained:" + nonce, await session.SendAsync("drain:" + nonce));
        Directory.CreateDirectory(request.Output);
        File.WriteAllText(Path.Combine(request.Output, "report.md"), "Bound breaks");
        if (kind == "changed") File.AppendAllText(path, " ");
        Assert.Null(await session.SendAsync($"result:{nonce}:{(kind == "out-of-range" ? 7 : 1)}"));
        var (exit, errors) = await session.ExitAsync();
        Assert.Equal(2, exit);
        Assert.Equal(error, errors.Trim());
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "completed.json")));
        Assert.False(Directory.Exists(session.Stage));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    [SkippableFact]
    public async Task OnlyACheckingChildInsideTheOwnedGroupIsAttachedAndTheDrainStopsIt()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        await using var session = await OwnerSession.StartAsync(scope.Path);
        var nonce = session.Nonce;
        await session.RegisterAsync();
        Assert.Equal("handoff:" + nonce, await session.SendAsync($"handoff:{nonce}:-"));
        using var child = Process.Start(Binary(scope.Path, "__statechart-posix-child", session.Group.ToString(),
            "/bin/sh", "-c", "echo joined; exec cat"))!;
        Assert.Equal("joined", await child.StandardOutput.ReadLineAsync());
        Assert.Equal($"attached:{nonce}:{child.Id}", await session.SendAsync($"attach:{nonce}:{child.Id}"));
        Assert.Equal("drained:" + nonce, await session.SendAsync("drain:" + nonce));
        await child.WaitForExitAsync();
        Assert.Equal(128 + 9, child.ExitCode);
        Assert.Equal("cleaned:" + nonce, await session.SendAsync("complete:" + nonce));
        Assert.Equal((0, ""), await session.ExitAsync());
    }

    [SkippableTheory]
    [InlineData("handoff:{0}:a:b", "Invalid POSIX handoff frame.")]
    [InlineData("attach:{0}:checker", "Invalid POSIX checking-child frame.")]
    [InlineData("attach:{0}:1", "Checking child did not enter the acknowledged statechart group.")]
    [InlineData("go:{0}", "")]
    public async Task AMalformedOrForeignControlFrameStopsTheOwnerBeforeAnyRelease(string frame, string error)
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        await using var session = await OwnerSession.StartAsync(scope.Path);
        await session.RegisterAsync();
        if (frame.StartsWith("attach", StringComparison.Ordinal))
            Assert.Equal("handoff:" + session.Nonce, await session.SendAsync($"handoff:{session.Nonce}:-"));
        Assert.Null(await session.SendAsync(string.Format(frame, session.Nonce)));
        var (exit, errors) = await session.ExitAsync();
        Assert.Equal(2, exit);
        Assert.Equal(error, errors.Trim());
        Assert.False(Directory.Exists(session.Stage));
    }

    [SkippableFact]
    public async Task LosingTheNpmLifetimeStopsControlAndStillCleansAfterTheDrain()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        await using var session = await OwnerSession.StartAsync(scope.Path, npmLifetime: true);
        await session.RegisterAsync();
        session.Owner.StandardInput.Close();
        var (exit, errors) = await session.ExitAsync();
        Assert.Equal(2, exit);
        Assert.Equal("Statechart group anchor or npm lifetime lost; no checking release.", errors.Trim());
        Assert.False(Directory.Exists(session.Stage));
        Assert.False(Directory.Exists(session.Temp));
        Assert.All(Directory.GetFiles(scope.Path, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    [SkippableTheory]
    [InlineData("__statechart-posix-owner", "Invalid statechart POSIX ownership channel.")]
    [InlineData("__statechart-posix-anchor", "Invalid statechart anchor channel.")]
    [InlineData("__statechart-posix-child", "Invalid statechart checking-group launch.")]
    public async Task APrivateModeWithoutAValidChannelStartsNothing(string mode, string error)
    {
        Skip.IfNot(OperatingSystem.IsLinux(), LinuxOnly);
        using var scope = new StatechartTestDirectory();
        using var process = Process.Start(Binary(scope.Path, mode, "statechart-missing", "not-a-nonce"))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(2, process.ExitCode);
        Assert.Equal("", await output);
        Assert.Equal(error, (await errors).Trim());
        Assert.Empty(Directory.EnumerateFileSystemEntries(scope.Path));
    }

    private static ProcessStartInfo Binary(string root, params string[] arguments)
    {
        var start = StatechartTestBinary.Start();
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DYDO_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        start.Environment["DYDO_STATECHART_ASSET_ROOT"] = Path.Combine(root, "assets");
        start.Environment["DYDO_STATECHART_DEPENDENCY_ROOT"] = Path.Combine(root, "dependencies");
        start.Environment["DYDO_STATECHART_STATE_ROOT"] = Path.Combine(root, "state");
        return start;
    }

    // A request the owner's own store, rooted by the same environment, accepts at handoff.
    private static (StatechartRequests Store, StatechartRequest Request, string Path) SavedRequest(string root)
    {
        var assets = new StatechartAssets();
        var rid = RuntimeInformation.RuntimeIdentifier;
        var caller = Directory.CreateDirectory(Path.Combine(root, "caller")).FullName;
        File.WriteAllText(Path.Combine(caller, "model.tla"), "---- MODULE model ----\n====\n");
        File.WriteAllText(Path.Combine(caller, "model.cfg"), "\n");
        var home = new StatechartHome(Path.Combine(root, "assets"), Path.Combine(root, "dependencies"), Path.Combine(root, "state"));
        var store = new StatechartRequests(home, assets, rid);
        var request = StatechartRequest.Capture(["model.tla", "--out", "chart"], caller, assets, rid);
        return (store, request, store.Save(request));
    }

    // Plays the public process and, after the handoff, the checking runtime against a real independent owner.
    private sealed class OwnerSession : IAsyncDisposable
    {
        private readonly PosixStatechartChannel _channel;
        private readonly Task<string> _errors;
        public Process Owner { get; }
        public string Nonce { get; }
        public int Group { get; }
        public string Stage { get; private set; } = "";
        public string Temp { get; private set; } = "";

        private OwnerSession(PosixStatechartChannel channel, Process owner, string nonce, int group)
        {
            _channel = channel;
            Owner = owner;
            Nonce = nonce;
            Group = group;
            _errors = owner.StandardError.ReadToEndAsync();
        }

        public static async Task<OwnerSession> StartAsync(string root, bool npmLifetime = false)
        {
            var name = "statechart-" + Guid.NewGuid().ToString("N");
            var nonce = Guid.NewGuid().ToString("N");
            var channel = PosixStatechartChannel.Listen(name);
            var start = Binary(root, "__statechart-posix-owner", name, nonce, root, root, root);
            if (npmLifetime) start.Environment["DYDO_STATECHART_NPM_LIFETIME"] = "1";
            var owner = Process.Start(start)!;
            await channel.AcceptAsync();
            var ready = (await channel.ReadAsync())!.Split(':');
            Assert.Equal(["ready", "1", nonce], ready[..3]);
            return new OwnerSession(channel, owner, nonce, int.Parse(ready[3]));
        }

        public async Task<string?> SendAsync(string frame)
        {
            await _channel.SendAsync(frame);
            return await _channel.ReadAsync();
        }

        public async Task RegisterAsync()
        {
            var created = (await SendAsync("reserve:" + Nonce))!.Split(':');
            Stage = StatechartCleanup.Decode(created[1]);
            Temp = StatechartCleanup.Decode(created[2]);
            Assert.Equal("registered:" + Nonce, await SendAsync("register:" + Nonce));
        }

        public async Task<(int Exit, string Errors)> ExitAsync()
        {
            await Owner.WaitForExitAsync();
            return (Owner.ExitCode, await _errors);
        }

        // Control EOF settles an owner a failed assertion left running.
        public async ValueTask DisposeAsync()
        {
            _channel.Dispose();
            await Owner.WaitForExitAsync();
            await _errors;
            Owner.Dispose();
        }
    }
}
