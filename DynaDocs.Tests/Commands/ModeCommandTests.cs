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

    [SkippableTheory]
    [InlineData("file", "off", "on")]
    [InlineData("file", "on", "off")]
    [InlineData("folder", "off", "on")]
    [InlineData("folder", "on", "off")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Set_DeniedByTheStorePermissions_FailsWithOneErrorAndKeepsTheBytes(string denied, string initial, string value)
    {
        Skip.If(OperatingSystem.IsWindows(), "Unix permission bits; the locked-file scenarios cover Windows.");
        var file = Path.Combine(_root, "mode.json");
        await Run("set", "overnight", initial);
        var before = File.ReadAllText(file);
        var deniedPath = denied == "file" ? file : _root;
        var mode = File.GetUnixFileMode(deniedPath);
        File.SetUnixFileMode(deniedPath, denied == "file" ? UnixFileMode.UserRead : UnixFileMode.None);

        (int ExitCode, string Stdout, string Stderr) result;
        try
        {
            result = await Run("set", "overnight", value);
        }
        finally
        {
            File.SetUnixFileMode(deniedPath, mode);
        }

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("", result.Stdout);
        Assert.Equal($"Cannot write the mode store {file}: Access to the path '{file}' is denied.{Environment.NewLine}", result.Stderr);
        Assert.Equal(before, File.ReadAllText(file));
    }

    [Fact]
    public async Task Set_WithoutAValue_IsAParseError()
    {
        var (exitCode, _, _) = await Run("set", "overnight");

        Assert.NotEqual(0, exitCode);
        Assert.False(Directory.Exists(_root));
    }

    /// <summary>
    /// DYD-431 F-30: overnight-mode's morning brief reports the observed mode as "unknown if
    /// unreadable", so a store that exists but cannot be read or parsed must not show as a successful
    /// <c>overnight: off</c>. Runs against this test's own root, never the machine store.
    /// </summary>
    [SkippableTheory]
    [InlineData("")]
    [InlineData("{\n  \"overnight\": \"o")]
    [InlineData("{ not json")]
    [InlineData("<denied>")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Show_OverAnEmptyCorruptOrUnreadableStore_NeverReportsASuccessfulOff(string content)
    {
        Skip.If(content == "<denied>" && OperatingSystem.IsWindows(), "Unix permission bits.");
        var file = Path.Combine(_root, "mode.json");
        Assert.StartsWith(_root, new ModeState(_root).FilePath);
        Directory.CreateDirectory(_root);
        File.WriteAllText(file, content == "<denied>" ? """{"overnight":"on"}""" : content);
        if (content == "<denied>")
            File.SetUnixFileMode(file, UnixFileMode.None);

        var (exitCode, stdout, _) = await Run("show");

        Assert.False(exitCode == 0 && stdout.Trim() == "overnight: off",
            $"an unusable store read as a successful off (exit {exitCode}, stdout '{stdout.Trim()}')");
    }
}
