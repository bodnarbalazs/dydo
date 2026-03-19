namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Utils;

public static class WatchdogService
{
    internal static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell", "pwsh", "bash", "sh", "cmd", "zsh"
    };

    public static string GetPidFilePath(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "watchdog.pid");

    /// <summary>
    /// Starts the watchdog if not already running. Called automatically by DispatchCommand
    /// when --auto-close is set. Idempotent: multiple calls are safe.
    /// Returns true if a new watchdog was started, false if one was already running.
    /// </summary>
    public static bool EnsureRunning() => EnsureRunning(PathUtils.FindDydoRoot() ?? ".");

    public static bool EnsureRunning(string dydoRoot)
    {
        var pidFile = GetPidFilePath(dydoRoot);

        if (File.Exists(pidFile))
        {
            if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var existingPid) &&
                ProcessUtils.IsProcessRunning(existingPid))
            {
                return false;
            }
            // Stale PID file — process died, clean up and restart
            File.Delete(pidFile);
        }

        try
        {
            var dydoPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(dydoPath)) return false;

            var psi = new ProcessStartInfo(dydoPath, "watchdog run")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var proc = Process.Start(psi);
            if (proc == null) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(pidFile)!);
            File.WriteAllText(pidFile, proc.Id.ToString());
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stops the watchdog process.
    /// Returns true if a running watchdog was stopped, false if none was running.
    /// </summary>
    public static bool Stop() => Stop(PathUtils.FindDydoRoot() ?? ".");

    public static bool Stop(string dydoRoot)
    {
        var pidFile = GetPidFilePath(dydoRoot);
        if (!File.Exists(pidFile)) return false;

        var stopped = false;
        try
        {
            if (int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid) &&
                ProcessUtils.IsProcessRunning(pid))
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(5000);
                stopped = true;
            }
        }
        catch { }

        try { File.Delete(pidFile); } catch { }
        return stopped;
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

            var (autoClose, isFree, agentName, windowId) = ParseStateForWatchdog(statePath);
            if (!autoClose || !isFree || agentName == null) continue;

            // Try closing the terminal window via Windows Terminal API
            if (windowId != null && TryCloseWindow(windowId))
            {
                ClearAutoClose(statePath);
                continue;
            }

            // Fallback: kill matching non-shell processes
            var pattern = $"{agentName} --inbox";
            var pids = ProcessUtils.FindProcessesByCommandLine(pattern);

            foreach (var pid in pids)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    if (ShellProcessNames.Contains(proc.ProcessName))
                        continue;
                    proc.Kill();
                }
                catch { }
            }
        }
    }

    internal static bool TryCloseWindow(string windowId)
    {
        try
        {
            var wtPath = ResolveWtExe();
            if (wtPath == null) return false;

            var psi = new ProcessStartInfo(wtPath, $"-w {windowId} close")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(5000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolve the full path to wt.exe. Background processes may not have
    /// the MSIX alias directory on PATH, so check the known location first.
    /// </summary>
    internal static string? ResolveWtExe()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var alias = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
            if (File.Exists(alias)) return alias;
        }

        // Fall back to PATH lookup
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir, "wt.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        return null;
    }

    internal static void ClearAutoClose(string statePath)
    {
        try
        {
            var content = File.ReadAllText(statePath);
            var updated = content.Replace("auto-close: true", "auto-close: false");
            if (updated != content)
                File.WriteAllText(statePath, updated);
        }
        catch { }
    }

    internal static (bool autoClose, bool isFree, string? agentName, string? windowId) ParseStateForWatchdog(string statePath)
    {
        try
        {
            var content = File.ReadAllText(statePath);
            if (!content.StartsWith("---")) return (false, false, null, null);

            var endIndex = content.IndexOf("---", 3);
            if (endIndex < 0) return (false, false, null, null);

            var yaml = content[3..endIndex];
            bool autoClose = false, isFree = false;
            string? agentName = null, windowId = null;

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
                    case "window-id":
                        windowId = value is "null" or "" ? null : value;
                        break;
                }
            }

            return (autoClose, isFree, agentName, windowId);
        }
        catch
        {
            return (false, false, null, null);
        }
    }
}
