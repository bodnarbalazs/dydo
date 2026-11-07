namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobIoCounters
{
    public ulong ReadOps;
    public ulong WriteOps;
    public ulong OtherOps;
    public ulong ReadBytes;
    public ulong WriteBytes;
    public ulong OtherBytes;
}
