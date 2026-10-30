namespace DynaDocs.Tests.Merge;

using DynaDocs.Services;
using DynaDocs.Services.Merge;
using static DynaDocs.Tests.Integration.PushedRepository;
using static MergeScenario;

/// <summary>
/// Repository identity (design §2, inputs): the repository is the one the located project's origin
/// names, whatever <c>GH_REPO</c>, <c>GH_HOST</c> or <c>GIT_DIR</c> say.
/// </summary>
[Collection("Integration")]
public sealed class MergeCheckoutTests : IDisposable
{
    private readonly MergeScenario _s = new();

    public void Dispose() => _s.Dispose();

    [Theory]
    [InlineData("https://github.com/acme/widgets.git", "github.com", false)]
    [InlineData("https://github.com/acme/widgets", "github.com", false)]
    [InlineData("https://github.com/acme/widgets.git/", "github.com", false)]
    [InlineData("https://x-access-token:secret@github.com/acme/widgets.git", "github.com", false)]
    [InlineData("http://ghe.example.com:8080/acme/widgets", "ghe.example.com", false)]
    [InlineData("git@github.com:acme/widgets.git", "github.com", true)]
    [InlineData("GitHub.com:acme/widgets", "github.com", true)]
    [InlineData("ssh://git@github.com/acme/widgets.git", "github.com", true)]
    [InlineData("ssh://git@ghe.example.com:2222/acme/widgets", "ghe.example.com", true)]
    public void OriginUrls_NameHostOwnerAndRepository(string url, string host, bool ssh)
    {
        Assert.Equal(new OriginRepository(host, "acme", "widgets", ssh), OriginRepository.Parse(url));
    }

    [Theory]
    [InlineData("C:/repositories/widgets.git")]
    [InlineData("/srv/git/widgets.git")]
    [InlineData("file:///srv/git/widgets.git")]
    [InlineData("https://github.com/acme")]
    [InlineData("https://github.com/acme/widgets/tree/main")]
    [InlineData("")]
    public void OtherOrigins_NameNoRepository(string url)
    {
        Assert.Null(OriginRepository.Parse(url));
    }

    [Theory]
    [InlineData("git@work-gh:acme/widgets.git", true)]
    [InlineData("ssh://git@work-gh/acme/widgets.git", true)]
    [InlineData("git@github.com:acme/widgets.git", false)]
    [InlineData("https://localhost/acme/widgets", false)]
    public void AnSshHostWithoutADot_IsAnAlias(string url, bool alias)
    {
        Assert.Equal(alias, OriginRepository.Parse(url)!.IsSshAlias);
    }

    [Fact]
    public void OriginsInsteadOfRewrite_IsHonoured()
    {
        Git(_s.Root, "config", "url.https://github.com/.insteadOf", "gh:");
        Git(_s.Root, "remote", "set-url", "origin", "gh:acme/widgets");

        var checkout = MergeCheckout.Locate(_s.Root);

        Assert.Equal(new OriginRepository("github.com", "acme", "widgets", false), checkout.Origin);
        Assert.Null(checkout.OriginHead);
    }

    [Fact]
    public void TheLocalOriginHead_IsReadWithoutItsRemoteName()
    {
        Git(_s.Root, "symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/trunk");

        Assert.Equal("trunk", MergeCheckout.Locate(_s.Root).OriginHead);
    }

    [Fact]
    public void ANestedRepository_IsNotTheProjectsCheckout()
    {
        var nested = Path.Combine(_s.Root, "vendor", "lib");
        Directory.CreateDirectory(nested);
        Git(nested, "init");
        Git(nested, "remote", "add", "origin", "https://github.com/mallory/lib.git");

        var (exit, _, stderr) = _s.Merge(directory: nested);

        Assert.Equal(1, exit);
        Assert.StartsWith("refused: no dydo project here; run dydo merge from a checkout of the project's repository", stderr);
        Assert.Null(_s.Host);
    }

    [Fact]
    public void ASubdirectoryOfTheCheckout_FindsTheProject()
    {
        var sub = Path.Combine(_s.Root, "src");
        Directory.CreateDirectory(sub);

        var (exit, _, stderr) = _s.Merge(dryRun: true, directory: sub);

        Assert.True(exit == 0, stderr);
    }

    [Fact]
    public void AnSshAlias_IsAToolError()
    {
        Git(_s.Root, "remote", "set-url", "origin", "git@work-gh:acme/widgets.git");

        var (exit, _, stderr) = _s.Merge();

        Assert.Equal(2, exit);
        Assert.StartsWith("error: origin's host work-gh is an SSH alias dydo merge cannot resolve", stderr);
        Assert.Empty(_s.GitHub.Calls);
    }

    [Fact]
    public void GhRepoGhHostAndGitDirPointingElsewhere_ChangeNothing()
    {
        var elsewhere = Path.Combine(_s.Root, "..", Path.GetFileName(_s.Root) + "-elsewhere");
        Directory.CreateDirectory(elsewhere);
        Git(elsewhere, "init");
        Git(elsewhere, "remote", "add", "origin", "https://evil.example/mallory/evil.git");
        var saved = new[] { "GH_REPO", "GH_HOST", "GIT_DIR" }.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("GH_REPO", "mallory/evil");
            Environment.SetEnvironmentVariable("GH_HOST", "evil.example");
            Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(elsewhere, ".git"));

            var (exit, _, stderr) = _s.Merge(dryRun: true);
            var start = GhApi.StartInfo("gh", "github.com", "GET", $"{Repo}/pulls/7", hasBody: false);

            Assert.True(exit == 0, stderr);
            Assert.Equal("github.com", _s.Host);
            Assert.All(_s.GitHub.Calls, call => Assert.StartsWith(Repo, call.Path));
            Assert.Equal(["api", $"{Repo}/pulls/7", "--hostname", "github.com", "--method", "GET", "--include"], start.ArgumentList);
            Assert.False(start.Environment.ContainsKey("GH_REPO"));
            Assert.False(start.Environment.ContainsKey("GH_HOST"));
        }
        finally
        {
            foreach (var (name, value) in saved)
                Environment.SetEnvironmentVariable(name, value);
            LinkSafeDelete.DeleteTree(elsewhere);
        }
    }
}
