namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>The merge routes a command line takes (DYD-425): what each names, before the guard resolves git state.</summary>
public class MergeRouteScannerTests
{
    private static MergeRoute Single(string command, ShellDialect dialect = ShellDialect.Unknown) =>
        Assert.Single(PrCallScanner.FindMergeRoutes(command, dialect));

    [Theory]
    [InlineData("gh pr merge 5", "gh pr merge")]
    [InlineData("gh pr merge 5 --auto", "gh pr merge")]
    [InlineData("gh -R o/r pr merge 5", "gh pr merge")]
    [InlineData(@"C:\Tools\GH.EXE pr merge 5", "gh pr merge")]
    [InlineData("gh pr -R o/r merge 5", "gh pr merge")]
    [InlineData("gh api -X PUT repos/o/r/pulls/5/merge", "gh api repos/o/r/pulls/5/merge")]
    [InlineData("gh api repos/o/r/pulls/5/merge", "gh api repos/o/r/pulls/5/merge")]
    [InlineData("gh api -X PUT /repos/o/r/pulls/5/merge-async", "gh api /repos/o/r/pulls/5/merge-async")]
    [InlineData("gh api repos/o/r/merges -f base=feature/x", "gh api repos/o/r/merges")]
    [InlineData("gh api 'https://api.github.com/repos/o/r/pulls/5/merge?x=1'", "gh api https://api.github.com/repos/o/r/pulls/5/merge?x=1")]
    [InlineData("gh api 'repos/{owner}/{repo}/pulls/5/merge'", "gh api repos/:owner/:repo/pulls/5/merge")]
    [InlineData("gh api graphql -f query='mutation { mergePullRequest(input: {}) { clientMutationId } }'", "gh api graphql mergePullRequest")]
    [InlineData("gh api graphql -F 'query=mutation{enablePullRequestAutoMerge(input:{}){clientMutationId}}'", "gh api graphql enablePullRequestAutoMerge")]
    [InlineData("gh api graphql --raw-field=query='mutation{mergeBranch(input:{}){clientMutationId}}'", "gh api graphql mergeBranch")]
    public void RawMerge_IsAMergeRoute(string command, string form)
    {
        var route = Single(command);

        Assert.Equal(MergeRouteKind.Merge, route.Kind);
        Assert.Equal(form, route.Form);
    }

    [Theory]
    [InlineData("gh repo edit --default-branch x", "gh repo edit --default-branch")]
    [InlineData("gh repo edit o/r --default-branch=x", "gh repo edit --default-branch")]
    [InlineData("gh api -X PATCH repos/o/r -f default_branch=x", "gh api -X PATCH repos/o/r default_branch")]
    [InlineData("gh api --method patch https://ghe.example.com/api/v3/repos/o/r -F default_branch=x", "gh api -X PATCH https://ghe.example.com/api/v3/repos/o/r default_branch")]
    public void DefaultBranchEdit_IsADefaultBranchChange(string command, string form)
    {
        var route = Single(command);

        Assert.Equal(MergeRouteKind.DefaultBranchChange, route.Kind);
        Assert.Equal(form, route.Form);
    }

