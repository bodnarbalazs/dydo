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
    /// Spawns a delayed kill of the ancestor Claude process.
    /// Gives Claude 3 seconds to render its final response before terminating.
    /// </summary>
    public static void ScheduleClaudeTermination()
    {
        var claudePid = ProcessUtils.FindAncestorProcess("claude")
                     ?? ProcessUtils.FindAncestorProcess("node");

        if (claudePid == null)
        {
            Console.WriteLine("  Could not detect Claude process. Use Ctrl+C to close.");
            return;
        }

        SpawnDelayedKill(claudePid.Value);
    }

    public static void SpawnDelayedKill(int pid)
    {
        ProcessStartInfo psi;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo("powershell")
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
            Console.WriteLine("  Could not schedule auto-close. Use Ctrl+C to close.");
        }
    }
}
