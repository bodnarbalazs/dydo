namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class PrCallScannerTests
{
    private static PrCall Single(string command) => Assert.Single(PrCallScanner.Find(command));

    [Theory]
    [InlineData("gh pr create --head topic", "topic", "gh pr create --head topic")]
    [InlineData("gh pr new -H me:topic", "topic", "gh pr new --head me:topic")]
    [InlineData("gh pr create -dHtopic", "topic", "gh pr create --head topic")]
    [InlineData("gh pr create -H=topic", "topic", "gh pr create --head topic")]
    [InlineData("gh pr create --head a --head b", "b", "gh pr create --head b")]
    [InlineData("gh pr ready me:topic", "topic", "gh pr ready me:topic")]
    [InlineData("gh api repos/o/r/pulls --raw-field head=a -f head=me:b", "b", "gh api repos/o/r/pulls head=me:b")]
    public void NamedHead_IsTheBranchWithoutItsOwner(string command, string branch, string form)
    {
        var call = Single(command);

        Assert.Equal(branch, call.Branch);
        Assert.Equal(form, call.BranchForm);
        Assert.Null(call.Wrapper);
    }

    [Theory]
    [InlineData("gh pr create --fill")]
    [InlineData("gh pr ready")]
    [InlineData("gh pr ready 12")]
    [InlineData("gh pr ready https://github.com/o/r/pull/12")]
    [InlineData("gh pr create --title -H --fill")]
    [InlineData("gh -H topic pr create")]
    [InlineData("gh api repos/{owner}/{repo}/pulls -F head={branch}")]
    [InlineData("gh api graphql -f query='mutation{createPullRequest(input:{headRefName:\"h\"}){clientMutationId}}'")]
    public void CallNamingNoHead_TakesTheDirectory(string command)
    {
        Assert.Null(Single(command).Branch);
    }

    [Theory]
    [InlineData("gh -R a/b pr create", "a/b")]
    [InlineData("gh pr -R a/b create", "a/b")]
    [InlineData("gh pr create --repo=a/b", "a/b")]
    [InlineData("gh pr create -Ra/b", "a/b")]
    [InlineData("GH_REPO=a/b gh pr create", "a/b")]
    [InlineData("env -u X GH_REPO=a/b gh pr ready", "a/b")]
    [InlineData("gh api https://ghe.example.com/api/v3/repos/a/b/pulls -X POST", "a/b")]
    public void NamedRepository_IsListed(string command, string repo)
    {
        Assert.Equal([repo], Single(command).Repos);
    }

    [Fact]
    public void RestEndpointWithPlaceholders_NamesNoRepository()
    {
        Assert.Empty(Single("gh api repos/{owner}/{repo}/pulls -f head=x").Repos);
    }

    [Theory]
    [InlineData("gh pr view 12")]
    [InlineData("gh pr create-notes")]
    [InlineData("gh repo view pr create")]
    [InlineData("gh")]
    [InlineData("gh api")]
    [InlineData("gh api repos/o/r/pulls")]
    [InlineData("gh api -X PATCH repos/o/r/pulls -f title=x")]
    [InlineData("gh api repos/o/r/pulls/12/comments -f body=x")]
    [InlineData("gh api graphql -f query='{viewer{login}}'")]
    [InlineData("git commit -m 'gh pr create'")]
    [InlineData("echo gh pr create # gh pr ready")]
    [InlineData("timeout 5")]
    [InlineData("cat <<'EOF'\ngh pr create\nEOF")]
    [InlineData("Write-Output @'\ngh pr create\n'@")]
    [InlineData("bash -c 'gh pr view 1; echo create'")]
    [InlineData("pwsh -ExecutionPolicy Bypass -File x.ps1")]
    public void CommandOpeningNoPr_HasNoCall(string command)
    {
        Assert.Empty(PrCallScanner.Find(command));
    }

    [Theory]
    [InlineData("timeout -s KILL 5 gh pr create", null)]
    [InlineData("nice -n 5 gh pr ready", null)]
    [InlineData("env -C /tmp/x gh pr create", null)]
    [InlineData("time -p gh pr new", null)]
    [InlineData("while gh pr ready; do :; done", null)]
    [InlineData("! gh pr create", null)]
    [InlineData("bash -c 'gh pr create'", "bash")]
    [InlineData("eval gh pr create", "eval")]
    [InlineData("cmd.exe /c \"gh.exe pr ready 1\"", "cmd")]
    [InlineData("pwsh -c \"gh api graphql -f query=markPullRequestReadyForReview\"", "pwsh")]
    [InlineData("sh -c 'gh api -X POST repos/o/r/pulls'", "sh")]
    [InlineData("powershell -EncodedCommand AAAA", "powershell")]
    [InlineData("pwsh /enc AAAA", "pwsh")]
    [InlineData("pwsh -e AAAA", "pwsh")]
    public void PrCall_IsFoundDirectOrWrapped(string command, string? wrapper)
    {
        Assert.Equal(wrapper, Single(command).Wrapper);
    }

    [Fact]
    public void EveryCallInALine_IsFound()
    {
        Assert.Equal(2, PrCallScanner.Find("gh pr create --head a && git push; gh pr ready b").Count);
    }
}
