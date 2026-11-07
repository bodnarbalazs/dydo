namespace DynaDocs.Services.Statechart;

using System.IO.Pipes;
using System.Text;

internal sealed class PosixStatechartChannel : IDisposable
{
    private readonly PipeStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly string _socket;

    private PosixStatechartChannel(PipeStream pipe, string socket)
    {
        _pipe = pipe;
        _socket = socket;
        _reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        _writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n" };
    }

    public static PosixStatechartChannel Listen(string name) => Listen(name, Path.GetTempPath());

    // The connector derives the same socket from the temp root it inherits.
    public static PosixStatechartChannel Listen(string name, string temp)
    {
        var socket = PosixStatechartSocket.For(name, temp);
        return new(new NamedPipeServerStream(socket, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly), socket);
    }

    // The path only admits the first connection; a run that ends by exec never disposes the listener to remove it.
    public async Task AcceptAsync()
    {
        try { await ((NamedPipeServerStream)_pipe).WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
        finally { File.Delete(_socket); }
    }

    public static async Task<PosixStatechartChannel> ConnectAsync(string name)
    {
        var socket = PosixStatechartSocket.For(name, Path.GetTempPath());
        var pipe = new NamedPipeClientStream(".", socket, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(30000);
            return new PosixStatechartChannel(pipe, socket);
        }
        catch { pipe.Dispose(); throw; }
    }

    public Task<string?> ReadAsync() => StatechartCleanup.Read(_reader);
    public Task SendAsync(string frame) => StatechartCleanup.Say(_writer, frame);

    public async Task ExpectAsync(string expected)
    {
        if (await ReadAsync().WaitAsync(TimeSpan.FromSeconds(30)) != expected)
            throw new InvalidDataException("Statechart POSIX channel lost: " + expected);
    }

    public int Export() => PosixStatechartDescriptors.Inherit(_pipe.SafePipeHandle.DangerousGetHandle());

    public void Dispose()
    {
        _writer.Dispose();
        _reader.Dispose();
        _pipe.Dispose();
    }
}
