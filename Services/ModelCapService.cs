namespace DynaDocs.Services;

using System.Globalization;
using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Utils;

/// <summary>
/// The operational half of the model-cap mechanism (issue #214). When a tier's bound model
/// becomes unavailable — the canonical case is Fable hitting its weekly spend cap — an operator
/// runs <c>dydo model cap &lt;model&gt; --until &lt;T&gt;</c>: every tier binding pointing at that
/// model is rebound to a declared fallback, <c>dydo sync</c> re-emits the native agents on the
/// fallback, and a local marker records what to put back. The watchdog restores it once
/// <c>T</c> passes; <c>dydo model uncap</c> does the same on demand.
///
/// This is deliberately a config swap plus a re-sync — not a runtime failover interceptor — so it
/// stays out of Anthropic's lane and is disposable the day native spend-cap failover ships.
/// </summary>
public static class ModelCapService
{
    /// <summary>Test hook: replaces the real <c>dydo sync</c> re-run so tests exercising the
    /// cap/restore config surgery don't emit native agent files. Receives the resolved project
    /// root so a test can assert the re-sync targets the right project (not the process CWD).</summary>
    internal static Func<string, int>? ResyncOverride { get; set; }

    private static int Resync(string projectRoot) => ResyncOverride?.Invoke(projectRoot) ?? SyncCommand.Execute(projectRoot);

    private static string MarkerDir(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "model-caps");

    private static string MarkerPath(string dydoRoot, string model) =>
        Path.Combine(MarkerDir(dydoRoot), PathUtils.SanitizeForFilename(model) + ".json");

