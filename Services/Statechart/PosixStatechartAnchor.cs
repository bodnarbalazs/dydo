namespace DynaDocs.Services.Statechart;

using System.Collections;
using System.Diagnostics;

internal sealed class PosixStatechartAnchor : IAsyncDisposable
{
    private readonly PosixStatechartChannel _channel;
    private readonly PosixStatechartProcess _child;
    private bool _stopped;
    private bool _drained;
    public int Group => _child.Id;
    public Task Exited { get; }

    private PosixStatechartAnchor(PosixStatechartChannel channel, PosixStatechartProcess child)
    {
        _channel = channel;
        _child = child;
        Exited = Task.Run(child.WaitWithoutReaping);
    }

    public static async Task<PosixStatechartAnchor> StartAsync(ProcessStartInfo executable)
    {
        var name = "statechart-" + Guid.NewGuid().ToString("N");
        var nonce = Guid.NewGuid().ToString("N");
        var channel = PosixStatechartChannel.Listen(name);
        var args = executable.ArgumentList.Concat(["__statechart-posix-anchor", name, nonce]);
        var child = PosixStatechartProcess.Start(executable.FileName, args, EnvironmentRows());
        var anchor = new PosixStatechartAnchor(channel, child);
        try
        {
            await channel.AcceptAsync();
            await channel.ExpectAsync("anchor:1:" + nonce + ":" + child.Id);
            child.ConfirmGroup(child.Id);
            return anchor;
        }
        catch
        {
            await anchor.DisposeAsync();
            throw;
        }
    }

    public async Task<bool> DrainAsync()
    {
        if (_stopped) return _drained;
        _stopped = true;
        _child.Stop();
        await Exited.WaitAsync(TimeSpan.FromSeconds(5));
        try { _drained = await _child.WaitForEmptyAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) { return false; }
        if (_drained) _child.Reap();
        return _drained;
    }

    public async ValueTask DisposeAsync()
    {
        try { await DrainAsync(); }
        finally { _channel.Dispose(); }
    }

    public static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments is not [var name, var nonce] || !Guid.TryParseExact(nonce, "N", out _))
            throw new InvalidDataException("Invalid statechart anchor channel.");
        PosixStatechartProcess.BecomeGroupLeader();
        try
        {
            using var channel = await PosixStatechartChannel.ConnectAsync(name);
            await channel.SendAsync("anchor:1:" + nonce + ":" + Environment.ProcessId);
            await channel.ReadAsync(); // Any data or EOF stops this still-owned group.
        }
        finally { PosixStatechartProcess.StopCurrentGroup(); }
        return 2;
    }

    internal static IEnumerable<string> EnvironmentRows() => Environment.GetEnvironmentVariables()
        .Cast<DictionaryEntry>().Select(entry => entry.Key + "=" + entry.Value);
}
