namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobAccounting
{
    public long UserTime;
    public long KernelTime;
    public long PeriodUserTime;
    public long PeriodKernelTime;
    public uint PageFaults;
    public uint TotalProcesses;
    public uint ActiveProcesses;
    public uint TerminatedProcesses;
}
