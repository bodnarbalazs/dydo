namespace DynaDocs.Services;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DynaDocs.Models;

/// <summary>
/// Fail-fast gate run by <see cref="DispatchService.Execute"/> BEFORE any reservation or
/// launch (issue 0239 generalized, DR 037 §6). Each check names the missing prerequisite
/// AND the fix, so a dispatch that cannot succeed fails in the dispatcher's terminal —
/// never as a downstream child-terminal CommandNotFoundException or a stale <c>Dispatched</c>
/// reservation the watchdog has to reclaim. The first failing check short-circuits. A passing
/// preflight is side-effect-free EXCEPT the hook-trust check (5), which may self-repair the codex
/// trust entry in <c>~/.codex/config.toml</c> on the pass path (issue 0269 self-repair).
/// </summary>
public static class DispatchPreflight
{
    // Test seams. The codex-specific checks (#3 sandbox, #4 hook trust) resolve their live
    // prerequisite off-machine; these Funcs let tests drive both branches deterministically.
    internal static Func<bool>? SandboxPrerequisiteProbeOverride { get; set; }
    internal static Func<string, HookTrust>? HookTrustResolverOverride { get; set; }

    /// <summary>Writes/repairs the pre_tool_use trust entry in the user-level codex config
    /// (issue 0269 self-repair). Returns <c>true</c> when a well-formed entry was written,
    /// <c>false</c> when the config is unwritable OR the repo path contains an apostrophe (which
    /// cannot be written as a TOML single-quoted literal key) — both fall through to the BLOCK path.
    /// Overridable so tests never write a real <c>~/.codex</c>.</summary>
    internal static Func<string, bool>? HookTrustRepairOverride { get; set; }

    /// <summary>Classification of a repo hooks.json against the codex trust ledger. Since 0281 the
    /// production resolver (<see cref="DefaultHookTrust"/>) returns only <see cref="Trusted"/> (an
    /// enabled, well-formed codex trusted_hash — preserved, codex-authoritative) or
    /// <see cref="Untrusted"/> (missing / malformed / disabled — self-repaired, else BLOCK). It no
    /// longer compares dydo's raw-file SHA256 against codex's pinned hash (codex uses a different
    /// hash), so <see cref="HashMismatch"/> is now VESTIGIAL — unreachable from the production
    /// resolver, retained only as a test-seam value; do not reintroduce a hash-compare in
    /// DefaultHookTrust (that was the 0269/0281 thrash).</summary>
    internal enum HookTrust
    {
        Trusted,
        Untrusted,
        HashMismatch
    }

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

