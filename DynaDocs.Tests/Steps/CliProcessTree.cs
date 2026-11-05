namespace DynaDocs.Tests.Steps;

internal sealed class CliProcessTree(Func<string, string> readStat)
{
    private readonly Dictionary<int, ulong> _descendants = [];

    internal void Capture(int parent, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
            return;
        _ = readStat("/proc/self/stat");
        var processes = new Dictionary<int, (string State, int Parent, ulong Start)>();
        foreach (var path in Directory.EnumerateDirectories("/proc"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (int.TryParse(Path.GetFileName(path), out var pid) && ProcessLiveness.ReadLinuxStat(pid, readStat) is { } stat)
                processes.Add(pid, stat);
        }
        var pending = new Queue<int>();
        pending.Enqueue(parent);
        while (pending.TryDequeue(out var ancestor))
        {
            foreach (var (pid, stat) in processes.Where(entry => entry.Value.Parent == ancestor))
            {
                if (_descendants.TryAdd(pid, stat.Start))
                    pending.Enqueue(pid);
            }
        }
    }

    internal async Task WaitForTerminationAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
            return;
        while (_descendants.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = readStat("/proc/self/stat");
            foreach (var (pid, start) in _descendants.ToArray())
            {
                var stat = ProcessLiveness.ReadLinuxStat(pid, readStat);
                if (stat is null || stat.Value.Start != start || ProcessLiveness.IsTerminatedLinuxState(pid, stat.Value.State))
                    _descendants.Remove(pid);
            }
            if (_descendants.Count > 0)
                await Task.Delay(20, cancellationToken);
        }
    }
}
