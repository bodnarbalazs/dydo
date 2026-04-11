namespace DynaDocs.Tests.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Integration tests verifying dydo commands work correctly in git worktree context.
/// Worktrees live at dydo/_system/.local/worktrees/{id}/ with:
///   - dydo/agents/ symlinked to main repo (shared agent state)
///   - dydo/_system/.local/ gitignored (missing in worktrees)
///   - dydo/_system/ (non-.local) present
///   - dydo/project/ present
///   - ConfigService walks up from CWD to find worktree's own dydo.json
/// </summary>
[Collection("ConsoleOutput")]
public class WorktreeCompatTests : IDisposable
{
    private readonly string _testDir;

    public WorktreeCompatTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wtcompat-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    #region GuardCommand — missing _system/.local/ handled gracefully

    [Fact]
    public void DailyValidation_MissingLocalDir_EnsureLocalDirCreatesIt()
    {
        // In worktrees, _system/.local/ is gitignored and absent.
        // The guard uses EnsureLocalDirExists before writing last-validation.
        var dydoRoot = Path.Combine(_testDir, "project", "dydo");
        Directory.CreateDirectory(Path.Combine(dydoRoot, "_system"));

        PathUtils.EnsureLocalDirExists(dydoRoot);

        Assert.True(Directory.Exists(Path.Combine(dydoRoot, "_system", ".local")));

        // Guard can now write the timestamp
        var timestampPath = Path.Combine(dydoRoot, "_system", ".local", "last-validation");
        File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));
        Assert.True(File.Exists(timestampPath));
    }

    [Fact]
    public void DailyValidation_ExistingTimestamp_IsReadable()
    {
        var dydoRoot = Path.Combine(_testDir, "dydo");
        PathUtils.EnsureLocalDirExists(dydoRoot);
        var timestampPath = Path.Combine(dydoRoot, "_system", ".local", "last-validation");

        File.WriteAllText(timestampPath, DateTime.UtcNow.ToString("O"));

        var lastRun = File.GetLastWriteTimeUtc(timestampPath);
        Assert.True((DateTime.UtcNow - lastRun).TotalHours < 1);
    }

    #endregion

    #region AgentRegistry.IsWorktreeStale

    [Fact]
    public void IsWorktreeStale_WorktreeDirExists_ReturnsFalse()
    {
        var basePath = Path.Combine(_testDir, "main-repo");
        var worktreeId = "fix-auth-20260320";
        // GetDydoRoot resolves to basePath/dydo/ when no dydo.json exists
        Directory.CreateDirectory(Path.Combine(basePath, "dydo", "_system", ".local", "worktrees", worktreeId));

        var registry = CreateRegistryWithBasePath(basePath);

        Assert.False(registry.IsWorktreeStale(worktreeId));
    }

    [Fact]
    public void IsWorktreeStale_WorktreeDirMissing_ReturnsTrue()
    {
        var basePath = Path.Combine(_testDir, "main-repo");
        Directory.CreateDirectory(basePath);

        var registry = CreateRegistryWithBasePath(basePath);

        Assert.True(registry.IsWorktreeStale("nonexistent-worktree"));
    }

    [Fact]
    public void IsWorktreeStale_BasePath_IsProjectRoot()
    {
        // When basePath is the project root, worktrees are at GetDydoRoot(basePath)/_system/.local/worktrees/
        var projectRoot = Path.Combine(_testDir, "project");
        var worktreeId = "Adele-20260320140000";
        // GetDydoRoot resolves to projectRoot/dydo/ when no dydo.json exists
        Directory.CreateDirectory(Path.Combine(projectRoot, "dydo", "_system", ".local", "worktrees", worktreeId));

        var registry = CreateRegistryWithBasePath(projectRoot);

        Assert.False(registry.IsWorktreeStale(worktreeId));
    }

    #endregion

    #region GuardCommand.ResolveWorktreePath

    [Fact]
    public void ResolveWorktreePath_NullPath_ReturnsNull()
    {
        Assert.Null(GuardCommand.ResolveWorktreePath(null));
    }

    [Fact]
    public void ResolveWorktreePath_EmptyPath_ReturnsEmpty()
    {
        Assert.Equal("", GuardCommand.ResolveWorktreePath(""));
    }

    [Fact]
    public void ResolveWorktreePath_AbsoluteNonWorktreePath_PassesThrough()
    {
        var absPath = Path.Combine(_testDir, "Commands", "Foo.cs");
        var result = GuardCommand.ResolveWorktreePath(absPath);
        Assert.Equal(absPath, result);
    }

    [Fact]
    public void ResolveWorktreePath_AbsoluteWorktreePath_NormalizesToMain()
    {
        // Create the worktree structure with dydo.json so NormalizeWorktreePath can detect it
        var mainRoot = Path.Combine(_testDir, "resolve-main");
        var worktreeRoot = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "task-1");
        Directory.CreateDirectory(worktreeRoot);
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");

        var absPath = Path.Combine(worktreeRoot, "Commands", "Foo.cs").Replace('\\', '/');
        var result = GuardCommand.ResolveWorktreePath(absPath);
        var expected = Path.Combine(mainRoot, "Commands", "Foo.cs").Replace('\\', '/');
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveWorktreePath_RelativePath_NonWorktreeCwd_PassesThrough()
    {
        // When CWD is not inside a worktree, relative paths pass through unchanged
        var mainDir = Path.Combine(_testDir, "resolve-nonwt");
        Directory.CreateDirectory(mainDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(mainDir);
            var result = GuardCommand.ResolveWorktreePath("Commands/Foo.cs");
            Assert.Equal("Commands/Foo.cs", result);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void ResolveWorktreePath_RelativePath_WorktreeCwd_ResolvesToAbsolute()
    {
        // When CWD is inside a worktree, relative paths get resolved to absolute first
        var mainRoot = Path.Combine(_testDir, "resolve-wt");
        var worktreeRoot = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "task-2");
        Directory.CreateDirectory(worktreeRoot);
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeRoot);
            var result = GuardCommand.ResolveWorktreePath("Commands/Foo.cs");
            // Should resolve to the main project path, not remain relative
            Assert.NotNull(result);
            Assert.True(Path.IsPathRooted(result) || result.Contains("Commands/Foo.cs"));
            // The worktree prefix should be stripped
            Assert.DoesNotContain("worktrees/task-2/Commands", result!.Replace('\\', '/'));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    #endregion

    #region GuardCommand helper methods

    [Fact]
    public void TruncateCommand_ShortCommand_ReturnsUnchanged()
    {
        Assert.Equal("dydo whoami", GuardCommand.TruncateCommand("dydo whoami"));
    }

    [Fact]
    public void TruncateCommand_LongCommand_TruncatesWithEllipsis()
    {
        var longCmd = new string('x', 200);
        var result = GuardCommand.TruncateCommand(longCmd);
        Assert.Equal(103, result.Length);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void IsReadAllowed_NullPath_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsReadAllowed(null, null));
    }

    [Fact]
    public void IsReadAllowed_EmptyPath_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsReadAllowed("", null));
    }

    [Fact]
    public void IsReadAllowed_NoAgent_BootstrapFile_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsReadAllowed("README.md", null));
    }

    [Fact]
    public void IsReadAllowed_NoAgent_NonBootstrapFile_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsReadAllowed("dydo/understand/about.md", null));
    }

    [Fact]
    public void IsReadAllowed_AgentWithRole_OtherAgentWorkflow_ReturnsFalse()
    {
        var agent = new AgentState { Name = "Adele", Role = "code-writer" };
        Assert.False(GuardCommand.IsReadAllowed("dydo/agents/Brian/workflow.md", agent));
    }

    [Fact]
    public void IsReadAllowed_AgentWithRole_OwnWorkflow_ReturnsTrue()
    {
        var agent = new AgentState { Name = "Adele", Role = "code-writer" };
        Assert.True(GuardCommand.IsReadAllowed("dydo/agents/Adele/workflow.md", agent));
    }

    [Fact]
    public void IsReadAllowed_AgentWithRole_AnyFile_ReturnsTrue()
    {
        var agent = new AgentState { Name = "Adele", Role = "code-writer" };
        Assert.True(GuardCommand.IsReadAllowed("Commands/Foo.cs", agent));
    }

    [Fact]
    public void IsBootstrapFile_RootFile_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsBootstrapFile("CLAUDE.md"));
    }

    [Fact]
    public void IsBootstrapFile_SingleComponentPath_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsBootstrapFile("dydo.json"));
    }

    [Fact]
    public void IsBootstrapFile_IndexMd_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsBootstrapFile("dydo/index.md"));
    }

    [Fact]
    public void IsBootstrapFile_DeepFile_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsBootstrapFile("dydo/understand/about.md"));
    }

    [Fact]
    public void IsOtherAgentWorkflow_DifferentAgent_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsOtherAgentWorkflow("dydo/agents/Brian/workflow.md", "Adele"));
    }

    [Fact]
    public void IsOtherAgentWorkflow_SameAgent_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsOtherAgentWorkflow("dydo/agents/Adele/workflow.md", "Adele"));
    }

    [Fact]
    public void IsOtherAgentWorkflow_NotWorkflow_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsOtherAgentWorkflow("dydo/agents/Brian/state.md", "Adele"));
    }

    [Fact]
    public void ExtractMessageIdFromPath_ValidInboxPath_ReturnsId()
    {
        Assert.Equal("abc12345", GuardCommand.ExtractMessageIdFromPath("dydo/agents/Adele/inbox/abc12345-msg-task.md"));
    }

    [Fact]
    public void ExtractMessageIdFromPath_NonInboxPath_ReturnsNull()
    {
        Assert.Null(GuardCommand.ExtractMessageIdFromPath("dydo/agents/Adele/state.md"));
    }

    [Fact]
    public void NormalizeForMustReadComparison_DydoPath_ExtractsDydoRelative()
    {
        var result = GuardCommand.NormalizeForMustReadComparison("C:/project/dydo/understand/about.md");
        Assert.Equal("dydo/understand/about.md", result);
    }

    [Fact]
    public void NormalizeForMustReadComparison_NonDydoPath_ReturnsNormalized()
    {
        var result = GuardCommand.NormalizeForMustReadComparison("Commands/Foo.cs");
        Assert.Equal("Commands/Foo.cs", result);
    }

    [Fact]
    public void IsBootstrapFile_PathWithLeadingSlash_SingleComponent_ReturnsTrue()
    {
        // Paths like "/README.md" split to ["README.md"] — single component
        Assert.True(GuardCommand.IsBootstrapFile("/README.md"));
    }

    [Fact]
    public void IsBootstrapFile_WorkflowMd_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsBootstrapFile("dydo/agents/Adele/workflow.md"));
    }

    [Fact]
    public void IsModeFile_MatchingAgent_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsModeFile("dydo/agents/Adele/modes/code-writer.md", "Adele"));
    }

    [Fact]
    public void IsModeFile_DifferentAgent_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsModeFile("dydo/agents/Brian/modes/code-writer.md", "Adele"));
    }

    [Fact]
    public void IsModeFile_NonModeFile_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsModeFile("dydo/agents/Adele/state.md", "Adele"));
    }

    [Fact]
    public void IsAnyModeFile_ModeFile_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsAnyModeFile("dydo/agents/Brian/modes/reviewer.md"));
    }

    [Fact]
    public void IsAnyModeFile_NonModeFile_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsAnyModeFile("dydo/agents/Brian/workflow.md"));
    }

    [Fact]
    public void IsReadAllowed_AgentNoRole_ModeFile_ReturnsTrue()
    {
        var agent = new AgentState { Name = "Adele" };
        Assert.True(GuardCommand.IsReadAllowed("dydo/agents/Adele/modes/code-writer.md", agent));
    }

    [Fact]
    public void IsReadAllowed_AgentNoRole_NonBootstrapNonMode_ReturnsFalse()
    {
        var agent = new AgentState { Name = "Adele" };
        Assert.False(GuardCommand.IsReadAllowed("Commands/Foo.cs", agent));
    }

    [Fact]
    public void ParseClaimCommand_ValidClaim_ReturnsTrueWithName()
    {
        var (isClaim, name) = GuardCommand.ParseClaimCommand("dydo agent claim Adele");
        Assert.True(isClaim);
        Assert.Equal("Adele", name);
    }

    [Fact]
    public void ParseClaimCommand_AutoClaim_ReturnsTrueWithAuto()
    {
        var (isClaim, name) = GuardCommand.ParseClaimCommand("dydo agent claim auto");
        Assert.True(isClaim);
        Assert.Equal("auto", name);
    }

    [Fact]
    public void ParseClaimCommand_NonClaimCommand_ReturnsFalse()
    {
        var (isClaim, _) = GuardCommand.ParseClaimCommand("dydo whoami");
        Assert.False(isClaim);
    }

    [Fact]
    public void IsDydoCommand_DydoPrefix_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsDydoCommand("dydo whoami"));
    }

    [Fact]
    public void IsDydoCommand_NonDydo_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsDydoCommand("dotnet build"));
    }

    [Fact]
    public void IsHumanOnlyDydoCommand_TaskApprove_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsHumanOnlyDydoCommand("dydo task approve"));
    }

    [Fact]
    public void IsHumanOnlyDydoCommand_AgentClaim_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsHumanOnlyDydoCommand("dydo agent claim auto"));
    }

    [Fact]
    public void IsDydoWaitCommand_WaitNoCancel_ReturnsTrue()
    {
        Assert.True(GuardCommand.IsDydoWaitCommand("dydo wait --task foo"));
    }

    [Fact]
    public void IsDydoWaitCommand_WaitWithCancel_ReturnsFalse()
    {
        Assert.False(GuardCommand.IsDydoWaitCommand("dydo wait --task foo --cancel"));
    }

    [Fact]
    public void DefaultNudges_NpxDydo_Matches()
    {
        var matched = ConfigFactory.DefaultNudges.Any(n =>
            new Regex(n.Pattern, RegexOptions.IgnoreCase).IsMatch("npx dydo whoami"));
        Assert.True(matched);
    }

    [Fact]
    public void DefaultNudges_DirectDydo_NoMatch()
    {
        var matched = ConfigFactory.DefaultNudges.Any(n =>
            new Regex(n.Pattern, RegexOptions.IgnoreCase).IsMatch("dydo whoami"));
        Assert.False(matched);
    }

    [Fact]
    public void ExtractMessageIdFromPath_BackslashPath_ReturnsId()
    {
        Assert.Equal("abc12345", GuardCommand.ExtractMessageIdFromPath(@"dydo\agents\Adele\inbox\abc12345-msg-task.md"));
    }

    [Fact]
    public void FindMessageInfo_NoInboxDir_ReturnsNull()
    {
        var workspace = Path.Combine(_testDir, "find-msg-test");
        Directory.CreateDirectory(workspace);
        Assert.Null(GuardCommand.FindMessageInfo(workspace, "abc12345"));
    }

    [Fact]
    public void FindMessageInfo_EmptyInbox_ReturnsNull()
    {
        var workspace = Path.Combine(_testDir, "find-msg-empty");
        Directory.CreateDirectory(Path.Combine(workspace, "inbox"));
        Assert.Null(GuardCommand.FindMessageInfo(workspace, "abc12345"));
    }

    [Fact]
    public void FindMessageInfo_MatchingFile_ReturnsParsedInfo()
    {
        var workspace = Path.Combine(_testDir, "find-msg-match");
        var inboxPath = Path.Combine(workspace, "inbox");
        Directory.CreateDirectory(inboxPath);
        var filePath = Path.Combine(inboxPath, "abc12345-msg-task.md");
        File.WriteAllText(filePath, """
            ---
            from: Grace
            subject: review-result
            ---
            Body here.
            """);

        var result = GuardCommand.FindMessageInfo(workspace, "abc12345");
        Assert.NotNull(result);
        Assert.Equal("Grace", result.Value.From);
        Assert.Equal("review-result", result.Value.Subject);
        Assert.Equal(filePath, result.Value.FilePath);
    }

    [Fact]
    public void FindMessageInfo_NoFrontmatter_ReturnsNull()
    {
        var workspace = Path.Combine(_testDir, "find-msg-nofm");
        var inboxPath = Path.Combine(workspace, "inbox");
        Directory.CreateDirectory(inboxPath);
        File.WriteAllText(Path.Combine(inboxPath, "abc12345-msg-task.md"), "No frontmatter here");

        Assert.Null(GuardCommand.FindMessageInfo(workspace, "abc12345"));
    }

    [Fact]
    public void FindMessageInfo_UnclosedFrontmatter_ReturnsNull()
    {
        var workspace = Path.Combine(_testDir, "find-msg-unclosed");
        var inboxPath = Path.Combine(workspace, "inbox");
        Directory.CreateDirectory(inboxPath);
        File.WriteAllText(Path.Combine(inboxPath, "abc12345-msg-task.md"), "---\nfrom: Grace\nno closing");

        Assert.Null(GuardCommand.FindMessageInfo(workspace, "abc12345"));
    }

    [Fact]
    public void DefaultNudges_DotnetDydo_Matches()
    {
        var matched = ConfigFactory.DefaultNudges.Any(n =>
            new Regex(n.Pattern, RegexOptions.IgnoreCase).IsMatch("dotnet dydo whoami"));
        Assert.True(matched);
    }

    [Fact]
    public void DefaultNudges_BashDydo_Matches()
    {
        var matched = ConfigFactory.DefaultNudges.Any(n =>
            new Regex(n.Pattern, RegexOptions.IgnoreCase).IsMatch("bash -c \"dydo whoami\""));
        Assert.True(matched);
    }

    [Fact]
    public void DefaultNudges_DotnetRunDydo_Matches()
    {
        var matched = ConfigFactory.DefaultNudges.Any(n =>
            new Regex(n.Pattern, RegexOptions.IgnoreCase).IsMatch("dotnet run -- agent claim auto"));
        Assert.True(matched);
    }

    [Fact]
    public void DefaultNudges_PythonDydo_Matches()
    {
        var matched = ConfigFactory.DefaultNudges.Any(n =>
            new Regex(n.Pattern, RegexOptions.IgnoreCase).IsMatch("python dydo whoami"));
        Assert.True(matched);
    }

    [Fact]
    public void LogAuditEvent_NullSessionId_ReturnsImmediately()
    {
        // Should not throw — exits early on null sessionId
        GuardCommand.LogAuditEvent(new AuditService(), null, new AgentRegistry(), new AuditEvent());
    }

    [Fact]
    public void LogAuditEvent_EmptySessionId_ReturnsImmediately()
    {
        GuardCommand.LogAuditEvent(new AuditService(), "", new AgentRegistry(), new AuditEvent());
    }

    [Fact]
    public void ShouldBypassOffLimits_BootstrapFile_ReturnsTrue()
    {
        Assert.True(GuardCommand.ShouldBypassOffLimits("dydo/index.md", null));
    }

    [Fact]
    public void ShouldBypassOffLimits_ModeFileForAgent_ReturnsTrue()
    {
        var agent = new AgentState { Name = "Adele" };
        Assert.True(GuardCommand.ShouldBypassOffLimits("dydo/agents/Adele/modes/code-writer.md", agent));
    }

    [Fact]
    public void ShouldBypassOffLimits_AnyModeFileWithRole_ReturnsTrue()
    {
        var agent = new AgentState { Name = "Adele", Role = "code-writer" };
        Assert.True(GuardCommand.ShouldBypassOffLimits("dydo/agents/Brian/modes/reviewer.md", agent));
    }

    [Fact]
    public void ShouldBypassOffLimits_NonBootstrapNoAgent_ReturnsFalse()
    {
        Assert.False(GuardCommand.ShouldBypassOffLimits("Commands/Foo.cs", null));
    }

    [Fact]
    public void ShouldBypassOffLimits_NonModeFileWithAgent_ReturnsFalse()
    {
        var agent = new AgentState { Name = "Adele" };
        Assert.False(GuardCommand.ShouldBypassOffLimits("Commands/Foo.cs", agent));
    }

    [Fact]
    public void CheckBashFileOperation_ReadOp_NotOffLimits_ReturnsNull()
    {
        var op = new FileOperation { Type = FileOperationType.Read, Path = "README.md", Command = "cat" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-guard-test"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "cat README.md", null, offLimits, registry, audit);
        Assert.Null(result);
    }

    [Fact]
    public void CheckBashFileOperation_WriteOp_NoAgent_ReturnsNull()
    {
        // Write op but no agent claimed → no RBAC check, returns null
        var op = new FileOperation { Type = FileOperationType.Write, Path = "output.txt", Command = "echo" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-guard-write"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "echo hi > output.txt", null, offLimits, registry, audit);
        Assert.Null(result);
    }

    [Fact]
    public void CheckBashFileOperation_DeleteOp_NoAgent_ReturnsNull()
    {
        var op = new FileOperation { Type = FileOperationType.Delete, Path = "temp.log", Command = "rm" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-guard-del"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "rm temp.log", null, offLimits, registry, audit);
        Assert.Null(result);
    }

    [Fact]
    public void CheckBashFileOperation_MoveOp_NoAgent_ReturnsNull()
    {
        var op = new FileOperation { Type = FileOperationType.Move, Path = "a.txt", Command = "mv" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-guard-mv"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "mv a.txt b.txt", null, offLimits, registry, audit);
        Assert.Null(result);
    }

    [Fact]
    public void CheckBashFileOperation_CopyOp_NoAgent_ReturnsNull()
    {
        var op = new FileOperation { Type = FileOperationType.Copy, Path = "a.txt", Command = "cp" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-guard-cp"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "cp a.txt b.txt", null, offLimits, registry, audit);
        Assert.Null(result);
    }

    [Fact]
    public void CheckBashFileOperation_ReadOp_NoAgent_BlocksNonBootstrapPath()
    {
        // An unclaimed agent should not be able to read non-bootstrap files via bash
        var op = new FileOperation { Type = FileOperationType.Read, Path = "Commands/GuardCommand.cs", Command = "cat" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-staged-read"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "cat Commands/GuardCommand.cs", null, offLimits, registry, audit);
        Assert.NotNull(result);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void CheckBashFileOperation_ReadOp_NoAgent_AllowsBootstrapFile()
    {
        // Bootstrap files (root-level) should still be readable without identity
        var op = new FileOperation { Type = FileOperationType.Read, Path = "README.md", Command = "cat" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-staged-bootstrap"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "cat README.md", null, offLimits, registry, audit);
        Assert.Null(result);
    }

    [Fact]
    public void CheckBashFileOperation_ReadOp_NoAgent_AllowsDydoIndex()
    {
        var op = new FileOperation { Type = FileOperationType.Read, Path = "dydo/index.md", Command = "cat" };
        var offLimits = new OffLimitsService();
        var registry = CreateRegistryWithBasePath(Path.Combine(_testDir, "bash-staged-index"));
        var audit = new AuditService();

        var result = GuardCommand.CheckBashFileOperation(op, "cat dydo/index.md", null, offLimits, registry, audit);
        Assert.Null(result);
    }

    #endregion

    #region InitCommand refuses inside worktree

    [Fact]
    public void IsInsideWorktree_CwdInWorktree_ReturnsTrue()
    {
        // Create a real directory with the worktree marker in its path
        var worktreeCwd = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", "test-wt");
        Directory.CreateDirectory(worktreeCwd);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(worktreeCwd);
            Assert.True(PathUtils.IsInsideWorktree());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void IsInsideWorktree_CwdInMainProject_ReturnsFalse()
    {
        var mainDir = Path.Combine(_testDir, "main-project");
        Directory.CreateDirectory(mainDir);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(mainDir);
            Assert.False(PathUtils.IsInsideWorktree());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    #endregion

    #region TemplateCommand refuses inside worktree

    [Fact]
    public void IsInsideWorktree_WithExplicitPath_DetectsWorktree()
    {
        // TemplateCommand calls IsInsideWorktree() with no args (uses CWD)
        // WorkspaceCommand calls IsInsideWorktree(basePath) with explicit path
        var worktreePath = "C:/project/dydo/_system/.local/worktrees/fix-auth/Commands";
        Assert.True(PathUtils.IsInsideWorktree(worktreePath));
    }

    [Fact]
    public void IsInsideWorktree_NonWorktreePath_ReturnsFalse()
    {
        Assert.False(PathUtils.IsInsideWorktree("C:/project/Commands/Foo.cs"));
    }

    #endregion

    #region WorkspaceCommand init refuses inside worktree

    [Fact]
    public void IsInsideWorktree_WorkspaceBasePath_InWorktree_ReturnsTrue()
    {
        // WorkspaceCommand.ExecuteInit passes basePath to IsInsideWorktree
        var basePath = "/home/user/project/dydo/_system/.local/worktrees/task-1";
        Assert.True(PathUtils.IsInsideWorktree(basePath));
    }

    [Fact]
    public void IsInsideWorktree_WorkspaceBasePath_InMainRepo_ReturnsFalse()
    {
        var basePath = "/home/user/project";
        Assert.False(PathUtils.IsInsideWorktree(basePath));
    }

    [Fact]
    public void IsInsideWorktree_BackslashPaths_StillDetected()
    {
        var basePath = @"C:\Projects\DynaDocs\dydo\_system\.local\worktrees\fix-auth";
        Assert.True(PathUtils.IsInsideWorktree(basePath));
    }

    #endregion

    #region Agent lifecycle through symlinked agents directory

    [Fact]
    public void AgentWorkspace_ThroughSymlink_ReadState()
    {
        var mainAgents = Path.Combine(_testDir, "main", "dydo", "agents");
        var agentWs = Path.Combine(mainAgents, "Adele");
        Directory.CreateDirectory(agentWs);

        File.WriteAllText(Path.Combine(agentWs, "state.md"), """
            ---
            status: working
            role: code-writer
            task: fix-auth
            ---
            """);

        var statePath = Path.Combine(agentWs, "state.md");
        Assert.True(File.Exists(statePath));
        var content = File.ReadAllText(statePath);
        Assert.Contains("role: code-writer", content);
        Assert.Contains("task: fix-auth", content);
    }

    [Fact]
    public void AgentWorkspace_ThroughSymlink_WriteSession()
    {
        var mainAgents = Path.Combine(_testDir, "main", "dydo", "agents");
        var agentWs = Path.Combine(mainAgents, "Adele");
        Directory.CreateDirectory(agentWs);

        var sessionData = """{"Agent":"Adele","SessionId":"test-session-123"}""";
        File.WriteAllText(Path.Combine(agentWs, ".session"), sessionData);

        Assert.Equal(sessionData, File.ReadAllText(Path.Combine(agentWs, ".session")));
    }

    [Fact]
    public void AgentWorkspace_ThroughSymlink_WorktreeMarkerReadable()
    {
        var mainAgents = Path.Combine(_testDir, "main", "dydo", "agents");
        var agentWs = Path.Combine(mainAgents, "Brian");
        Directory.CreateDirectory(agentWs);

        var worktreeId = "Brian-20260320140000";
        File.WriteAllText(Path.Combine(agentWs, ".worktree"), worktreeId);
        File.WriteAllText(Path.Combine(agentWs, ".worktree-path"), "/some/worktree/path");

        Assert.Equal(worktreeId, File.ReadAllText(Path.Combine(agentWs, ".worktree")).Trim());
        Assert.Equal("/some/worktree/path", File.ReadAllText(Path.Combine(agentWs, ".worktree-path")).Trim());
    }

    #endregion

    #region InboxService — read/write through symlinked agents directory

    [Fact]
    public void InboxItem_WriteAndRead_ThroughAgentsDirectory()
    {
        var agentWs = Path.Combine(_testDir, "dydo", "agents", "Adele");
        var inboxPath = Path.Combine(agentWs, "inbox");
        Directory.CreateDirectory(inboxPath);

        var messageId = "abc12345";
        var content = $"""
            ---
            id: {messageId}
            type: dispatch
            role: code-writer
            task: fix-auth
            from: Emma
            received: 2026-03-20T14:00:00Z
            brief: Fix the authentication bug
            reply-required: true
            ---
            """;

        var filePath = Path.Combine(inboxPath, $"{messageId}-code-writer-fix-auth.md");
        File.WriteAllText(filePath, content);

        Assert.True(File.Exists(filePath));
        var readContent = File.ReadAllText(filePath);
        Assert.Contains("task: fix-auth", readContent);
        Assert.Contains("from: Emma", readContent);
    }

    [Fact]
    public void InboxItem_Archive_MovesFile()
    {
        var agentWs = Path.Combine(_testDir, "dydo", "agents", "Brian");
        var inboxPath = Path.Combine(agentWs, "inbox");
        var archivePath = Path.Combine(agentWs, "archive", "inbox");
        Directory.CreateDirectory(inboxPath);
        Directory.CreateDirectory(archivePath);

        var fileName = "abc12345-code-writer-task.md";
        File.WriteAllText(Path.Combine(inboxPath, fileName), "---\nid: abc12345\n---");

        File.Move(Path.Combine(inboxPath, fileName), Path.Combine(archivePath, fileName));

        Assert.False(File.Exists(Path.Combine(inboxPath, fileName)));
        Assert.True(File.Exists(Path.Combine(archivePath, fileName)));
    }

    #endregion

    #region MessageService — write through symlinked agents directory

    [Fact]
    public void Message_Write_CreatesInboxFile()
    {
        var targetWs = Path.Combine(_testDir, "dydo", "agents", "Charlie");
        var inboxPath = Path.Combine(targetWs, "inbox");
        Directory.CreateDirectory(inboxPath);

        var messageId = Guid.NewGuid().ToString("N")[..8];
        var sanitizedSubject = PathUtils.SanitizeForFilename("worktree-compat-tests");
        var filePath = Path.Combine(inboxPath, $"{messageId}-msg-{sanitizedSubject}.md");

        var content = $"""
            ---
            id: {messageId}
            type: message
            from: Grace
            subject: worktree-compat-tests
            received: {DateTime.UtcNow:o}
            ---

            # Message from Grace

            ## Body

            Tests complete, ready for review.
            """;

        File.WriteAllText(filePath, content);

        Assert.True(File.Exists(filePath));
        var readContent = File.ReadAllText(filePath);
        Assert.Contains("from: Grace", readContent);
        Assert.Contains("subject: worktree-compat-tests", readContent);
    }

    [Fact]
    public void Message_MultipleMessages_AllPresent()
    {
        var targetWs = Path.Combine(_testDir, "dydo", "agents", "Adele");
        var inboxPath = Path.Combine(targetWs, "inbox");
        Directory.CreateDirectory(inboxPath);

        for (var i = 0; i < 3; i++)
        {
            var id = $"msg{i:D5}00";
            File.WriteAllText(
                Path.Combine(inboxPath, $"{id}-msg-test.md"),
                $"---\nid: {id}\ntype: message\nfrom: Grace\n---\nBody {i}");
        }

        var files = Directory.GetFiles(inboxPath, "*.md");
        Assert.Equal(3, files.Length);
    }

    #endregion

    #region ConfigService.GetProjectRoot from worktree subdirectory

    [Fact]
    public void ConfigService_GetProjectRoot_FindsWorktreeDydoJson()
    {
        var worktreeRoot = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", "fix-auth");
        Directory.CreateDirectory(worktreeRoot);
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), """{"version":"1.0"}""");

        var configService = new ConfigService();
        var projectRoot = configService.GetProjectRoot(worktreeRoot);

        Assert.NotNull(projectRoot);
        Assert.Equal(worktreeRoot, projectRoot);
    }

    [Fact]
    public void ConfigService_GetProjectRoot_FromSubdirectory_FindsRoot()
    {
        var worktreeRoot = Path.Combine(_testDir, "worktrees", "fix-auth");
        Directory.CreateDirectory(worktreeRoot);
        File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), """{"version":"1.0"}""");

        var subDir = Path.Combine(worktreeRoot, "Commands", "Sub");
        Directory.CreateDirectory(subDir);

        var configService = new ConfigService();
        var projectRoot = configService.GetProjectRoot(subDir);

        Assert.NotNull(projectRoot);
        Assert.Equal(worktreeRoot, projectRoot);
    }

    [Fact]
    public void ConfigService_GetProjectRoot_NoDydoJson_ReturnsNull()
    {
        var emptyDir = Path.Combine(_testDir, "no-config");
        Directory.CreateDirectory(emptyDir);

        var configService = new ConfigService();
        Assert.Null(configService.GetProjectRoot(emptyDir));
    }

    #endregion

    #region NormalizeWorktreePath — worktree-specific edge case

    [Fact]
    public void NormalizeWorktreePath_WorktreeWithSystemContent_StripsCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dydo-wtc-{Guid.NewGuid():N}");
        try
        {
            var worktreeRoot = Path.Combine(tempDir, "dydo", "_system", ".local", "worktrees", "task-1");
            Directory.CreateDirectory(worktreeRoot);
            File.WriteAllText(Path.Combine(worktreeRoot, "dydo.json"), "{}");

            // File inside the worktree's _system (non-.local) — must resolve to main repo
            var input = Path.Combine(tempDir, "dydo/_system/.local/worktrees/task-1/dydo/_system/roles/code-writer.role.json").Replace('\\', '/');
            var expected = Path.Combine(tempDir, "dydo/_system/roles/code-writer.role.json").Replace('\\', '/');

            Assert.Equal(expected, PathUtils.NormalizeWorktreePath(input));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    #endregion

    #region AgentRegistry.GetWorktreeId

    [Fact]
    public void GetWorktreeId_MarkerExists_ReturnsId()
    {
        var basePath = Path.Combine(_testDir, "project");
        var registry = CreateRegistryWithBasePath(basePath);
        var agentWs = registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(agentWs);

        var worktreeId = "Adele-20260320140000";
        File.WriteAllText(Path.Combine(agentWs, ".worktree"), worktreeId);

        Assert.Equal(worktreeId, registry.GetWorktreeId("Adele"));
    }

    [Fact]
    public void GetWorktreeId_NoMarker_ReturnsNull()
    {
        var basePath = Path.Combine(_testDir, "project");
        var registry = CreateRegistryWithBasePath(basePath);
        var agentWs = registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(agentWs);

        Assert.Null(registry.GetWorktreeId("Adele"));
    }

    [Fact]
    public void GetWorktreeId_MarkerHasWhitespace_ReturnsTrimmed()
    {
        var basePath = Path.Combine(_testDir, "project");
        var registry = CreateRegistryWithBasePath(basePath);
        var agentWs = registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(agentWs);

        File.WriteAllText(Path.Combine(agentWs, ".worktree"), "  Adele-20260320\n  ");

        Assert.Equal("Adele-20260320", registry.GetWorktreeId("Adele"));
    }

    #endregion

    #region Helpers

    private AgentRegistry CreateRegistryWithBasePath(string basePath)
    {
        Directory.CreateDirectory(basePath);
        return new AgentRegistry(basePath);
    }

    private class FakeConfigService : IConfigService
    {
        private readonly string _basePath;
        public FakeConfigService(string basePath) => _basePath = basePath;
        public string? FindConfigFile(string? startPath = null) => null;
        public DydoConfig? LoadConfig(string? startPath = null) => null;
        public void SaveConfig(DydoConfig config, string path) { }
        public string? GetHumanFromEnv() => "tester";
        public string? GetProjectRoot(string? startPath = null) => _basePath;
        public string GetDydoRoot(string? startPath = null) => Path.Combine(_basePath, "dydo");
        public string GetAgentsPath(string? startPath = null) => Path.Combine(_basePath, "agents");
        public string GetDocsPath(string? startPath = null) => Path.Combine(_basePath, "docs");
        public string GetTasksPath(string? startPath = null) => Path.Combine(_basePath, "tasks");
        public string GetAuditPath(string? startPath = null) => Path.Combine(_basePath, "audit");
        public string GetChangelogPath(string? startPath = null) => Path.Combine(_basePath, "changelog");
        public string GetIssuesPath(string? startPath = null) => Path.Combine(_basePath, "issues");
        public (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config) => (true, null);
    }

    #endregion
}
