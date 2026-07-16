namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

[Collection("ProcessUtils")]
public class ProcessUtilsTests
{
    [Fact]
    public void IsProcessRunning_ReturnsTrue_ForCurrentProcess()
    {
        var result = ProcessUtils.IsProcessRunning(Environment.ProcessId);

        Assert.True(result);
    }

    [Fact]
    public void IsProcessRunning_ReturnsFalse_ForInvalidPid()
    {
        Assert.False(ProcessUtils.IsProcessRunning(-1));
        Assert.False(ProcessUtils.IsProcessRunning(0));
    }

    [Fact]
    public void GetProcessName_ReturnsName_ForCurrentProcess()
    {
        var name = ProcessUtils.GetProcessName(Environment.ProcessId);

        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    [Fact]
    public void GetProcessName_ReturnsNull_ForInvalidPid()
    {
        var result = ProcessUtils.GetProcessName(-1);

        Assert.Null(result);
    }

    [Fact]
    public void ResolvePowerShell_PwshAvailable_ReturnsPwsh()
    {
        ProcessUtils.PowerShellResolverOverride = () => "pwsh.exe";
        try
        {
            Assert.Equal("pwsh.exe", ProcessUtils.ResolvePowerShell());
        }
        finally
        {
            ProcessUtils.PowerShellResolverOverride = null;
        }
    }

    [Fact]
    public void ResolvePowerShell_PwshNotAvailable_ReturnsPowershell()
    {
        ProcessUtils.PowerShellResolverOverride = () => "powershell.exe";
        try
        {
            Assert.Equal("powershell.exe", ProcessUtils.ResolvePowerShell());
        }
        finally
        {
            ProcessUtils.PowerShellResolverOverride = null;
        }
    }

    [Fact]
    public void ResolvePowerShell_WithoutOverride_ReturnsPwshOrPowershell()
    {
        ProcessUtils.PowerShellResolverOverride = null;

        var result = ProcessUtils.ResolvePowerShell();

        Assert.True(result == "pwsh.exe" || result == "powershell.exe",
            $"Expected 'pwsh.exe' or 'powershell.exe' but got '{result}'");
    }

    [Fact]
    public void GetProcessName_ReturnsNull_ForNonexistentPid()
    {
        // PID that's very unlikely to exist — exercises the catch branch
        var result = ProcessUtils.GetProcessName(int.MaxValue);

        Assert.Null(result);
    }

    [Fact]
    public void IsProcessRunning_ReturnsFalse_ForNonexistentPid()
    {
        Assert.False(ProcessUtils.IsProcessRunning(int.MaxValue));
    }


    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("it's", "it''s")]
    [InlineData("say \"hi\"", "say \"hi\"")]
    [InlineData("$var", "$var")]
    [InlineData("back`tick", "back`tick")]
    [InlineData("a'b\"c$d`e", "a''b\"c$d`e")]
    public void EscapeForPowerShellLike_EscapesDangerousCharacters(string input, string expected)
    {
        Assert.Equal(expected, ProcessUtils.EscapeForPowerShellLike(input));
    }







    // --- ParseNewlineSeparatedPids ---






    // --- ParsePsEoPidArgs ---








    // --- FindByPowerShell (integration, Windows only) ---




    // --- FindByWmic (integration, Windows only) ---



    // --- Platform-specific methods on Windows (error path coverage) ---



    // --- RunProcess ---

    [Fact]
    public void RunProcess_ValidCommand_ReturnsOutput()
    {
        var output = OperatingSystem.IsWindows()
            ? ProcessUtils.RunProcess("cmd", "/c echo hello")
            : ProcessUtils.RunProcess("echo", "hello");

        Assert.NotNull(output);
        Assert.Contains("hello", output);
    }

    [Fact]
    public void RunProcess_NonExistentCommand_ReturnsNull()
    {
        var output = ProcessUtils.RunProcess("nonexistent-command-xyz", "");

        Assert.Null(output);
    }
}
