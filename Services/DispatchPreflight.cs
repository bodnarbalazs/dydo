namespace DynaDocs.Services;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DynaDocs.Models;

/// <summary>
/// Fail-fast gate run by <see cref="DispatchService.Execute"/> BEFORE any reservation or
/// launch (issue 0239 generalized, DR 037 §6). Each check names the missing prerequisite
/// AND the fix, so a dispatch that cannot succeed fails in the dispatcher's terminal —
/// never as a downstream child-terminal CommandNotFoundException or a stale <c>Dispatched</c>
/// reservation the watchdog has to reclaim. The first failing check short-circuits; a passing
/// preflight makes no state change.
/// </summary>
public static class DispatchPreflight
{
    // Test seams. The codex-specific checks (#3 sandbox, #4 hook trust) resolve their live
    // prerequisite off-machine; these Funcs let tests drive both branches deterministically.
    internal static Func<bool>? SandboxPrerequisiteProbeOverride { get; set; }
    internal static Func<string, bool>? HookTrustResolverOverride { get; set; }

    /// <summary>User-level codex home (<c>~/.codex</c>). Overridable so tests never read or
    /// write the real user's codex config.</summary>
    internal static string? CodexHomeOverride { get; set; }

    /// <summary>Probes whether the override target vendor's compiled agent bodies are present
    /// (host, projectRoot) — the "was <c>dydo sync</c> run for this vendor" signal. Overridable
    /// so tests drive both branches without laying down real <c>.claude</c>/<c>.codex</c> trees.</summary>
    internal static Func<string, string?, bool>? SyncedBodiesProbeOverride { get; set; }

    public static PreflightResult Run(DydoConfig? config, string launchHost, DispatchOptions opts, string? projectRoot)
    {
        var executable = CheckExecutableResolvable(launchHost, opts.NoLaunch);
        if (!executable.Ok) return executable;

        var vendor = CheckVendorConfigured(config, opts.HostOverride);
        if (!vendor.Ok) return vendor;

        var bodies = CheckSyncedBodies(config, opts.HostOverride, projectRoot);
        if (!bodies.Ok) return bodies;

        var posture = CheckCodexPostureValid(config, launchHost);
        if (!posture.Ok) return posture;

        var sandbox = CheckWindowsSandboxPrerequisite(config, launchHost, opts.NoLaunch);
        if (!sandbox.Ok) return sandbox;

        var trust = CheckHookTrust(launchHost, projectRoot);
        if (!trust.Ok) return trust;

        return PreflightResult.Pass;
    }

    // (1) The launch binary must resolve on the dispatcher's PATH. TerminalLauncher throws when
    // codex resolves only to the non-launchable WindowsApps alias (#227) — the smoke's real
    // failure, which today surfaces as a CommandNotFoundException in the child terminal. Surface
    // it here instead, naming the binary and the fix.
    private static PreflightResult CheckExecutableResolvable(string launchHost, bool noLaunch)
    {
        if (noLaunch)
            return PreflightResult.Pass;

        try
        {
            _ = TerminalLauncher.GetLaunchExecutable(launchHost);
            return PreflightResult.Pass;
        }
        catch (Exception ex)
        {
            return PreflightResult.Fail(
                $"Cannot launch {launchHost}: {ex.Message} " +
                $"Install a launchable {launchHost} CLI on your PATH (or dispatch with --no-launch and start it manually), then re-dispatch.");
        }
    }

    // (2) A vendor named by an explicit --claude/--codex override must actually be configured:
    // its integration enabled and, when a models section is present, its tier mapping bound.
    // Only fires on an explicit override — the default/inherited host is the caller's own live
    // host, already known good.
    private static PreflightResult CheckVendorConfigured(DydoConfig? config, string? hostOverride)
    {
        var host = AgentSession.NormalizeHost(hostOverride);
        if (host is not ("claude" or "codex"))
            return PreflightResult.Pass;

        var vendor = ModelVendorFor(host);

        if (config?.Integrations is { } integrations
            && integrations.TryGetValue(host, out var enabled) && !enabled)
            return PreflightResult.Fail(
                $"The --{host} launch target is disabled: dydo.json integrations.{host} is false. " +
                $"Enable it (set integrations.{host}: true, or run `dydo init --{host}`) and run `dydo sync`, then re-dispatch.");

        // An absent models section means every agent inherits the session model — nothing to
        // map. A present-but-partial map that binds other vendors but not this one is a real gap.
        if (config?.Models is { Tiers.Count: > 0 } models && !models.Tiers.ContainsKey(vendor))
            return PreflightResult.Fail(
                $"The --{host} launch target has no model tier mapping: dydo.json models.tiers.{vendor} is absent. " +
                $"Add the {vendor} tier bindings and run `dydo sync`, then re-dispatch.");

        return PreflightResult.Pass;
    }