    [Theory]
    [InlineData("gh pr view 5")]
    [InlineData("gh pr merge --help")]
    [InlineData("gh pr merge -h")]
    [InlineData("gh pr")]
    [InlineData("gh merge pr 5")]
    [InlineData("gh repo view o/r")]
    [InlineData("gh repo edit --description x")]
    [InlineData("gh repo edit --help --default-branch x")]
    [InlineData("gh api repos/o/r/pulls/5")]
    [InlineData("gh api repos/o/r/pulls/5/merged")]
    [InlineData("gh api -X PATCH repos/o/r -f description=x")]
    [InlineData("gh api repos/o/r -f default_branch=x")]
    [InlineData("gh api -X PATCH repos/o/r/branches -f default_branch=x")]
    [InlineData("gh api graphql -f query='query { viewer { login } }'")]
    [InlineData("gh api")]
    [InlineData("git commit -m 'gh pr merge 5'")]
    [InlineData("echo 'gh pr merge 5'")]
    [InlineData("grep -rn 'gh pr merge' .")]
    [InlineData("cat <<'EOF'\ngh pr merge 5\nEOF")]
    [InlineData("# gh pr merge 5")]
    [InlineData("git status")]
    [InlineData("git")]
    [InlineData("git -C")]
    [InlineData("dydo merge 5 --match-head-commit 0123456789abcdef0123456789abcdef01234567")]
    [InlineData("pwsh -EncodedCommand ZwBoACAAcAByACAAbQBlAHIAZwBlACAANQA=")]
    [InlineData("")]
    public void LookAlike_TakesNoRoute(string command)
    {
        Assert.Empty(PrCallScanner.FindMergeRoutes(command));
    }

    [Theory]
    [InlineData("git push origin HEAD:main", "main")]
    [InlineData("git push origin +HEAD:refs/heads/Master", "Master")]
    [InlineData("git push origin main", "main")]
    [InlineData("git push origin refs/heads/main", "main")]
    [InlineData("git push origin :main", "main")]
    [InlineData("git push origin --delete main", "main")]
    [InlineData("git push origin HEAD", MergeRoute.CurrentBranch)]
    [InlineData("git push origin @", MergeRoute.CurrentBranch)]
    [InlineData("git push", MergeRoute.Upstream)]
    [InlineData("git push -u origin", MergeRoute.Upstream)]
    [InlineData("git push -o ci.skip origin feature/x", "feature/x")]
    [InlineData("git push --push-option ci.skip origin", MergeRoute.Upstream)]
    [InlineData("git push origin -- main", "main")]
    [InlineData("git push --all origin", "*")]
    [InlineData("git push --mirror", "*")]
    [InlineData("git push origin 'refs/heads/*:refs/heads/*'", "*")]
    [InlineData("git -c push.default=current push origin x:y", "y")]
    public void Push_NamesTheBranchItUpdates(string command, string destination)
    {
        var route = Single(command);

        Assert.Equal(MergeRouteKind.Push, route.Kind);
        Assert.Equal(destination, route.Destination);
        Assert.Null(route.Directory);
    }

    [Fact]
    public void PushOfSeveralRefspecs_IsOneRoutePerBranch()
    {
        var routes = PrCallScanner.FindMergeRoutes("git push origin a HEAD:main b:c");

        Assert.Equal(["a", "main", "c"], routes.Select(r => r.Destination));
        Assert.All(routes, r => Assert.Equal("git push origin a HEAD:main b:c", r.Form));
    }

    [Theory]
    [InlineData("git push --tags")]
    [InlineData("git push origin --tags")]
    public void TagsOnlyPush_UpdatesNoBranch(string command)
    {
        Assert.Empty(PrCallScanner.FindMergeRoutes(command));
    }

    [Fact]
    public void TagsWithARefspec_UpdatesThatBranch()
    {
        Assert.Equal("main", Single("git push --tags origin main").Destination);
    }

