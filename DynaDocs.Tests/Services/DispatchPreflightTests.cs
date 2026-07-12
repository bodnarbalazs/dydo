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
        DispatchPreflight.HookTrustRepairOverride = null;
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

    private static ModelsConfig ModelsFor(string role, string tier, string model) => new()
    {
        Roles = new() { [role] = tier },
        Tiers = new() { ["openai"] = new() { [tier] = model } }
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
    public void CodexInvalidResolvedModel_FailsNamingUnsafeValue()
    {
        var config = ConfigWith(models: ModelsFor("code-writer", "standard", "x; rm -rf ~ #"));

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
        Assert.Contains("models.tiers.openai", result.Error);
        Assert.Contains("x; rm -rf ~ #", result.Error);
        Assert.Contains("unsafe characters", result.Error);
        Assert.Contains("re-dispatch", result.Error!);
    }

    [Fact]
    public void CodexUnmappedRole_UsesFallbackModelAndPasses()
    {
        var config = ConfigWith(models: new ModelsConfig());

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true), _dir);

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

    // --- (5) Hook trust (codex, issue 0269 self-repair; 0281 preserve-enabled) ---
    // An enabled, well-formed codex trusted_hash is PRESERVED (codex-authoritative, 0281) — never
    // overwritten. Only a missing/malformed/disabled entry is REPAIRED in-place. A repair can still
    // fail — and BLOCK — on two paths: an unwritable config, OR an apostrophe-bearing target path that
    // DefaultHookTrustRepair refuses (cannot be represented in the single-quoted TOML literal key).

    [Fact]
    public void CodexUntrustedHooks_RepairFails_BlocksWithManualReApprove()
    {
        WriteHooksJson();
        DispatchPreflight.HookTrustResolverOverride = _ => DispatchPreflight.HookTrust.Untrusted;
        DispatchPreflight.HookTrustRepairOverride = _ => false; // config unwritable

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
        Assert.Contains("UNGUARDED", result.Error);
        Assert.Contains("could not self-repair", result.Error);
        Assert.Contains("pre_tool_use", result.Error);
        Assert.Contains("Re-approve it in codex", result.Error!);
    }

    [Fact]
    public void CodexUntrustedHooks_RepairSucceedsAndReEvaluatesTrusted_Passes()
    {
        WriteHooksJson();
        // The seam contract: repair writes a correct entry, and the post-repair re-evaluation reads
        // it back as trusted — dispatch proceeds without a manual codex re-approval.
        var calls = 0;
        DispatchPreflight.HookTrustResolverOverride = _ =>
            calls++ == 0 ? DispatchPreflight.HookTrust.Untrusted : DispatchPreflight.HookTrust.Trusted;
        DispatchPreflight.HookTrustRepairOverride = _ => true;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void CodexTrustedHooks_Passes_WithoutAttemptingRepair()
    {
        WriteHooksJson();
        DispatchPreflight.HookTrustResolverOverride = _ => DispatchPreflight.HookTrust.Trusted;
        DispatchPreflight.HookTrustRepairOverride = _ => throw new InvalidOperationException("must not repair a trusted entry");

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void CodexNoHooksJson_SkipsTrustCheck()
    {
        DispatchPreflight.HookTrustResolverOverride = _ => DispatchPreflight.HookTrust.Untrusted;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void ClaudeHost_SkipsTrustCheck()
    {
        WriteHooksJson();
        DispatchPreflight.HookTrustResolverOverride = _ => DispatchPreflight.HookTrust.Untrusted;

        var result = DispatchPreflight.Run(ConfigWith(), "claude", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    // --- (5) Default trust resolver: REAL codex config.toml schema parse (issue 0270) ---
    // Codex records trust as per-event dotted sub-tables:
    //   [hooks.state.'<abs-path>:<event>:0:0']
    //   trusted_hash = 'sha256:<lowercase-hex>'
    //   enabled = true
    // The guard is the pre_tool_use hook; the parser must key on that event specifically.
    // Codex owns the trusted_hash value; dydo must not overwrite a well-formed enabled entry just
    // because it does not equal dydo's whole-file SHA256.

    [Fact]
    public void DefaultTrust_PreToolUseEnabledMatchingHash_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state.'{{hooksPath}}:pre_tool_use:0:0']
            trusted_hash = 'sha256:{{Sha256Lower(hooksPath)}}'
            enabled = true

            [hooks.state.'{{hooksPath}}:stop:0:0']
            trusted_hash = 'sha256:{{Sha256Lower(hooksPath)}}'
            enabled = true
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    // enabled DEFAULTS TRUE unless an explicit `enabled = false` is present (issue 0270).
    [Fact]
    public void DefaultTrust_PreToolUseMatchingHashNoEnabledLine_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state.'{{hooksPath}}:pre_tool_use:0:0']
            trusted_hash = 'sha256:{{Sha256Lower(hooksPath)}}'
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
    }

    [Fact]
    public void DefaultTrust_PreToolUseEnabledCodexHashDifferentFromFileHash_PassesWithoutRepair()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state.'{{hooksPath}}:pre_tool_use:0:0']
            trusted_hash = 'sha256:581b21e8c4248575822b243e3470d04a1fab9dbdd242d0da869a240782db84d7'
            enabled = true
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
        var written = ReadCodexConfig();
        Assert.Contains("581b21e8", written);
        Assert.DoesNotContain(Sha256Lower(hooksPath), written);
    }

    [Fact]
    public void DefaultTrust_PreToolUseEnabledMalformedHash_RepairedToLiveHashAndEnabled_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state.'{{hooksPath}}:pre_tool_use:0:0']
            trusted_hash = 'sha256:not-a-real-hash'
            enabled = true
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
        var written = ReadCodexConfig();
        Assert.Contains($"trusted_hash = 'sha256:{Sha256Lower(hooksPath)}'", written);
        Assert.DoesNotContain("not-a-real-hash", written);
        Assert.Contains("enabled = true", written);
    }

    // --- (5) Self-repair against the REAL codex config.toml schema (issue 0269) ---

    // A disabled entry is not active trust. The repair rewrites it to the live hash + enabled, and
    // dispatch proceeds.
    [Fact]
    public void DefaultTrust_PreToolUseDisabledStaleHash_RepairedToLiveHashAndEnabled_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state.'{{hooksPath}}:pre_tool_use:0:0']
            trusted_hash = 'sha256:581b21e8c4248575822b243e3470d04a1fab9dbdd242d0da869a240782db84d7'
            enabled = false
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
        var written = ReadCodexConfig();
        Assert.Contains($"trusted_hash = 'sha256:{Sha256Lower(hooksPath)}'", written);
        Assert.DoesNotContain("581b21e8", written);
        Assert.Contains("enabled = true", written);
    }

    [Fact]
    public void DefaultTrust_PreToolUseDisabled_RepairedToEnabled_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state.'{{hooksPath}}:pre_tool_use:0:0']
            trusted_hash = 'sha256:{{Sha256Lower(hooksPath)}}'
            enabled = false
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
        var written = ReadCodexConfig();
        Assert.Contains("enabled = true", written);
        Assert.DoesNotContain("enabled = false", written);
    }

    // The c1-8 smoke found a sibling `stop` entry enabled while pre_tool_use was not. The repair
    // must ADD the pre_tool_use trust WITHOUT clobbering the unrelated stop sub-table.
    [Fact]
    public void DefaultTrust_OnlyStopEntry_RepairAddsPreToolUseAndPreservesStop_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig($$"""
            [hooks.state.'{{hooksPath}}:stop:0:0']
            trusted_hash = 'sha256:{{Sha256Lower(hooksPath)}}'
            enabled = true
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
        var written = ReadCodexConfig();
        Assert.Contains($"'{hooksPath}:stop:0:0'", written);        // stop entry preserved
        Assert.Contains($"'{hooksPath}:pre_tool_use:0:0'", written); // guard entry added
    }

    // A wholly unrelated [hooks.state.*] sub-table (different repo's hooks.json) must survive.
    [Fact]
    public void DefaultTrust_ForeignEntry_RepairAppendsAndPreservesForeign_Passes()
    {
        var hooksPath = WriteHooksJson();
        WriteCodexConfig("""
            [hooks.state.'C:\some\other\hooks.json:pre_tool_use:0:0']
            trusted_hash = 'sha256:abcdef'
            enabled = true
            """);

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
        var written = ReadCodexConfig();
        Assert.Contains(@"'C:\some\other\hooks.json:pre_tool_use:0:0'", written); // foreign entry kept
        Assert.Contains("trusted_hash = 'sha256:abcdef'", written);
        Assert.Contains($"'{hooksPath}:pre_tool_use:0:0'", written);              // our entry added
    }

    [Fact]
    public void DefaultTrust_NoConfigFile_RepairWritesFreshConfig_Passes()
    {
        var hooksPath = WriteHooksJson();
        DispatchPreflight.CodexHomeOverride = Path.Combine(_dir, "fresh-codex-home");

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.True(result.Ok);
        var written = File.ReadAllText(Path.Combine(_dir, "fresh-codex-home", "config.toml"));
        Assert.Contains($"[hooks.state.'{hooksPath}:pre_tool_use:0:0']", written);
        Assert.Contains($"trusted_hash = 'sha256:{Sha256Lower(hooksPath)}'", written);
        Assert.Contains("enabled = true", written);
    }

    // Repair cannot be performed (config path is a directory ⇒ unwritable) ⇒ BLOCK with the manual
    // re-approval fix — one of the two repair-refusal block paths (the other is an apostrophe-bearing
    // target path).
    [Fact]
    public void DefaultTrust_UnwritableConfig_BlocksWithManualReApprove()
    {
        WriteHooksJson();
        var home = Path.Combine(_dir, "unwritable-codex-home");
        Directory.CreateDirectory(Path.Combine(home, "config.toml")); // config.toml is a directory
        DispatchPreflight.CodexHomeOverride = home;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), _dir);

        Assert.False(result.Ok);
        Assert.Contains("UNGUARDED", result.Error);
        Assert.Contains("could not self-repair", result.Error);
        Assert.Contains("unwritable", result.Error);
        Assert.Contains("Re-approve it in codex", result.Error!);
    }

    // A repo path containing an apostrophe cannot be written as a TOML single-quoted literal key
    // (codex's trust-key form admits no escapes). Rather than corrupt the user's GLOBAL config with
    // invalid TOML, the repair REFUSES the path — falling through to the manual re-approval BLOCK —
    // and writes NOTHING to the config (issue 0269 wave-audit finding 4).
    [Fact]
    public void DefaultTrust_ApostropheInPath_RefusesRepair_BlocksAndWritesNothing()
    {
        var projectRoot = Path.Combine(_dir, "O'Brien-repo");
        Directory.CreateDirectory(Path.Combine(projectRoot, ".codex"));
        File.WriteAllText(
            Path.Combine(projectRoot, ".codex", "hooks.json"),
            """{"PreToolUse":[{"command":"dydo guard"}]}""");
        var home = Path.Combine(_dir, "apostrophe-codex-home");
        DispatchPreflight.CodexHomeOverride = home;

        var result = DispatchPreflight.Run(ConfigWith(), "codex", Opts(noLaunch: true), projectRoot);

        Assert.False(result.Ok);
        Assert.Contains("UNGUARDED", result.Error);
        Assert.Contains("could not self-repair", result.Error);
        Assert.Contains("apostrophe", result.Error!);            // names the real cause, not a phantom permissions problem
        Assert.DoesNotContain("unwritable", result.Error!);      // the config is writable; the path is the problem
        Assert.Contains("Re-approve it in codex", result.Error!);
        Assert.False(File.Exists(Path.Combine(home, "config.toml"))); // global config untouched
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

    private string ReadCodexConfig() =>
        File.ReadAllText(Path.Combine(_dir, "codex-home", "config.toml"));

    private static string Sha256Lower(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
