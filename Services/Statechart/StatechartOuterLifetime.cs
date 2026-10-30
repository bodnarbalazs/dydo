namespace DynaDocs.Services.Statechart;

internal static class StatechartOuterLifetime
{
    public static async Task ObserveAsync(Stream input, CancellationTokenSource owner)
    {
        try
        {
            // This private channel carries no data: EOF and unexpected bytes both fail closed.
            await input.ReadExactlyAsync(new byte[1]);
        }
        catch (IOException) { }
        try { await owner.CancelAsync(); }
        catch (ObjectDisposedException) { }
    }
}
