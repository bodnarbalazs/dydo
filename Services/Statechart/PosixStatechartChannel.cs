namespace DynaDocs.Services.Statechart;

using System.IO.Pipes;
using System.Text;

internal sealed class PosixStatechartChannel : IDisposable
{
    private readonly PipeStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private PosixStatechartChannel(PipeStream pipe)
    {
        _pipe = pipe;
        _reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        _writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\n" };
    }

    public static PosixStatechartChannel Listen(string name) => new(new NamedPipeServerStream(name,
        PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly));

    public async Task AcceptAsync() => await ((NamedPipeServerStream)_pipe).WaitForConnectionAsync()
        .WaitAsync(TimeSpan.FromSeconds(30));

    public static async Task<PosixStatechartChannel> ConnectAsync(string name)
    {
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(30000);
            return new PosixStatechartChannel(pipe);
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
