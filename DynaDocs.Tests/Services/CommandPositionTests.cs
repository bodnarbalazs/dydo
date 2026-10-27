namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class CommandPositionTests
{
    [Theory]
    [InlineData(new[] { "gh", "pr" }, 0)]
    [InlineData(new[] { "A=1", "B=2", "gh" }, 2)]
    [InlineData(new[] { "env", "-u", "X", "Y=1", "gh" }, 4)]
    [InlineData(new[] { "timeout", "-s", "KILL", "5", "gh" }, 4)]
    [InlineData(new[] { "timeout", "--", "5", "gh" }, 3)]
    [InlineData(new[] { "nohup", "nice", "-n", "5", "gh" }, 4)]
    [InlineData(new[] { "if", "!", "gh" }, 2)]
    [InlineData(new[] { "env" }, 1)]
    public void Skip_FindsTheProgramWord(string[] words, int at)
    {
        Assert.Equal(at, CommandPosition.Skip(words));
    }

    [Fact]
    public void Skip_ReportsTheAssignmentsItPasses()
    {
        List<(string Name, string Value)> assignments = [];

        CommandPosition.Skip(["GH_REPO=a/b", "env", "X=", "gh"], assignments);

        Assert.Equal([("GH_REPO", "a/b"), ("X", "")], assignments);
    }

    // R2-2: env's -u/--unset and -i are reported in order with the assignments.
    [Fact]
    public void Skip_ReportsEnvsEffectsInOrder()
    {
        List<(string Name, string Value)> assignments = [];

        var at = CommandPosition.Skip(["A=1", "env", "-u", "B", "--unset=C", "--unset", "D", "-i", "E=2", "gh"], assignments);

        Assert.Equal(9, at);
        Assert.Equal([("A", "1"), ("B", ""), ("C", ""), ("D", ""), (CommandPosition.AllVariables, ""), ("E", "2")], assignments);
    }

    // R2-4: env -S runs a string, so env itself is the program.
    [Theory]
    [InlineData(new[] { "env", "-S", "gh pr" }, 0)]
    [InlineData(new[] { "X=1", "env", "-u", "Y", "--split-string=gh pr" }, 1)]
    [InlineData(new[] { "nohup", "env", "-Sgh" }, 1)]
    public void Skip_StopsAtEnvSplittingAString(string[] words, int at)
    {
        Assert.Equal(at, CommandPosition.Skip(words));
    }

    [Theory]
    [InlineData(new[] { "-S", "gh pr ready", "12" }, "gh pr ready 12")]
    [InlineData(new[] { "--split-string=gh pr ready" }, "gh pr ready")]
    [InlineData(new[] { "--split-string", "gh pr ready" }, "gh pr ready")]
    [InlineData(new[] { "-u", "X", "-Sgh pr ready" }, "gh pr ready")]
    public void ScriptOfEnv_IsItsSplitString(string[] rest, string script)
    {
        var shell = CommandPosition.ScriptOf("env", rest);

        Assert.NotNull(shell);
        Assert.Equal(script, shell.Text);
    }

    [Theory]
    [InlineData(@"C:\Program Files\GitHub CLI\gh.exe", "gh")]
    [InlineData("/usr/bin/GH", "gh")]
    [InlineData("tool.ps1", "tool")]
    [InlineData("gh.cmd", "gh")]
    public void ProgramName_DropsPathCaseAndSuffix(string word, string name)
    {
        Assert.Equal(name, CommandPosition.ProgramName(word));
    }

    // B1 (admiral ruling 2026-10-27): Codex's envelope is a shell, its options, its script flag and one script.
    [Theory]
    [InlineData(new[] { "bash", "-lc", "x" }, "Posix")]
    [InlineData(new[] { "/bin/sh", "-c", "x" }, "Posix")]
    [InlineData(new[] { "zsh", "-l", "-c", "x" }, "Posix")]
    [InlineData(new[] { "pwsh", "-Command", "x" }, "PowerShell")]
    [InlineData(new[] { @"C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe", "-NoProfile", "-Command", "x" }, "PowerShell")]
    [InlineData(new[] { "pwsh.exe", "-NoLogo", "-ExecutionPolicy", "Bypass", "-c", "x" }, "PowerShell")]
    public void Envelope_IsItsScriptInItsShellsDialect(string[] argv, string dialect)
    {
        var script = CommandPosition.Envelope(argv);

        Assert.NotNull(script);
        Assert.Equal("x", script.Text);
        Assert.Equal(Enum.Parse<ShellDialect>(dialect), script.Dialect);
    }

    [Theory]
    [InlineData(new object[] { new[] { "bash", "x.sh" } })]
    [InlineData(new object[] { new[] { "bash", "-c", "x", "arg0" } })]
    [InlineData(new object[] { new[] { "pwsh", "-Command", "gh", "pr", "ready" } })]
    [InlineData(new object[] { new[] { "pwsh", "-EncodedCommand", "eAA=" } })]
    [InlineData(new object[] { new[] { "pwsh", "-File", "x.ps1", "-Command" } })]
    [InlineData(new object[] { new[] { "cmd", "/c", "x" } })]
    [InlineData(new object[] { new[] { "dash", "-c", "x" } })]
    [InlineData(new object[] { new[] { "gh", "pr", "ready" } })]
    public void OtherArray_IsNoEnvelope(string[] argv)
    {
        Assert.Null(CommandPosition.Envelope(argv));
    }

    [Theory]
    [InlineData(new[] { "/c", "gh", "pr" }, "gh pr")]
    [InlineData(new[] { "//c", "gh" }, "gh")]
    [InlineData(new[] { "/s", "/C", "a && b" }, "a && b")]
    [InlineData(new[] { "/cgh" }, "gh")]
    public void CmdString_IsTheWordsAfterItsSwitch(string[] rest, string script)
    {
        Assert.Equal(script, CommandPosition.CmdString(rest));
    }
}
