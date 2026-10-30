namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsJobPort
{
    public nuint Key;
    public nint Port;
}
