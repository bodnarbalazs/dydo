namespace DynaDocs.Services;

using System.Diagnostics;
using System.Runtime.InteropServices;

public static class TerminalCloser
{
    /// <summary>
    /// When set, SpawnDelayedKill uses this instead of Process.Start.
    /// Prevents tests from killing the real Claude process.
    /// </summary>
    public static IProcessStarter? ProcessStarterOverride { get; set; }

    /// <summary>
    /// Walks the process tree to find the Claude CLI process and schedules its termination.
    /// Gives the CLI 3 seconds to render its final response before terminating.
    /// </summary>
    public static void ScheduleClaudeTermination()
    {
        var claudePid = ProcessUtils.FindAncestorProcess("claude");
        if (claudePid == null)
        {
            Console.WriteLine("  Auto-close failed: could not find Claude process in ancestor chain. Close this terminal manually.");
            return;
        }

        SpawnDelayedKill(claudePid.Value);
    }

    public static void SpawnDelayedKill(int pid)
    {
        ProcessStartInfo psi;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo(ProcessUtils.ResolvePowerShell())
            {
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"Start-Sleep 3; Stop-Process -Id {pid} -Force -ErrorAction SilentlyContinue\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            psi = new ProcessStartInfo("bash")
            {
                Arguments = $"-c \"sleep 3; kill -TERM {pid} 2>/dev/null\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        try
        {
            if (ProcessStarterOverride != null)
                ProcessStarterOverride.Start(psi);
            else
                Process.Start(psi);
        }
        catch
        {
            Console.WriteLine("  Auto-close failed: could not start termination process. Close this terminal manually.");
        }
    }
}
