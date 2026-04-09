namespace DynaDocs.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// Handles baseline+delta compaction for audit snapshots.
/// Eliminates massive duplication by storing only differences between snapshots.
/// </summary>
public class SnapshotCompactionService
{
    public const int MaxChainDepth = 50;

    /// <summary>
    /// Computes the delta between a snapshot and a base snapshot.
    /// </summary>
    public static SnapshotDelta ComputeDelta(ProjectSnapshot snapshot, ProjectSnapshot baseSnapshot)
    {
        var baseFiles = new HashSet<string>(baseSnapshot.Files, StringComparer.OrdinalIgnoreCase);
        var baseFolders = new HashSet<string>(baseSnapshot.Folders, StringComparer.OrdinalIgnoreCase);

        var currentFiles = new HashSet<string>(snapshot.Files, StringComparer.OrdinalIgnoreCase);
        var currentFolders = new HashSet<string>(snapshot.Folders, StringComparer.OrdinalIgnoreCase);

        var delta = new SnapshotDelta
        {
            FilesAdded = snapshot.Files.Where(f => !baseFiles.Contains(f)).ToList(),
            FilesRemoved = baseSnapshot.Files.Where(f => !currentFiles.Contains(f)).ToList(),
            FoldersAdded = snapshot.Folders.Where(f => !baseFolders.Contains(f)).ToList(),
            FoldersRemoved = baseSnapshot.Folders.Where(f => !currentFolders.Contains(f)).ToList()
        };

        // Doc links added
        foreach (var (source, targets) in snapshot.DocLinks)
        {
            if (!baseSnapshot.DocLinks.TryGetValue(source, out var baseTargets))
            {
                delta.DocLinksAdded[source] = targets;
                continue;
            }

            var baseSet = new HashSet<string>(baseTargets, StringComparer.OrdinalIgnoreCase);
            var added = targets.Where(t => !baseSet.Contains(t)).ToList();
            if (added.Count > 0)
                delta.DocLinksAdded[source] = added;
        }

        // Doc links removed
        foreach (var (source, baseTargets) in baseSnapshot.DocLinks)
        {
            if (!snapshot.DocLinks.TryGetValue(source, out var currentTargets))
            {
                delta.DocLinksRemoved[source] = baseTargets;
                continue;
            }

            var currentSet = new HashSet<string>(currentTargets, StringComparer.OrdinalIgnoreCase);
            var removed = baseTargets.Where(t => !currentSet.Contains(t)).ToList();
            if (removed.Count > 0)
                delta.DocLinksRemoved[source] = removed;
        }

        return delta;
    }

