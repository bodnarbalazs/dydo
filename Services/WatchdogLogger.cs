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

    private sealed record ModelCapRestoreEvent(
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("fallback")] string Fallback);

    [JsonSerializable(typeof(ModelCapRestoreEvent))]
    private partial class WatchdogLogJsonContext : JsonSerializerContext { }
}
