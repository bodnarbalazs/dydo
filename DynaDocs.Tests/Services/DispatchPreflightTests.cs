namespace DynaDocs.Tests.Services;

using System.Security.Cryptography;
using DynaDocs.Models;
using DynaDocs.Services;

/// <summary>
/// Unit tests for the dispatch preflight (issue 0239). Each of the four fail-fast checks is
/// exercised for both its pass and fail path, and the failure message is asserted to name the
/// missing prerequisite and the fix.
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
        // the test host's PATH — the checks under test are (2)/(3)/(4), and the throw path is
        // driven explicitly where it is the subject.
        _originalResolver = TerminalLauncher.ExecutableResolverOverride;
        TerminalLauncher.ExecutableResolverOverride = host => host;
    }

    public void Dispose()
    {
        TerminalLauncher.ExecutableResolverOverride = _originalResolver;
        DispatchPreflight.SandboxPrerequisiteProbeOverride = null;
        DispatchPreflight.HookTrustResolverOverride = null;
        DispatchPreflight.CodexHomeOverride = null;
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

        var result = DispatchPreflight.Run(config, "codex", Opts(noLaunch: true, hostOverride: "codex"), _dir);

        Assert.True(result.Ok);
    }

    // --- (3) Windows sandbox prerequisite (codex) ---

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

    // --- (4) Hook trust (codex) ---

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

    // --- (4) Default trust resolver: real codex config.toml parse ---

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
