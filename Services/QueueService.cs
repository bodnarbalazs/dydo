namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

/// <summary>
/// Manages named dispatch queues that serialize terminal launches.
/// Queues defer only the terminal launch — agent selection, inbox, and markers
/// all happen at dispatch time.
/// </summary>
public class QueueService
{
    private static readonly string[] DefaultPersistentQueues = ["merge"];

    private readonly string _queuesDir;
    private readonly DydoConfig? _config;

    public QueueService(string? dydoRoot = null, DydoConfig? config = null)
    {
        var root = dydoRoot ?? PathUtils.FindDydoRoot() ?? ".";
        // Worktree agents must share one queue — normalize to the main project root
        root = PathUtils.NormalizeWorktreePath(root) ?? root;
        _queuesDir = Path.Combine(root, "_system", ".local", "queues");
        _config = config;
    }

    internal string QueuesDir => _queuesDir;

    public List<string> GetPersistentQueues()
    {
        var configured = _config?.Queues;
        return configured is { Count: > 0 } ? configured : DefaultPersistentQueues.ToList();
    }

    public bool QueueExists(string name)
    {
        if (GetPersistentQueues().Contains(name, StringComparer.OrdinalIgnoreCase))
            return true;
        return Directory.Exists(GetQueueDir(name));
    }

    public bool IsPersistent(string name) =>
        GetPersistentQueues().Contains(name, StringComparer.OrdinalIgnoreCase);

    public string GetQueueDir(string name) =>
        Path.Combine(_queuesDir, name);

    /// <summary>
    /// Creates a transient queue. Persistent queues don't need explicit creation.
    /// Returns false with error if queue already exists.
    /// </summary>
    public bool CreateQueue(string name, out string error)
    {
        error = string.Empty;

        if (IsPersistent(name))
        {
            error = $"Queue '{name}' is a persistent queue (defined in config). No need to create it.";
            return false;
        }

        var dir = GetQueueDir(name);
        if (Directory.Exists(dir))
        {
            error = $"Queue '{name}' already exists.";
            return false;
        }

        Directory.CreateDirectory(dir);
        return true;
    }

