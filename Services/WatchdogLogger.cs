namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
            // Logger MUST never throw out of the watchdog loop. Disk-full,
            // permission denied, locked file — all silently dropped.
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
        catch { /* rotation failure must not kill the watchdog either */ }
    }

    private static string Now() => DateTime.UtcNow.ToString("O");

    public static void LogStart(string dydoRoot, int? anchorPid, string? anchorName, int pollIntervalMs, int anchorCount) =>
        Write(dydoRoot,
            new StartEvent(Now(), "start", anchorPid, anchorName, anchorCount, pollIntervalMs, Environment.ProcessId),
            WatchdogLogJsonContext.Default.StartEvent);

    public static void LogTick(string dydoRoot, int agentsObserved, int killsAttempted) =>
        Write(dydoRoot,
            new TickEvent(Now(), "tick", agentsObserved, killsAttempted),
            WatchdogLogJsonContext.Default.TickEvent);

    public static void LogKill(string dydoRoot, string agent, int targetPid, string? targetProc,
                               string pattern, string status, bool autoClose, string? dispatchedBy, string? since) =>
        Write(dydoRoot,
            new KillEvent(Now(), "kill", agent, targetPid, targetProc, pattern,
                new KillState(status, autoClose, dispatchedBy, since)),
            WatchdogLogJsonContext.Default.KillEvent);

    public static void LogParseFailure(string dydoRoot, string statePath, string reason) =>
        Write(dydoRoot,
            new ParseFailureEvent(Now(), "parse_failure", statePath, reason),
            WatchdogLogJsonContext.Default.ParseFailureEvent);

    public static void LogPollError(string dydoRoot, string error) =>
        Write(dydoRoot,
            new PollErrorEvent(Now(), "poll_error", error),
            WatchdogLogJsonContext.Default.PollErrorEvent);

    public static void LogExit(string dydoRoot, string reason) =>
        Write(dydoRoot,
            new ExitEvent(Now(), "exit", reason),
            WatchdogLogJsonContext.Default.ExitEvent);

    private sealed record StartEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("anchor_pid")] int? AnchorPid,
        [property: JsonPropertyName("anchor_name")] string? AnchorName,
        [property: JsonPropertyName("anchor_count")] int AnchorCount,
        [property: JsonPropertyName("poll_interval_ms")] int PollIntervalMs,
        [property: JsonPropertyName("watchdog_pid")] int WatchdogPid);

    private sealed record TickEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("agents")] int Agents,
        [property: JsonPropertyName("kills_attempted")] int KillsAttempted);

    private sealed record KillEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("agent")] string Agent,
        [property: JsonPropertyName("target_pid")] int TargetPid,
        [property: JsonPropertyName("target_proc")] string? TargetProc,
        [property: JsonPropertyName("pattern")] string Pattern,
        [property: JsonPropertyName("state")] KillState State);

    private sealed record KillState(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("auto_close")] bool AutoClose,
        [property: JsonPropertyName("dispatched_by")] string? DispatchedBy,
        [property: JsonPropertyName("since")] string? Since);

    private sealed record ParseFailureEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("state_path")] string StatePath,
        [property: JsonPropertyName("reason")] string Reason);

    private sealed record PollErrorEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("error")] string Error);

    private sealed record ExitEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("reason")] string Reason);

    [JsonSerializable(typeof(StartEvent))]
    [JsonSerializable(typeof(TickEvent))]
    [JsonSerializable(typeof(KillEvent))]
    [JsonSerializable(typeof(ParseFailureEvent))]
    [JsonSerializable(typeof(PollErrorEvent))]
    [JsonSerializable(typeof(ExitEvent))]
    private partial class WatchdogLogJsonContext : JsonSerializerContext { }
}
