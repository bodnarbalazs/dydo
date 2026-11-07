namespace DynaDocs.Services.Statechart;

using System.Diagnostics;

internal sealed class WindowsStatechartLifetime : StatechartLifetime
{
    private readonly WindowsStatechartJob _job = new();
    private readonly Process _cleanup;
    private readonly string _nonce = Guid.NewGuid().ToString("N");
    private string _stage = "";
    private string _temp = "";
    public override string Stage => _stage;
    public override string Temp => _temp;
    private bool _registered;
    private bool _completed;
    private bool _drained;
    private bool _drainAttempted;

    private WindowsStatechartLifetime(ProcessStartInfo start, string parent, string state, string temp)
    {
        start.ArgumentList.Add("__statechart-cleanup");
        foreach (var argument in new[] { _nonce, parent, state, temp }) start.ArgumentList.Add(argument);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        try
        {
            _cleanup = Process.Start(start) ?? throw new IOException("Cannot start statechart cleanup owner.");
        }
        catch
        {
            _job.Dispose();
            throw;
        }
    }

    public static async Task<StatechartLifetime> StartAsync(ProcessStartInfo start,
        string parent, string state, string temp)
    {
        var owner = new WindowsStatechartLifetime(start, parent, state, temp);
        try
        {
            await owner.Expect("ready:1:" + owner._nonce);
            if (owner._job.Contains(owner._cleanup.Id))
                throw new IOException("Cleanup owner must be outside the checking job.");
            return owner;
        }
        catch
        {
            await owner.DisposeAsync();
            throw;
        }
    }

    public override async Task RegisterAsync()
    {
        if (_registered || Stage.Length != 0) throw new InvalidOperationException("Already reserved.");
        await StatechartCleanup.Say(_cleanup.StandardInput, "reserve:" + _nonce);
        (_stage, _temp) = StatechartCleanup.Created(await StatechartCleanup.Read(_cleanup.StandardOutput),
            "Cleanup did not reserve statechart scratch.");
        await StatechartCleanup.Say(_cleanup.StandardInput, "register:" + _nonce);
        await Expect("registered:" + _nonce);
        _registered = true;
    }

    public override void Attach(int pid)
    {
        if (!_registered || _completed || _cleanup.HasExited)
            throw new InvalidOperationException("Statechart requires live registered cleanup before ownership release.");
        _job.Attach(pid);
    }

    public override async Task<bool> CompleteAsync()
    {
        if (!_registered || _completed) throw new InvalidOperationException("No active registered statechart lifetime.");
        _completed = true;
        if (!await DrainAsync()) return false;
        await StatechartCleanup.Say(_cleanup.StandardInput, "drained:" + _nonce);
        await Expect("cleaned:" + _nonce);
        await _cleanup.WaitForExitAsync();
        return _cleanup.ExitCode == 0;
    }

    public override async Task<bool> DrainAsync()
    {
        if (!_registered) throw new InvalidOperationException("No registered statechart lifetime.");
        if (!_drainAttempted)
        {
            _drainAttempted = true;
            _drained = await _job.SealAndDrainAsync(TimeSpan.FromSeconds(5));
        }
        return _drained;
    }

    public override async Task WaitAsync(Process checking, CancellationToken token)
    {
        var exit = checking.WaitForExitAsync(token);
        var helper = _cleanup.WaitForExitAsync(token);
        var finished = await Task.WhenAny(exit, helper);
        await finished;
        if (!checking.HasExited)
        {
            _job.Terminate();
            throw new IOException("Statechart cleanup owner was lost; checking stopped and scratch retained.");
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _job.Dispose();
        _cleanup.StandardInput.Close();
        // EOF is not an authoritative drain message: the independent helper journals retention.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await _cleanup.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            _cleanup.Kill();
            await _cleanup.WaitForExitAsync();
            throw new IOException("Statechart cleanup owner did not settle; scratch retained.");
        }
        _cleanup.Dispose();
    }

    private async Task Expect(string message)
    {
        if (await StatechartCleanup.Read(_cleanup.StandardOutput) != message)
            throw new InvalidDataException("Statechart cleanup protocol lost: " + message);
    }
}
