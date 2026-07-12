namespace DynaDocs.Tests.Integration;

using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Services;

/// <summary>
/// c1-7 / issues 0253 + 0239: end-to-end cover for the codex DISPATCH seams — the launch command
/// line and the fail-fast preflight — driven through the real <c>dydo dispatch</c> command.
///
/// Test 6 pins the configured model/posture emission all the way through DispatchService → launcher
/// and the invariant that the dangerous-bypass flag NEVER appears. Test 7 walks the DispatchPreflight
/// failure matrix, asserting each mode surfaces its actionable message and leaves NO reservation.
/// (The executable-unresolvable and untrusted-hooks modes already have their own e2e cover in
/// DispatchCommandTests — c1-4's file; this matrix covers the vendor, posture, and sandbox modes
/// that its landing did not exercise end-to-end.)
/// </summary>
[Collection("Integration")]
public class CodexDispatchPostureE2ETests : IntegrationTestBase
{
    // (0253, 0277) A codex dispatch carries the role-resolved OpenAI model and configured
    // posture on its launch command line — proven through the whole DispatchService → launcher
    // path, not the launcher argument builder in isolation.
    [Fact]
    public async Task CodexDispatch_EmitsRoleResolvedModelAndConfiguredPosture_NeverBypassFlag_EndToEnd()
    {
        await InitProjectAsync("none", "testuser", 3);
        (await ClaimAgentWithRuntimeAsync("Adele", "codex", "gpt-5-codex")).AssertSuccess();
        MutateConfig(config =>
        {
            var openaiTiers = config["models"]!["tiers"]!["openai"]!.AsObject();
            openaiTiers["standard"] = "gpt-5.6-terra";
        });

        var recorder = new RecordingProcessStarter();
        TerminalLauncher.ProcessStarterOverride = recorder;
        WatchdogService.StartProcessOverride = _ => null;
        try
        {
            var result = await RunAsync(DispatchCommand.Create(),
                "--role", "code-writer", "--task", "codex-posture", "--brief", "Implement the slice",
                "--to", "Brian", "--auto-close");

            result.AssertSuccess();
            Assert.NotEmpty(recorder.Started);

            var args = string.Join("\n", recorder.Started.Select(p => p.Arguments));
            Assert.Contains("codex -m gpt-5.6-terra --sandbox workspace-write --ask-for-approval on-request 'Brian --inbox'", args);
            Assert.DoesNotContain("--dangerously-bypass", args);
            Assert.DoesNotContain("--yolo", args);
        }
        finally
        {
            WatchdogService.StartProcessOverride = null;
        }
    }

    // (0239 §2) An explicit --codex whose integration is disabled fails fast, names the fix, and
    // reserves no one.
    [Fact]
    public async Task Preflight_VendorDisabled_FailsFast_NoReservation()
    {
        await InitProjectAsync("none", "testuser", 3);
        MutateConfig(config =>
        {
            config.Remove("models");
            config["integrations"] = new JsonObject { ["codex"] = false };
        });

        var result = await DispatchCodexNoLaunch("vendor-disabled");

        result.AssertExitCode(2);
        result.AssertStderrContains("integrations.codex is false");
        result.AssertStderrContains("dydo sync");
        AssertNoReservation("Brian");
    }

    // (0239 §2) An explicit --codex whose model tier is unmapped fails fast naming the absent tier.
    [Fact]
    public async Task Preflight_VendorMissingTierMapping_FailsFast_NoReservation()
    {
        await InitProjectAsync("none", "testuser", 3);
        MutateConfig(config =>
        {
            config["integrations"] = new JsonObject();
            config["models"] = new JsonObject
            {
                ["tiers"] = new JsonObject { ["anthropic"] = new JsonObject { ["standard"] = "claude" } }
            };
        });

        var result = await DispatchCodexNoLaunch("vendor-missing-tier");

        result.AssertExitCode(2);
        result.AssertStderrContains("models.tiers.openai is absent");
        AssertNoReservation("Brian");
    }

