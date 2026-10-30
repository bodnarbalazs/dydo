namespace DynaDocs.Tests.Merge;

using System.Text.Json.Nodes;
using DynaDocs.Services.Merge;

/// <summary>
/// The gh adapter: the argv it runs (no shell, an explicit host, the caller's path only) and how it
/// reads gh's <c>--include</c> output. Nothing here reaches the network.
/// </summary>
[Collection("Integration")]
public sealed class GhApiTests
{
    [Fact]
    public void AReadNamesTheHostMethodAndPath()
    {
        var start = GhApi.StartInfo("gh", "ghe.example.com", "GET", "repos/acme/widgets/pulls/7", hasBody: false);

        Assert.Equal("gh", start.FileName);
        Assert.False(start.UseShellExecute);
        Assert.Equal(["api", "repos/acme/widgets/pulls/7", "--hostname", "ghe.example.com", "--method", "GET", "--include"], start.ArgumentList);
    }

    [Fact]
    public void AWriteSendsItsBodyOnStandardInput()
    {
        var start = GhApi.StartInfo("gh", "github.com", "POST", "repos/acme/widgets/merges", hasBody: true);

        Assert.Equal(["api", "repos/acme/widgets/merges", "--hostname", "github.com", "--method", "POST", "--include", "--input", "-"],
            start.ArgumentList);
        Assert.True(start.RedirectStandardInput);
    }

    [Fact]
    public void GhRepoAndGhHost_AreDroppedFromGhsEnvironment()
    {
        var saved = (Environment.GetEnvironmentVariable("GH_REPO"), Environment.GetEnvironmentVariable("GH_HOST"));
        try
        {
            Environment.SetEnvironmentVariable("GH_REPO", "mallory/evil");
            Environment.SetEnvironmentVariable("GH_HOST", "evil.example");

            var start = GhApi.StartInfo("gh", "github.com", "GET", "repos/acme/widgets", hasBody: false);

            Assert.False(start.Environment.ContainsKey("GH_REPO"));
            Assert.False(start.Environment.ContainsKey("GH_HOST"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_REPO", saved.Item1);
            Environment.SetEnvironmentVariable("GH_HOST", saved.Item2);
        }
    }

    [Fact]
    public void ACreatedAnswer_HasItsStatusAndBody()
    {
        var reply = GhApi.Parse("HTTP/2.0 201 Created\nContent-Type: application/json\r\nX-GitHub-Request-Id: 1\r\n\r\n{\"sha\":\"abc\"}", "");

        Assert.Equal(201, reply.Status);
        Assert.Equal("abc", reply.Json!["sha"]!.GetValue<string>());
    }

    [Fact]
    public void ANoContentAnswer_HasNoBody()
    {
        var reply = GhApi.Parse("HTTP/2.0 204 No Content\r\nX-GitHub-Request-Id: 1\r\n\r\n", "");

        Assert.Equal(204, reply.Status);
        Assert.Null(reply.Json);
    }

    [Fact]
    public void AFailedAnswer_CarriesGitHubsMessage()
    {
        var reply = GhApi.Parse("HTTP/1.1 404 Not Found\nServer: GitHub.com\n\n{\"message\":\"Not Found\"}", "gh: Not Found (HTTP 404)");

        Assert.Equal(404, reply.Status);
        Assert.Equal("Not Found", reply.Message);
    }

    [Fact]
    public void ABodyThatIsNotJson_KeepsItsStatus()
    {
        var reply = GhApi.Parse("HTTP/2.0 502 Bad Gateway\r\n\r\n<html>bad gateway</html>", "");

        Assert.Equal(502, reply.Status);
        Assert.Null(reply.Json);
    }

    [Fact]
    public void NoHttpAnswer_IsStatusZeroWithGhsError()
    {
        var reply = GhApi.Parse("", "To get started with GitHub CLI, please run:  gh auth login");

        Assert.Equal(0, reply.Status);
        Assert.Equal("To get started with GitHub CLI, please run:  gh auth login", reply.Message);
    }

    [Fact]
    public void AMissingGh_IsStatusZero()
    {
        var reply = new GhApi("github.com", "dydo-test-no-such-gh").Send("GET", "repos/acme/widgets");

        Assert.Equal(0, reply.Status);
        Assert.StartsWith("gh could not start", reply.Error);
    }

    // git stands in for a gh that fails before it reads its input: the body write must not throw.
    [Fact]
    public void AToolThatFailsBeforeReadingTheBody_IsStatusZeroWithItsError()
    {
        var reply = new GhApi("github.com", "git").Send("POST", "repos/acme/widgets/merges", new JsonObject { ["base"] = "feature/x" });

        Assert.Equal(0, reply.Status);
        Assert.Contains("api", reply.Error);
    }
}