    // (2b) A vendor named by an explicit --claude/--codex override that is ON but never synced must
    // still be caught: `dydo sync` emits the vendor's spawnable agent bodies (claude:
    // .claude/agents/<role>.md, codex: .codex/agents/<role>.toml). An integration that is enabled
    // yet never compiled passes the config checks yet launches an agent with NO role definition —
    // the fail-downstream class issue 0239 targets (a child terminal running the host CLI against
    // an empty agents surface). Probe the artifacts and fail fast naming the fix (`dydo sync`).
    // Fires only on an explicit override AND only when that vendor's integration is explicitly
    // enabled (integrations.<host> == true): the default/inherited host is the caller's own live
    // host already running against its bodies, and an absent/disabled integration is the
    // vendor-configured check's concern, not a sync obligation.
    private static PreflightResult CheckSyncedBodies(DydoConfig? config, string? hostOverride, string? projectRoot)
    {
        var host = AgentSession.NormalizeHost(hostOverride);
        if (host is not ("claude" or "codex") || projectRoot == null)
            return PreflightResult.Pass;

        if (config?.Integrations is not { } integrations
            || !integrations.TryGetValue(host, out var enabled) || !enabled)
            return PreflightResult.Pass;

        var present = (SyncedBodiesProbeOverride ?? DefaultSyncedBodies)(host, projectRoot);
        if (present)
            return PreflightResult.Pass;

        return PreflightResult.Fail(
            $"The --{host} launch target has no compiled agent bodies: {AgentArtifactLabel(host)} " +
            "is empty — `dydo sync` was never run for this vendor, so the launched agent would have " +
            "no compiled role definition. Run `dydo sync`, then re-dispatch.");
    }

    // (3) The codex launch posture (dispatch.codex) is emitted on every codex command line. An
    // invalid sandbox/approval value would otherwise fail deep in argument assembly — after the
    // agent is reserved — and be re-thrown by the launcher's manual-command fallback, crashing the
    // dispatch (issue 0253 round-2 review). Validate it here so a config typo fails fast with the
    // accepted values, before any reservation.
    private static PreflightResult CheckCodexPostureValid(DydoConfig? config, string launchHost)
    {
        if (launchHost != "codex")
            return PreflightResult.Pass;

        var errors = (config?.Dispatch.Codex ?? new CodexDispatchConfig()).Validate();
        if (errors.Count == 0)
            return PreflightResult.Pass;

        return PreflightResult.Fail(
            "Invalid dispatch.codex configuration: " + string.Join("; ", errors) +
            ". Fix dydo.json, then re-dispatch.");
    }

