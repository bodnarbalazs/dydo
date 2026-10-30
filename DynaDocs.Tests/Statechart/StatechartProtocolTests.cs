namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartProtocolTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task WorkerCannotRunBeforeOwnershipAndScratchRegistration(int acknowledgments)
    {
        var protocol = new StatechartProtocol();
        protocol.AcceptReady("ready:1");
        if (acknowledgments >= 1) protocol.ConfirmOwnership();
        if (acknowledgments >= 2) protocol.ConfirmScratchRegistration();
        using var output = new StringWriter();

        if (acknowledgments < 2)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.ReleaseAsync(output));
            Assert.Equal("", output.ToString());
        }
        else
        {
            await protocol.ReleaseAsync(output);
            Assert.Equal("go:1" + Environment.NewLine, output.ToString());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("ready:2")]
    [InlineData("ready:1\nready:1")]
    public void UnsupportedOrMalformedReadyCannotEstablishOwnership(string message)
    {
        var protocol = new StatechartProtocol();
        Assert.Throws<InvalidDataException>(() => protocol.AcceptReady(message));
        Assert.Throws<InvalidOperationException>(protocol.ConfirmOwnership);
    }

    [Fact]
    public async Task AcknowledgmentsCannotBeReorderedOrReplayed()
    {
        var protocol = new StatechartProtocol();
        Assert.Throws<InvalidOperationException>(protocol.ConfirmScratchRegistration);
        protocol.AcceptReady("ready:1");
        Assert.Throws<InvalidOperationException>(() => protocol.AcceptReady("ready:1"));
        protocol.ConfirmOwnership();
        Assert.Throws<InvalidOperationException>(protocol.ConfirmOwnership);
        protocol.ConfirmScratchRegistration();
        using var output = new StringWriter();
        await protocol.ReleaseAsync(output);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.ReleaseAsync(output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("go:2\n")]
    [InlineData("anything\n")]
    public async Task ParentLossAndInvalidGoFailBeforeWork(string input)
    {
        using var reader = new StringReader(input);
        using var output = new StringWriter();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StatechartProtocol.WaitForGoAsync(reader, output));
        Assert.Equal("ready:1" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public async Task ARegisteredOwnedWorkerAcceptsTheVersionedGo()
    {
        using var reader = new StringReader("go:1\n");
        using var output = new StringWriter();
        await StatechartProtocol.WaitForGoAsync(reader, output);
        Assert.Equal("ready:1" + Environment.NewLine, output.ToString());
    }
}
