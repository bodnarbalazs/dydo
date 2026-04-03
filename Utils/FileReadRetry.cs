namespace DynaDocs.Utils;

/// <summary>
/// Shared file read with retry on IO contention.
/// Replaces duplicate FileReadWithRetry in AgentRegistry and AgentSessionManager.
/// </summary>
public static class FileReadRetry
{
    /// <summary>
    /// Reads a file using FileShare.ReadWrite with exponential backoff retry.
    /// Returns null if the file cannot be read after all retries.
    /// </summary>
    public static string? Read(string path, int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                return sr.ReadToEnd();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < maxRetries - 1)
                    Thread.Sleep(50 * (int)Math.Pow(3, attempt));
            }
        }

        return null;
    }
}
