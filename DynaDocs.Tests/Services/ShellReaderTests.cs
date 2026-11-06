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

    // DYD-723: a PowerShell group word took every command read inside it, a nested group's commands
    // too, so their words came again in each enclosing group and the word text doubled with each
    // level. A nested group's commands reach it once, through the nested group's own word. Any other
    // command read inside the group, such as a quoted substitution's, is still taken.
    [Theory]
    [InlineData("Write-Output @(b @(c))", new[] { "c", "b c", "Write-Output b c" })]
    [InlineData("Write-Output (b (c))", new[] { "c", "b c", "Write-Output b c" })]
    [InlineData("echo $(b $(c))", new[] { "c", "b $c", "echo $b $c" })]
    [InlineData("Start-Process dydo -ArgumentList @('mode', 'set') -Wait", new[] { "mode, set", "Start-Process dydo -ArgumentList mode, set -Wait" })]
    [InlineData("Write-Output @(\"a $(b)\" c)", new[] { "b", "a  c", "Write-Output b a  c" })]
    public void GroupWord_HoldsEachCommandReadInsideItOnce(string text, string[] commands)
    {
        Assert.False(ShellReader.Read(text, posix: false).Failed);
        Assert.Equal(commands, Commands(text));
    }

    [Theory]
    [InlineData("$(", "")]
    [InlineData("$(", ")")]
    [InlineData("@(", "")]
    [InlineData("@(", ")")]
    [InlineData("a (", "")]
    [InlineData("a (", ")")]
    public void NestedGroups_HoldTheTextAtMostOncePerLevel(string opener, string closer)
    {
        const int levels = 20;
        var text = "Write-Output x " + Repeat(opener, levels) + "y" + Repeat(closer, levels);

        var words = ShellReader.Read(text, posix: false).Commands.Sum(command => command.Words.Sum(word => word.Length));

        Assert.InRange(words, 1, (levels + 1) * text.Length);
    }

    // Every character of a word maps back to the character it was read from, through nested group
    // words and here-strings too; only the spaces a group word joins its words with map nowhere.
    [Theory]
    [InlineData("Write-Output @(b @(c)) d")]
    [InlineData("echo $(b $(c))e")]
    [InlineData("Write-Output (b (\"c $(d)\"))")]
    [InlineData("echo @(@\"\nx $(y)\n\"@ z)")]
    public void GroupWordOrigins_MapEachCharacterToItsSource(string text)
    {
        foreach (var command in ShellReader.Read(text, posix: false).Commands)
        {
            for (var w = 0; w < command.Words.Count; w++)
            {
                for (var i = 0; i < command.Words[w].Length; i++)
                {
                    var origin = command.WordOrigins[w].At(i);
                    Assert.Equal(command.Words[w][i], origin is { } at ? at.Source.Text[at.Offset] : ' ');
                }
            }
        }
    }

    [Fact]
    public void GroupWordOrigins_MapARangeThroughTheNestedWord()
    {
        var reading = ShellReader.Read("cat <<< @(ab)", posix: false);

        var body = reading.Commands.SelectMany(command => command.Bodies).Single();

        Assert.Equal("ab", body.Text);
        Assert.Equal<Range?>(10..12, body.Origins.RangeIn(reading.Source.Children.Single(), 0..2));
    }

    // DYD-723: each level of nesting holds a reader on the stack, and a group word the text of every
    // level inside it, so a reading nested past 32 levels (ShellReader.MaxNesting) fails. Sixteen
    // levels and the bound itself still read.
    [Theory]
    [InlineData(true, 16, false)]
    [InlineData(true, 32, false)]
    [InlineData(true, 33, true)]
    [InlineData(false, 16, false)]
    [InlineData(false, 32, false)]
    [InlineData(false, 33, true)]
    public void QuotedSubstitutionsPastTheBound_FailTheReading(bool posix, int levels, bool fails)
    {
        var text = "echo " + Repeat("\"$(", levels) + "x" + Repeat(")\"", levels);

        Assert.Equal(fails, ShellReader.Read(text, posix).Failed);
    }

    // Before DYD-723, 33 levels of groups made 2^33 copies of the innermost word: run on the fix only.
    [Theory]
    [InlineData("$(", 32, false)]
    [InlineData("$(", 33, true)]
    [InlineData("@(", 32, false)]
    [InlineData("@(", 33, true)]
    [InlineData("a (", 32, false)]
    [InlineData("a (", 33, true)]
    public void GroupsPastTheBound_FailTheReading(string opener, int levels, bool fails)
    {
        var text = "Write-Output " + Repeat(opener, levels) + "y" + Repeat(")", levels);

        Assert.Equal(fails, ShellReader.Read(text, posix: false).Failed);
    }

    // A ( that opens a group nests as a substitution does, so bash's unquoted $( and a group at a
    // command's start fail past the bound too, and read at it (DYD-723).
    [Theory]
    [InlineData(true, "echo ", "$(", 32, false)]
    [InlineData(true, "echo ", "$(", 33, true)]
    [InlineData(true, "", "(", 32, false)]
    [InlineData(true, "", "(", 33, true)]
    [InlineData(false, "", "(", 32, false)]
    [InlineData(false, "", "(", 33, true)]
    public void GroupsPastTheBound_ReadTooDeep(bool posix, string head, string opener, int levels, bool tooDeep)
    {
        var reading = ShellReader.Read(head + Repeat(opener, levels) + "x" + Repeat(")", levels), posix);

        Assert.Equal(tooDeep, reading.TooDeep);
        Assert.Equal(tooDeep, reading.Failed);
    }

    // pwsh reads '' in a single-quoted string as one quote and goes on. The reading recursed once per
    // pair, so 10,000 pairs overflowed a 1 MB stack, the size of a Windows main thread's (DYD-723).
    [Fact]
    public void DoubledSingleQuotes_ReadOnAOneMegabyteStack()
    {
        var text = "echo '" + Repeat("''", 50_000) + "'";
        ShellReader.Reading? reading = null;

        var thread = new Thread(() => reading = ShellReader.Read(text, posix: false), 1 << 20);
        thread.Start();
        thread.Join();

        Assert.False(reading!.Failed);
        Assert.Equal(["echo", new string('\'', 50_000)], reading.Commands.Single().Words);
    }

    private static string Repeat(string unit, int count) => string.Concat(Enumerable.Repeat(unit, count));
}
