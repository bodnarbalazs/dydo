namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

// The watchdog run-loop's structured events (start/tick/kill/resume/parse_failure/poll_error/
// exit) and the resume-outcome episode event were stripped with the watchdog itself in the
// 2.1.0 campaign (DR-041). Only model_cap_restored survives — emitted by
// ModelCapService.RestoreExpired, now driven from the guard trigger.
public static partial class WatchdogLogger
{
    private const long DefaultMaxBytes = 2L * 1024 * 1024;
    private const int DefaultMaxRotations = 3;

    internal static string? LogPathOverride { get; set; }
    internal static long? MaxBytesOverride { get; set; }
    internal static int? MaxRotationsOverride { get; set; }

    private static readonly object _lock = new();

    public static string GetLogPath(string dydoRoot) =>
        LogPathOverride ?? Path.Combine(dydoRoot, "_system", ".local", "watchdog.log");

    private static void Write<T>(string dydoRoot, T payload, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            var path = GetLogPath(dydoRoot);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var line = JsonSerializer.Serialize(payload, typeInfo) + "\n";

            lock (_lock)
            {
                RotateIfNeeded(path);
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Logger MUST never throw out of its caller. Disk-full, permission denied,
            // locked file — all silently dropped.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            var max = MaxBytesOverride ?? DefaultMaxBytes;
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length < max) return;

            var rotations = MaxRotationsOverride ?? DefaultMaxRotations;
            var oldest = $"{path}.{rotations}";
            if (File.Exists(oldest)) File.Delete(oldest);
            for (var i = rotations - 1; i >= 1; i--)
            {
                var src = $"{path}.{i}";
                var dst = $"{path}.{i + 1}";
                if (File.Exists(src)) File.Move(src, dst);
            }
            File.Move(path, $"{path}.1");
        }
        catch { /* rotation failure must not kill the caller either */ }
    }

    private static string Now() => DateTime.UtcNow.ToString("O");

    public static void LogModelCapRestore(string dydoRoot, string model, string fallback) =>
        Write(dydoRoot,
            new ModelCapRestoreEvent(Now(), "model_cap_restored", model, fallback),
            WatchdogLogJsonContext.Default.ModelCapRestoreEvent);

    /// <summary>The sync daemon's one summary line per tick (ns-13): a quiet tick carries all-zero counters and the
    /// fast-path count, so the log makes plain that 99%+ of ticks did no work — the cheap-tick contract, visible.</summary>
    public static void LogSyncTick(
        string dydoRoot, int reconciled, int created, int updated, int archived, int conflicts, int fuseTrips,
        int requests, long durationMs, bool quiet, bool census) =>
        Write(dydoRoot,
            new SyncTickEvent(Now(), "sync_tick", reconciled, created, updated, archived, conflicts, fuseTrips,
                requests, durationMs, quiet, census),
            WatchdogLogJsonContext.Default.SyncTickEvent);

    /// <summary>A tick the single-flight guard skipped because the previous one was still running (ns-13) — logged
    /// once, never queued.</summary>
    public static void LogTickSkipped(string dydoRoot) =>
        Write(dydoRoot, new SimpleEvent(Now(), "tick_skipped"), WatchdogLogJsonContext.Default.SimpleEvent);

    /// <summary>A tick whose sync/API call threw (ns-13): logged loudly; the loop continues and retries next tick.</summary>
    public static void LogTickError(string dydoRoot, string message) =>
        Write(dydoRoot, new TickErrorEvent(Now(), "tick_error", message), WatchdogLogJsonContext.Default.TickErrorEvent);

    /// <summary>The daemon self-exited because its activity lease lapsed (watchdog-autostart-lease): the guard has
    /// not refreshed the activity stamp for the lease window, so the session it was serving has gone quiet — a later
    /// tool call's guard hook auto-starts a fresh daemon.</summary>
    public static void LogLeaseExpired(string dydoRoot, int leaseMinutes) =>
        Write(dydoRoot, new LeaseExpiredEvent(Now(), "lease_expired", leaseMinutes), WatchdogLogJsonContext.Default.LeaseExpiredEvent);

    /// <summary>The daemon exited because a suppress marker appeared mid-run (watchdog-autostart-lease): a <c>stop</c>
    /// raced the loop or its kill silently failed, and the per-tick hold check honored it — "stop means stop".</summary>
    public static void LogHoldHonored(string dydoRoot) =>
        Write(dydoRoot, new SimpleEvent(Now(), "hold_honored"), WatchdogLogJsonContext.Default.SimpleEvent);

    private sealed record ModelCapRestoreEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("fallback")] string Fallback);

    private sealed record SyncTickEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("reconciled")] int Reconciled,
        [property: JsonPropertyName("created")] int Created,
        [property: JsonPropertyName("updated")] int Updated,
        [property: JsonPropertyName("archived")] int Archived,
        [property: JsonPropertyName("conflicts")] int Conflicts,
        [property: JsonPropertyName("fuse_trips")] int FuseTrips,
        [property: JsonPropertyName("requests")] int Requests,
        [property: JsonPropertyName("duration_ms")] long DurationMs,
        [property: JsonPropertyName("quiet")] bool Quiet,
        [property: JsonPropertyName("census")] bool Census);

    private sealed record TickErrorEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("message")] string Message);

    private sealed record LeaseExpiredEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("lease_minutes")] int LeaseMinutes);

    private sealed record SimpleEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event);

    [JsonSerializable(typeof(ModelCapRestoreEvent))]
    [JsonSerializable(typeof(SyncTickEvent))]
    [JsonSerializable(typeof(TickErrorEvent))]
    [JsonSerializable(typeof(LeaseExpiredEvent))]
    [JsonSerializable(typeof(SimpleEvent))]
    private partial class WatchdogLogJsonContext : JsonSerializerContext { }
}
