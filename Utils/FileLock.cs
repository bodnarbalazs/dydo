namespace DynaDocs.Utils;

using DynaDocs.Services;

/// <summary>
/// Shared exclusive file lock with stale lock detection.
/// Replaces duplicate WithWorktreeLock/WithQueueLock in DispatchService and QueueService.
/// </summary>
public static class FileLock
{
    /// <summary>
    /// Executes an action while holding an exclusive file lock.
    /// Creates a lock file atomically, runs the action, then cleans up.
    /// Detects and removes stale locks from dead processes.
    /// </summary>
    public static void WithExclusiveLock(string lockPath, Action action, int maxAttempts = 30, int retryDelayMs = 1000)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var lockInfo = $"{{\"Pid\":{Environment.ProcessId},\"Acquired\":\"{DateTime.UtcNow:o}\"}}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(lockInfo);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();

                try
                {
                    action();
                }
                finally
                {
                    stream.Close();
                    try { File.Delete(lockPath); } catch { }
                }
                return;
            }
            catch (IOException) when (File.Exists(lockPath))
            {
                if (TryRemoveStaleLock(lockPath))
                    continue;

                if (attempt < maxAttempts - 1)
                    Thread.Sleep(retryDelayMs);
            }
        }

        throw new TimeoutException($"Could not acquire lock after {maxAttempts} attempts. Lock file: {lockPath}");
    }

    /// <summary>
    /// Checks if a lock file was left by a dead process and removes it if so.
    /// </summary>
    public static bool TryRemoveStaleLock(string lockPath)
    {
        try
        {
            var json = File.ReadAllText(lockPath);
            var pidStart = json.IndexOf("\"Pid\":", StringComparison.Ordinal);
            if (pidStart < 0) return false;

            pidStart += 6;
            var pidEnd = json.IndexOfAny([',', '}'], pidStart);
            if (pidEnd < 0) return false;

            if (!int.TryParse(json[pidStart..pidEnd].Trim(), out var pid))
                return false;

            if (ProcessUtils.IsProcessRunning(pid))
                return false;

            File.Delete(lockPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