    // (0253 round-2) An invalid codex posture value fails fast naming the accepted values, before
    // any reservation — never deep in argument assembly after the target is reserved.
    [Fact]
    public async Task Preflight_InvalidCodexPosture_FailsFast_NoReservation()
    {
        await InitProjectAsync("none", "testuser", 3);
        MutateConfig(config =>
        {
            config.Remove("models");
            config["integrations"] = new JsonObject();
            config["dispatch"] = new JsonObject { ["codex"] = new JsonObject { ["sandbox"] = "loose" } };
        });

        var result = await DispatchCodexNoLaunch("bad-posture");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid dispatch.codex");
        result.AssertStderrContains("workspace-write");
        AssertNoReservation("Brian");
    }

    // (0277 round-2) A malformed role-resolved model is a shell-injection risk because dydo
    // emits it as `-m <model>` on codex launch lines. Reject it before any reservation.
    [Fact]
    public async Task Preflight_InvalidCodexModel_FailsFast_NoReservation()
    {
        await InitProjectAsync("none", "testuser", 3);
        MutateConfig(config =>
        {
            var openaiTiers = config["models"]!["tiers"]!["openai"]!.AsObject();
            openaiTiers["standard"] = "x; rm -rf ~ #";
        });

        var result = await DispatchCodexNoLaunch("bad-model");

        result.AssertExitCode(2);
        result.AssertStderrContains("models.tiers.openai");
        result.AssertStderrContains("unsafe characters");
        AssertNoReservation("Brian");
    }

    // (0239 §4, co-think outcome 4) When the codex Windows sandbox prerequisite is absent, a
    // workspace-write dispatch fails fast pointing at the setup — never silently downgraded, never
    // reserving the target. The check is Windows-only, so elsewhere the dispatch proceeds.
    [Fact]
    public async Task Preflight_MissingWindowsSandboxPrerequisite_FailsFastOnWindows_NoReservation()
    {
        await InitProjectAsync("none", "testuser", 3);
        MutateConfig(config =>
        {
            config.Remove("models");
            config["integrations"] = new JsonObject();
        });

        var recorder = new RecordingProcessStarter();
        TerminalLauncher.ProcessStarterOverride = recorder;
        WatchdogService.StartProcessOverride = _ => null;
        DispatchPreflight.SandboxPrerequisiteProbeOverride = () => false;
        DispatchPreflight.HookTrustResolverOverride = _ => DispatchPreflight.HookTrust.Trusted;   // isolate the sandbox check
        try
        {
            var result = await RunAsync(DispatchCommand.Create(),
                "--role", "code-writer", "--task", "sandbox-missing", "--brief", "Implement",
                "--to", "Brian", "--codex", "--auto-close");

            if (OperatingSystem.IsWindows())
            {
                result.AssertExitCode(2);
                result.AssertStderrContains("codex-windows-sandbox-setup.exe");
                result.AssertStderrContains("never silently downgraded");
                AssertNoReservation("Brian");
            }
            else
            {
                // The Windows sandbox prerequisite does not gate other platforms.
                result.AssertSuccess();
            }
        }
        finally
        {
            DispatchPreflight.SandboxPrerequisiteProbeOverride = null;
            DispatchPreflight.HookTrustResolverOverride = null;
            WatchdogService.StartProcessOverride = null;
        }
    }

    private async Task<CommandResult> DispatchCodexNoLaunch(string task) =>
        await RunAsync(DispatchCommand.Create(),
            "--role", "code-writer", "--task", task, "--brief", "Implement",
            "--to", "Brian", "--codex", "--no-launch");

    private void MutateConfig(Action<JsonObject> mutate)
    {
        var path = Path.Combine(TestDir, "dydo.json");
        var config = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        mutate(config);
        File.WriteAllText(path, config.ToJsonString());
    }

    private void AssertNoReservation(string agent)
    {
        var inbox = Path.Combine(TestDir, "dydo", "agents", agent, "inbox");
        if (Directory.Exists(inbox))
            Assert.Empty(Directory.GetFiles(inbox, "*.md"));

        var state = Path.Combine(TestDir, "dydo", "agents", agent, "state.md");
        if (File.Exists(state))
            Assert.DoesNotContain("status: dispatched", File.ReadAllText(state));
    }
}
