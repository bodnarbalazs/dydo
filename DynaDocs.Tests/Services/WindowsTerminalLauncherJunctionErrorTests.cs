namespace DynaDocs.Tests.Services;

using System.Text.RegularExpressions;
using DynaDocs.Services;

/// <summary>
/// Tests that junction creation errors in WindowsTerminalLauncher are NOT silenced.
/// Currently, New-Item Junction commands pipe to Out-Null, hiding errors.
/// The fix will remove Out-Null so junction failures are visible.
///
/// These tests are expected to FAIL against current code (red phase TDD).
/// </summary>
public class WindowsTerminalLauncherJunctionErrorTests
{
    private static readonly Regex JunctionOutNullPattern =
        new(@"New-Item\s.*Junction.*\|\s*Out-Null", RegexOptions.None, TimeSpan.FromSeconds(1));

    [Fact]
    public void GetArguments_Worktree_JunctionDoesNotPipeToOutNull()
    {
        var args = WindowsTerminalLauncher.GetArguments("Adele", worktreeId: "test-wt");

        Assert.False(
            JunctionOutNullPattern.IsMatch(args),
            $"Junction creation should not pipe to Out-Null (errors would be hidden).\nMatch found in: {args}");
    }

    [Fact]
    public void GetArguments_WorktreeWithMainRoot_JunctionDoesNotPipeToOutNull()
    {
        var args = WindowsTerminalLauncher.GetArguments("Adele", worktreeId: "test-wt", mainProjectRoot: @"C:\project");

        Assert.False(
            JunctionOutNullPattern.IsMatch(args),
            $"Junction creation should not pipe to Out-Null (errors would be hidden).\nMatch found in: {args}");
    }
}