    /// <summary>
    /// Rebinds every tier currently (or previously) pointing at <paramref name="model"/> to the
    /// fallback, re-syncs, and writes the restore marker. Re-capping is idempotent: an existing
    /// marker's rebound tiers are folded back in so a second cap (e.g. to extend the reset time or
    /// change the fallback) never loses the record of what to restore.
    /// </summary>
    public static int Cap(string model, DateTimeOffset until, string? fallbackOverride,
        TextWriter @out, TextWriter err, string? startPath = null)
    {
        var config = new ConfigService();
        var configPath = config.FindConfigFile(startPath);
        if (configPath == null)
        {
            err.WriteLine("model cap: not inside a dydo project (no dydo.json found).");
            return ExitCodes.ToolError;
        }

        var loaded = config.LoadConfig(startPath);
        if (loaded?.Models is not { } models)
        {
            err.WriteLine("model cap: dydo.json has no models section — nothing to cap.");
            return ExitCodes.ValidationErrors;
        }

        var fallback = string.IsNullOrWhiteSpace(fallbackOverride) ? models.Fallback : fallbackOverride;
        if (string.IsNullOrWhiteSpace(fallback))
        {
            err.WriteLine("model cap: no --fallback given and models.fallback is not set in dydo.json.");
            return ExitCodes.ValidationErrors;
        }
        if (fallback == model)
        {
            err.WriteLine("model cap: --fallback must differ from the capped model.");
            return ExitCodes.ValidationErrors;
        }
        // Boundary validation: a non-future reset (a typo'd date, or a year-omitted reset that
        // resolved to the past) would make the watchdog restore the cap on its very next tick,
        // silently undoing it. Reject it here rather than write a self-defeating marker.
        if (until <= DateTimeOffset.Now)
        {
            err.WriteLine($"model cap: --until {until:yyyy-MM-dd HH:mm} is not in the future.");
            return ExitCodes.ValidationErrors;
        }

        var dydoRoot = config.GetDydoRoot(startPath);
        var existing = LoadMarker(MarkerPath(dydoRoot, model));

        // The tiers to move = those pointing at the model now, unioned with any a prior cap already
        // moved (recorded in the marker) — so restore still knows the full set after a re-cap.
        var toRebind = new List<ModelCapBinding>();
        foreach (var (vendor, tiers) in models.Tiers)
            foreach (var (tier, boundModel) in tiers)
                if (boundModel == model || (existing?.ReboundTiers.Any(b => b.Vendor == vendor && b.Tier == tier) ?? false))
                    toRebind.Add(new ModelCapBinding { Vendor = vendor, Tier = tier });

        if (toRebind.Count == 0)
        {
            err.WriteLine($"model cap: no tier currently binds '{model}' — nothing to cap.");
            return ExitCodes.ValidationErrors;
        }

        foreach (var b in toRebind)
            models.Tiers[b.Vendor][b.Tier] = fallback;

        config.SaveConfig(loaded, configPath);
        WriteMarker(dydoRoot, new ModelCap
        {
            Model = model,
            Fallback = fallback,
            Until = until,
            ReboundTiers = toRebind,
        });
        Resync(Path.GetDirectoryName(configPath)!);

        @out.WriteLine($"Capped {model} → {fallback} until {until:yyyy-MM-dd HH:mm} "
            + $"({toRebind.Count} tier binding(s) rebound). Re-synced native agents.");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Restores a specific model's tier bindings on demand: reverses the rebind, clears the marker,
    /// and re-syncs. The manual counterpart to the watchdog's time-based restore.
    /// </summary>
    public static int Uncap(string model, TextWriter @out, TextWriter err, string? startPath = null)
    {
        var config = new ConfigService();
        var configPath = config.FindConfigFile(startPath);
        if (configPath == null)
        {
            err.WriteLine("model uncap: not inside a dydo project (no dydo.json found).");
            return ExitCodes.ToolError;
        }

        var dydoRoot = config.GetDydoRoot(startPath);
        var markerPath = MarkerPath(dydoRoot, model);
        var cap = LoadMarker(markerPath);
        if (cap == null)
        {
            err.WriteLine($"model uncap: no active cap for '{model}'.");
            return ExitCodes.ValidationErrors;
        }

        var loaded = config.LoadConfig(startPath);
        if (loaded?.Models == null)
        {
            err.WriteLine("model uncap: dydo.json has no models section.");
            return ExitCodes.ValidationErrors;
        }

        RestoreBindings(loaded.Models, cap);
        config.SaveConfig(loaded, configPath);
        TryDelete(markerPath);
        Resync(Path.GetDirectoryName(configPath)!);

        @out.WriteLine($"Uncapped {model} — restored {cap.ReboundTiers.Count} tier binding(s). Re-synced native agents.");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Watchdog entry point: restores every cap whose reset time has passed, re-syncing once if any
    /// did. A no-op (no config load, no sync) when the marker directory is absent or empty, so it is
    /// cheap to call every tick. Returns the number of caps restored.
    /// </summary>
    public static int RestoreExpired(DateTimeOffset now, string? startPath = null)
    {
        var config = new ConfigService();
        var configPath = config.FindConfigFile(startPath);
        if (configPath == null) return 0;

        var dydoRoot = config.GetDydoRoot(startPath);
        var dir = MarkerDir(dydoRoot);
        if (!Directory.Exists(dir)) return 0;

        var expired = new List<(string path, ModelCap cap)>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var cap = LoadMarker(file);
            if (cap != null && now >= cap.Until)
                expired.Add((file, cap));
        }
        if (expired.Count == 0) return 0;

        var loaded = config.LoadConfig(startPath);
        if (loaded?.Models == null) return 0;

        foreach (var (_, cap) in expired)
            RestoreBindings(loaded.Models, cap);
        config.SaveConfig(loaded, configPath);
        foreach (var (path, cap) in expired)
        {
            TryDelete(path);
            // Watchdog-only path: a cap silently expiring is a state change worth a trace, matching
            // every other per-tick reconcile in the loop (the marker itself is gitignored).
            WatchdogLogger.LogModelCapRestore(dydoRoot, cap.Model, cap.Fallback);
        }
        Resync(Path.GetDirectoryName(configPath)!);

        return expired.Count;
    }

    /// <summary>
    /// Parses the user-stated reset time. Weekly caps state a wall-clock reset in the error, so this
    /// reads generously: <c>[yyyy-]mm-dd hh:mm</c> with the year optional (current year assumed),
    /// interpreted as local time. Returns null when nothing sensible parses.
    /// </summary>
    public static DateTimeOffset? ParseUntil(string input)
    {
        var s = input.Trim();

        string[] withYear = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm" };
        foreach (var f in withYear)
            if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local));

        string[] noYear = { "MM-dd HH:mm", "M-d H:mm", "MM/dd HH:mm" };
        foreach (var f in noYear)
            if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                // Year omitted: a weekly reset stated as "01-03" in late December means NEXT January,
                // not the one ~360 days in the past. Roll forward so a year-omitted cap never resolves
                // to a past instant (which the watchdog would restore on its very next tick).
                var candidate = new DateTime(DateTime.Now.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Local);
                if (candidate <= DateTime.Now)
                    candidate = candidate.AddYears(1);
                return new DateTimeOffset(candidate);
            }

        // Last resort: honor anything the framework can read (e.g. an ISO string), assuming local.
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var any)
            ? new DateTimeOffset(any)
            : null;
    }

    private static void RestoreBindings(ModelsConfig models, ModelCap cap)
    {
        foreach (var b in cap.ReboundTiers)
            if (models.Tiers.TryGetValue(b.Vendor, out var tiers) && tiers.ContainsKey(b.Tier))
                tiers[b.Tier] = cap.Model;
    }

    private static ModelCap? LoadMarker(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), DydoDefaultJsonContext.Default.ModelCap);
        }
        catch { return null; }
    }

    private static void WriteMarker(string dydoRoot, ModelCap cap)
    {
        Directory.CreateDirectory(MarkerDir(dydoRoot));
        File.WriteAllText(MarkerPath(dydoRoot, cap.Model),
            JsonSerializer.Serialize(cap, DydoDefaultJsonContext.Default.ModelCap));
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
