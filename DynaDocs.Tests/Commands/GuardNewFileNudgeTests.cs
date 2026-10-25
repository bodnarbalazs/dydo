namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// New-file nudges ("match": "new-file"): a pattern matched against the project-relative path
/// of a file a write tool is about to create (LC-759).
/// </summary>
public class GuardNewFileNudgeTests : IDisposable
{
    private const string ToolsPattern = "^tools/(?!one-off/)";

    private readonly string _root;
    private readonly string _markerDir;

    public GuardNewFileNudgeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-newfile-nudge-" + Guid.NewGuid().ToString("N")[..8]);
        _markerDir = Path.Combine(_root, "dydo", "_system", ".local");
        Directory.CreateDirectory(Path.Combine(_root, "tools"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private GuardCommand.GuardEnv Env(params NudgeConfig[] nudges) =>
        new(new DydoConfig { Nudges = nudges.ToList() }, _markerDir, _root);

    private static NudgeConfig NewFile(string pattern, string severity = "block", string message = "Put it in tools/one-off/.") =>
        new() { Pattern = pattern, Message = message, Severity = severity, Match = "new-file" };

    private (int? Result, string Stderr) Check(GuardCommand.GuardEnv env, params string[] targets)
    {
        var original = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        try
        {
            return (GuardCommand.CheckNewFileNudges(targets, _root, env), capture.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    #region Firing

    [Fact]
    public void NewFileUnderMatchingPath_Blocks()
    {
        var (result, stderr) = Check(Env(NewFile(ToolsPattern)), "tools/x.py");

        Assert.Equal(ExitCodes.ToolError, result);
        Assert.Contains("BLOCKED: Put it in tools/one-off/.", stderr);
    }

    [Fact]
    public void NewFileOutsidePattern_Passes()
    {
        var (result, stderr) = Check(Env(NewFile(ToolsPattern)), "tools/one-off/x.py");

        Assert.Null(result);
        Assert.Empty(stderr);
    }

    [Fact]
    public void ExistingFile_DoesNotFire()
    {
        File.WriteAllText(Path.Combine(_root, "tools", "existing.py"), "");

        var (result, _) = Check(Env(NewFile(ToolsPattern)), "tools/existing.py");

        Assert.Null(result);
    }

    [Fact]
    public void ExistingDirectory_DoesNotFire()
    {
        Directory.CreateDirectory(Path.Combine(_root, "tools", "pkg"));

        var (result, _) = Check(Env(NewFile(ToolsPattern)), "tools/pkg");

        Assert.Null(result);
    }

    [Fact]
    public void PatternMatchesCaseInsensitively()
    {
        var (result, _) = Check(Env(NewFile(ToolsPattern)), "Tools/x.py");

        Assert.Equal(ExitCodes.ToolError, result);
    }

    [Fact]
    public void FirstMatchingNewTarget_FillsCaptureGroups()
    {
        var nudge = NewFile("^tools/([^/]+)$", message: "Move $1 under tools/one-off/.");
        File.WriteAllText(Path.Combine(_root, "tools", "old.py"), "");

        var (result, stderr) = Check(Env(nudge), "src/a.cs", "tools/old.py", "tools/first.py", "tools/second.py");

        Assert.Equal(ExitCodes.ToolError, result);
        Assert.Contains("BLOCKED: Move first.py under tools/one-off/.", stderr);
    }

    [Fact]
    public void NoticeSeverity_PassesWithMessage()
    {
        var (result, stderr) = Check(Env(NewFile(ToolsPattern, "notice")), "tools/x.py");

        Assert.Null(result);
        Assert.Contains("NOTICE: Put it in tools/one-off/.", stderr);
    }

    [Fact]
    public void InvalidRegex_IsSkipped()
    {
        var (result, _) = Check(Env(NewFile("[unclosed"), NewFile(ToolsPattern, message: "second")), "tools/x.py");

        Assert.Equal(ExitCodes.ToolError, result);
    }

    [Fact]
    public void WithoutProjectRoot_NothingFires()
    {
        var env = new GuardCommand.GuardEnv(new DydoConfig { Nudges = [NewFile(ToolsPattern)] }, _markerDir);

        Assert.Null(GuardCommand.CheckNewFileNudges(["tools/x.py"], _root, env));
    }

    [Fact]
    public void RelativeTargetResolvesAgainstCwd()
    {
        var cwd = Path.Combine(_root, "src");

        var result = GuardCommand.CheckNewFileNudges(["../tools/x.py"], cwd, Env(NewFile(ToolsPattern)));

        Assert.Equal(ExitCodes.ToolError, result);
    }

    #endregion

    #region Warn - block once, pass on retry

    [Fact]
    public void WarnSeverity_BlocksOnceThenPassesOnRetry()
    {
        var env = Env(NewFile(ToolsPattern, "warn"));
        var marker = Path.Combine(_markerDir, $".nudge-new-file-{GuardCommand.ComputeNudgeHash(ToolsPattern)}");

        var (first, stderr) = Check(env, "tools/x.py");
        Assert.Equal(ExitCodes.ToolError, first);
        Assert.Contains("Retry the same write to proceed anyway.", stderr);
        Assert.True(File.Exists(marker));

        var (second, _) = Check(env, "tools/x.py");
        Assert.Null(second);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void WarnMarker_DoesNotCollideWithCommandNudgeOfSamePattern()
    {
        var command = new NudgeConfig { Pattern = ToolsPattern, Message = "command lane", Severity = "warn" };
        var env = Env(command, NewFile(ToolsPattern, "warn", "file lane"));

        // The command nudge blocks once and leaves its pass-through marker.
        Assert.Equal(ExitCodes.ToolError, GuardCommand.CheckNudges("tools/run.sh", env));

        // That marker must not let the first new-file write through.
        var (result, stderr) = Check(env, "tools/x.py");
        Assert.Equal(ExitCodes.ToolError, result);
        Assert.Contains("BLOCKED: file lane", stderr);

        // And the command nudge still has its own pass waiting.
        Assert.Null(GuardCommand.CheckNudges("tools/run.sh", env));
    }

    #endregion

    #region Lane separation

    [Fact]
    public void NewFileNudge_NeverMatchesCommandLine()
    {
        Assert.Null(GuardCommand.CheckNudges("tools/x.py", Env(NewFile("tools/"))));
    }

    [Fact]
    public void CommandNudge_NeverMatchesWritePath()
    {
        var command = new NudgeConfig { Pattern = "tools/", Message = "m", Severity = "block" };

        Assert.Null(GuardCommand.CheckNewFileNudges(["tools/x.py"], _root, Env(command)));
    }

    [Fact]
    public void UnknownMatchValue_MatchesNeitherLane()
    {
        var nudge = new NudgeConfig { Pattern = "tools/", Message = "m", Severity = "block", Match = "path" };

        Assert.Null(GuardCommand.CheckNudges("tools/x.py", Env(nudge)));
        Assert.Null(GuardCommand.CheckNewFileNudges(["tools/x.py"], _root, Env(nudge)));
    }

    #endregion

    #region Project-relative path (Windows forms, escapes)

    [Fact]
    public void WindowsAbsoluteBackslashPath_IsProjectRelative()
    {
        if (!OperatingSystem.IsWindows()) return;

        var target = Path.Combine(_root, "tools", "x.py").Replace('/', '\\');

        var (result, _) = Check(Env(NewFile(ToolsPattern)), target);

        Assert.Equal(ExitCodes.ToolError, result);
    }

    [Fact]
    public void MixedCaseDriveLetter_IsProjectRelative()
    {
        if (!OperatingSystem.IsWindows()) return;

        var target = Path.Combine(_root, "tools", "x.py");
        var flipped = (char.IsUpper(target[0]) ? char.ToLowerInvariant(target[0]) : char.ToUpperInvariant(target[0])) + target[1..];

        var (result, _) = Check(Env(NewFile(ToolsPattern)), flipped);

        Assert.Equal(ExitCodes.ToolError, result);
    }

    [Fact]
    public void DotDotEscape_OutsideRoot_NeverMatches()
    {
        var (result, _) = Check(Env(NewFile("tools/")), "../outside/tools/x.py", "tools/../../tools/y.py");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("tools/a/../b.py", "tools/b.py")]
    [InlineData("./tools/b.py", "tools/b.py")]
    public void ProjectRelativePath_CollapsesAndUsesForwardSlashes(string target, string expected)
    {
        var absolute = PathUtils.CollapseRelativeSegments(Path.Combine(_root, target));

        Assert.Equal(expected, GuardCommand.ProjectRelativePath(absolute, _root));
    }

    [Fact]
    public void ProjectRelativePath_RootItself_IsDot()
    {
        Assert.Equal(".", GuardCommand.ProjectRelativePath(_root, _root));
    }

    [Fact]
    public void ProjectRelativePath_OtherDrive_IsOutside()
    {
        if (!OperatingSystem.IsWindows()) return;

        var otherDrive = char.ToUpperInvariant(_root[0]) == 'Z' ? "Y:/tools/x.py" : "Z:/tools/x.py";

        Assert.Null(GuardCommand.ProjectRelativePath(otherDrive, _root));
    }

    #endregion

    #region System-nudge merge identity is (match, pattern)

    private static NudgeConfig H19() =>
        ConfigFactory.DefaultNudges.First(n => n.Severity == "block" && n.Pattern.Contains("npx"));

    [Fact]
    public void Merge_NewFileEntryWithH19Pattern_KeepsH19AndTheEntry()
    {
        var h19 = H19();
        var entry = NewFile(h19.Pattern, "warn", "file lane");

        var merged = GuardCommand.MergeSystemNudges([entry]);

        var command = Assert.Single(merged, n => n.Pattern == h19.Pattern && n.IsCommandMatch);
        Assert.Equal("block", command.Severity);
        var kept = Assert.Single(merged, n => n.Pattern == h19.Pattern && n.IsNewFileMatch);
        Assert.Same(entry, kept);
        Assert.Equal("warn", kept.Severity);
    }

    [Fact]
    public void Merge_NewFileEntryWithH19Pattern_StillBlocksBash()
    {
        var h19 = H19();
        var env = Env(NewFile(h19.Pattern, "notice", "file lane"));

        Assert.Equal(ExitCodes.ToolError, GuardCommand.CheckNudges("npx dydo agent claim auto", env));
    }

    [Fact]
    public void Merge_RebuiltCommandNudge_PreservesExplicitCommandMatch()
    {
        var h19 = H19();
        var downgraded = new NudgeConfig { Pattern = h19.Pattern, Message = "mine", Severity = "warn", Match = "command" };

        var merged = GuardCommand.MergeSystemNudges([downgraded]);

        var rebuilt = Assert.Single(merged, n => n.Pattern == h19.Pattern);
        Assert.Equal("block", rebuilt.Severity);
        Assert.Equal("command", rebuilt.Match);
        Assert.Equal("mine", rebuilt.Message);
    }

    [Fact]
    public void Merge_NewFileEntryCarryingStaleMessage_IsNotHealed()
    {
        const string retired = "Use dydo worktree commands instead of git worktree directly.";
        var entry = NewFile("^tools/", "warn", retired);

        var merged = GuardCommand.MergeSystemNudges([entry]);

        Assert.Contains(entry, merged);
    }

    #endregion

    #region Shipped default: soft notice for docs outside dydo/ (DYD-375)

    private const string DocsNotice = "NOTICE: Docs usually live under `dydo/`. If this is a pointer README, or it belongs here, carry on.";

    private GuardCommand.GuardEnv DefaultEnv() => new(ConfigFactory.CreateDefault(), _markerDir, _root);

    [Theory]
    [InlineData("src/notes.md")]
    [InlineData("src/README.md")]
    [InlineData("notes.md")]
    [InlineData("docs/guide.MD")]
    [InlineData("src/dydo/x.md")]
    [InlineData("src/skills/x.md")]
    [InlineData("dydo.md")]
    public void DefaultDocsNudge_NewMdOutsideDydo_PrintsNoticeAndPasses(string target)
    {
        var (result, stderr) = Check(DefaultEnv(), target);

        Assert.Null(result);
        Assert.Contains(DocsNotice, stderr);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("readme.md")]
    [InlineData("CLAUDE.md")]
    [InlineData("AGENTS.md")]
    [InlineData("THIRD-PARTY-NOTICES.md")]
    [InlineData("CHANGELOG.md")]
    [InlineData("dydo/x.md")]
    [InlineData("dydo/guides/new-guide.md")]
    [InlineData("skills/a/SKILL.md")]
    [InlineData(".claude/skills/a/SKILL.md")]
    [InlineData(".agents/skills/a/SKILL.md")]
    [InlineData(".codex/notes.md")]
    [InlineData(".github/x.md")]
    [InlineData("src/notes.txt")]
    [InlineData("src/notes.md.bak")]
    public void DefaultDocsNudge_ExceptedPath_PrintsNothing(string target)
    {
        var (result, stderr) = Check(DefaultEnv(), target);

        Assert.Null(result);
        Assert.Empty(stderr);
    }

    [Fact]
    public void DefaultDocsNudge_EditToExistingMd_PrintsNothing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "src", "notes.md"), "# notes");

        var (result, stderr) = Check(DefaultEnv(), "src/notes.md");

        Assert.Null(result);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Merge_NeverForcesTheDocsNotice()
    {
        var docs = ConfigFactory.DocsOutsideDydoNudge;

        Assert.DoesNotContain(GuardCommand.MergeSystemNudges([]), n => n.Pattern == docs.Pattern);

        var edited = new NudgeConfig { Pattern = docs.Pattern, Message = "mine", Severity = "notice", Match = "new-file" };
        var kept = Assert.Single(GuardCommand.MergeSystemNudges([edited]), n => n.Pattern == docs.Pattern);
        Assert.Same(edited, kept);
    }

    #endregion
}
