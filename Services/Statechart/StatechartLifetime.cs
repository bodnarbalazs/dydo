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

    public static Task<StatechartLifetime> CreateAsync(ProcessStartInfo executable,
        string parent, string state, string temp) => OperatingSystem.IsWindows()
        ? CreateWindowsAsync(executable, parent, state, temp)
        : CreatePosixAsync(executable, parent, state, temp);

    // The Windows arm is its own member, so coverage measures it on Windows, where it executes.
    private static Task<StatechartLifetime> CreateWindowsAsync(ProcessStartInfo executable,
        string parent, string state, string temp) =>
        WindowsStatechartLifetime.StartAsync(executable, parent, state, temp);

    private static async Task<StatechartLifetime> CreatePosixAsync(ProcessStartInfo executable,
        string parent, string state, string temp) =>
        await PosixStatechartLifetime.StartAsync(executable, parent, state, temp);
}
