namespace DynaDocs.Services.Statechart;

using System.Diagnostics;

internal abstract class StatechartLifetime : IAsyncDisposable
{
    public abstract string Stage { get; }
    public abstract string Temp { get; }
    public abstract Task RegisterAsync();
    public abstract void Attach(int pid);
    public abstract Task<bool> DrainAsync();
    public abstract Task<bool> CompleteAsync();
    public abstract Task WaitAsync(Process checking, CancellationToken token);
    public abstract ValueTask DisposeAsync();
    public virtual ProcessStartInfo Gate(ProcessStartInfo start) => start;

    public static async Task<StatechartLifetime> CreateAsync(ProcessStartInfo executable,
        string parent, string state, string temp) => OperatingSystem.IsWindows()
        ? await WindowsStatechartLifetime.StartAsync(executable, parent, state, temp)
        : await PosixStatechartLifetime.StartAsync(executable, parent, state, temp);
}
