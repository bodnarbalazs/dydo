namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;
using System.IO.Pipes;

public class StatechartOuterLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivatePipeLossOrUnexpectedBytesCancelTheActualOwner(bool write)
    {
        using var writer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var reader = new AnonymousPipeClientStream(PipeDirection.In, writer.ClientSafePipeHandle);
        using var owner = new CancellationTokenSource();
        var observed = StatechartOuterLifetime.ObserveAsync(reader, owner);
        Assert.False(owner.IsCancellationRequested);
        if (write) await writer.WriteAsync(new byte[] { 1 });
        else writer.Dispose();
        await observed.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(owner.IsCancellationRequested);
    }
}
