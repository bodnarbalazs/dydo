namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>
/// DYD-641: the non-posix reading against pwsh 7.6.9, as measured in /tmp/dyd641-evidence. A bare
/// carriage return ends a statement, a comment, a here-string header and a here-string body; the
/// typographic quotes are PowerShell's quote characters; a caret is literal to pwsh and an escape
/// only to cmd. The posix reading is bash's, which keeps all three as word text.
/// </summary>
public class ShellReaderTests
{
    private static List<string> Commands(string text, bool posix = false) =>
        [.. ShellReader.Read(text, posix).Commands.Select(command => string.Join(' ', command.Words))];

    [Theory]
    [InlineData("echo 'x'\riex 'y'", new[] { "echo x", "iex y" })]
    [InlineData("echo x # note\rrm y", new[] { "echo x", "rm y" })]
    [InlineData("echo @'\rabc\r'@\rrm y", new[] { "echo abc", "rm y" })]
    [InlineData("echo @'  \r\rabc\r'@\rrm y", new[] { "echo \rabc", "rm y" })]
    [InlineData("echo @'\nabc\r'@; rm y\n", new[] { "echo abc", "rm y" })]
    [InlineData("echo @'\rabc\r '@\rx\r'@\rrm y", new[] { "echo abc\r '@\rx", "rm y" })]
    [InlineData("echo a `\rb", new[] { "echo a b" })]
    [InlineData("echo a`\rb", new[] { "echo a\rb" })]
    [InlineData("echo a\r\nrm y", new[] { "echo a", "rm y" })]
    [InlineData("echo 'a\rrm y'", new[] { "echo a\rrm y" })]
    [InlineData("echo \"a\rrm y\"", new[] { "echo a\rrm y" })]
    public void BareCarriageReturn_EndsWhatALineFeedEnds(string text, string[] commands)
    {
        var reading = ShellReader.Read(text, posix: false);

        Assert.False(reading.Failed);
        Assert.Equal(commands, Commands(text));
    }

    [Theory]
    [InlineData("echo 'x'\riex 'y'", new[] { "echo x iex y" })]
    [InlineData("echo x # note\rrm y", new[] { "echo x" })]
    [InlineData("echo ‘a'; rm y; ’b'", new[] { "echo ‘a; rm y; ’b" })]
    [InlineData("echo a^;rm y", new[] { "echo a^", "rm y" })]
    public void PosixReading_KeepsAllThreeAsWordText(string text, string[] commands)
    {
        Assert.False(ShellReader.Read(text, posix: true).Failed);
        Assert.Equal(commands, Commands(text, posix: true));
    }

    [Theory]
    [InlineData("echo 'a’; rm y", new[] { "echo a", "rm y" })]
    [InlineData("echo 'a‘; rm y", new[] { "echo a", "rm y" })]
    [InlineData("echo 'a‚; rm y", new[] { "echo a", "rm y" })]
    [InlineData("echo 'a‛; rm y", new[] { "echo a", "rm y" })]
    [InlineData("echo \"a”; rm y", new[] { "echo a", "rm y" })]
    [InlineData("echo \"a“; rm y", new[] { "echo a", "rm y" })]
    [InlineData("echo \"a„; rm y", new[] { "echo a", "rm y" })]
    [InlineData("echo ‘a'; rm y; ’b'", new[] { "echo a", "rm y", "b" })]
    [InlineData("echo 'a’’b' ‘c''d’ 'e'‛f'", new[] { "echo a’b c'd e‛f" })]
    [InlineData("echo \"a””b\" “c\"\"d” „e““f\"", new[] { "echo a”b c\"d e“f" })]
    [InlineData("echo \"a’b\" 'c”d' “e'f” ‘g\"h’", new[] { "echo a’b c”d e'f g\"h" })]
    [InlineData("echo \"a`”b\"", new[] { "echo a”b" })]
    [InlineData("echo a‘b c’d", new[] { "echo ab cd" })]
    [InlineData("echo @‘\nabc\n’@; rm y", new[] { "echo abc", "rm y" })]
    [InlineData("echo @'\nabc\n‘@; rm y\n", new[] { "echo abc", "rm y" })]
    [InlineData("echo @‚\nabc\n‛@; echo @„\nxyz\n\"@; rm y", new[] { "echo abc", "echo xyz", "rm y" })]
    public void TypographicQuotes_ArePowerShellQuotes(string text, string[] commands)
    {
        Assert.False(ShellReader.Read(text, posix: false).Failed);
        Assert.Equal(commands, Commands(text));
    }

    [Theory]
    [InlineData("echo @“\n$(rm y)\n”@")]
    [InlineData("echo @\"\n$(rm y)\n“@; echo z")]
    public void TypographicExpandingHereString_ReadsItsSubstitution(string text)
    {
        Assert.Contains("rm y", Commands(text));
    }

    // pwsh reads a caret as a literal character; cmd reads it as an escape. Where the two would
    // split, quote or substitute differently, the reading holds both and fails.
    [Theory]
    [InlineData("echo a^;rm y", "rm y")]
    [InlineData("echo a^\nrm y", "rm y")]
    [InlineData("echo a^\r\nrm y", "rm y")]
    [InlineData("echo a^ b", "echo a^ b")]
    [InlineData("echo ^$(rm y)", "rm y")]
    [InlineData("echo ^'a b'", "echo ^a b")]
    [InlineData("echo $(echo a^)", "echo a^")]
    [InlineData("dydo ^\r\ncheck", "dydo check")]
    public void CaretPwshAndCmdReadDifferently_FailsWithBothReadings(string text, string pwshOrCmdCommand)
    {
        var reading = ShellReader.Read(text, posix: false);

        Assert.True(reading.Failed);
        Assert.Contains(pwshOrCmdCommand, Commands(text));
    }

    [Theory]
    [InlineData("echo a^b x^^y HEAD^2 HEAD^", "echo ab x^y HEAD2 HEAD")]
    [InlineData("echo \"a^;b\"", "echo a^;b")]
    [InlineData("echo 'a^;b'", "echo a^;b")]
    public void CaretBothReadAlike_IsCertain(string text, string command)
    {
        Assert.False(ShellReader.Read(text, posix: false).Failed);
        Assert.Equal([command], Commands(text));
    }
}
