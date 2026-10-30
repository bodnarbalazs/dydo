namespace DynaDocs.Services.Statechart;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class WindowsStatechartJob : IDisposable
{
    private readonly SafeFileHandle _job;
    private readonly SafeFileHandle _port;
    internal const nuint CompletionKey = 0x4151;

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
            _port = WindowsStatechartNative.CreateIoCompletionPort(-1, 0, 0, 1);
            if (_port.IsInvalid) throw new Win32Exception();
            AssociateCompletionPort();
        }
        catch
        {
            _port?.Dispose();
            _job.Dispose();
            throw;
        }
    }

    public void Attach(int pid)
    {
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

    public nint DuplicateCompletionPortTo(Process cleanup)
    {
        // Duplicate the notification port only. A job handle in cleanup would defeat last-close killing.
        Check(WindowsStatechartNative.DuplicateHandle(-1, _port, cleanup.Handle,
            out var duplicate, 0, false, 2));
        return duplicate;
    }

    public void Terminate() => Check(WindowsStatechartNative.TerminateJobObject(_job, 2));

    public void Dispose()
    {
        _job.Dispose();
        _port.Dispose();
    }

    private void AssociateCompletionPort()
    {
        var association = new WindowsJobPort { Key = CompletionKey, Port = _port.DangerousGetHandle() };
        Check(WindowsStatechartNative.SetInformationJobObject(_job, 7, ref association,
            (uint)Marshal.SizeOf<WindowsJobPort>()));
    }

    private static void Check(bool result)
    {
        if (!result) throw new Win32Exception();
    }
}