    /// <summary>
    /// Applies a delta to a base snapshot, producing a full snapshot.
    /// </summary>
    public static ProjectSnapshot ApplyDelta(ProjectSnapshot baseSnapshot, SnapshotDelta delta, string gitCommit)
    {
        var files = new HashSet<string>(baseSnapshot.Files, StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(baseSnapshot.Folders, StringComparer.OrdinalIgnoreCase);
        var docLinks = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Copy base doc links
        foreach (var (source, targets) in baseSnapshot.DocLinks)
            docLinks[source] = new List<string>(targets);

        // Apply file changes
        foreach (var f in delta.FilesRemoved) files.Remove(f);
        foreach (var f in delta.FilesAdded) files.Add(f);

        // Apply folder changes
        foreach (var f in delta.FoldersRemoved) folders.Remove(f);
        foreach (var f in delta.FoldersAdded) folders.Add(f);

        // Apply doc link removals
        foreach (var (source, targets) in delta.DocLinksRemoved)
        {
            if (!docLinks.TryGetValue(source, out var existing)) continue;
            var removeSet = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
            existing.RemoveAll(t => removeSet.Contains(t));
            if (existing.Count == 0)
                docLinks.Remove(source);
        }

        // Apply doc link additions
        foreach (var (source, targets) in delta.DocLinksAdded)
        {
            if (!docLinks.TryGetValue(source, out var existing))
            {
                docLinks[source] = new List<string>(targets);
                continue;
            }
            var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            existing.AddRange(targets.Where(t => !existingSet.Contains(t)));
        }

        return new ProjectSnapshot
        {
            GitCommit = gitCommit,
            Files = files.OrderBy(f => f).ToList(),
            Folders = folders.OrderBy(f => f).ToList(),
            DocLinks = docLinks
        };
    }

    /// <summary>
    /// Resolves a session's snapshot, following delta chains if needed.
    /// Returns the full ProjectSnapshot whether it was inline or a delta reference.
    /// </summary>
    public static ProjectSnapshot? ResolveSnapshot(
        AuditSession session,
        Func<string, SnapshotBaseline?> loadBaseline,
        Func<string, AuditSession?> loadSession,
        Dictionary<string, ProjectSnapshot>? cache = null)
    {
        // Inline snapshot (legacy or uncompacted) — return directly
        if (session.Snapshot != null)
            return session.Snapshot;

        if (session.SnapshotRef == null)
            return null;

        var cacheKey = session.SessionId;
        if (cache?.TryGetValue(cacheKey, out var cached) == true)
            return cached;

        var resolved = ResolveRef(session.SnapshotRef, session.GitHead ?? "unknown", loadBaseline, loadSession, cache, 0);

        cache?.TryAdd(cacheKey, resolved);
        return resolved;
    }

    private static ProjectSnapshot ResolveRef(
        SnapshotRef snapshotRef,
        string gitCommit,
        Func<string, SnapshotBaseline?> loadBaseline,
        Func<string, AuditSession?> loadSession,
        Dictionary<string, ProjectSnapshot>? cache,
        int depth)
    {
        if (depth > MaxChainDepth)
            throw new InvalidOperationException($"Snapshot delta chain exceeded max depth {MaxChainDepth}");

        // Try loading as a baseline first
        var baseline = loadBaseline(snapshotRef.BaseId);
        if (baseline != null)
        {
            var baseSnapshot = baseline.Snapshot;
            if (snapshotRef.Delta == null || snapshotRef.Delta.IsEmpty)
                return baseSnapshot;
            return ApplyDelta(baseSnapshot, snapshotRef.Delta, gitCommit);
        }

        // Must be a session reference — load the session and resolve its snapshot
        var baseSession = loadSession(snapshotRef.BaseId);
        if (baseSession == null)
            throw new InvalidOperationException($"Cannot resolve snapshot ref: base '{snapshotRef.BaseId}' not found as baseline or session");

        var resolvedBase = ResolveSnapshot(baseSession, loadBaseline, loadSession, cache)
            ?? throw new InvalidOperationException($"Base session '{snapshotRef.BaseId}' has no resolvable snapshot");

        if (snapshotRef.Delta == null || snapshotRef.Delta.IsEmpty)
            return resolvedBase;
        return ApplyDelta(resolvedBase, snapshotRef.Delta, gitCommit);
    }

    /// <summary>
    /// Runs full compaction on a set of sessions in an audit year folder.
    /// Unrolls all snapshots, creates an optimal baseline, rebuilds all deltas.
    /// Returns stats about the compaction.
    /// </summary>
    public static CompactionResult Compact(string yearDir)
    {
        var result = new CompactionResult();

        var sessionFiles = Directory.GetFiles(yearDir, "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith("_baseline-"))
            .OrderBy(f => f)
            .ToList();

        if (sessionFiles.Count == 0)
            return result;

        // Load all baselines in this folder
        var baselines = LoadBaselines(yearDir);

        // Phase 1: Unroll — resolve every session to its full snapshot
        var resolved = new Dictionary<string, (AuditSession Session, ProjectSnapshot? Snapshot, string FilePath)>();
        var resolveCache = new Dictionary<string, ProjectSnapshot>();

        foreach (var file in sessionFiles)
        {
            var session = LoadSession(file);
            if (session == null) continue;

            var snapshot = ResolveSnapshot(
                session,
                id => baselines.GetValueOrDefault(id),
                id => FindSessionById(sessionFiles, id),
                resolveCache);

            resolved[session.SessionId] = (session, snapshot, file);
            result.SessionsProcessed++;
        }

        // Phase 2: Find optimal baseline — use the most common git_head's snapshot
        var sessionsWithSnapshots = resolved.Values
            .Where(r => r.Snapshot != null)
            .ToList();

        if (sessionsWithSnapshots.Count == 0)
            return result;

        var gitHeadGroups = sessionsWithSnapshots
            .GroupBy(r => r.Session.GitHead ?? r.Session.SessionId)
            .OrderByDescending(g => g.Count())
            .ToList();

        result.UniqueCommits = gitHeadGroups.Count;

        var baselineSnapshot = gitHeadGroups[0].First().Snapshot!;

        // Create new baseline
        var baselineId = ComputeBaselineId(baselineSnapshot);
        var newBaseline = new SnapshotBaseline
        {
            Id = baselineId,
            Created = DateTime.UtcNow,
            Snapshot = baselineSnapshot
        };

        // Write baseline file atomically — write to temp, then rename
        var baselinePath = Path.Combine(yearDir, $"_baseline-{baselineId}.json");
        var baselineJson = JsonSerializer.Serialize(newBaseline, DydoDefaultJsonContext.Default.SnapshotBaseline);
        result.NewBaselineSizeBytes = Encoding.UTF8.GetByteCount(baselineJson);
        WriteAtomic(baselinePath, baselineJson);

        // Phase 3: Rebuild sessions with delta references, caching deltas by snapshot content
        var deltaCache = new Dictionary<string, SnapshotDelta?>();
        foreach (var (sessionId, (session, snapshot, filePath)) in resolved)
        {
            var originalSize = new FileInfo(filePath).Length;
            result.OldTotalSizeBytes += originalSize;

            if (snapshot == null)
            {
                // No snapshot — just re-measure
                result.NewTotalSizeBytes += originalSize;
                continue;
            }

            // Cache delta by snapshot content hash — identical snapshots produce identical deltas
            var snapshotHash = ComputeBaselineId(snapshot);
            if (!deltaCache.TryGetValue(snapshotHash, out var delta))
            {
                var computed = ComputeDelta(snapshot, baselineSnapshot);
                delta = computed.IsEmpty ? null : computed;
                deltaCache[snapshotHash] = delta;
            }

            // Replace inline snapshot with delta ref
            session.Snapshot = null;
            session.SnapshotRef = new SnapshotRef
            {
                BaseId = baselineId,
                Depth = 1,
                Delta = delta
            };

            // Write updated session atomically
            var json = JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AuditSession);
            WriteAtomic(filePath, json);
            result.NewTotalSizeBytes += Encoding.UTF8.GetByteCount(json);
        }

        result.NewTotalSizeBytes += result.NewBaselineSizeBytes;

        // Phase 4: Clean up old baselines
        foreach (var (id, _) in baselines)
        {
            if (id != baselineId)
            {
                var oldPath = Path.Combine(yearDir, $"_baseline-{id}.json");
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                    result.OldBaselinesRemoved++;
                }
            }
        }

        return result;
    }

