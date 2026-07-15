namespace DynaDocs.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

// The watchdog run-loop's structured events (start/tick/kill/resume/parse_failure/poll_error/
// exit) were stripped with the watchdog itself in the 2.1.0 campaign (DR-041). Only the two
// events with surviving KEEP callers remain: resume_outcome (RecoveryClassifier's guard-side
// recovery emit) and model_cap_restored (ModelCapService.RestoreExpired).
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

    /// <summary>
    /// Terminal-state event for a resume episode (PR3 of agent-crash-fixes): "succeeded"
    /// (same-session reclaim observed), "failed" (launched PID dead past warmup), or "gave_up"
    /// (cap reached without a refresh).
    /// </summary>
    public static void LogResumeOutcome(string dydoRoot, string agent, string sessionId, string outcome,
                                        int attempts, int elapsedSeconds, string reason) =>
        Write(dydoRoot,
            new ResumeOutcomeEvent(Now(), "resume_outcome", agent, sessionId, outcome, attempts, elapsedSeconds, reason),
            WatchdogLogJsonContext.Default.ResumeOutcomeEvent);

    public static void LogModelCapRestore(string dydoRoot, string model, string fallback) =>
        Write(dydoRoot,
            new ModelCapRestoreEvent(Now(), "model_cap_restored", model, fallback),
            WatchdogLogJsonContext.Default.ModelCapRestoreEvent);

    private sealed record ResumeOutcomeEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("agent")] string Agent,
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("outcome")] string Outcome,
        [property: JsonPropertyName("attempts")] int Attempts,
        [property: JsonPropertyName("elapsed_seconds")] int ElapsedSeconds,
        [property: JsonPropertyName("reason")] string Reason);

    private sealed record ModelCapRestoreEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("fallback")] string Fallback);

    [JsonSerializable(typeof(ResumeOutcomeEvent))]
    [JsonSerializable(typeof(ModelCapRestoreEvent))]
    private partial class WatchdogLogJsonContext : JsonSerializerContext { }
}