    [Theory]
    [InlineData("git -C wt push", "wt")]
    [InlineData("git -C a -C b push origin HEAD", "a/b")]
    [InlineData("git -C a -C /abs push origin HEAD", "/abs")]
    [InlineData("git --git-dir .git -C wt push", "wt")]
    public void GitDashC_NamesTheTreeToRead(string command, string directory)
    {
        Assert.Equal(directory.Replace('/', Path.DirectorySeparatorChar), Single(command).Directory?.Replace('/', Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData("bash -c 'gh pr merge 5'")]
    [InlineData("sh -lc \"git status; gh pr merge 5\"")]
    [InlineData("eval 'gh pr merge 5'")]
    [InlineData("pwsh -NoProfile -Command 'gh pr merge 5'")]
    [InlineData("cmd /c gh pr merge 5")]
    [InlineData("sudo -u me gh pr merge 5")]
    [InlineData("env GH_REPO=o/r gh pr merge 5")]
    [InlineData("echo \"$(gh pr merge 5)\"")]
    [InlineData("echo `gh pr merge 5`")]
    [InlineData("git status && gh pr merge 5")]
    [InlineData("git status\ngh pr merge 5")]
    public void WrappedOrChainedMerge_IsRead(string command)
    {
        Assert.Equal(MergeRouteKind.Merge, Single(command).Kind);
    }

    [Fact]
    public void PowerShellLine_IsReadAsPowerShell()
    {
        Assert.Equal("main", Single("& gh.exe api graphql -f query='x'; git push origin `\n  HEAD:main", ShellDialect.PowerShell).Destination);
    }

    [Fact]
    public void NestingPastTheLimit_IsLeftToThePrCheck()
    {
        var command = "gh pr merge 5";
        for (var i = 0; i < 12; i++)
            command = $"eval {command}";

        Assert.Empty(PrCallScanner.FindMergeRoutes(command));
    }

    [Theory]
    [InlineData(new[] { "bash", "-lc", "gh pr merge 5" }, "Merge")]
    [InlineData(new[] { "pwsh", "-Command", "git push origin HEAD:main" }, "Push")]
    [InlineData(new[] { "gh", "pr", "merge", "5" }, "Merge")]
    [InlineData(new[] { "gh.exe", "repo", "edit", "--default-branch", "x" }, "DefaultBranchChange")]
    [InlineData(new[] { "git", "push", "origin", "HEAD:main" }, "Push")]
    [InlineData(new[] { "eval", "gh pr merge 5" }, "Merge")]
    [InlineData(new[] { "timeout", "10", "gh", "pr", "merge", "5" }, "Merge")]
    [InlineData(new[] { "nohup", "gh", "pr", "merge", "5" }, "Merge")]
    [InlineData(new[] { "env", "gh", "pr", "merge", "5" }, "Merge")]
    public void Argv_IsReadAsItRuns(string[] argv, string kind)
    {
        Assert.Equal(kind, Assert.Single(PrCallScanner.FindMergeRoutesInArgv(argv)).Kind.ToString());
    }

    [Fact]
    public void Argv_LookAlike_TakesNoRoute()
    {
        string[][] lookAlikes =
        [
            [],
            ["git", "commit", "-m", "gh pr merge 5"],
            ["gh", "pr", "comment", "5", "-b", "gh pr merge 5"],
            ["echo", "hello"],
        ];

        Assert.All(lookAlikes, argv => Assert.Empty(PrCallScanner.FindMergeRoutesInArgv(argv)));
    }

    // Another program's words are its data, as a shell reads them: no route, whatever they spell.
    [Fact]
    public void ArgvOfAnotherProgram_TakesNoRoute()
    {
        string[][] dataWords =
        [
            ["grep", "-rn", "gh pr merge", "."],
            ["echo", "git push origin x", "git push origin HEAD:main"],
            ["nohup", "gh pr merge 5"],
        ];

        Assert.All(dataWords, argv => Assert.Empty(PrCallScanner.FindMergeRoutesInArgv(argv)));
        Assert.All(dataWords, argv => Assert.Empty(PrCallScanner.FindMergeRoutes(string.Join(' ', argv.Select(w => $"'{w}'")))));
    }

    // The PR check's bare-argv rule (admiral ruling 2026-10-27) is its own and stays: there another
    // program's word that reads as a PR call is still refused as a wrapper's.
    [Fact]
    public void PrCheckBareArgvRule_IsUnchanged()
    {
        Assert.Equal("grep", Assert.Single(PrCallScanner.FindInArgv(["grep", "-rn", "gh pr create", "."])).Wrapper);
    }
}
