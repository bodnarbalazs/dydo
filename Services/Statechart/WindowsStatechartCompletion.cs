namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class WindowsStatechartCompletion(nint inheritedPort) : IDisposable
{
    private readonly SafeFileHandle _port = new(inheritedPort, ownsHandle: true);

    public bool WaitForEmpty(TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        var sawProcess = false;
        while (watch.Elapsed < timeout)
        {
            var remaining = (uint)Math.Max(1, Math.Ceiling((timeout - watch.Elapsed).TotalMilliseconds));
            if (!WindowsStatechartNative.GetQueuedCompletionStatus(_port, out var message,
                    out var key, out _, remaining))
            {
                if (Marshal.GetLastWin32Error() == 258) return false;
                throw new Win32Exception();
            }
            if (key != WindowsStatechartJob.CompletionKey) return false;
            if (message == 6) sawProcess = true; // JOB_OBJECT_MSG_NEW_PROCESS
            if (message == 4 && sawProcess) return true; // JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO
        }
        return false;
    }

    public void Dispose() => _port.Dispose();
}