    // (5) A repo .codex/hooks.json is SILENTLY skipped by codex unless its pre_tool_use guard hook is
    // trusted AND enabled in the user-level codex config ([hooks.state] in ~/.codex/config.toml,
    // per-event sub-tables keyed by path). Codex computes and pins its OWN hash of hooks.json (NOT
    // dydo's raw-file SHA256 — issue 0281), so an ENABLED, well-formed pre_tool_use trusted_hash is
    // treated as codex-authoritative and PRESERVED here — dydo does NOT overwrite it. (Overwriting an
    // enabled entry with dydo's differing hash was the 0269/0281 trust-thrash: codex restored its own
    // hash after each human Trust and re-prompted every dispatch.) Only a MISSING, malformed, or
    // disabled entry is self-repaired: dydo writes [hooks.state.'<abs-hooks.json>:pre_tool_use:0:0']
    // with enabled=true (the exact schema issue 0270 reads), preserving every other entry, then
    // proceeds. The repair can fail on two paths: the config is unwritable, OR the resolved target path
    // contains an apostrophe (which cannot be represented in the single-quoted TOML literal key, so
    // DefaultHookTrustRepair refuses rather than write invalid TOML to the global config); on either we
    // BLOCK with the manual-re-approval fix. When the repo carries no hooks.json there is nothing to
    // trust-check.
    private static PreflightResult CheckHookTrust(string launchHost, string? projectRoot)
    {
        if (launchHost != "codex" || projectRoot == null)
            return PreflightResult.Pass;

        var hooksPath = Path.Combine(projectRoot, ".codex", "hooks.json");
        if (!File.Exists(hooksPath))
            return PreflightResult.Pass;

        var resolve = HookTrustResolverOverride ?? DefaultHookTrust;
        if (resolve(hooksPath) == HookTrust.Trusted)
            return PreflightResult.Pass;

        // Missing, malformed, or disabled entry (an enabled well-formed entry is preserved as
        // codex-authoritative above, per 0281): repair the entry ourselves, then re-evaluate against
        // the 0270 schema to confirm the write is well-formed and proceed.
        if ((HookTrustRepairOverride ?? DefaultHookTrustRepair)(hooksPath)
            && resolve(hooksPath) == HookTrust.Trusted)
            return PreflightResult.Pass;

        // Name the actual refusal cause: an apostrophe in the path cannot be represented in codex's
        // single-quoted TOML literal trust key (DefaultHookTrustRepair refuses it rather than writing
        // invalid global config), which is a different problem from an unwritable config.
        var cause = hooksPath.Contains('\'')
            ? "the repo path contains an apostrophe, which cannot be represented in codex's TOML trust key"
            : "the config is unwritable";
        return PreflightResult.Fail(
            $"Codex dispatch would run UNGUARDED and dydo could not self-repair the trust entry: writing the " +
            $"pre_tool_use guard trust for {hooksPath} to your codex config ([hooks.state] in ~/.codex/config.toml) " +
            $"failed — {cause}. Codex silently skips an untrusted hook, so the dydo guard never fires. " +
            "Re-approve it in codex — run codex once in this repo and approve the pre_tool_use hook — then re-dispatch.");
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

    // Reads the codex trust ledger for the pre_tool_use guard hook. Codex records trust as
    // per-event dotted sub-tables — [hooks.state.'<abs-path>:<event>:0:0'] with `trusted_hash`
    // (value form sha256:<lowercase-hex>) and `enabled` child keys. The guard is the pre_tool_use
    // hook, so we key on that event specifically: a sibling `stop` entry being enabled does not
    // trust the guard. Codex's interactive trust review is the authority for the pinned hash; it
    // does not hash the whole hooks.json file. An enabled, well-formed pinned hash means Codex has
    // trusted that hook, and dydo must not overwrite it with its own file hash or every dispatch
    // re-prompts. No config, no matching entry, malformed hash, or disabled entry is Untrusted.
    private static HookTrust DefaultHookTrust(string hooksPath)
    {
        var codexHome = CodexHomeOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var configPath = Path.Combine(codexHome, "config.toml");
        if (!File.Exists(configPath))
            return HookTrust.Untrusted;

        if (FindPreToolUseTrustEntry(File.ReadAllText(configPath), hooksPath) is not { Enabled: true, TrustedHash: { } pinned })
            return HookTrust.Untrusted;

        return IsSha256Hex(pinned) ? HookTrust.Trusted : HookTrust.Untrusted;
    }

    // Self-repairs the pre_tool_use trust entry (issue 0269): rewrites (or adds) the dotted
    // sub-table [hooks.state.'<abs-hooks.json>:pre_tool_use:0:0'] with the live SHA256 of hooks.json
    // and enabled=true, preserving every other entry/sub-table (the stop hook, unrelated state).
    // Returns false when the config is unwritable, or when the target path contains an apostrophe —
    // both fall through to the caller's BLOCK path.
    private static bool DefaultHookTrustRepair(string hooksPath)
    {
        var codexHome = CodexHomeOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var configPath = Path.Combine(codexHome, "config.toml");
        var target = Path.GetFullPath(hooksPath);

        // The codex trust key is a TOML single-quoted literal, which admits NO escapes — an
        // apostrophe in the repo path (e.g. C:\Users\O'Brien\repo) cannot be represented and would
        // write invalid TOML into the user's GLOBAL config, breaking every codex invocation while the
        // lenient reader still matches it back as Trusted (preflight would falsely PASS). Refuse the
        // automated repair for such paths and let the caller BLOCK with the manual re-approval fix.
        if (target.Contains('\''))
            return false;

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(hooksPath))).ToLowerInvariant();

