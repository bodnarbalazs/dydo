namespace DynaDocs.Services.Statechart;

using System.Diagnostics;

// An exclusive lock file that another dydo process holds while it installs, re-stamps or extracts.
// A waiter retries until the holder finishes, so concurrent runs queue instead of failing.
internal static class StatechartLock
{
    // Longer than the ten-minute setup deadline a holder works under.
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(15);

    public static Task<FileStream> AcquireAsync(string path, CancellationToken token) => AcquireAsync(path, Bound, token);

    internal static async Task<FileStream> AcquireAsync(string path, TimeSpan bound, CancellationToken token)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error) when (Held(error))
            {
                if (waited.Elapsed >= bound)
                    throw new IOException($"Another dydo statechart process still holds {path} after {bound.TotalMinutes:0.#} minutes; retry when it finishes.", error);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), token);
        }
    }

    // EWOULDBLOCK on Linux, ERROR_SHARING_VIOLATION on Windows.
    private static bool Held(IOException error) => error.HResult is 11 or unchecked((int)0x80070020);
}
