namespace DynaDocs.Tests.Map;

using System.ComponentModel;
using System.Diagnostics;
using DynaDocs.Services.Map;

public class BrowserLauncherTests
{
    private static readonly Uri Url = new("http://localhost:4321/");

    [Fact]
    public void Windows_ShellExecutesTheUrl()
    {
        var startInfo = BrowserLauncher.StartInfo(Url, windows: true, macOS: false);

        Assert.Equal("http://localhost:4321/", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }

    [Theory]
    [InlineData(true, "open")]
    [InlineData(false, "xdg-open")]
    public void Unix_RunsTheOpener(bool macOS, string opener)
    {
        var startInfo = BrowserLauncher.StartInfo(Url, windows: false, macOS);

        Assert.Equal(opener, startInfo.FileName);
        Assert.Equal(["http://localhost:4321/"], startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void Open_StartsThisOsOpenerAndDisposesTheProcess()
    {
        var expected = BrowserLauncher.StartInfo(Url, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());
        ProcessStartInfo? started = null;
        using var process = new DisposeProbe();

        var stderr = ConsoleCapture.Stderr(() => BrowserLauncher.Open(Url, startInfo =>
        {
            started = startInfo;
            return process;
        }));

        Assert.NotNull(started);
        Assert.Equal(expected.FileName, started.FileName);
        Assert.Equal(expected.ArgumentList, started.ArgumentList);
        Assert.Equal(expected.UseShellExecute, started.UseShellExecute);
        Assert.True(process.IsDisposed);
        Assert.Empty(stderr);
    }

    [Fact]
    public void Open_ToleratesAStartThatReturnsNoProcess()
    {
        var stderr = ConsoleCapture.Stderr(() => BrowserLauncher.Open(Url, _ => null));

        Assert.Empty(stderr);
    }

    [Fact]
    public void Open_WithNoOpener_TellsTheUserToOpenTheUrl()
    {
        var stderr = ConsoleCapture.Stderr(() =>
            BrowserLauncher.Open(Url, _ => throw new Win32Exception(2, "No such file or directory")));

        Assert.Equal("Could not open a browser; open the URL above yourself." + Environment.NewLine, stderr);
    }

    private sealed class DisposeProbe : Process
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
