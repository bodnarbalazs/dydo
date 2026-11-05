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

    // A pipe already at EOF still cancels the owner, whose registered work runs; an owner already gone is left alone.
    [Fact]
    public async Task AnAlreadyClosedPipeCancelsTheOwnerAndAGoneOwnerIsIgnored()
    {
        using var owner = new CancellationTokenSource();
        var stopped = new TaskCompletionSource();
        await using (owner.Token.Register(stopped.SetResult))
        {
            await StatechartOuterLifetime.ObserveAsync(new MemoryStream(), owner);
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(owner.IsCancellationRequested);
        var gone = new CancellationTokenSource();
        gone.Dispose();
        await StatechartOuterLifetime.ObserveAsync(new MemoryStream(), gone);
    }
}
