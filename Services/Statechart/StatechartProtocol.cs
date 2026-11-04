namespace DynaDocs.Services.Statechart;

internal sealed class StatechartProtocol
{
    private int _phase;
    private readonly string _suffix;

    public StatechartProtocol(string? nonce = null) => _suffix = nonce is null ? "" : ":" + nonce;

    public void AcceptReady(string? message)
    {
        Expect(0);
        if (message != "ready:1" + _suffix)
            throw new InvalidDataException("Statechart requires one ready:1 acknowledgment.");
        _phase = 1;
    }

    public void ConfirmOwnership()
    {
        Expect(1);
        _phase = 2;
    }

    public void ConfirmScratchRegistration()
    {
        Expect(2);
        _phase = 3;
    }

    public async Task ReleaseAsync(TextWriter control, CancellationToken cancellationToken = default)
    {
        Expect(3);
        _phase = 4;
        await control.WriteLineAsync(("go:1" + _suffix).AsMemory(), cancellationToken);
        await control.FlushAsync(cancellationToken);
    }

    public static async Task WaitForGoAsync(TextReader control, TextWriter acknowledgment,
        CancellationToken cancellationToken = default)
    {
        await acknowledgment.WriteLineAsync("ready:1".AsMemory(), cancellationToken);
        await acknowledgment.FlushAsync(cancellationToken);
        if (await control.ReadLineAsync(cancellationToken) != "go:1")
            throw new InvalidDataException("Statechart owner closed or rejected the startup gate.");
    }

    private void Expect(int phase)
    {
        if (_phase != phase)
            throw new InvalidOperationException("Statechart startup acknowledgments are out of order.");
    }
}