        try
        {
            var existing = File.Exists(configPath) ? File.ReadAllText(configPath) : "";
            Directory.CreateDirectory(codexHome);
            File.WriteAllText(configPath, UpsertPreToolUseEntry(existing, target, hash));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Drops any existing pre_tool_use sub-table for target (header + its child lines up to the next
    // header) and appends a fresh one; all other lines are preserved verbatim so sibling sub-tables
    // survive. Codex keys are single-quoted TOML literals: backslashes need no escaping, but an
    // apostrophe cannot be represented at all — DefaultHookTrustRepair refuses such target paths
    // (returns false) before reaching here, so this writer never has to escape one.
    private static string UpsertPreToolUseEntry(string toml, string target, string hash)
    {
        var kept = new List<string>();
        var skipping = false;
        foreach (var raw in toml.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
                skipping = IsPreToolUseHeader(line, target);
            if (!skipping)
                kept.Add(raw);
        }
        while (kept.Count > 0 && kept[^1].Trim().Length == 0)
            kept.RemoveAt(kept.Count - 1);

        if (kept.Count > 0)
            kept.Add("");
        kept.Add($"[hooks.state.'{target}:pre_tool_use:0:0']");
        kept.Add($"trusted_hash = 'sha256:{hash}'");
        kept.Add("enabled = true");
        return string.Join("\n", kept) + "\n";
    }

    // Scans the [hooks.state.'<abs-path>:pre_tool_use:0:0'] sub-table matching hooksPath and reads
    // its child trusted_hash/enabled lines until the next header, or null if no such sub-table.
    private static (bool Enabled, string? TrustedHash)? FindPreToolUseTrustEntry(string toml, string hooksPath)
    {
        var target = Path.GetFullPath(hooksPath);
        var inTarget = false;
        var enabled = true;
        string? trustedHash = null;

        foreach (var raw in toml.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                if (inTarget)
                    return (enabled, trustedHash);
                inTarget = IsPreToolUseHeader(line, target);
                enabled = true;
                trustedHash = null;
                continue;
            }
            if (!inTarget || line.Length == 0 || line.StartsWith('#'))
                continue;

            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key == "enabled")
                enabled = !value.StartsWith("false", StringComparison.Ordinal);
            else if (key == "trusted_hash")
                trustedHash = NormalizeHash(TomlUnquote(value));
        }
        return inTarget ? (enabled, trustedHash) : null;
    }

    // Matches a [hooks.state.'<key>'] sub-table header whose key begins with the resolved hooks.json
    // path AND names the pre_tool_use event: key form is <abs-path>:<event>:0:0.
    private static bool IsPreToolUseHeader(string header, string target)
    {
        const string prefix = "[hooks.state.";
        if (!header.StartsWith(prefix, StringComparison.Ordinal) || !header.EndsWith(']'))
            return false;
        var key = TomlUnquote(header[prefix.Length..^1]);
        if (key == null || !key.StartsWith(target, StringComparison.OrdinalIgnoreCase))
            return false;
        var rest = key[target.Length..];
        return rest.StartsWith(':') &&
               rest[1..].Split(':')[0].Equals("pre_tool_use", StringComparison.OrdinalIgnoreCase);
    }

    // Strips TOML single- (literal) or double- (basic) quotes; basic strings un-escape \\ so a
    // Windows path stored as "C:\\repo\\..." resolves. Codex writes single-quoted literal keys.
    private static string? TomlUnquote(string s)
    {
        s = s.Trim();
        if (s.Length < 2 || s[0] != s[^1] || s[0] is not ('\'' or '"'))
            return s.Length == 0 ? null : s;
        var inner = s[1..^1];
        return s[0] == '"' ? inner.Replace("\\\\", "\\") : inner;
    }

    // Codex stores the trust hash as sha256:<lowercase-hex>; strip the prefix and lowercase so the
    // comparison against Convert.ToHexString (uppercase) output is form-agnostic.
    private static string? NormalizeHash(string? raw)
    {
        if (raw == null)
            return null;
        const string prefix = "sha256:";
        if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            raw = raw[prefix.Length..];
        return raw.ToLowerInvariant();
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}
