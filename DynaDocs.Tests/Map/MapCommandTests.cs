namespace DynaDocs.Tests.Map;

using DynaDocs.Commands;
using DynaDocs.Utils;

public class MapCommandTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingKey_ExplainsHowToCreateOne_AndExits2BeforeAnyServer(string? key)
    {
        var opened = new List<Uri>();

        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsync(
            () => MapCommand.RunAsync(key, "http://unused.test/graphql", opened.Add, CancellationToken.None));

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("LINEAR_API_KEY is not set", stderr);
        Assert.Contains("Personal API keys", stderr);
        Assert.Empty(stdout);
        Assert.Empty(opened);
    }

    [Fact]
    public async Task MissingKey_ThroughTheCommandLine_Exits2()
    {
        var saved = Environment.GetEnvironmentVariable("LINEAR_API_KEY");
        Environment.SetEnvironmentVariable("LINEAR_API_KEY", null);
        try
        {
            var (exitCode, _, stderr) = await ConsoleCapture.AllAsync(
                () => MapCommand.Create().Parse(["--no-browser"]).InvokeAsync());

            Assert.Equal(ExitCodes.ToolError, exitCode);
            Assert.Contains("LINEAR_API_KEY is not set", stderr);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LINEAR_API_KEY", saved);
        }
    }

    [Fact]
    public async Task WithAKey_PrintsTheLocalhostUrl_OpensIt_AndStopsOnCancel()
    {
        var opened = new List<Uri>();
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        var (exitCode, stdout, _) = await ConsoleCapture.AllAsync(
            () => MapCommand.RunAsync(FakeLinear.ApiKey, null, opened.Add, stopped.Token));

        Assert.Equal(ExitCodes.Success, exitCode);
        var url = Assert.Single(opened);
        Assert.Equal("localhost", url.Host);
        Assert.Contains($"serving {url}", stdout);
        Assert.DoesNotContain(FakeLinear.ApiKey, stdout);
    }

    [Fact]
    public async Task NoBrowser_OnlyPrintsTheUrl()
    {
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        var (exitCode, stdout, _) = await ConsoleCapture.AllAsync(
            () => MapCommand.RunAsync(FakeLinear.ApiKey, null, null, stopped.Token));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("serving http://localhost:", stdout);
    }

    [Fact]
    public void Command_OffersNoBrowser()
    {
        var command = MapCommand.Create();

        Assert.Equal("map", command.Name);
        Assert.Contains(command.Options, option => option.Name == "--no-browser");
        Assert.Contains("LINEAR_API_KEY", command.Description);
    }

    [Fact]
    public void Help_SaysWhenTheViewerWasNotBuilt()
    {
        Assert.Contains("viewer not built", MapCommand.Description(viewerBuilt: false));
        Assert.DoesNotContain("not built", MapCommand.Description(viewerBuilt: true));
    }
}