    private static Dictionary<string, SnapshotBaseline> LoadBaselines(string yearDir)
    {
        var baselines = new Dictionary<string, SnapshotBaseline>();
        foreach (var file in Directory.GetFiles(yearDir, "_baseline-*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var baseline = JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.SnapshotBaseline);
                if (baseline != null)
                    baselines[baseline.Id] = baseline;
            }
            catch { /* skip corrupt baselines */ }
        }
        return baselines;
    }

    private static AuditSession? LoadSession(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, DydoDefaultJsonContext.Default.AuditSession);
        }
        catch { return null; }
    }

    private static AuditSession? FindSessionById(List<string> sessionFiles, string sessionId)
    {
        var file = sessionFiles.FirstOrDefault(f => f.Contains(sessionId));
        return file != null ? LoadSession(file) : null;
    }

    /// <summary>
    /// Write content to a file atomically: write to a temp file in the same directory,
    /// then rename. On NTFS, same-volume renames are atomic — if the process crashes
    /// mid-write, the original file remains intact.
    /// </summary>
    internal static void WriteAtomic(string targetPath, string content)
    {
        var tempPath = targetPath + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    private static string ComputeBaselineId(ProjectSnapshot snapshot)
    {
        // Deterministic ID from snapshot content
        var content = string.Join("\n", snapshot.Files) + "\n" +
                      string.Join("\n", snapshot.Folders) + "\n" +
                      snapshot.GitCommit + "\n" +
                      string.Join("\n", snapshot.DocLinks
                          .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                          .Select(kv => kv.Key + ":" + string.Join(",", kv.Value.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }
}

