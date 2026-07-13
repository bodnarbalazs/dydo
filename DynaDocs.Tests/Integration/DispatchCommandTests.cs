namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Services;

/// <summary>
/// Integration tests for dispatch command --to and --escalate flags.
/// </summary>
[Collection("Integration")]
public class DispatchCommandTests : IntegrationTestBase
{
    #region --to Success Cases

    [Fact]
    public async Task Dispatch_ToValidAgent_DispatchesToSpecifiedAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature", to: "Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        // Verify inbox file created for Brian specifically
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0, "Inbox item should be created for Brian");

        // Verify NOT created for Adele (first alphabetically)
        var adeleInbox = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        if (Directory.Exists(adeleInbox))
        {
            var adeleFiles = Directory.GetFiles(adeleInbox, "*.md");
            Assert.Empty(adeleFiles);
        }
    }

    [Fact]
    public async Task Dispatch_WithoutTo_AutoSelectsFirstFreeAlphabetically()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele"); // First alphabetically

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);
    }

    [Fact]
    public async Task Dispatch_MetadataWriteContended_WarnsButProceeds()
    {
        // Finding 6: SetDispatchMetadata's write can lose to persistent per-agent lock contention and return false.
        // The dispatch must NOT fail — it proceeds (the assignment is written, exit success) and surfaces a
        // non-fatal warning so the operator knows the window/auto-close metadata was not recorded and can
        // re-dispatch, rather than a terminal silently mis-closing.
        await InitProjectAsync("none", "testuser", 3);

        var brianDir = Path.Combine(TestDir, "dydo", "agents", "Brian");
        var lockPath = Path.Combine(brianDir, ".claim.lock");
        var inboxDir = Path.Combine(brianDir, "inbox");
        Directory.CreateDirectory(brianDir);
        int InboxCount() => Directory.Exists(inboxDir) ? Directory.GetFiles(inboxDir, "*.md").Length : 0;
        var inboxBefore = InboxCount();

        // Reservation runs BEFORE the metadata write, with the lock free — so the dispatch still reserves Brian. The
        // assignment inbox item is written right after reservation (the lock already released) and several steps
        // before SetDispatchMetadata, so it is a reliable "lock is now free" signal: grab the lock the instant the
        // item appears and hold it through SetDispatchMetadata's whole retry window (10x / 50 ms), so every retry
        // fails and it returns false. Tight-spin (no sleep) so the grab lands within the pre-metadata-write window.
        var stop = false;
        FileStream? held = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var holder = new Thread(() =>
        {
            while (held == null && sw.ElapsedMilliseconds < 5000)
            {
                try
                {
                    if (InboxCount() > inboxBefore)
                        held = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                }
                catch (IOException) { /* transiently contended; retry */ }
            }
            while (!stop) // hold the lock across the dispatch's metadata write
                Thread.Sleep(5);
        });
        holder.Start();

        var result = await DispatchAsync("code-writer", "contended-dispatch", "Brief", to: "Brian");
        stop = true;
        holder.Join();
        held?.Dispose();
        if (File.Exists(lockPath)) File.Delete(lockPath);

        result.AssertSuccess(); // non-fatal: the dispatch proceeded despite the contended metadata write
        result.AssertStdoutContains("could not record dispatch window/auto-close metadata");
        var inbox = Directory.GetFiles(Path.Combine(brianDir, "inbox"), "*.md");
        Assert.NotEmpty(inbox); // the assignment inbox item was still written
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task Dispatch_DefaultsToCallingAgentSessionHost(string host)
    {
        await InitProjectAsync("none", "testuser", 3);
        var claim = await ClaimAgentWithHostAsync("Adele", host);
        claim.AssertSuccess();

        var recorder = new RecordingProcessStarter();
        TerminalLauncher.ProcessStarterOverride = recorder;
        WatchdogService.StartProcessOverride = _ => null;
        try
        {
            var result = await DispatchLaunchedAsync("code-writer", $"host-default-{host}", "Brief", to: "Brian", autoClose: true);

            result.AssertSuccess();
            Assert.NotEmpty(recorder.Started);
            // Codex carries the configured launch posture (issue 0253); claude launches bare.
            Assert.Contains(ExpectedLaunchLine(host, "Brian"), JoinedArguments(recorder));
        }
        finally
        {
            WatchdogService.StartProcessOverride = null;
        }
    }

    [Fact]
    public async Task Dispatch_UnknownCallingHost_DefaultsToClaude()
    {
        await InitProjectAsync("none", "testuser", 3);
        var claim = await ClaimAgentAsync("Adele");
        claim.AssertSuccess();

        var recorder = new RecordingProcessStarter();
        TerminalLauncher.ProcessStarterOverride = recorder;
        WatchdogService.StartProcessOverride = _ => null;
        try
        {
            var result = await DispatchLaunchedAsync("code-writer", "host-unknown", "Brief", to: "Brian", autoClose: true);

            result.AssertSuccess();
            Assert.NotEmpty(recorder.Started);
            Assert.Contains("claude 'Brian --inbox'", JoinedArguments(recorder));
        }
        finally
        {
            WatchdogService.StartProcessOverride = null;
        }
    }

    [Theory]
    [InlineData("claude", "--codex", "codex")]
    [InlineData("codex", "--claude", "claude")]
    public async Task Dispatch_HostOverrideWinsOverCallingAgentSessionHost(string callerHost, string flag, string expectedHost)
    {
        await InitProjectAsync("none", "testuser", 3);
        var claim = await ClaimAgentWithHostAsync("Adele", callerHost);
        claim.AssertSuccess();

        var recorder = new RecordingProcessStarter();
        TerminalLauncher.ProcessStarterOverride = recorder;
        WatchdogService.StartProcessOverride = _ => null;
        try
        {
            var result = await DispatchLaunchedAsync("code-writer", $"host-override-{expectedHost}", "Brief",
                to: "Brian", autoClose: true, hostFlag: flag);

            result.AssertSuccess();
            Assert.NotEmpty(recorder.Started);
            Assert.Contains(ExpectedLaunchLine(expectedHost, "Brian"), JoinedArguments(recorder));
        }
        finally
        {
            WatchdogService.StartProcessOverride = null;
        }
    }

    [Fact]
    public async Task Dispatch_CodexAndClaudeFlagsAreMutuallyExclusive()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var result = await RunAsync(command,
            "--role", "code-writer",
            "--task", "host-conflict",
            "--brief", "Brief",
            "--no-launch",
            "--codex",
            "--claude");

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot specify both --codex and --claude");
    }

    [Fact]
    public async Task Dispatch_LaunchExecutableResolutionFailure_FailsBeforeMutatingTarget()
    {
        await InitProjectAsync("none", "testuser", 3);
        var claim = await ClaimAgentWithHostAsync("Adele", "codex");
        claim.AssertSuccess();

        TerminalLauncher.ExecutableResolverOverride = host =>
            throw new InvalidOperationException($"{host} WindowsApps alias is not launchable");

        var result = await DispatchLaunchedAsync("code-writer", "codex-launch-fails", "Brief",
            to: "Brian", autoClose: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot launch codex");
        Assert.Empty(Directory.GetFiles(Path.Combine(TestDir, "dydo", "agents", "Brian", "inbox"), "*.md"));
        var brianState = Path.Combine(TestDir, "dydo", "agents", "Brian", "state.md");
        if (File.Exists(brianState))
            Assert.DoesNotContain("status: dispatched", File.ReadAllText(brianState));
    }

    [Fact]
    public async Task Dispatch_CodexUntrustedHooks_FailsBeforeMutatingTarget()
    {
        await InitProjectAsync("none", "testuser", 3);
        var claim = await ClaimAgentWithHostAsync("Adele", "codex");
        claim.AssertSuccess();

        // The repo carries a .codex/hooks.json the user has not trust-enabled — codex would run
        // the agent unguarded. Preflight must fail before Brian is reserved or given an inbox item.
        var codexDir = Path.Combine(TestDir, ".codex");
        Directory.CreateDirectory(codexDir);
        File.WriteAllText(Path.Combine(codexDir, "hooks.json"), """{"PreToolUse":[{"command":"dydo guard"}]}""");
        DispatchPreflight.HookTrustResolverOverride = _ => DispatchPreflight.HookTrust.Untrusted;

        try
        {
            var result = await DispatchLaunchedAsync("code-writer", "codex-untrusted-hooks", "Brief",
                to: "Brian", autoClose: true);

            result.AssertExitCode(2);
            result.AssertStderrContains("UNGUARDED");
            Assert.Empty(Directory.GetFiles(Path.Combine(TestDir, "dydo", "agents", "Brian", "inbox"), "*.md"));
            var brianState = Path.Combine(TestDir, "dydo", "agents", "Brian", "state.md");
            if (File.Exists(brianState))
                Assert.DoesNotContain("status: dispatched", File.ReadAllText(brianState));
        }
        finally
        {
            DispatchPreflight.HookTrustResolverOverride = null;
        }
    }

    #endregion

    #region --to Error Cases

    [Fact]
    public async Task Dispatch_ToNonExistentAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Zorro");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Dispatch_ToAgentAssignedToDifferentHuman_Fails()
    {
        // Init project with alice's agents
        await InitProjectAsync("none", "alice", 3);

        // Switch to bob's context
        SetHuman("bob");

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("not assigned to you");
    }

    [Fact]
    public async Task Dispatch_ToBusyAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Mark Brian busy via an initial dispatch (no claimed sender → no self-collision)
        var initialDispatch = await DispatchAsync("code-writer", "busy-task", "Brief", to: "Brian");
        initialDispatch.AssertSuccess();

        // Claim Adele, then dispatch from Adele to the now-busy Brian
        await ClaimAgentAsync("Adele");

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("not free");
    }

    #endregion

    #region --escalate Tests

    [Fact]
    public async Task Dispatch_WithEscalate_SetsEscalatedFlag()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Urgent fix needed", escalate: true);

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");

        // Verify inbox file contains escalation fields
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("escalated: true", content);
        Assert.Contains("escalated_at:", content);
    }

    [Fact]
    public async Task Dispatch_WithEscalate_InboxHeaderShowsEscalated()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "urgent-fix", "Fix this now", escalate: true);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);

        // Header should have ESCALATED prefix
        Assert.Contains("# ESCALATED CODE-WRITER Request: urgent-fix", content);
    }

    [Fact]
    public async Task Dispatch_WithoutEscalate_NoEscalationInFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "normal-task", "Normal work");

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);

        Assert.DoesNotContain("escalated:", content);
        Assert.DoesNotContain("ESCALATED", content);
    }

    #endregion

    #region Inbox Display Tests

    [Fact]
    public async Task InboxShow_DisplaysEscalatedIndicator()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create an escalated inbox item
        CreateEscalatedInboxItem("Adele", "Brian", "code-writer", "urgent-task", "Urgent work");

        // Claim Adele to view inbox
        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");
    }

    [Fact]
    public async Task InboxShow_NonEscalatedItem_NoIndicator()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create a non-escalated inbox item
        CreateInboxItem("Adele", "Brian", "code-writer", "normal-task", "Normal work");

        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        Assert.DoesNotContain("[ESCALATED]", result.Stdout);
    }

    #endregion

    #region Combined Flag Tests

    [Fact]
    public async Task Dispatch_ToValidAgent_WithEscalate_BothWork()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "critical-fix", "Critical bug", to: "Brian", escalate: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("[ESCALATED]");

        // Verify dispatched to Brian
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("escalated: true", content);
    }

    [Fact]
    public async Task Dispatch_ToInvalidAgent_WithEscalate_FailsBeforeEscalation()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "InvalidAgent", escalate: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");

        // Verify no inbox files created anywhere
        foreach (var agentName in new[] { "Adele", "Brian", "Charlie" })
        {
            var inboxPath = Path.Combine(TestDir, "dydo/agents", agentName, "inbox");
            if (Directory.Exists(inboxPath))
            {
                var files = Directory.GetFiles(inboxPath, "*.md");
                Assert.Empty(files);
            }
        }
    }

    #endregion

    #region CanTakeRole at Dispatch Time

    [Fact]
    public async Task Dispatch_ReviewerToCodeWriter_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Make Adele the code-writer on "auth" by writing task role history into state
        SetTaskRoleHistory("Adele", "auth", "code-writer");

        var result = await DispatchAsync("reviewer", "auth", "Review this code", to: "Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("code-writer");
    }

    [Fact]
    public async Task Dispatch_AutoSelect_SkipsIneligibleAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Make Adele the code-writer on "auth" — she can't be reviewer
        SetTaskRoleHistory("Adele", "auth", "code-writer");

        var result = await DispatchAsync("reviewer", "auth", "Review this code");

        result.AssertSuccess();
        // Should skip Adele (ineligible) and select Brian (next alphabetically)
        result.AssertStdoutContains("Brian");
    }

    #endregion

    #region Role Validation (#0240) and Chief-of-Staff Routing (#0237)

    [Fact]
    public async Task Dispatch_UndefinedRole_FailsFastWithDefinedRoleList()
    {
        // #0240: `dydo dispatch --role planner` was silently accepted though no such role is a
        // valid dispatch target; it must now fail fast listing the defined roles.
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("planner", "some-task", "Plan the work", to: "Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("Unknown role 'planner'");
        result.AssertStderrContains("Defined roles:");
        result.AssertStderrContains("orchestrator");

        // The target must NOT have received an inbox item — the dispatch failed before selection.
        var brianInbox = Path.Combine(TestDir, "dydo", "agents", "Brian", "inbox");
        if (Directory.Exists(brianInbox))
            Assert.Empty(Directory.GetFiles(brianInbox, "*.md"));
    }

    [Fact]
    public async Task Dispatch_DefinedCustomRole_Passes()
    {
        // #0240: a custom `.role.json` defines a legitimate dispatch target — validation resolves
        // it via RoleDefinitionService, not a hardcoded list.
        await InitProjectAsync("none", "testuser", 3);
        WriteCustomRoleFile("analyst");

        var result = await DispatchAsync("analyst", "analysis-task", "Do analysis", to: "Brian");

        result.AssertSuccess();
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0, "Inbox item should be created for Brian");
    }

    [Fact]
    public async Task Dispatch_ChiefOfStaff_ToFreshOrchestrator_Succeeds()
    {
        // #0237(2): a claimed chief-of-staff routing an orchestrator to a fresh session is the
        // documented path and must satisfy the requires-prior gate.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("chief-of-staff");

        var result = await DispatchAsync("orchestrator", "route-task", "Run the sprint", to: "Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Dispatch_ChiefOfStaff_ToFreshOrchestrator_TargetTakesRole_EndToEnd()
    {
        // #0237 finding 1 (round 2): the CoS->orchestrator routing must succeed END TO END. It is
        // not enough that the dispatch command exits 0 — the launched target then claims a FRESH
        // session (no co-thinker history) and follows the documented workflow
        // (`dydo agent role orchestrator --task <task>`). Before the fix that role-set re-ran the
        // requires-prior gate with no dispatcher context and BLOCKED, wedging the reserved agent
        // downstream where the dispatcher never saw the error.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("chief-of-staff");

        var dispatch = await DispatchAsync("orchestrator", "route-task", "Run the sprint", to: "Brian");
        dispatch.AssertSuccess();

        // The dispatched target claims its own fresh session and takes the orchestrator role via
        // the documented workflow (`dydo agent role orchestrator --task <task>`). SetRole resolves
        // the dispatch provenance (from_role: chief-of-staff) and clears the requires-prior gate.
        await ReleaseAgentAsync();
        var claim = await ClaimAgentAsync("Brian");
        claim.AssertSuccess();
        var role = await SetRoleAsync("orchestrator", "route-task");

        role.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_NonChiefOfStaff_ToFreshOrchestrator_StaysGated_RendersCallerRole()
    {
        // #0237(1): a non-chief-of-staff caller stays gated, and the message resolves the CALLER's
        // real role (co-thinker) rather than the target's unset role ("unknown").
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("co-thinker");

        var result = await DispatchAsync("orchestrator", "route-task", "Run the sprint", to: "Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("You are a co-thinker.");
        result.AssertStderrContains("Orchestrator requires prior co-thinker experience");
    }

    [Fact]
    public async Task Dispatch_CaseVariantRole_CanonicalizesAndGatesIdenticallyToLowercase()
    {
        // #0240 round-3: `--role` is matched case-insensitively for UX, but every downstream role
        // gate (requires-prior constraint eval, SetRole permission map) is case-SENSITIVE. A case
        // variant like `--role Orchestrator` must be canonicalized to the defined role's exact
        // casing BEFORE the service call, so it is constraint-evaluated identically to the
        // lowercase form — not passed through verbatim, which would skip the requires-prior gate,
        // reserve+launch the target, then wedge it on a role its own `dydo agent role` rejects.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("co-thinker");

        var result = await DispatchAsync("Orchestrator", "route-task", "Run the sprint", to: "Brian");

        // Same outcome as the lowercase non-CoS repro: gated by requires-prior, caller role rendered.
        result.AssertExitCode(2);
        result.AssertStderrContains("You are a co-thinker.");
        result.AssertStderrContains("Orchestrator requires prior co-thinker experience");

        // The gate fired BEFORE selection — the target must have no inbox item and no reservation.
        var brianInbox = Path.Combine(TestDir, "dydo", "agents", "Brian", "inbox");
        if (Directory.Exists(brianInbox))
            Assert.Empty(Directory.GetFiles(brianInbox, "*.md"));
        var brianState = Path.Combine(TestDir, "dydo", "agents", "Brian", "state.md");
        if (File.Exists(brianState))
            Assert.DoesNotContain("status: dispatched", File.ReadAllText(brianState));
    }

    private void WriteCustomRoleFile(string name)
    {
        var custom = new DynaDocs.Models.RoleDefinition
        {
            Name = name,
            Description = $"Custom {name} role",
            Base = false,
            WritablePaths = ["dydo/agents/{self}/**"],
            ReadOnlyPaths = ["**"],
            TemplateFile = $"mode-{name}.template.md",
            Constraints = []
        };

        var rolesDir = Path.Combine(TestDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);
        File.WriteAllText(
            Path.Combine(rolesDir, $"{name}.role.json"),
            System.Text.Json.JsonSerializer.Serialize(
                custom, DynaDocs.Serialization.DydoConfigJsonContext.Default.RoleDefinition));
    }

    #endregion

    #region Auto-Return Routing

    [Fact]
    public async Task Dispatch_WithoutTo_ReturnsToOriginAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Simulate: Adele dispatched to Brian, so Brian has an inbox item with origin: Adele
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Brian is the sender (claimed), dispatching back without --to
        await ClaimAgentAsync("Brian");

        var result = await DispatchAsync("code-writer", "auth", "Review failed. Fix issues.");

        result.AssertSuccess();
        // Should auto-return to Adele (the origin)
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Dispatch_WithoutTo_FallsBackToAlphabetical_WhenOriginBusy()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Simulate: Adele dispatched to Brian, so Brian has an inbox item with origin: Adele
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Brian is claimed (sender)
        await ClaimAgentAsync("Brian");

        // Adele is also busy (claimed by someone else)
        var registry = new DynaDocs.Services.AgentRegistry(TestDir);
        registry.StorePendingSessionId("Adele", "other-session");
        registry.StoreSessionContext("other-session");
        var adeleCmd = DynaDocs.Commands.AgentCommand.Create();
        await RunAsync(adeleCmd, "claim", "Adele");

        // Restore session context for Brian
        StoreSessionContext();

        var result = await DispatchAsync("code-writer", "auth", "Review failed. Fix issues.");

        result.AssertSuccess();
        // Adele is busy, so should fall through to alphabetical — Charlie is next free
        result.AssertStdoutContains("Charlie");
    }

    [Fact]
    public async Task Dispatch_WithoutTo_OriginFromArchive_ReturnsToOriginAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Simulate: Adele dispatched to Brian, Brian cleared inbox (moved to archive)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Move item from inbox to archive/inbox (simulates `inbox clear`)
        var brianInboxPath = Path.Combine(TestDir, "dydo", "agents", "Brian", "inbox");
        var brianArchivePath = Path.Combine(TestDir, "dydo", "agents", "Brian", "archive", "inbox");
        Directory.CreateDirectory(brianArchivePath);
        foreach (var file in Directory.GetFiles(brianInboxPath, "*.md"))
        {
            File.Move(file, Path.Combine(brianArchivePath, Path.GetFileName(file)));
        }

        // Brian is the sender (claimed), dispatching back without --to
        await ClaimAgentAsync("Brian");

        var result = await DispatchAsync("code-writer", "auth", "Review failed. Fix issues.");

        result.AssertSuccess();
        // Should still find origin from archive and auto-return to Adele
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Dispatch_OriginPropagatesAcrossMultipleHops()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Hop 1: Adele dispatches to Brian (origin: Adele)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Brian claims and dispatches explicitly to Charlie — origin should carry forward
        await ClaimAgentAsync("Brian");
        var result = await DispatchAsync("reviewer", "auth", "Please review", to: "Charlie");
        result.AssertSuccess();

        // Verify origin: Adele is in Charlie's inbox (not origin: Brian)
        var charlieInbox = Path.Combine(TestDir, "dydo", "agents", "Charlie", "inbox");
        Assert.True(Directory.Exists(charlieInbox), "Charlie should have an inbox");
        var charlieFiles = Directory.GetFiles(charlieInbox, "*-auth.md");
        Assert.Single(charlieFiles);

        var content = File.ReadAllText(charlieFiles[0]);
        Assert.Contains("origin: Adele", content);
        Assert.Contains("from: Brian", content);
    }

    [Fact]
    public async Task Dispatch_CodeWriterToFormerReviewer_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Make Adele a former reviewer on "auth" — should NOT block code-writer dispatch
        SetTaskRoleHistory("Adele", "auth", "reviewer");

        var result = await DispatchAsync("code-writer", "auth", "Implement this feature", to: "Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    #endregion

    #region Origin in Inbox File

    [Fact]
    public async Task Dispatch_WritesOriginToInboxFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature", to: "Brian");

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.Single(inboxFiles);

        var content = File.ReadAllText(inboxFiles[0]);
        // Origin should be present — sender is Unknown (no claimed agent) so origin is Unknown
        Assert.Contains("origin:", content);
    }

    [Fact]
    public async Task InboxShow_DisplaysOriginWhenDifferentFromSender()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create inbox item where origin differs from sender
        CreateInboxItemWithOrigin("Adele", "Brian", "Zara", "code-writer", "auth", "Fix review issues");

        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("From: Brian");
        result.AssertStdoutContains("Origin: Zara");
    }

    [Fact]
    public async Task InboxShow_HidesOriginWhenSameAsSender()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create inbox item where origin == sender (first dispatch)
        CreateInboxItemWithOrigin("Adele", "Brian", "Brian", "code-writer", "auth", "Implement auth");

        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("From: Brian");
        Assert.DoesNotContain("Origin:", result.Stdout);
    }

    #endregion

    #region Double-Dispatch Race Condition Tests

    [Fact]
    public async Task Dispatch_ToSameAgentTwice_SecondFails()
    {
        await InitProjectAsync("none", "testuser", 3);

        // First dispatch to Brian succeeds
        var result1 = await DispatchAsync("code-writer", "task-1", "First dispatch", to: "Brian");
        result1.AssertSuccess();
        result1.AssertStdoutContains("Brian");

        // Second dispatch to Brian fails (status: dispatched)
        var result2 = await DispatchAsync("code-writer", "task-2", "Second dispatch", to: "Brian");
        result2.AssertExitCode(2);
        result2.AssertStderrContains("not free");
    }

    [Fact]
    public async Task Dispatch_AutoSelect_SkipsDispatchedAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // First auto-dispatch selects Adele (first alphabetically)
        var result1 = await DispatchAsync("code-writer", "task-1", "First dispatch");
        result1.AssertSuccess();
        result1.AssertStdoutContains("Adele");

        // Second auto-dispatch should skip Adele (dispatched) and select Brian
        var result2 = await DispatchAsync("code-writer", "task-2", "Second dispatch");
        result2.AssertSuccess();
        result2.AssertStdoutContains("Brian");

        // Verify Adele was NOT selected for second dispatch
        Assert.DoesNotContain("Adele", result2.Stdout.Split("Brian")[0].Length > 0 ? result2.Stdout : "");

        // Verify both inbox items exist
        var adeleInbox = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var brianInbox = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.Single(adeleInbox);
        Assert.Single(brianInbox);
    }

    #endregion

    #region --tab / --new-window Tests

    [Fact]
    public async Task Dispatch_TabAndNewWindow_BothSpecified_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", tab: true, newWindow: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot specify both --tab and --new-window");
    }

    [Fact]
    public async Task Dispatch_WithTabOnly_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", tab: true);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_WithNewWindowOnly_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", newWindow: true);

        result.AssertSuccess();
    }

    #endregion

    #region Reviewer Auto-Transition Tests

    [Fact]
    public async Task Dispatch_ToReviewer_TransitionsTaskToReviewPending()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("review-task", area: "backend");

        var result = await DispatchAsync("reviewer", "review-task", "Implemented OAuth flow");

        result.AssertSuccess();
        result.AssertStdoutContains("Task state: marked ready for review");

        // Verify task file status changed to review-pending
        var taskContent = ReadFile("dydo/project/tasks/review-task.md");
        Assert.Contains("status: review-pending", taskContent);

        // Verify brief became the review summary
        Assert.Contains("## Review Summary", taskContent);
        Assert.Contains("Implemented OAuth flow", taskContent);
        Assert.DoesNotContain("(Pending)", taskContent);
    }

    [Fact]
    public async Task Dispatch_ToNonReviewer_DoesNotTransitionTask()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("code-task", area: "backend");

        var result = await DispatchAsync("code-writer", "code-task", "Implement feature");

        result.AssertSuccess();
        Assert.DoesNotContain("Task state:", result.Stdout);

        // Verify task file status remains pending
        var taskContent = ReadFile("dydo/project/tasks/code-task.md");
        Assert.Contains("status: pending", taskContent);
    }

    [Fact]
    public async Task Dispatch_ToReviewer_WithoutTaskFile_StillSucceeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        // No task file created — dispatch should still succeed
        var result = await DispatchAsync("reviewer", "nonexistent-task", "Review this");

        result.AssertSuccess();
        Assert.DoesNotContain("Task state:", result.Stdout);
    }

    [Fact]
    public async Task Dispatch_ToReviewer_ReviewFailedToReviewPending()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("failed-task", area: "backend");

        // Manually set task status to review-failed (simulates a failed review)
        var taskPath = Path.Combine(TestDir, "dydo/project/tasks/failed-task.md");
        var content = File.ReadAllText(taskPath);
        content = content.Replace("status: pending", "status: review-failed");
        File.WriteAllText(taskPath, content);

        var result = await DispatchAsync("reviewer", "failed-task", "Fixed issues, ready for re-review");

        result.AssertSuccess();
        result.AssertStdoutContains("Task state: marked ready for review");

        // Verify task transitions from review-failed to review-pending
        var taskContent = ReadFile("dydo/project/tasks/failed-task.md");
        Assert.Contains("status: review-pending", taskContent);
    }

    #endregion

    #region --agent Alias Tests

    [Fact]
    public async Task Dispatch_AgentAlias_DispatchesToSpecifiedAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchWithAgentAliasAsync("code-writer", "my-task", "Implement feature", agent: "Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0, "Inbox item should be created for Brian");
    }

    [Fact]
    public async Task Dispatch_AgentAlias_NonExistentAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchWithAgentAliasAsync("code-writer", "my-task", "Brief", agent: "Zorro");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Dispatch_AgentAlias_ProducesSameResultAsTo()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch with --agent to Brian
        var agentResult = await DispatchWithAgentAliasAsync("code-writer", "task-a", "Brief A", agent: "Brian");
        agentResult.AssertSuccess();

        // Dispatch with --to to Charlie
        var toResult = await DispatchAsync("code-writer", "task-b", "Brief B", to: "Charlie");
        toResult.AssertSuccess();

        // Both should have inbox items with same structure
        var brianFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        var charlieFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Charlie/inbox"), "*.md");
        Assert.Single(brianFiles);
        Assert.Single(charlieFiles);

        var brianContent = File.ReadAllText(brianFiles[0]);
        var charlieContent = File.ReadAllText(charlieFiles[0]);

        // Both should have same structure (role, from, brief section)
        Assert.Contains("role: code-writer", brianContent);
        Assert.Contains("role: code-writer", charlieContent);
    }

    #endregion

    #region Filename Sanitization Tests

    [Fact]
    public async Task Dispatch_TaskNameWithColon_CreatesInboxFileWithSanitizedName()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "Review Coordinator: Auth", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Warning: Task name sanitized");

        // Verify inbox file was created with sanitized filename
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.Single(inboxFiles);
        Assert.Contains("Review Coordinator- Auth", Path.GetFileName(inboxFiles[0]));

        // Verify original task name preserved in file content
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("task: Review Coordinator: Auth", content);
    }

    [Fact]
    public async Task Dispatch_TaskNameWithColon_InboxShowDisplaysCorrectly()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "Review Coordinator: Auth", "Implement feature", to: "Brian");
        result.AssertSuccess();

        // Claim Brian and check inbox
        await ClaimAgentAsync("Brian");
        var inboxResult = await InboxShowAsync();

        inboxResult.AssertSuccess();
        inboxResult.AssertStdoutContains("Review Coordinator: Auth");
    }

    #endregion

    #region --brief-file Tests

    [Fact]
    public async Task Dispatch_WithBriefFile_ReadsBriefFromFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var briefPath = Path.Combine(TestDir, "brief.txt");
        File.WriteAllText(briefPath, "This is a brief from a file.");

        var result = await DispatchWithBriefFileAsync("code-writer", "my-task", briefPath);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.Single(inboxFiles);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("This is a brief from a file.", content);
    }

    [Fact]
    public async Task Dispatch_WithBriefFileNotFound_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchWithBriefFileAsync("code-writer", "my-task", "/nonexistent/brief.txt");

        result.AssertExitCode(2);
        result.AssertStderrContains("Brief file not found");
    }

    [Fact]
    public async Task Dispatch_WithNeitherBriefNorBriefFile_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "my-task", "--no-launch" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("Provide --brief or --brief-file");
    }

    [Fact]
    public async Task Dispatch_WithBothBriefAndBriefFile_BriefFileWins()
    {
        await InitProjectAsync("none", "testuser", 3);

        var briefPath = Path.Combine(TestDir, "brief.txt");
        File.WriteAllText(briefPath, "Brief from file.");

        var command = DispatchCommand.Create();
        var args = new[]
        {
            "--role", "code-writer",
            "--task", "my-task",
            "--brief", "Inline brief",
            "--brief-file", briefPath,
            "--no-launch"
        };
        var result = await RunAsync(command, args);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("Brief from file.", content);
    }

    #endregion

    #region --auto-close Tests

    [Fact]
    public async Task Dispatch_WithAutoClose_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", autoClose: true);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_WithAutoClose_SetsStateFlag()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", autoClose: true);

        result.AssertSuccess();

        // Auto-close is now stored in the agent's state.md, not a marker file
        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", stateContent);
    }

    [Fact]
    public async Task Dispatch_AutoClose_CombinesWithTab()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", tab: true, autoClose: true);

        result.AssertSuccess();

        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", stateContent);
    }

    [Fact]
    public async Task Dispatch_AutoClose_CombinesWithEscalate()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", escalate: true, autoClose: true);

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");

        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", stateContent);
    }

    [Fact]
    public async Task Dispatch_AutoClose_CombinesWithTo()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Brian", autoClose: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        var statePath = Path.Combine(TestDir, "dydo/agents/Brian/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", stateContent);
    }

    #endregion

    #region Window Routing Tests

    [Fact]
    public async Task Dispatch_Default_SetsNullWindowId()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief");

        result.AssertSuccess();

        // Default dispatch (no --new-window) should not set a window-id
        // because ConfigureWindowSettings returns null for MRU routing
        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("window-id: null", stateContent);
    }

    [Fact]
    public async Task Dispatch_WithNewWindow_SetsWindowId()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", newWindow: true);

        result.AssertSuccess();

        // --new-window should generate a GUID window-id
        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.DoesNotContain("window-id: null", stateContent);
        // Window ID should be an 8-char hex string
        Assert.Matches(@"window-id: [0-9a-f]{8}", stateContent);
    }

    [Fact]
    public async Task Dispatch_WithAutoCloseAndNoNewWindow_HasNullWindowId()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", autoClose: true);

        result.AssertSuccess();

        // auto-close without --new-window: should still have null window-id (MRU routing)
        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", stateContent);
        Assert.Contains("window-id: null", stateContent);
    }

    #endregion

    #region Shell Metacharacter Rejection Tests

    [Theory]
    [InlineData("Fixed hang when chaining dydo whoami && dydo agent status", "&&")]
    [InlineData("Check this || that condition", "||")]
    [InlineData("Run $(whoami) to find user", "$(")]
    [InlineData("Use `command` for output", "`")]
    [InlineData("Path is ${HOME}/projects", "${")]
    public async Task Dispatch_BriefWithShellMetacharacters_Fails(string brief, string expectedPattern)
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", brief);

        result.AssertExitCode(2);
        result.AssertStderrContains(expectedPattern);
        result.AssertStderrContains("--brief-file");
    }

    [Theory]
    [InlineData("Simple brief without special chars")]
    [InlineData("Fixed the login & signup flow")]
    [InlineData("Added retry logic (3 attempts max)")]
    [InlineData("Refactored auth module - see PR #42")]
    [InlineData("Fixed all 5 review issues: (1) Removed dead code ternary in TemplateCommand.cs:157. (2) Simplified logic.")]
    [InlineData("Fixed all 5 review issues. 1 Removed dead code ternary in TemplateCommand.cs line 157. 2 Simplified.")]
    [InlineData("Implemented OAuth flow; added token refresh")]
    [InlineData("See file Services/Auth.cs for details")]
    public async Task Dispatch_BriefWithSafeContent_Succeeds(string brief)
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", brief);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_BriefFile_BypassesShellMetacharacterCheck()
    {
        await InitProjectAsync("none", "testuser", 3);

        var briefPath = Path.Combine(TestDir, "brief.txt");
        File.WriteAllText(briefPath, "Fixed hang when chaining dydo whoami && dydo agent status");

        var result = await DispatchWithBriefFileAsync("code-writer", "my-task", briefPath);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("&&", content);
    }

    #endregion

    #region --no-launch Nudge Tests

    [Fact]
    public async Task Dispatch_NoLaunch_FirstAttempt_FailsWithNudge()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "nudge-test");

        // Dispatch WITHOUT bypassing the nudge (call command directly)
        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "nudge-test", "--brief", "Test brief", "--no-launch" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("--no-launch flag");
        result.AssertStderrContains("run it again");
    }

    [Fact]
    public async Task Dispatch_NoLaunch_SecondAttempt_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "nudge-test");

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "nudge-test", "--brief", "Test brief", "--no-launch" };

        // First attempt: fails with nudge
        var result1 = await RunAsync(command, args);
        result1.AssertExitCode(2);

        // Second attempt: passes
        var result2 = await RunAsync(command, args);
        result2.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_NoLaunch_WithoutSender_SkipsNudge()
    {
        await InitProjectAsync("none", "testuser", 3);
        // No agent claimed — no sender context

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "nudge-test", "--brief", "Test brief", "--no-launch" };
        var result = await RunAsync(command, args);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_NoLaunch_MarkerCleanedOnRelease()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "nudge-test");

        // First attempt creates marker
        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "nudge-test", "--brief", "Test brief", "--no-launch" };
        await RunAsync(command, args);

        // Marker should exist
        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.no-launch-nudge-nudge-test");
        Assert.True(File.Exists(markerPath));

        // Release cleans up markers
        await ReleaseAgentAsync();
        Assert.False(File.Exists(markerPath));
    }

    #endregion

    #region --auto-close Nudge Tests (#222 Slice B)

    [Fact]
    public async Task Dispatch_MissingAutoClose_FirstAttempt_FailsWithNudge()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "autoclose-test");

        // Launched dispatch (no --no-launch) without --auto-close: soft-block once.
        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "autoclose-test", "--brief", "Test brief" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("without --auto-close");
        result.AssertStderrContains("re-run to proceed");
    }

    [Fact]
    public async Task Dispatch_MissingAutoClose_SecondAttempt_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "autoclose-test");

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "autoclose-test", "--brief", "Test brief" };

        // First attempt: blocked by the nudge
        var result1 = await RunAsync(command, args);
        result1.AssertExitCode(2);

        // Re-running the same bare command proceeds
        var result2 = await RunAsync(command, args);
        result2.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_WithAutoClose_PassesClean()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "autoclose-test");

        // --auto-close spins up the watchdog; keep it a no-op in tests.
        WatchdogService.StartProcessOverride = _ => null;
        try
        {
            var command = DispatchCommand.Create();
            var args = new[] { "--role", "code-writer", "--task", "autoclose-test", "--brief", "Test brief", "--auto-close" };
            var result = await RunAsync(command, args);

            result.AssertSuccess();
            Assert.DoesNotContain("without --auto-close", result.Stderr);
            AssertFileNotExists("dydo/agents/Adele/.auto-close-nudge-autoclose-test");
        }
        finally
        {
            WatchdogService.StartProcessOverride = null;
        }
    }

    [Fact]
    public async Task Dispatch_NoLaunch_DoesNotFireAutoCloseNudge()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "autoclose-test");

        // --no-launch has no terminal to linger, so it takes the no-launch nudge path,
        // never the auto-close one — no auto-close marker is written.
        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "autoclose-test", "--brief", "Test brief", "--no-launch" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("--no-launch flag");
        Assert.DoesNotContain("without --auto-close", result.Stderr);
        AssertFileNotExists("dydo/agents/Adele/.auto-close-nudge-autoclose-test");
    }

    [Fact]
    public async Task Dispatch_MissingAutoClose_MarkerCleanedOnRelease()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("orchestrator", "autoclose-test");

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "autoclose-test", "--brief", "Test brief" };
        await RunAsync(command, args);

        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.auto-close-nudge-autoclose-test");
        Assert.True(File.Exists(markerPath));

        await ReleaseAgentAsync();
        Assert.False(File.Exists(markerPath));
    }

    #endregion

    #region Inbox Prioritization

    [Fact]
    public async Task Dispatch_AutoSelect_PrefersAgentWithEmptyInbox()
    {
        await InitProjectAsync("none", "testuser", 3);
        // Give Adele an inbox item — Brian should be selected instead
        CreateInboxItem("Adele", "Test", "code-writer", "old-task", "Stale task");

        var result = await DispatchAsync("code-writer", "new-task", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Dispatch_AutoSelect_AllWithInbox_FallsBackToAlphabetical()
    {
        await InitProjectAsync("none", "testuser", 3);
        // Give all agents inbox items
        CreateInboxItem("Adele", "Test", "code-writer", "task-a", "Task A");
        CreateInboxItem("Brian", "Test", "code-writer", "task-b", "Task B");
        CreateInboxItem("Charlie", "Test", "code-writer", "task-c", "Task C");

        var result = await DispatchAsync("code-writer", "new-task", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele"); // All have inbox, falls back to alphabetical
    }

    [Fact]
    public async Task Dispatch_AutoSelect_EmptyInboxBeatsAlphabeticOrder()
    {
        await InitProjectAsync("none", "testuser", 3);
        // Adele and Brian have inbox items, Charlie does not
        CreateInboxItem("Adele", "Test", "code-writer", "task-a", "Task A");
        CreateInboxItem("Brian", "Test", "code-writer", "task-b", "Task B");

        var result = await DispatchAsync("code-writer", "new-task", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Charlie"); // Only one without inbox
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> TaskCreateAsync(string name, string? description = null, string area = "general")
    {
        var command = TaskCommand.Create();
        var args = new List<string> { "create", name, "--area", area };
        if (description != null)
        {
            args.Add("--description");
            args.Add(description);
        }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchAsync(
        string role,
        string task,
        string brief,
        string? files = null,
        string? to = null,
        bool escalate = false,
        bool tab = false,
        bool newWindow = false,
        bool autoClose = false)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-launch",
        };

        BypassNoLaunchNudge(task);

        if (files != null) { args.Add("--files"); args.Add(files); }
        if (to != null) { args.Add("--to"); args.Add(to); }
        if (escalate) { args.Add("--escalate"); }
        if (tab) { args.Add("--tab"); }
        if (newWindow) { args.Add("--new-window"); }
        if (autoClose) { args.Add("--auto-close"); }

        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchWithAgentAliasAsync(
        string role,
        string task,
        string brief,
        string? agent = null)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-launch"
        };

        BypassNoLaunchNudge(task);
        if (agent != null) { args.Add("--agent"); args.Add(agent); }

        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchWithBriefFileAsync(
        string role,
        string task,
        string briefFile)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief-file", briefFile,
            "--no-launch"
        };

        BypassNoLaunchNudge(task);
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchLaunchedAsync(
        string role,
        string task,
        string brief,
        string? to = null,
        bool autoClose = false,
        string? hostFlag = null)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
        };

        if (to != null) { args.Add("--to"); args.Add(to); }
        if (autoClose) { args.Add("--auto-close"); }
        if (hostFlag != null) { args.Add(hostFlag); }

        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> ClaimAgentWithHostAsync(string agentName, string host)
    {
        var registry = new AgentRegistry(TestDir);
        registry.StorePendingSessionId(agentName, TestSessionId, host);

        var command = AgentCommand.Create();
        var result = await RunAsync(command, "claim", agentName);
        StoreSessionContext();
        return result;
    }

    private static string JoinedArguments(RecordingProcessStarter recorder) =>
        string.Join("\n", recorder.Started.Select(p => p.Arguments));

    // The exact launch invocation per host: codex carries the shipped posture (issue 0253),
    // claude launches bare.
    private static string ExpectedLaunchLine(string host, string agentName) =>
        host == "codex"
            ? $"codex -m gpt-5.6-terra --sandbox workspace-write --ask-for-approval on-request '{agentName} --inbox'"
            : $"{host} '{agentName} --inbox'";

    private async Task<CommandResult> InboxShowAsync()
    {
        var command = InboxCommand.Create();
        return await RunAsync(command, "show");
    }

    private void CreateInboxItem(string agentName, string fromAgent, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            role: {role}
            task: {task}
            received: {DateTime.UtcNow:o}
            ---

            # {role.ToUpperInvariant()} Request: {task}

            ## From

            {fromAgent}

            ## Brief

            {brief}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-{task}.md"), content);
    }

    private void CreateInboxItemWithOrigin(string agentName, string fromAgent, string origin, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            origin: {origin}
            role: {role}
            task: {task}
            received: {DateTime.UtcNow:o}
            ---

            # {role.ToUpperInvariant()} Request: {task}

            ## From

            {fromAgent}

            ## Brief

            {brief}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-{task}.md"), content);
    }

    private void SetTaskRoleHistory(string agentName, string task, string role)
    {
        var statePath = Path.Combine(TestDir, "dydo", "agents", agentName, "state.md");
        if (File.Exists(statePath))
        {
            var content = File.ReadAllText(statePath);
            // Replace the task-role-history line
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"^task-role-history:.*$",
                $"task-role-history: {{ \"{task}\": [\"{role}\"] }}",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            File.WriteAllText(statePath, content);
        }
        else
        {
            // Create a minimal state file with task role history
            var workspace = Path.Combine(TestDir, "dydo", "agents", agentName);
            Directory.CreateDirectory(workspace);
            var historyValue = $"{{ \"{task}\": [\"{role}\"] }}";
            var content = $"""
                ---
                agent: {agentName}
                role: null
                task: null
                status: free
                assigned: testuser
                started: null
                writable-paths: []
                readonly-paths: []
                unread-must-reads: []
                task-role-history: {historyValue}
                ---

                # {agentName} — Session State
                """;
            File.WriteAllText(statePath, content);
        }
    }

    private void CreateEscalatedInboxItem(string agentName, string fromAgent, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var escalatedAt = DateTime.UtcNow;
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            role: {role}
            task: {task}
            received: {DateTime.UtcNow:o}
            escalated: true
            escalated_at: {escalatedAt:o}
            ---

            # ESCALATED {role.ToUpperInvariant()} Request: {task}

            ## From

            {fromAgent}

            ## Brief

            {brief}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-{task}.md"), content);
    }

    #endregion
}
