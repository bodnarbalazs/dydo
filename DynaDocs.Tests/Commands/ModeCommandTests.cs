namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

public sealed class ModeCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-mode-cmd-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> Run(params string[] args) =>
        await ConsoleCapture.AllAsync(() => ModeCommand.Create(new ModeState(_root)).Parse(args).InvokeAsync());

    [Fact]
    public async Task Set_PrintsEveryFlagAfterTheChange()
    {
        var (exitCode, stdout, stderr) = await Run("set", "overnight", "on");

        Assert.Equal(0, exitCode);
        Assert.Equal("overnight: on", stdout.Trim());
        Assert.Equal("", stderr);
    }

    [Theory]
    [InlineData("Overnight", "on", "Unknown mode flag 'Overnight'. Flags: overnight.")]
    [InlineData("overnight", "ON", "Invalid value 'ON' for overnight. Values: off, on.")]
    public async Task Set_IsCaseSensitive_AndRefusalsWriteNothing(string flag, string value, string error)
    {
        var (exitCode, stdout, stderr) = await Run("set", flag, value);

        Assert.Equal(2, exitCode);
        Assert.Equal("", stdout);
        Assert.Equal(error, stderr.Trim());
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task Set_WithoutAValue_IsAParseError()
    {
        var (exitCode, _, _) = await Run("set", "overnight");

        Assert.NotEqual(0, exitCode);
        Assert.False(Directory.Exists(_root));
    }
}
