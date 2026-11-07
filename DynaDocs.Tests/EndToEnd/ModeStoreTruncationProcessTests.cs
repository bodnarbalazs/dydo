namespace DynaDocs.Tests.EndToEnd;

using System.Diagnostics;

/// <summary>
/// DYD-431 F-29 through the built CLI: a <c>dydo mode set</c> whose write fails after the store file
/// was truncated must leave the previous value in place. The child runs with HOME and XDG_DATA_HOME
/// inside this test's own scratch folder, so LocalApplicationData, and the store, resolve there.
/// The write is made to fail with a zero file-size limit (RLIMIT_FSIZE) and SIGXFSZ ignored, so the
/// open-and-truncate succeeds and the first write() returns EFBIG, the same shape as a full disk.
/// </summary>
[Collection("EndToEnd")]
public sealed class ModeStoreTruncationProcessTests(EndToEndFixture fixture) : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "dydo-mode-trunc-" + Guid.NewGuid().ToString("N")[..8]);
    private string StorePath => Path.Combine(_scratch, "xdg", "dydo", "mode.json");

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    private (int ExitCode, string Stdout, string Stderr) Run(bool failWrites, params string[] arguments)
    {
        Directory.CreateDirectory(Path.Combine(_scratch, "home"));
        Directory.CreateDirectory(Path.Combine(_scratch, "xdg"));
        var start = new ProcessStartInfo("/bin/sh")
        {
            WorkingDirectory = _scratch,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(failWrites
            ? "trap '' XFSZ; ulimit -c 0; ulimit -f 0; exec dotnet \"$0\" \"$@\""
            : "exec dotnet \"$0\" \"$@\"");
        start.ArgumentList.Add(fixture.DllPath);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["HOME"] = Path.Combine(_scratch, "home");
        start.Environment["XDG_DATA_HOME"] = Path.Combine(_scratch, "xdg");
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        start.Environment["DOTNET_EnableWriteXorExecute"] = "0"; // W^X double-maps through a sized memfd

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result, stderr.Result);
    }

    [SkippableFact]
    public void Set_FailingAfterTheStoreIsTruncated_KeepsThePreviousOnValue()
    {
        Skip.If(OperatingSystem.IsWindows(), "RLIMIT_FSIZE and SIGXFSZ are POSIX.");
        Assert.Equal(0, Run(false, "mode", "set", "overnight", "on").ExitCode);
        Assert.True(File.Exists(StorePath), $"the seeded store must sit in the redirected scratch: {StorePath}");
        var before = File.ReadAllText(StorePath);

        var failed = Run(true, "mode", "set", "overnight", "off");

        Assert.NotEqual(0, failed.ExitCode);
        Assert.Equal(before, File.ReadAllText(StorePath));
        Assert.Equal("overnight: on", Run(false, "mode", "show").Stdout.Trim());
    }
}
