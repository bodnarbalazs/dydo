namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobLimits
{
    public WindowsJobBasicLimits Basic;
    public WindowsJobIoCounters Io;
    public nuint ProcessMemory;
    public nuint JobMemory;
    public nuint PeakProcessMemory;
    public nuint PeakJobMemory;
}
