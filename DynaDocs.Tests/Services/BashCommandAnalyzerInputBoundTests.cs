namespace DynaDocs.Tests.Services;

using System.Reflection;
using System.Text.RegularExpressions;
using DynaDocs.Services;

public class BashCommandAnalyzerInputBoundTests
{
    [Theory]
    [InlineData("Posix", 0, false)]
    [InlineData("Posix", 1_048_576, false)]
    [InlineData("Posix", 1_048_577, true)]
    [InlineData("PowerShell", 0, false)]
    [InlineData("PowerShell", 1_048_576, false)]
    [InlineData("PowerShell", 1_048_577, true)]
    [InlineData("Unknown", 0, false)]
    [InlineData("Unknown", 1_048_576, false)]
    [InlineData("Unknown", 1_048_577, true)]
    public void LengthBoundary_WhitespaceIsRefusedOnlyPastTheLimit(string dialect, int length, bool refused)
    {
        var analyzer = new BashCommandAnalyzer(Enum.Parse<ShellDialect>(dialect));
        var result = analyzer.Analyze(new string(' ', length));

        Assert.Equal(refused, result.HasDangerousPattern);
        Assert.Equal(refused
            ? "Command too complex to verify safely: command length exceeds limit of 1048576 characters"
            : null, result.DangerousPatternReason);
        Assert.Empty(result.Operations);
        Assert.False(result.HasBypassAttempt);
    }

    [Fact]
    public void PythonFallback_HasTheDangerousPatternTimeout()
    {
        var pattern = (Regex)typeof(BashCommandAnalyzer).GetMethod("PythonDashRegex",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        Assert.Equal(TimeSpan.FromMilliseconds(250), pattern.MatchTimeout);
        Assert.Matches(pattern, "python3 -u -");
        Assert.DoesNotMatch(pattern, "python3 -u script.py");
    }

    [Theory]
    [InlineData("Posix")]
    [InlineData("PowerShell")]
    [InlineData("Unknown")]
    public void LengthBoundary_BenignCommandIsAllowedAndExcessIsRefused(string dialect)
    {
        var analyzer = new BashCommandAnalyzer(Enum.Parse<ShellDialect>(dialect));
        var command = new string(' ', 1_048_576);
        Assert.False(analyzer.Analyze(command).HasDangerousPattern);
        foreach (var length in new[] { 1_048_577, 2_097_152 })
        {
            var input = "echo " + new string('x', length - 5);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = analyzer.Analyze(input);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 256L * 1024 * 1024);
            Assert.True(result.HasDangerousPattern);
            Assert.Contains("limit of 1048576 characters", result.DangerousPatternReason);
            Assert.Empty(result.Operations);
            Assert.False(result.HasBypassAttempt);
        }
    }

    [Theory]
    [InlineData("Write-Output @(Write-Output @(<# tail -f #> x))", false)]
    [InlineData("Write-Output @(<# outer #> Write-Output @(<# tail -f #> x))", false)]
    [InlineData("Write-Output @(Write-Output @(# tail -f\nx))", false)]
    [InlineData("Write-Output @(Write-Output @(rg 'tail -f' notes))", true)]
    [InlineData("Write-Output @(Write-Output @(@'\ntail -f\n'@))", true)]
    [InlineData("echo @(cat <<E\n$(rg -n 'tail -f' docs)\nE\n)", true)]
    [InlineData("echo @(cat <<E\r\n$(rg -n 'tail -f' docs)\r\nE\r\n)", true)]
    [InlineData("echo @(cat <<-E\n\t$(rg -n 'tail -f' docs)\n\tE\n)", true)]
    public void GroupNudgeClassification_PreservesEmittedWordOrigins(string command, bool counts)
    {
        foreach (var dialect in new[] { ShellDialect.PowerShell, ShellDialect.Unknown })
        {
            var classify = BashCommandAnalyzer.NudgeClassifier(command, dialect);
            Assert.Equal(counts, classify(command.IndexOf("tail", StringComparison.Ordinal)));
        }
    }

    [Theory]
    [InlineData("Posix")]
    [InlineData("PowerShell")]
    [InlineData("Unknown")]
    public void PythonFallback_UncertainBenignInputKeepsItsVerdict(string dialect)
    {
        var analyzer = new BashCommandAnalyzer(Enum.Parse<ShellDialect>(dialect));
        Assert.False(analyzer.Analyze("python --version '").HasDangerousPattern);
        Assert.False(analyzer.Analyze("python script.py '").HasDangerousPattern);
    }
}
