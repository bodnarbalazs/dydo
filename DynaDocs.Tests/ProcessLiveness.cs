namespace DynaDocs.Tests;

using System.Diagnostics;

internal static class ProcessLiveness
{
    internal static bool HasTerminated(int pid)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pid);
        if (OperatingSystem.IsLinux())
            return HasTerminatedOnLinux(pid, File.ReadAllText);
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException) { return true; }
    }

    internal static bool HasTerminatedOnLinux(int pid, Func<string, string> readStat)
    {
        // A missing proc mount must not make every PID look terminated.
        _ = readStat("/proc/self/stat");
        string stat;
        try
        {
            stat = readStat($"/proc/{pid}/stat");
        }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }

        var end = stat.LastIndexOf(')');
        if (!stat.StartsWith($"{pid} (", StringComparison.Ordinal) || end < 0)
            throw new InvalidDataException($"Invalid process stat for {pid}.");
        var fields = stat[(end + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 20 || !ulong.TryParse(fields[19], out _))
            throw new InvalidDataException($"Incomplete process stat for {pid}.");

        // A zombie cannot run or hold pipes; only its adopting parent can reap it.
        return fields[0] switch
        {
            "Z" => true,
            "R" or "S" or "D" or "T" or "t" or "X" or "x" or "K" or "W" or "P" or "I" => false,
            _ => throw new InvalidDataException($"Unknown process state for {pid}: {fields[0]}.")
        };
    }

    internal static async Task WaitForTerminationAsync(int pid)
    {
        var elapsed = Stopwatch.StartNew();
        while (!HasTerminated(pid))
        {
            if (elapsed.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException($"Process {pid} is still running.");
            await Task.Delay(20);
        }
    }
}