    // (4) The codex posture needs the codex Windows sandbox only when the configured sandbox mode
    // enforces it (workspace-write — co-think outcome 1). When that prerequisite is absent (the
    // smoke's missing codex-windows-sandbox-setup.exe), fail fast pointing at the documented setup —
    // the sandbox is the enforcement boundary and is NEVER silently downgraded (co-think outcome 4).
    // Modes that do not require the OS sandbox (read-only, danger-full-access) skip the check.
    private static PreflightResult CheckWindowsSandboxPrerequisite(DydoConfig? config, string launchHost, bool noLaunch)
    {
        if (noLaunch || launchHost != "codex" || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PreflightResult.Pass;

        if (!(config?.Dispatch.Codex ?? new CodexDispatchConfig()).RequiresWindowsSandbox)
            return PreflightResult.Pass;

        var available = (SandboxPrerequisiteProbeOverride ?? DefaultSandboxPrerequisite)();
        if (available)
            return PreflightResult.Pass;

        return PreflightResult.Fail(
            "Codex dispatch on Windows requires the codex sandbox prerequisite, which is not installed " +
            "(codex-windows-sandbox-setup.exe absent). Install it per the codex Windows sandbox setup " +
            "(dydo/reference/configuration.md), then re-dispatch. The sandbox is never silently downgraded.");
    }

    // (5) A repo .codex/hooks.json is SILENTLY skipped unless it is trust-enabled in the
    // user-level codex config ([hooks.state] in ~/.codex/config.toml, path-keyed and
    // SHA256-pinned). An untrusted hook means an UNGUARDED agent, so fail fast with the re-trust
    // instruction. When the repo carries no hooks.json there is nothing to trust-check.
    private static PreflightResult CheckHookTrust(string launchHost, string? projectRoot)
    {
        if (launchHost != "codex" || projectRoot == null)
            return PreflightResult.Pass;

        var hooksPath = Path.Combine(projectRoot, ".codex", "hooks.json");
        if (!File.Exists(hooksPath))
            return PreflightResult.Pass;

        var trusted = (HookTrustResolverOverride ?? DefaultHookTrust)(hooksPath);
        if (trusted)
            return PreflightResult.Pass;

        return PreflightResult.Fail(
            $"Codex dispatch would run UNGUARDED: {hooksPath} is not trust-enabled in your codex config " +
            "([hooks.state] in ~/.codex/config.toml). Codex silently skips untrusted hooks, so the dydo guard " +
            "never fires. Trust it — run codex once in this repo and approve the hook (or add the trust entry) — " +
            "then re-dispatch.");
    }

    private static string ModelVendorFor(string host) =>
        host == "codex" ? "openai" : "anthropic";

    // The vendor's compiled-agent surface (SyncCommand): worker roles emit .md (claude) / .toml
    // (codex) under the host's agents directory. A directory that exists and holds at least one
    // artifact of the expected shape is the "sync ran for this vendor" signal.
    private static (string Dir, string Pattern) AgentArtifactSurface(string host, string projectRoot) =>
        host == "codex"
            ? (Path.Combine(projectRoot, ".codex", "agents"), "*.toml")
            : (Path.Combine(projectRoot, ".claude", "agents"), "*.md");

    private static string AgentArtifactLabel(string host) =>
        host == "codex" ? ".codex/agents/*.toml" : ".claude/agents/*.md";

    private static bool DefaultSyncedBodies(string host, string? projectRoot)
    {
        if (projectRoot == null)
            return true;
        var (dir, pattern) = AgentArtifactSurface(host, projectRoot);
        return Directory.Exists(dir) && Directory.EnumerateFiles(dir, pattern).Any();
    }

    // The posture-config trigger (workspace-write requires the sandbox) is wired in
    // CheckWindowsSandboxPrerequisite; this default probe returns present until c1-8 verifies the
    // live check, so a correctly-set-up host is never falsely blocked. Tests drive the missing
    // branch through SandboxPrerequisiteProbeOverride.
    private static bool DefaultSandboxPrerequisite() => true;

    // Best-effort read of the codex trust ledger. The exact [hooks.state] schema is pinned live
    // in c1-8; the shape assumed here is a path-keyed inline table carrying per-hook enable/
    // disable and a SHA256 of the trusted hooks.json content. A present entry is trusted unless
    // it is explicitly disabled or its pinned hash no longer matches the on-disk file. No config
    // (or no matching entry) means untrusted — the safe, fail-fast default.
    private static bool DefaultHookTrust(string hooksPath)
    {
        var codexHome = CodexHomeOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var configPath = Path.Combine(codexHome, "config.toml");
        if (!File.Exists(configPath))
            return false;

        var entry = FindHookStateEntry(File.ReadAllText(configPath), hooksPath);
        if (entry == null)
            return false;

        if (entry.Contains("enabled = false") || entry.Contains("enabled=false"))
            return false;

        var pinned = ExtractTomlString(entry, "sha256");
        return pinned == null || string.Equals(pinned, HashFile(hooksPath), StringComparison.OrdinalIgnoreCase);
    }

    // Returns the inline-table value for the [hooks.state] key matching hooksPath, or null.
    // Matches the key by its resolved absolute path, tolerating TOML backslash-escaping so a
    // Windows path stored as "C:\\repo\\.codex\\hooks.json" resolves against C:\repo\....
    private static string? FindHookStateEntry(string toml, string hooksPath)
    {
        var normalizedTarget = Path.GetFullPath(hooksPath);
        var inSection = false;
        foreach (var raw in toml.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inSection = line is "[hooks.state]";
                continue;
            }
            if (!inSection || line.Length == 0 || line.StartsWith('#'))
                continue;

            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;

            var key = line[..eq].Trim().Trim('"').Replace("\\\\", "\\");
            if (string.Equals(Path.GetFullPath(key), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return line[(eq + 1)..];
        }
        return null;
    }

    private static string? ExtractTomlString(string fragment, string key)
    {
        var idx = fragment.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0)
            return null;
        var open = fragment.IndexOf('"', idx);
        if (open < 0)
            return null;
        var close = fragment.IndexOf('"', open + 1);
        return close < 0 ? null : fragment[(open + 1)..close];
    }

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
