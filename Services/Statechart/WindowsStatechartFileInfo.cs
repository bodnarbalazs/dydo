namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsStatechartFileInfo
{
    internal uint Attributes;
    internal System.Runtime.InteropServices.ComTypes.FILETIME Created;
    internal System.Runtime.InteropServices.ComTypes.FILETIME Accessed;
    internal System.Runtime.InteropServices.ComTypes.FILETIME Written;
    internal uint Volume;
    internal uint SizeHigh;
    internal uint SizeLow;
    internal uint Links;
    internal uint IndexHigh;
    internal uint IndexLow;
}
