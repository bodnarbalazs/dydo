namespace DynaDocs.Tests.Services;

using System.Security.Cryptography;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Unit tests for the dispatch preflight (issue 0239, plus the issue 0253 posture checks). Each
/// fail-fast check is exercised for both its pass and fail path, and the failure message is
/// asserted to name the missing prerequisite and the fix.
/// </summary>
public class DispatchPreflightTests : IDisposable
{
    private readonly string _dir;
    private readonly Func<string, string>? _originalResolver;

    public DispatchPreflightTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-preflight-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);

        // Pin executable resolution to the bare name so check (1) never throws on the whims of
        // the test host's PATH — the checks under test are (2)/(3)/(4)/(5), and the throw path
        // is driven explicitly where it is the subject.
        _originalResolver = TerminalLauncher.ExecutableResolverOverride;
        TerminalLauncher.ExecutableResolverOverride = host => host;
    }

    public void Dispose()
    {
        TerminalLauncher.ExecutableResolverOverride = _originalResolver;
        DispatchPreflight.SandboxPrerequisiteProbeOverride = null;
        DispatchPreflight.HookTrustResolverOverride = null;
        DispatchPreflight.CodexHomeOverride = null;
        DispatchPreflight.SyncedBodiesProbeOverride = null;
        if (Directory.Exists(_dir))
            try { Directory.Delete(_dir, true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private static DispatchOptions Opts(bool noLaunch = false, string? hostOverride = null) =>
        new("code-writer", "task", "brief", NoLaunch: noLaunch, HostOverride: hostOverride);

    private static DydoConfig ConfigWith(
        Dictionary<string, bool>? integrations = null, ModelsConfig? models = null) =>
        new()
        {
            Integrations = integrations ?? new Dictionary<string, bool>(),
            Models = models
        };

    private static DydoConfig ConfigWithCodex(string sandbox, string approvalPolicy = "on-request") =>
        new()
        {
            Integrations = new Dictionary<string, bool>(),
            Dispatch = new DispatchConfig
            {
                Codex = new CodexDispatchConfig { Sandbox = sandbox, ApprovalPolicy = approvalPolicy }
            }
        };

    private static ModelsConfig BothVendors() => new()
    {
        Tiers = new()
        {
            ["anthropic"] = new() { ["standard"] = "claude" },
            ["openai"] = new() { ["standard"] = "gpt" }
        }
    };

    // --- (1) Executable resolvable ---

    [Fact]
    public void UnresolvableExecutable_FailsNamingBinaryAndFix()
    {
        TerminalLauncher.ExecutableResolverOverride =
            host => throw new InvalidOperationException($"{host} WindowsApps alias is not launchable");

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(), _dir);

        Assert.False(result.Ok);
        Assert.Contains("Cannot launch codex", result.Error);
        Assert.Contains("Install a launchable codex CLI", result.Error);
    }

    [Fact]
    public void NoLaunch_SkipsExecutableCheck()
    {
        TerminalLauncher.ExecutableResolverOverride =
            host => throw new InvalidOperationException("would fail if probed");

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    // --- (2) Vendor configured ---

    [Fact]
    public void OverrideVendorIntegrationDisabled_Fails()
    {
        var config = ConfigWith(integrations: new() { ["codex"] = false }, models: BothVendors());

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.False(result.Ok);
        Assert.Contains("integrations.codex is false", result.Error);
        Assert.Contains("dydo sync", result.Error);
    }

    [Fact]
    public void OverrideVendorMissingTierMapping_Fails()
    {
        var anthropicOnly = new ModelsConfig { Tiers = new() { ["anthropic"] = new() { ["standard"] = "claude" } } };
        var config = ConfigWith(models: anthropicOnly);

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.False(result.Ok);
        Assert.Contains("models.tiers.openai is absent", result.Error);
    }

    [Fact]
    public void OverrideClaudeMissingTierMapping_Fails()
    {
        var openAiOnly = new ModelsConfig { Tiers = new() { ["openai"] = new() { ["standard"] = "gpt" } } };
        var config = ConfigWith(models: openAiOnly);

        var result = DispatchPreflight.Run(config, "claude", Opts(noLaunch: true, hostOverride: "claude"), _dir);

        Assert.False(result.Ok);
        Assert.Contains("models.tiers.anthropic is absent", result.Error);
    }

    [Fact]
    public void ConfiguredVendorOverride_Passes()
    {
        var config = ConfigWith(integrations: new() { ["codex"] = true }, models: BothVendors());
        // Vendor config is the subject here; pin bodies present so the synced-bodies leg (2b)
        // does not stand in for a config failure.
        DispatchPreflight.SyncedBodiesProbeOverride = (_, _) => true;

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void NoOverride_SkipsVendorCheck_EvenWhenMisconfigured()
    {
        // codex integration is disabled, but no explicit --codex/--claude override was given,
        // so the vendor check must not fire — the default host is the caller's own live host.
        var config = ConfigWith(integrations: new() { ["codex"] = false });

        var result = DispatchPreflight.Run(config, "claude", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void AbsentModelsSection_SkipsTierCheck()
    {
        var config = ConfigWith(models: null);
        // Tier check is the subject; pin bodies present so leg (2b) does not gate this pass path.
        DispatchPreflight.SyncedBodiesProbeOverride = (_, _) => true;

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.True(result.Ok);
    }

    // --- (2b) Synced agent bodies (issue 0239 — the vendor is compiled, not just enabled) ---

    [Fact]
    public void CodexOverride_SyncedBodiesPresent_Passes()
    {
        var config = ConfigWith(integrations: new() { ["codex"] = true }, models: BothVendors());
        WriteAgentBodies("codex"); // real default probe sees .codex/agents/code-writer.toml

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void CodexOverride_SyncedBodiesAbsent_FailsNamingArtifactsAndSync()
    {
        // Integration on and tiers mapped (config checks pass), but `dydo sync` was never run —
        // no .codex/agents/*.toml. The launched agent would have no compiled role definition.
        var config = ConfigWith(integrations: new() { ["codex"] = true }, models: BothVendors());

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.False(result.Ok);
        Assert.Contains("no compiled agent bodies", result.Error);
        Assert.Contains(".codex/agents/*.toml", result.Error);
        Assert.Contains("dydo sync", result.Error!);
    }

    [Fact]
    public void ClaudeOverride_SyncedBodiesPresent_Passes()
    {
        var config = ConfigWith(integrations: new() { ["claude"] = true }, models: BothVendors());
        WriteAgentBodies("claude"); // real default probe sees .claude/agents/code-writer.md

        var result = DispatchPreflight.Run(config, "claude", Opts(noLaunch: true, hostOverride: "claude"), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void ClaudeOverride_SyncedBodiesAbsent_FailsNamingClaudeSurface()
    {
        // The probe targets the OVERRIDE vendor's surface: a claude override checks .claude/agents.
        var config = ConfigWith(integrations: new() { ["claude"] = true }, models: BothVendors());

        var result = DispatchPreflight.Run(config, "claude", Opts(noLaunch: true, hostOverride: "claude"), _dir);

        Assert.False(result.Ok);
        Assert.Contains(".claude/agents/*.md", result.Error);
        Assert.Contains("dydo sync", result.Error!);
    }

    [Fact]
    public void NoOverride_SkipsSyncedBodiesCheck_EvenWhenAbsent()
    {
        // No explicit --claude/--codex override: the launch host is the caller's own live host,
        // already running against its own bodies. The synced-bodies leg must not fire.
        var config = ConfigWith();

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void OverrideVendorIntegrationNotEnabled_SkipsSyncedBodiesCheck_EvenWhenAbsent()
    {
        // The vendor is not explicitly integrated (absent from integrations), so there is no sync
        // obligation — the bodies leg only guards the "integration is on but never synced" gap.
        var config = ConfigWith(models: BothVendors());

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void SyncedBodiesProbeOverride_DrivesFailBranch()
    {
        // The injectable seam lets the check fail without laying down a real vendor tree.
        var config = ConfigWith(integrations: new() { ["codex"] = true }, models: BothVendors());
        DispatchPreflight.SyncedBodiesProbeOverride = (_, _) => false;

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.False(result.Ok);
        Assert.Contains("no compiled agent bodies", result.Error);
    }

    // --- (3) Codex posture valid (issue 0253) ---

    [Fact]
    public void CodexInvalidSandboxPosture_FailsNamingAcceptedValues()
    {
        var result = DispatchPreflight.Run(ConfigWithCodex("loose"), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
        Assert.Contains("dispatch.codex", result.Error);
        Assert.Contains("loose", result.Error);
        Assert.Contains("workspace-write", result.Error);
        Assert.Contains("re-dispatch", result.Error!);
    }

    [Fact]
    public void CodexInvalidApprovalPosture_Fails()
    {
        // on-failure is DEPRECATED in the codex CLI — not an accepted value.
        var result = DispatchPreflight.Run(
            ConfigWithCodex("workspace-write", approvalPolicy: "on-failure"), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
        Assert.Contains("on-failure", result.Error);
    }

    [Fact]
    public void CodexValidPosture_Passes()
    {
        var result = DispatchPreflight.Run(ConfigWithCodex("read-only", "never"), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void ClaudeHost_SkipsPostureCheck_EvenWhenCodexPostureInvalid()
    {
        var result = DispatchPreflight.Run(ConfigWithCodex("loose"), "claude", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    // --- (4) Windows sandbox prerequisite (codex) ---

    [Fact]
    public void CodexSandboxPrerequisiteMissing_FailsOnWindows_InertElsewhere()
    {
        DispatchPreflight.SandboxPrerequisiteProbeOverride = () => false;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(), _dir);

        if (OperatingSystem.IsWindows())
        {
            Assert.False(result.Ok);
            Assert.Contains("codex-windows-sandbox-setup.exe", result.Error);
            Assert.Contains("never silently downgraded", result.Error);
        }
        else
        {
            Assert.True(result.Ok);
        }
    }

    [Fact]
    public void CodexSandboxPrerequisitePresent_Passes()
    {
        DispatchPreflight.SandboxPrerequisiteProbeOverride = () => true;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void ClaudeHost_SkipsSandboxCheck()
    {
        DispatchPreflight.SandboxPrerequisiteProbeOverride = () => false;

        var result = DispatchPreflight.Run(ConfigWith(), "claude", Opts(), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void NoLaunch_SkipsSandboxCheck()
    {
        DispatchPreflight.SandboxPrerequisiteProbeOverride = () => false;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    // Wiring (issue 0253): the sandbox prerequisite is only required by the workspace-write posture
    // — the mode that runs under the elevated Windows sandbox. Modes that do not use it skip the
    // check even when the prerequisite is absent.
    [Fact]
    public void WorkspaceWritePosture_MissingPrerequisite_FailsOnWindows()
    {
        DispatchPreflight.SandboxPrerequisiteProbeOverride = () => false;

        var result = DispatchPreflight.Run(ConfigWithCodex("workspace-write"), "codex", Opts(), _dir);

        if (OperatingSystem.IsWindows())
        {
            Assert.False(result.Ok);
            Assert.Contains("codex-windows-sandbox-setup.exe", result.Error);
        }
        else
        {
            Assert.True(result.Ok);
        }
    }

    [Theory]
    [InlineData("read-only")]
    [InlineData("danger-full-access")]
    public void NonWorkspaceWritePosture_SkipsSandboxCheck_EvenWhenPrerequisiteMissing(string sandbox)
    {
        // read-only and danger-full-access do not run under the elevated Windows sandbox, so a
        // missing codex-windows-sandbox-setup.exe must not block them.
        DispatchPreflight.SandboxPrerequisiteProbeOverride = () => false;

        var result = DispatchPreflight.Run(ConfigWithCodex(sandbox), "codex", Opts(), _dir);

        Assert.True(result.Ok);
    }

    // --- (5) Hook trust (codex) ---

    [Fact]
    public void CodexUntrustedHooks_FailsWithReTrustInstruction()
    {
        WriteHooksJson();
        DispatchPreflight.HookTrustResolverOverride = _ => false;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
        Assert.Contains("UNGUARDED", result.Error);
        Assert.Contains("hooks.state", result.Error);
        Assert.Contains(".codex", result.Error!);
    }

    [Fact]
    public void CodexTrustedHooks_Passes()
    {
        WriteHooksJson();
        DispatchPreflight.HookTrustResolverOverride = _ => true;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void CodexNoHooksJson_SkipsTrustCheck()
    {
        DispatchPreflight.HookTrustResolverOverride = _ => false;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void ClaudeHost_SkipsTrustCheck()
    {
        WriteHooksJson();
        DispatchPreflight.HookTrustResolverOverride = _ => false;

        var result = DispatchPreflight.Run(ConfigWith(), "claude", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    // --- (5) Default trust resolver: real codex config.toml parse ---

    [Fact]
    public void DefaultTrust_EnabledEntryWithMatchingHash_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state]
            "{{Escape(hooksPath)}}" = { enabled = true, sha256 = "{{Sha256(hooksPath)}}" }
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void DefaultTrust_DisabledEntry_Fails()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state]
            "{{Escape(hooksPath)}}" = { enabled = false, sha256 = "{{Sha256(hooksPath)}}" }
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
    }

    [Fact]
    public void DefaultTrust_StaleHash_Fails()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state]
            "{{Escape(hooksPath)}}" = { enabled = true, sha256 = "DEADBEEF" }
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
    }

    [Fact]
    public void DefaultTrust_NoConfigFile_Fails()
    {
        WriteHooksJson();
        DispatchPreflight.CodexHomeOverride = Path.Combine(_dir, "empty-codex-home");

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
    }

    [Fact]
    public void DefaultTrust_NoMatchingEntry_Fails()
    {
        WriteHooksJson();
        WriteCodexConfig("""
            [hooks.state]
            "C:\\some\\other\\hooks.json" = { enabled = true }
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
    }

    // Lays down the vendor's compiled agent surface as `dydo sync` would: a worker-role body
    // under .claude/agents (claude) or .codex/agents (codex).
    private void WriteAgentBodies(string host)
    {
        var (dir, ext) = host == "codex"
            ? (Path.Combine(_dir, ".codex", "agents"), "toml")
            : (Path.Combine(_dir, ".claude", "agents"), "md");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"code-writer.{ext}"), "compiled body");
    }

    private string WriteHooksJson()
    {
        var codexDir = Path.Combine(_dir, ".codex");
        Directory.CreateDirectory(codexDir);
        var path = Path.Combine(codexDir, "hooks.json");
        File.WriteAllText(path, """{"PreToolUse":[{"command":"dydo guard"}]}""");
        return path;
    }

    private void WriteCodexConfig(string toml)
    {
        var home = Path.Combine(_dir, "codex-home");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, "config.toml"), toml);
        DispatchPreflight.CodexHomeOverride = home;
    }

    private static string Escape(string path) => path.Replace("\\", "\\\\");

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
