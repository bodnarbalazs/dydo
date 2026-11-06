namespace DynaDocs.Tests.Services;

using System.Reflection;
using System.Text.RegularExpressions;
using DynaDocs.Services;

public class BashCommandAnalyzerScanTests
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    public static IEnumerable<object[]> LegacyRules()
    {
        yield return ["RecursiveDeleteRootRegex", @"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+(\./|/|~|/\*)(\s+--[a-z-]+)*\s*($|;|&&|\|\||&|\|)", false];
        yield return ["RecursiveDeleteGlobRegex", @"rm\s+(-[a-zA-Z]*[rfRF][a-zA-Z]*\s+)+\*\s*($|;|&&|\|\||&|\|)", false];
        yield return ["PowerShellRecursiveDeleteRegex", @"Remove-Item\b(?=.*-Recurse\b)(?=.*-Force\b).*(\s[/~]|\s[A-Z]:\\)\s*($|;|&&|\|\|)", true];
        yield return ["DdKnownDiskWriteRegex", @"dd\s+.*of\s*=\s*/dev/(?:sd[a-z]|nvme\d|vd[a-z]|mmcblk\d)", true];
        yield return ["Base64DecodePipeExecRegex", @"base64\s+(-d|--decode)[^|]*\|\s*(python[23]?|bash|sh|zsh|perl|ruby|node|pwsh|powershell)", true];
        yield return ["CurlPipeShRegex", @"curl\s+[^|]*\|\s*(ba)?sh", true];
        yield return ["WgetPipeShRegex", @"wget\s+[^|]*\|\s*(ba)?sh", true];
        yield return ["WgetOutputPipeShRegex", @"wget\s+-O\s*-?\s+[^|]*\|\s*(ba)?sh", true];
        yield return ["PowerShellIwrIexRegex", @"Invoke-WebRequest[^|]*\|\s*Invoke-Expression", true];
        yield return ["PowerShellIwrIexShortRegex", @"iwr\s+[^|]*\|\s*iex", true];
        yield return ["PowerShellDownloadStringRegex", @"DownloadString\s*\([^)]+\)[^|]*\|\s*(iex|Invoke-Expression)", true];
        yield return ["PowerShellHistoryDeleteRegex", @"Remove-Item.*ConsoleHost_history\.txt", true];
    }

    [Theory]
    [MemberData(nameof(LegacyRules))]
    public void LegacyRules_KeepMatchedLanguageAndTimeout(string name, string pattern, bool ignoreCase)
    {
        var options = ignoreCase ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.None;
        var original = new Regex(pattern, options, TimeSpan.FromMilliseconds(250));
        var changed = (Regex)typeof(BashCommandAnalyzer).GetField(name, PrivateStatic)!.GetValue(null)!;
        Assert.Equal(original.MatchTimeout, changed.MatchTimeout);
        foreach (var text in RuleInputs())
            Assert.True(original.IsMatch(text) == changed.IsMatch(text), $"{name}: {text}");
    }

    private static IEnumerable<string> RuleInputs()
    {
        string[] commands = ["rm -rf /", "rm -rf *", "Remove-Item -Recurse -Force /", "dd of=/dev/sda",
            "base64 -d x | python", "curl x | sh", "wget x | bash", "wget -O - x | sh",
            "Invoke-WebRequest x | Invoke-Expression", "iwr x | iex", "DownloadString(x) | iex",
            "Remove-Item ConsoleHost_history.txt"];
        foreach (var command in commands)
        foreach (var prefix in new[] { "", "x", "echo '", "ssh x ", "\n", "\r", ";" })
        foreach (var suffix in new[] { "", "x", "'", "\n", "\r", ";", " && x" })
        {
            yield return prefix + command + suffix;
            yield return prefix + command.ToUpperInvariant() + suffix;
        }
        foreach (var text in PowerShellDeleteInputs())
            yield return text;
        var random = new Random(7235);
        string[] pieces = ["rm ", "-rrf ", "*", "/", "~", "dd ", "of=", "/dev/sda", "curl ", "wget ",
            "-O ", "- ", "|", "bash", "sh", "base64 -d ", "iwr ", "iex", "Invoke-WebRequest",
            "Invoke-Expression", "DownloadString(", ")", "Remove-Item", "-Recurse", "-Force",
            "ConsoleHost_history.txt", " ", "\n", "\r", ";", "&&", "||", "x", "'", "C:\\"];
        for (var sample = 0; sample < 2_000; sample++)
            yield return string.Concat(Enumerable.Range(0, random.Next(1, 15)).Select(_ => pieces[random.Next(pieces.Length)]));
    }

    private static IEnumerable<string> PowerShellDeleteInputs()
    {
        string[][] orders = [["-Recurse", "-Force", "/;"], ["-Force", "-Recurse", "/;"],
            ["-Recurse", "/;", "-Force"], ["-Force", "/;", "-Recurse"],
            ["/;", "-Recurse", "-Force"], ["/;", "-Force", "-Recurse"]];
        foreach (var order in orders)
        foreach (var first in new[] { " ", "\n", "\r", "\r\n", " x ", ";" })
        foreach (var second in new[] { " ", "\n", "\r", "\r\n", " x ", ";" })
            yield return "Remove-Item " + order[0] + first + order[1] + second + order[2];
    }

    [Fact]
    public void CatHeredocStripping_KeepsOriginalMatchesAndReplacementExtents()
    {
        var type = typeof(BashCommandAnalyzer);
        var original = (Regex)type.GetMethod("CatHeredocRegex", PrivateStatic)!.Invoke(null, null)!;
        var strip = type.GetMethod("StripCatHeredocs", PrivateStatic)!;
        foreach (var text in CatInputs())
            Assert.Equal(original.Replace(text, "HEREDOC_STRIPPED"), strip.Invoke(null, [text]));
    }

    private static IEnumerable<string> CatInputs()
    {
        foreach (var delimiter in new[] { "E", "EEE", "_", "é", "a1" })
        foreach (var quote in new[] { "", "'", "\"", "'\"" })
        foreach (var blank in new[] { "", " ", "\n", "\r\n" })
        foreach (var closing in new[] { delimiter, "E", "missing" })
            yield return "x $(cat <<-" + quote + delimiter + new string(quote.Reverse().ToArray())
                + blank + "\nbody\n" + closing + blank + ") y $(cat <<E\n\nE)";
        var random = new Random(7236);
        string[] parts = ["$(cat <<E", "$(cat <<'E'", "$(cat <<EEE", "$(cat\n<<E", "\nE)",
            "\nEEE)", "\nE \n)", "\n", "\r", " ", "'", "\"", "cat", ")", "body", "\nE\n"];
        for (var sample = 0; sample < 3_000; sample++)
            yield return string.Concat(Enumerable.Range(0, random.Next(1, 20)).Select(_ => parts[random.Next(parts.Length)]));
    }
}
