namespace DynaDocs.Tests.Map;

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
}
