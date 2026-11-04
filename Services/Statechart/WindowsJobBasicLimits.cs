namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobBasicLimits
{
    public long ProcessTime;
    public long JobTime;
    public uint Flags;
    public nuint MinWorkingSet;
    public nuint MaxWorkingSet;
    public uint ActiveProcesses;
    public nuint Affinity;
    public uint Priority;
    public uint Scheduling;
}
