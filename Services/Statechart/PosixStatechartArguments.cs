namespace DynaDocs.Services.Statechart;

using System.Runtime.InteropServices;

internal sealed class PosixStatechartArguments : IDisposable
{
    private readonly List<nint> _strings = [];
    public nint Pointer { get; private set; }

    public PosixStatechartArguments(IEnumerable<string> arguments)
    {
        var values = arguments.ToArray();
        if (values.Any(value => value.Contains('\0')))
            throw new ArgumentException("Statechart arguments cannot contain NUL.", nameof(arguments));
        Pointer = Marshal.AllocHGlobal(checked((values.Length + 1) * nint.Size));
        try
        {
            for (var index = 0; index < values.Length; index++)
            {
                var value = Marshal.StringToCoTaskMemUTF8(values[index]);
                _strings.Add(value);
                Marshal.WriteIntPtr(Pointer, index * nint.Size, value);
            }
            Marshal.WriteIntPtr(Pointer, values.Length * nint.Size, 0);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var value in _strings) Marshal.FreeCoTaskMem(value);
        _strings.Clear();
        Marshal.FreeHGlobal(Pointer);
        Pointer = 0;
    }
}
