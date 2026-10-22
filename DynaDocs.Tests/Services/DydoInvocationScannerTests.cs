namespace DynaDocs.Tests.Services;

using System.Text;
using DynaDocs.Services;

public class DydoInvocationScannerTests
{
    private static List<string> All(string command) =>
        DydoInvocationScanner.Find(command).Invocations.Select(words => string.Join(' ', words)).ToList();

    [Theory]
    [InlineData("dydo check", "check")]
    [InlineData("/usr/local/bin/dydo check", "check")]
    [InlineData(@".\dydo.ps1 check", "check")]
    [InlineData("DYDO_HOME=/x env -u PATH -i dydo check", "check")]
    [InlineData("timeout -s KILL 30 dydo check", "check")]
    [InlineData("timeout -- 30 dydo check", "check")]
    [InlineData("time -p nice -n 5 dydo check", "check")]
    [InlineData("if true; then dydo check; fi", "check")]
    [InlineData("{ dydo check; }", "check")]
    [InlineData("(cd x && dydo check)", "check")]
    [InlineData("echo `dydo check`", "check")]
    [InlineData("echo \"`dydo check`\"", "check")]
    [InlineData("echo \"${HOME}$(dydo check)\"", "check")]
    [InlineData("dydo check 2>&1 >/dev/null", "check")]
    [InlineData("dydo check &> out.txt", "check")]
    [InlineData("dydo check < in.txt", "check")]
    [InlineData("dydo `\ncheck", "check")]
    [InlineData("dydo ^\r\ncheck", "check")]
    [InlineData("dotnet run --project x -- check", "check")]
    [InlineData("dotnet --roll-forward Major dydo.dll check", "check")]
    [InlineData("eval 'dydo check'", "check")]
    [InlineData("iex -Command 'dydo check'", "check")]
    [InlineData("bash <<< 'dydo check'", "check")]
    [InlineData("bash <<'EOF'\ndydo check\nEOF", "check")]
    [InlineData("bash -o pipefail --norc -c 'dydo check'", "check")]
    [InlineData("pwsh -NoLogo -ExecutionPolicy Bypass -c dydo check", "check")]
    [InlineData("powershell dydo check", "check")]
    [InlineData("start \"\" /b dydo check", "check")]
    [InlineData("Start-Process -Wait -WindowStyle Hidden dydo 'check', '--strict'", "check --strict")]
    [InlineData("Start-Process -args check -FilePath dydo", "check")]
    [InlineData("@\"\n$(dydo check)\n\"@ | Out-Null", "check")]
    [InlineData("cat <<EOF\n$(dydo check)\nEOF", "check")]
    [InlineData("cat <<-EOF\n\tdydo check\n\tEOF\ndydo fix", "fix")]
    [InlineData("echo hi # ; dydo check\ndydo fix", "fix")]
    [InlineData("echo 'dydo check'", null)]
    [InlineData("echo \"dydo check\" | grep dydo", null)]
    [InlineData("cmd /c echo dydo check", null)]
    [InlineData("bash script.sh dydo check", null)]
    [InlineData("pwsh -File x.ps1 dydo check", null)]
    [InlineData("dotnet build dydo.csproj", null)]
    [InlineData("dotnet run check", null)]
    [InlineData("Start-Process notepad dydo", null)]
    [InlineData("cat <<'EOF'\n$(dydo check)\nEOF", null)]
    public void FindsTheInvocationsAtCommandPosition(string command, string? expected)
    {
        if (expected == null)
            Assert.Empty(All(command));
        else
            Assert.Contains(expected, All(command));
    }

    [Theory]
    [InlineData("PowerShell", "Start-Process dydo -ArgumentList @('check', '--strict') -Wait", "check --strict")]
    [InlineData("PowerShell", "Start-Process dydo -ArgumentList ('check') -NoNewWindow", "check")]
    [InlineData("PowerShell", "if ($x) { dydo check }", "check")]
    [InlineData("PowerShell", "$n = @(dydo check)", "check")]
    [InlineData("PowerShell", "echo \"a `\"b`\"; dydo check\"", null)]
    [InlineData("PowerShell", "bash -c 'echo \"a\\\\\"; dydo check'", "check")]
    [InlineData("Posix", "echo \"a\\\\\" \"; dydo check\"", null)]
    [InlineData("Posix", "echo \"a `echo b`\"; dydo check", "check")]
    [InlineData("Posix", "pwsh -c 'echo \"a `\"; dydo check\"'", null)]
    public void ReadsWithTheGivenShell(string shell, string command, string? expected)
    {
        var found = DydoInvocationScanner.Find(command, Enum.Parse<ShellDialect>(shell)).Invocations
            .Select(words => string.Join(' ', words)).ToList();

        if (expected == null)
            Assert.Empty(found);
        else
            Assert.Contains(expected, found);
    }

    [Fact]
    public void EncodedPowerShellCommand_IsDecoded()
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes("dydo check"));

        Assert.Contains("check", All($"pwsh -NoProfile -EncodedCommand {encoded}"));
    }

    [Theory]
    [InlineData("echo \"unbalanced")]
    [InlineData("echo 'unbalanced")]
    [InlineData("echo `unbalanced")]
    [InlineData("echo \"$(dydo check\"")]
    [InlineData("echo ${unbalanced")]
    [InlineData("@'\nno terminator")]
    [InlineData("cat << ; echo")]
    [InlineData("echo dydo check | bash")]
    [InlineData("Get-Content x.ps1 | pwsh -NoProfile")]
    [InlineData("cmd")]
    [InlineData("pwsh -EncodedCommand not-base64!")]
    public void UnreadableOrPipedCommands_AreUncertain(string command)
    {
        Assert.True(DydoInvocationScanner.Find(command).Uncertain);
    }

    [Fact]
    public void PlainCommand_IsCertain()
    {
        Assert.False(DydoInvocationScanner.Find("git commit -m \"a; b\" && dydo check").Uncertain);
    }

    [Fact]
    public void NestingPastTheLimit_IsUncertain()
    {
        var command = "dydo check";
        for (var i = 0; i < 10; i++)
            command = $"eval {command}";

        Assert.True(DydoInvocationScanner.Find(command).Uncertain);
    }
}
