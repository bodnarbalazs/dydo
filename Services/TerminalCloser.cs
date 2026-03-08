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
    /// Kills the grandparent process (parent of parent of current process).
    /// Process tree: CLI tool → shell → dydo, so grandparent = CLI tool.
    /// Gives the CLI 3 seconds to render its final response before terminating.
    /// </summary>
    public static void ScheduleClaudeTermination()
    {
        var myPid = Environment.ProcessId;
        var parentPid = ProcessUtils.GetParentPid(myPid);
        if (parentPid == null)
        {
            Console.WriteLine("  Could not detect parent process. Use Ctrl+C to close.");
            return;
        }

        var grandparentPid = ProcessUtils.GetParentPid(parentPid.Value);
        if (grandparentPid == null)
        {
            Console.WriteLine("  Could not detect CLI process. Use Ctrl+C to close.");
            return;
        }

        SpawnDelayedKill(grandparentPid.Value);
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
