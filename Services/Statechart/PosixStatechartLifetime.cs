namespace DynaDocs.Services.Statechart;

using System.Diagnostics;

internal sealed class PosixStatechartLifetime : StatechartLifetime
{
    private readonly PosixStatechartChannel _channel;
    private readonly Process _cleanup;
    private readonly string _nonce;
    private readonly ProcessStartInfo _executable;
    private string _stage = "";
    private string _temp = "";
    private bool _registered;
    private bool _drained;
    private bool _completed;
    public override string Stage => _stage;
    public override string Temp => _temp;
    public int Group { get; private set; }

    private PosixStatechartLifetime(PosixStatechartChannel channel, Process cleanup,
        string nonce, ProcessStartInfo executable)
    {
        _channel = channel;
        _cleanup = cleanup;
        _nonce = nonce;
        _executable = executable;
    }

    public static async Task<PosixStatechartLifetime> StartAsync(ProcessStartInfo executable,
        string parent, string state, string temp)
    {
        PosixStatechartFiles.RequireLinux();
        var name = "statechart-" + Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        var channel = PosixStatechartChannel.Listen(name);
        var start = new ProcessStartInfo(executable.FileName) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in executable.ArgumentList.Concat(["__statechart-posix-owner", name, nonce, parent, state, temp]))
            start.ArgumentList.Add(argument);
        var cleanup = Process.Start(start) ?? throw new IOException("Cannot start independent POSIX statechart owner.");
        var owner = new PosixStatechartLifetime(channel, cleanup, nonce, executable);
        try
        {
            await channel.AcceptAsync();
            var fields = (await channel.ReadAsync().WaitAsync(TimeSpan.FromSeconds(30)))?.Split(':');
            if (fields is not ["ready", "1", var actual, var group] || actual != nonce ||
                !int.TryParse(group, out var pid) || pid <= 0)
                throw new InvalidDataException("Invalid statechart POSIX group acknowledgment.");
            owner.Group = pid;
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    public override async Task RegisterAsync()
    {
        if (_registered || Stage.Length != 0) throw new InvalidOperationException("Statechart POSIX scratch already reserved.");
        await _channel.SendAsync("reserve:" + _nonce);
        (_stage, _temp) = StatechartCleanup.Created(await _channel.ReadAsync().WaitAsync(TimeSpan.FromSeconds(30)),
            "Missing statechart POSIX reservations.");
        await _channel.SendAsync("register:" + _nonce);
        await _channel.ExpectAsync("registered:" + _nonce);
        _registered = true;
    }

    public override ProcessStartInfo Gate(ProcessStartInfo target)
    {
        RequireActive();
        var start = new ProcessStartInfo(_executable.FileName)
        {
            WorkingDirectory = target.WorkingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = target.RedirectStandardInput, RedirectStandardOutput = target.RedirectStandardOutput,
            RedirectStandardError = target.RedirectStandardError
        };
        foreach (var argument in _executable.ArgumentList.Concat(["__statechart-posix-child", Group.ToString(), target.FileName]).Concat(target.ArgumentList))
            start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach (var pair in target.Environment) start.Environment[pair.Key] = pair.Value;
        return start;
    }

    public override void Attach(int pid)
    {
        RequireActive();
        PosixStatechartProcess.RequireGroup(pid, Group);
    }

    public override async Task WaitAsync(Process checking, CancellationToken token)
    {
        var exit = checking.WaitForExitAsync(token);
        var lost = _cleanup.WaitForExitAsync(token);
        await await Task.WhenAny(exit, lost);
        if (!checking.HasExited) throw new IOException("Independent POSIX statechart owner lost; checking group stopped.");
    }

    public override async Task<bool> DrainAsync()
    {
        if (_drained) return true;
        RequireActive();
        await _channel.SendAsync("drain:" + _nonce);
        await _channel.ExpectAsync("drained:" + _nonce);
        _drained = true;
        return true;
    }

    public override async Task<bool> CompleteAsync()
    {
        if (_completed || !_registered) throw new InvalidOperationException("No active POSIX statechart cleanup.");
        await DrainAsync();
        _completed = true;
        await _channel.SendAsync("complete:" + _nonce);
        await _channel.ExpectAsync("cleaned:" + _nonce);
        await _cleanup.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        return _cleanup.ExitCode == 0;
    }

    public async Task<int> HandoffAsync(ProcessStartInfo target, StatechartContinuation? continuation)
    {
        RequireActive();
        var request = continuation is null ? "-" : StatechartCleanup.Encode(continuation.Path);
        await _channel.SendAsync("handoff:" + _nonce + ":" + request);
        await _channel.ExpectAsync("handoff:" + _nonce);
        var fd = _channel.Export();
        if (continuation is not null)
            PosixStatechartDescriptors.Inherit(continuation.ExecutionLock.SafeFileHandle.DangerousGetHandle());
        if (target.WorkingDirectory.Length != 0) Environment.CurrentDirectory = target.WorkingDirectory;
        throw PosixStatechartProcess.Handoff(target.FileName, target.ArgumentList.Concat([fd.ToString(), _nonce, Group.ToString()]),
            target.Environment.Select(pair => pair.Key + "=" + pair.Value));
    }

    public override async ValueTask DisposeAsync()
    {
        _channel.Dispose();
        try { await _cleanup.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { throw new IOException("Independent POSIX cleanup did not settle; scratch retained."); }
        finally { _cleanup.Dispose(); }
    }

    private void RequireActive()
    {
        if (!_registered || _completed || _drained || _cleanup.HasExited)
            throw new InvalidOperationException("Statechart needs live registered POSIX ownership before GO.");
    }
}
