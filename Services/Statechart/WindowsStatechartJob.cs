namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class WindowsStatechartJob : IDisposable
{
    private readonly SafeFileHandle _job;
    private bool _sealed;

    public WindowsStatechartJob()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        _job = WindowsStatechartNative.CreateJobObject(0, 0);
        if (_job.IsInvalid) throw new Win32Exception();
        try
        {
            var limits = new WindowsJobLimits { Basic = new WindowsJobBasicLimits { Flags = 0x2000 } };
            Check(WindowsStatechartNative.SetInformationJobObject(_job, 9, ref limits,
                (uint)Marshal.SizeOf<WindowsJobLimits>()));
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
        using var process = WindowsStatechartNative.OpenProcess(0x101, false, pid);
        if (process.IsInvalid) throw new Win32Exception();
        Check(WindowsStatechartNative.AssignProcessToJobObject(_job, process));
    }

    public bool Contains(int pid)
    {
        using var process = WindowsStatechartNative.OpenProcess(0x1000, false, pid);
        if (process.IsInvalid) throw new Win32Exception();
        Check(WindowsStatechartNative.IsProcessInJob(process, _job, out var member));
        return member;
    }

    public uint ActiveProcesses()
    {
        Check(WindowsStatechartNative.QueryInformationJobObject(_job, 1, out var accounting,
            (uint)Marshal.SizeOf<WindowsJobAccounting>(), 0));
        return accounting.ActiveProcesses;
    }

    public void Terminate() => Check(WindowsStatechartNative.TerminateJobObject(_job, 2));

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

    private static void Check(bool result)
    {
        if (!result) throw new Win32Exception();
    }
}
