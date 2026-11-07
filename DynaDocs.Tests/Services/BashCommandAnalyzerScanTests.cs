namespace DynaDocs.Tests.Services;

using System.Reflection;
using System.Text.RegularExpressions;
using DynaDocs.Services;

public class BashCommandAnalyzerScanTests
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [Fact]
    public void QuotedText_KeepsMatchesCapturesAndReplacementExtents()
    {
        var original = new Regex(@"""(?:[^""\\]|\\.)*""|(?<single>'[^']*')");
        var changed = (Regex)typeof(BashCommandAnalyzer).GetField("QuotedTextRegex", PrivateStatic)!.GetValue(null)!;
        foreach (var text in CatInputs().Concat(RuleInputs()))
        {
            Assert.Equal(original.Matches(text).Select(match => (match.Index, match.Length, match.Value, match.Groups["single"].Success)),
                changed.Matches(text).Select(match => (match.Index, match.Length, match.Value, match.Groups["single"].Success)));
            Assert.Equal(original.Replace(text, "x"), changed.Replace(text, "x"));
        }
    }

    [Fact]
    public void SharedNudgeClassification_PreservesSinglePositionVerdicts()
    {
        var inputs = typeof(Commands.GuardCommandTests).GetMethods()
            .SelectMany(method => method.GetCustomAttributesData())
            .Where(attribute => attribute.AttributeType == typeof(InlineDataAttribute))
            .SelectMany(attribute => (IEnumerable<CustomAttributeTypedArgument>)attribute.ConstructorArguments[0].Value!)
            .Select(argument => argument.Value).OfType<string>()
            .Where(text => text.Contains("tail", StringComparison.Ordinal)).Distinct().Order(StringComparer.Ordinal);
        var proof = new System.Text.StringBuilder();
        foreach (var text in inputs)
        foreach (var dialect in Enum.GetValues<ShellDialect>())
        {
            var counts = (Func<int, bool>)typeof(BashCommandAnalyzer).GetMethod("NudgeClassifier", PrivateStatic)!
                .Invoke(null, [text, dialect])!;
            foreach (Match match in Regex.Matches(text, "tail"))
                RecordNudge(proof, dialect, text, match.Index, counts(match.Index));
        }
        AssertNudgeProof(proof, "48b5b7b10b792a6b5fa932832a9341146ef35924c9c2465b97fac53cf0ff364c");
    }

    [Fact]
    public void SharedNudgeClassification_PreservesNestedAndRepeatedContexts()
    {
        string[] bodies = ["rg -n 'tail -f' docs", "git grep -n -E 'tail -f' -- docs",
            "# tail -f\necho x", "cat <<E\ntail -f\nE\n", "echo @'\ntail -f\n'@", "tail -f"];
        string[] formats = ["{0}", "{0}; {0}", "echo @({0})", "Write-Output a ({0})", "echo \"$({0})\"",
            "echo $({0})", "bash -c '{0}'", "eval '{0}'", "cat <<E\n$({0})\nE\n", "cat <<E | bash\n$({0})\nE\n"];
        var proof = new System.Text.StringBuilder();
        foreach (var body in bodies)
        foreach (var format in formats)
        foreach (var dialect in Enum.GetValues<ShellDialect>())
        {
            var text = format.Replace("{0}", body, StringComparison.Ordinal);
            var counts = (Func<int, bool>)typeof(BashCommandAnalyzer).GetMethod("NudgeClassifier", PrivateStatic)!
                .Invoke(null, [text, dialect])!;
            foreach (Match match in Regex.Matches(text, "tail"))
                RecordNudge(proof, dialect, text, match.Index, counts(match.Index));
        }
        AssertNudgeProof(proof, "6f7ba8da3aa6866eff2a54934b1b4c9a2db0b53071e32d16654c791fc995bcc8");
    }

    // Full verdict streams from b2b53a6c (unchanged at integration aa07f270): 579 fixture
    // positions and 198 nested positions. The independent probe retains each input and decision.
    private static void RecordNudge(System.Text.StringBuilder proof, ShellDialect dialect, string text, int index, bool counts) =>
        proof.Append(dialect).Append('\0').Append(text).Append('\0').Append(index).Append('\0').Append(counts).Append('\n');

    private static void AssertNudgeProof(System.Text.StringBuilder proof, string expected) =>
        Assert.Equal(expected, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(proof.ToString()))));

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