    public QueueActiveEntry? GetActive(string queueName)
    {
        var activePath = Path.Combine(GetQueueDir(queueName), "_active.json");
        if (!File.Exists(activePath)) return null;

        try
        {
            var json = File.ReadAllText(activePath);
            return JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.QueueActiveEntry);
        }
        catch
        {
            return null;
        }
    }

    public List<(string FileName, QueueEntry Entry)> GetPending(string queueName)
    {
        var dir = GetQueueDir(queueName);
        if (!Directory.Exists(dir)) return [];

        var result = new List<(string, QueueEntry)>();
        var files = Directory.GetFiles(dir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .OrderBy(f => f)
            .ToArray();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.QueueEntry);
                if (entry != null)
                    result.Add((Path.GetFileName(file), entry));
            }
            catch { }
        }

        return result;
    }

    /// <summary>
    /// Atomically checks whether the queue slot is free. If free, marks the caller as active
    /// (with placeholder PID=0) and returns Acquired. If occupied, writes a pending entry and
    /// returns Queued. File lock prevents two dispatches from both acquiring the slot.
    /// </summary>
    public QueueResult TryAcquireOrEnqueue(string queueName, string agentName, string task,
        bool launchInTab, bool autoClose, string? worktreeId, string? windowName,
        string? workingDirOverride, string? cleanupWorktreeId, string? mainProjectRoot)
    {
        var dir = GetQueueDir(queueName);
        Directory.CreateDirectory(dir);
        var lockPath = Path.Combine(dir, ".lock");

        var result = QueueResult.Acquired;

        WithQueueLock(lockPath, () =>
        {
            var activePath = Path.Combine(dir, "_active.json");
            if (File.Exists(activePath))
            {
                var entry = new QueueEntry
                {
                    Agent = agentName,
                    Task = task,
                    LaunchInTab = launchInTab,
                    AutoClose = autoClose,
                    WorktreeId = worktreeId,
                    WindowName = windowName,
                    WorkingDirOverride = workingDirOverride,
                    CleanupWorktreeId = cleanupWorktreeId,
                    MainProjectRoot = mainProjectRoot,
                    Enqueued = DateTime.UtcNow
                };

                var seq = GetNextSequenceNumber(dir);
                var sanitizedTask = PathUtils.SanitizeForFilename(task);
                var fileName = $"{seq:D4}-{sanitizedTask}.json";
                var json = JsonSerializer.Serialize(entry, DydoDefaultJsonContext.Default.QueueEntry);
                File.WriteAllText(Path.Combine(dir, fileName), json);
                result = QueueResult.Queued;
            }
            else
            {
                // Queue free — write placeholder _active.json; caller updates PID after launch
                var active = new QueueActiveEntry
                {
                    Agent = agentName,
                    Task = task,
                    Pid = 0,
                    Started = DateTime.UtcNow
                };
                var json = JsonSerializer.Serialize(active, DydoDefaultJsonContext.Default.QueueActiveEntry);
                File.WriteAllText(activePath, json);
                result = QueueResult.Acquired;
            }
        });

        return result;
    }

    public void UpdateActivePid(string queueName, int pid)
    {
        var active = GetActive(queueName);
        if (active == null) return;
        active.Pid = pid;
        var json = JsonSerializer.Serialize(active, DydoDefaultJsonContext.Default.QueueActiveEntry);
        File.WriteAllText(Path.Combine(GetQueueDir(queueName), "_active.json"), json);
    }

    /// <summary>
    /// Tries to enqueue a terminal launch. If the queue has no active item,
    /// returns false (caller should launch immediately). If an active item exists,
    /// writes the pending entry and returns true (launch deferred).
    /// </summary>
    public bool TryEnqueue(string queueName, string agentName, string task,
        bool launchInTab, bool autoClose, string? worktreeId, string? windowName,
        string? workingDirOverride, string? cleanupWorktreeId, string? mainProjectRoot)
    {
        var dir = GetQueueDir(queueName);
        Directory.CreateDirectory(dir);

        var activePath = Path.Combine(dir, "_active.json");
        if (!File.Exists(activePath))
            return false;

        var entry = new QueueEntry
        {
            Agent = agentName,
            Task = task,
            LaunchInTab = launchInTab,
            AutoClose = autoClose,
            WorktreeId = worktreeId,
            WindowName = windowName,
            WorkingDirOverride = workingDirOverride,
            CleanupWorktreeId = cleanupWorktreeId,
            MainProjectRoot = mainProjectRoot,
            Enqueued = DateTime.UtcNow
        };

        var seq = GetNextSequenceNumber(dir);
        var sanitizedTask = PathUtils.SanitizeForFilename(task);
        var fileName = $"{seq:D4}-{sanitizedTask}.json";
        var json = JsonSerializer.Serialize(entry, DydoDefaultJsonContext.Default.QueueEntry);
        File.WriteAllText(Path.Combine(dir, fileName), json);
        return true;
    }

    /// <summary>
    /// Marks an agent as the active item in a queue. Called when a dispatch
    /// goes through (no existing active item) or when dequeuing the next entry.
    /// </summary>
    public void SetActive(string queueName, string agentName, string task, int pid)
    {
        var dir = GetQueueDir(queueName);
        Directory.CreateDirectory(dir);

        var active = new QueueActiveEntry
        {
            Agent = agentName,
            Task = task,
            Pid = pid,
            Started = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(active, DydoDefaultJsonContext.Default.QueueActiveEntry);
        File.WriteAllText(Path.Combine(dir, "_active.json"), json);
    }

    /// <summary>
    /// Clears the active entry. Called when an agent releases or when
    /// the watchdog detects a stale active entry.
    /// </summary>
    public void ClearActive(string queueName)
    {
        var activePath = Path.Combine(GetQueueDir(queueName), "_active.json");
        if (File.Exists(activePath))
            File.Delete(activePath);
    }

    /// <summary>
    /// Dequeues the next pending entry from a queue. Returns null if empty.
    /// Removes the pending file from disk.
    /// </summary>
    public QueueEntry? DequeueNext(string queueName)
    {
        var pending = GetPending(queueName);
        if (pending.Count == 0) return null;

        var (fileName, entry) = pending[0];
        var filePath = Path.Combine(GetQueueDir(queueName), fileName);
        File.Delete(filePath);
        return entry;
    }

    /// <summary>
    /// Finds all queues where the given agent is the active entry.
    /// Used during agent release to trigger dequeue.
    /// </summary>
    public List<string> FindQueuesWithActiveAgent(string agentName)
    {
        if (!Directory.Exists(_queuesDir)) return [];

        var result = new List<string>();
        foreach (var dir in Directory.GetDirectories(_queuesDir))
        {
            var active = GetActive(Path.GetFileName(dir));
            if (active != null && string.Equals(active.Agent, agentName, StringComparison.OrdinalIgnoreCase))
                result.Add(Path.GetFileName(dir));
        }
        return result;
    }

    /// <summary>
    /// Cancels a specific pending entry by its filename (without the queue directory prefix).
    /// Returns true if found and removed.
    /// </summary>
    public bool CancelEntry(string queueName, string entryId, out string error)
    {
        error = string.Empty;
        var dir = GetQueueDir(queueName);
        if (!Directory.Exists(dir))
        {
            error = $"Queue '{queueName}' not found.";
            return false;
        }

        var files = Directory.GetFiles(dir, $"{entryId}*.json");
        if (files.Length == 0)
        {
            error = $"No pending entry matching '{entryId}' in queue '{queueName}'.";
            return false;
        }

        // Read entry to get agent name for .queued marker cleanup
        QueueEntry? entry = null;
        try
        {
            var json = File.ReadAllText(files[0]);
            entry = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.QueueEntry);
        }
        catch { }

        File.Delete(files[0]);

        if (entry != null)
            ClearQueuedMarker(entry.Agent);

        return true;
    }

    /// <summary>
    /// Clears an entire queue: removes active and all pending entries.
    /// Also cleans up .queued markers on affected agents.
    /// </summary>
    public bool ClearQueue(string queueName, out string error)
    {
        error = string.Empty;
        var dir = GetQueueDir(queueName);
        if (!Directory.Exists(dir))
        {
            error = $"Queue '{queueName}' not found.";
            return false;
        }

        // Clean up .queued markers for all pending entries
        foreach (var (_, entry) in GetPending(queueName))
            ClearQueuedMarker(entry.Agent);

        foreach (var file in Directory.GetFiles(dir, "*.json"))
            File.Delete(file);

        // Delete transient queue directory if empty
        CleanupIfEmptyTransient(queueName);
        return true;
    }

    /// <summary>
    /// Removes empty transient queue directories. Called inline at dequeue
    /// and by the watchdog as a fallback sweep.
    /// </summary>
    public void CleanupIfEmptyTransient(string queueName)
    {
        if (IsPersistent(queueName)) return;

        var dir = GetQueueDir(queueName);
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0)
        {
            try { Directory.Delete(dir); } catch { }
        }
    }

    /// <summary>
    /// Sweeps all queue directories for empty transient queues.
    /// Called by the watchdog.
    /// </summary>
    public void CleanupAllEmptyTransient()
    {
        if (!Directory.Exists(_queuesDir)) return;

        foreach (var dir in Directory.GetDirectories(_queuesDir))
        {
            var name = Path.GetFileName(dir);
            CleanupIfEmptyTransient(name);
        }
    }

    /// <summary>
    /// Finds active queue entries where the PID is no longer running.
    /// Returns (queueName, activeEntry) pairs for stale entries.
    /// </summary>
    public List<(string QueueName, QueueActiveEntry Entry)> FindStaleActiveEntries()
    {
        if (!Directory.Exists(_queuesDir)) return [];

        var result = new List<(string, QueueActiveEntry)>();
        foreach (var dir in Directory.GetDirectories(_queuesDir))
        {
            var name = Path.GetFileName(dir);
            var active = GetActive(name);
            if (active != null && !ProcessUtils.IsProcessRunning(active.Pid))
                result.Add((name, active));
        }
        return result;
    }

    /// <summary>
    /// Lists all known queues (persistent + any transient directories that exist).
    /// </summary>
    public List<string> ListQueues()
    {
        var queues = new HashSet<string>(GetPersistentQueues(), StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(_queuesDir))
        {
            foreach (var dir in Directory.GetDirectories(_queuesDir))
                queues.Add(Path.GetFileName(dir));
        }

        return queues.OrderBy(q => q).ToList();
    }

    public void WriteQueuedMarker(string agentName, string queueName, int position)
    {
        var dydoRoot = PathUtils.FindDydoRoot() ?? ".";
        var workspace = Path.Combine(dydoRoot, "agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".queued"), $"{queueName}:{position}");
    }

    public void ClearQueuedMarker(string agentName)
    {
        var dydoRoot = PathUtils.FindDydoRoot() ?? ".";
        var marker = Path.Combine(dydoRoot, "agents", agentName, ".queued");
        if (File.Exists(marker))
            File.Delete(marker);
    }

    private static int GetNextSequenceNumber(string dir)
    {
        var max = 0;
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var dashIndex = name.IndexOf('-');
            if (dashIndex > 0 && int.TryParse(name[..dashIndex], out var seq) && seq > max)
                max = seq;
        }
        return max + 1;
    }

    private static void WithQueueLock(string lockPath, Action action)
    {
        const int maxAttempts = 30;
        const int retryDelayMs = 1000;

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

        throw new TimeoutException($"Could not acquire queue lock after {maxAttempts}s. Lock file: {lockPath}");
    }

    private static bool TryRemoveStaleLock(string lockPath)
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
