namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Models;
using DynaDocs.Utils;

public static class WatchdogService
{
    private static string GetPidFilePath()
    {
        var dydoRoot = PathUtils.FindDydoRoot();
        return Path.Combine(dydoRoot ?? ".", ".dydo", "watchdog.pid");
    }

    /// <summary>
    /// Starts the watchdog if not already running. Called automatically by DispatchCommand
    /// when --auto-close is set. Idempotent: multiple calls are safe.
    /// </summary>
    public static void EnsureRunning()
    {
        var pidFile = GetPidFilePath();

        if (File.Exists(pidFile))
        {
            if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var existingPid) &&
                ProcessUtils.IsProcessRunning(existingPid))
            {
                return;
            }
            // Stale PID file — process died, clean up and restart
            File.Delete(pidFile);
        }

        try
        {
            // Find the dydo executable path
            var dydoPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(dydoPath)) return;

            var psi = new ProcessStartInfo(dydoPath, "watchdog run")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var proc = Process.Start(psi);
            if (proc == null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
            File.WriteAllText(pidFile, proc.Id.ToString());
        }
        catch
        {
            // Non-fatal: watchdog is best-effort. Next dispatch will retry.
        }
    }

    /// <summary>
    /// Stops the watchdog process.
    /// </summary>
    public static void Stop()
    {
        var pidFile = GetPidFilePath();
        if (!File.Exists(pidFile)) return;

        try
        {
            if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill();
            }
        }
        catch { }

        try { File.Delete(pidFile); } catch { }
    }

    /// <summary>
    /// The watchdog polling loop. Runs as a background process, scanning for released
    /// auto-close agents and killing their claude processes.
    /// </summary>
    public static void Run()
    {
        var dydoRoot = PathUtils.FindDydoRoot();
        if (dydoRoot == null) return;

        while (true)
        {
            Thread.Sleep(10_000);

            try
            {
                PollAndCleanup(dydoRoot);
            }
            catch
            {
                // Swallow individual poll errors — keep the loop alive
            }
        }
    }

    public static void PollAndCleanup(string dydoRoot)
    {
        var agentsDir = Path.Combine(dydoRoot, "agents");
        if (!Directory.Exists(agentsDir)) return;

        foreach (var agentDir in Directory.GetDirectories(agentsDir))
        {
            var statePath = Path.Combine(agentDir, "state.md");
            if (!File.Exists(statePath)) continue;

            var (autoClose, isFree, agentName) = ParseStateForWatchdog(statePath);
            if (!autoClose || !isFree || agentName == null) continue;

            // Find and kill claude processes for this agent
            var pattern = $"{agentName} --inbox";
            var pids = ProcessUtils.FindProcessesByCommandLine(pattern);

            foreach (var pid in pids)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    proc.Kill();
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Lightweight parse of state.md for just the fields the watchdog needs.
    /// </summary>
    private static (bool autoClose, bool isFree, string? agentName) ParseStateForWatchdog(string statePath)
    {
        try
        {
            var content = File.ReadAllText(statePath);
            if (!content.StartsWith("---")) return (false, false, null);

            var endIndex = content.IndexOf("---", 3);
            if (endIndex < 0) return (false, false, null);

            var yaml = content[3..endIndex];
            bool autoClose = false, isFree = false;
            string? agentName = null;

            foreach (var line in yaml.Split('\n'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                switch (key)
                {
                    case "agent": agentName = value; break;
                    case "status": isFree = value == "free"; break;
                    case "auto-close": autoClose = value == "true"; break;
                }
            }

            return (autoClose, isFree, agentName);
        }
        catch
        {
            return (false, false, null);
        }
    }
}
