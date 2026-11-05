namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// Each native call reads its Win32 error in the call's own statement. Anything that runs between two
// statements, a coverage visit included, may make P/Invoke calls of its own and replace that error.
internal sealed class WindowsStatechartJob : IDisposable
{
    private readonly SafeFileHandle _job;
    private bool _sealed;

    public WindowsStatechartJob()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        _job = Valid(WindowsStatechartNative.CreateJobObject(0, 0), Marshal.GetLastPInvokeError());
        try
        {
            var limits = new WindowsJobLimits { Basic = new WindowsJobBasicLimits { Flags = 0x2000 } };
            Check(WindowsStatechartNative.SetInformationJobObject(_job, 9, ref limits,
                (uint)Marshal.SizeOf<WindowsJobLimits>()), Marshal.GetLastPInvokeError());
        }
        catch
        {
            _job.Dispose();
            throw;
        }
    }

    public void Attach(int pid)
    {
        if (_sealed) throw new InvalidOperationException("Statechart checking is sealed; no further assignment.");
        using var process = Valid(WindowsStatechartNative.OpenProcess(0x101, false, pid), Marshal.GetLastPInvokeError());
        Check(WindowsStatechartNative.AssignProcessToJobObject(_job, process), Marshal.GetLastPInvokeError());
    }

    public bool Contains(int pid)
    {
        using var process = Valid(WindowsStatechartNative.OpenProcess(0x1000, false, pid), Marshal.GetLastPInvokeError());
        Check(WindowsStatechartNative.IsProcessInJob(process, _job, out var member), Marshal.GetLastPInvokeError());
        return member;
    }

    public uint ActiveProcesses()
    {
        Check(WindowsStatechartNative.QueryInformationJobObject(_job, 1, out var accounting,
            (uint)Marshal.SizeOf<WindowsJobAccounting>(), 0), Marshal.GetLastPInvokeError());
        return accounting.ActiveProcesses;
    }

    public void Terminate() =>
        Check(WindowsStatechartNative.TerminateJobObject(_job, 2), Marshal.GetLastPInvokeError());

    public async Task<bool> SealAndDrainAsync(TimeSpan timeout)
    {
        _sealed = true;
        Terminate();
        var watch = Stopwatch.StartNew();
        while (ActiveProcesses() != 0)
        {
            if (watch.Elapsed >= timeout) return false;
            await Task.Delay(10);
        }
        return true;
    }

    public void Dispose() => _job.Dispose();

    private static void Check(bool result, int error)
    {
        if (!result) throw new Win32Exception(error);
    }

    private static SafeFileHandle Valid(SafeFileHandle handle, int error)
    {
        if (!handle.IsInvalid) return handle;
        handle.Dispose();
        throw new Win32Exception(error);
    }
}
