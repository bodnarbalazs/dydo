namespace DynaDocs.Tests.Statechart;

using System.Runtime.InteropServices;
using DynaDocs.Services.Statechart;

public class StatechartArgumentsTests
{
    [Fact]
    public void NativeArgumentVectorsRejectTruncatingNulBeforeAllocation()
    {
        Assert.Throws<ArgumentException>(() => new PosixStatechartArguments(["before\0after"]));
    }

    [Fact]
    public void NativeArgumentVectorsPreserveUtf8AndTerminateWithNull()
    {
        using var arguments = new PosixStatechartArguments(["spaces and é", ""]);
        Assert.Equal("spaces and é", Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(arguments.Pointer)));
        Assert.Equal("", Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(arguments.Pointer, nint.Size)));
        Assert.Equal(0, Marshal.ReadIntPtr(arguments.Pointer, 2 * nint.Size));
        arguments.Dispose();
        Assert.Equal(0, arguments.Pointer);
    }
}
