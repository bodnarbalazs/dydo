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

    // DYD-887: the branch words of the changes a push of the current branch carries, in the order the
    // line runs them; one the line creates is marked +, and one only && separates from the push, &&.
    private static List<string> Switches(string command, ShellDialect dialect = ShellDialect.Unknown) =>
        [.. PrCallScanner.FindMergeRoutes(command, dialect).SelectMany(r => r.Changes.Items)
            .Select(c => (c.Creates ? "+" : "") + c.Branch + (c.Chained ? "&&" : ""))];

    [Fact]
    public void BarePush_AfterACheckout_CarriesTheChange()
    {
        var route = Single("git checkout main && git merge feature/x && git push");

        Assert.Equal(MergeRoute.Upstream, route.Destination);
        Assert.Equal([new BranchChange("main", Chained: true)], route.Changes.Items);
    }

    // The branch word is git's to resolve in the change's tree (a local branch, a commit, a remote's branch or paths).
    [Theory]
    [InlineData("git checkout main && git push", "main")]
    [InlineData("git switch main; git push origin", "main")]
    [InlineData("git switch main && git push origin @", "main")]
    [InlineData("git checkout -q refs/heads/main && git push origin HEAD", "refs/heads/main")]
    [InlineData("git checkout aa5ca5e8 && git push origin HEAD", "aa5ca5e8")]
    [InlineData("git checkout origin/main && git push origin HEAD", "origin/main")]
    [InlineData("git checkout -b new && git push", "+new")]
    [InlineData("git checkout -B new && git push", "+new")]
    [InlineData("git checkout --orphan new && git push", "+new")]
    [InlineData("git switch -c new && git push", "+new")]
    [InlineData("git switch --create=new && git push", "+new")]
    [InlineData("git switch --no-track -C new && git push", "+new")]
    [InlineData("git branch -M main && git push", "+main")]
    [InlineData("git -C wt checkout main && git push", "main")]
    [InlineData("git checkout main || git push", "main")]
    [InlineData("git checkout main | cat; git push", "main")]
    [InlineData("bash -c 'git checkout main' && git push", "main")]
    [InlineData("git checkout feature/x\ngit push", "feature/x")]
    public void BranchChange_BeforeAPushOfTheCurrentBranch_NamesItsBranch(string command, string branch)
    {
        Assert.Equal([branch], Switches(command).Select(s => s.TrimEnd('&')));
    }

    // N1: an option's value is taken with it, never as the branch; the bypass probes of round 1.
    [Theory]
    [InlineData("git checkout --conflict diff3 main && git push")]
    [InlineData("git checkout --conflict=diff3 main && git push")]
    [InlineData("git switch --conflict merge main && git push")]
    [InlineData("git -c advice.detachedHead=false checkout main && git push")]
    [InlineData("git --no-pager checkout main && git push")]
    [InlineData("git checkout --recurse-submodules main && git push")]
    [InlineData("git checkout --no-recurse-submodules main && git push")]
    [InlineData("git checkout main 2>/dev/null && git push")]
    [InlineData("git switch --discard-changes main && git push")]
    [InlineData("git checkout -m main && git push")]
    [InlineData("git checkout --no-track main && git push")]
    [InlineData("git checkout -f main && git push -f")]
    public void ValueOption_IsNotTheBranch(string command)
    {
        Assert.Equal(["main&&"], Switches(command));
    }

    // S1: only && between a change and the push means the push runs only after it succeeds.
    [Theory]
    [InlineData("git checkout main && git merge feature/x && git push", "main&&")]
    [InlineData("git checkout main && git switch feature/x && git push", "main&& feature/x&&")]
    [InlineData("git checkout main && git pull && git checkout DYD-1-x && git push", "main&& DYD-1-x&&")]
    [InlineData("git checkout main; git switch feature/x && git push", "main feature/x&&")]
    [InlineData("git checkout main && git switch feature/x; git push", "main feature/x")]
    [InlineData("git checkout main && git switch feature/x || git push", "main feature/x")]
    [InlineData("true || git checkout feature/x && git push", "feature/x")]
    [InlineData("git checkout main && true || git checkout feature/x && git push", "main feature/x")]
    [InlineData("true || git checkout x && git checkout main && git push", "x main&&")]
    [InlineData("git checkout main && git merge y | tee log && git push", "main")]
    [InlineData("git checkout main | cat && git push", "main")]
    [InlineData("git checkout main & git push", "main")]
    [InlineData("git checkout main 2>&1 && git push", "main&&")]
    [InlineData("git checkout main &>log && git push", "main&&")]
    [InlineData("git checkout main &&\ngit push", "main&&")]
    [InlineData("git checkout main && # note\ngit push", "main")]
    [InlineData("! git checkout main && git push", "main")]
    [InlineData("(git checkout main) && git push", "main")]
    [InlineData("bash -c 'git checkout main' && git push", "main")]
    [InlineData("bash -c 'git checkout main && git push'", "main&&")]
    [InlineData("git branch -m old main && git push", "+main")]
    [InlineData("git branch -m main && git push", "+main&&")]
    public void BranchChange_OnlyAndAndBeforeThePush_IsChained(string command, string changes)
    {
        Assert.Equal(changes.Split(' '), Switches(command));
    }

    // Round 2: the order bash runs substitutions in is not read. A line that changes branch and pushes,
    // where a substitution (backtick or $( ... ), wherever it sits) holds a change or a push, makes
    // every push unreadable, a push naming its branch included. A line with a backtick is also read
    // as PowerShell, where the backtick escapes; the guard refuses when either reading does.
    [Theory]
    [InlineData("echo `git checkout main && git merge feature/x` \"$(git push)\"")]
    [InlineData("echo \"$(git push)\" `git checkout main && git merge feature/x`")]
    [InlineData("echo `git push` \"$(git checkout main && git merge feature/x)\"")]
    [InlineData("echo \"$(git checkout main && git merge feature/x)\" `git push`")]
    [InlineData("echo \"$(git checkout main)\"; git push")]
    [InlineData("echo \"$(git checkout main)\" && git push")]
    [InlineData("git checkout main && echo \"$(git push)\"")]
    [InlineData("echo \"$(git checkout main && git push)\"")]
    [InlineData("echo \"$(echo \"$(git checkout main)\")\"; git push")]
    [InlineData("git checkout main && echo \"$(git checkout x)\" && git push")]
    [InlineData("echo \"$(gh pr checkout 5)\"; git push")]
    [InlineData("echo \"$(git checkout main)\"; git push origin feature/x")]
    [InlineData("bash -c 'echo \"$(git checkout main)\"' && git push")]
    [InlineData("echo `git checkout main`; git push")]
    [InlineData("git push; echo `git checkout main`")]
    [InlineData("git push \"`git checkout main`\"")]
    [InlineData("git checkout main && echo `git push`")]
    [InlineData("echo `git checkout main && git push`")]
    [InlineData("git checkout x && echo `git checkout main` && git push")]
    public void BranchChangeAndPush_WithASubstitutionHoldingEither_LeaveEveryPushUnreadable(string command)
    {
        var routes = PrCallScanner.FindMergeRoutes(command, ShellDialect.Posix);

        Assert.NotEmpty(routes);
        Assert.All(routes.GroupBy(route => route.Form), push => Assert.Contains(push, route => route.Unreadable));
    }

    // Its honest twins: a substitution holding neither a change nor a push leaves the line read as
    // before, and so does a substitution holding a push when the line changes no branch, or a change
    // when the line pushes nothing.
    [Theory]
    [InlineData("git push; echo `date`", "")]
    [InlineData("git push origin \"$(git branch --show-current)\"", "")]
    [InlineData("git checkout -b new && git push -u origin \"$(git branch --show-current)\"", "")]
    [InlineData("git checkout main && echo `date` && git push", "main&&")]
    [InlineData("echo \"$(git push)\"", "")]
    [InlineData("echo `git push` && git merge feature/x", "")]
    public void Push_BesideASubstitutionHoldingNoChangeAndNoPush_IsReadAsBefore(string command, string changes)
    {
        var routes = PrCallScanner.FindMergeRoutes(command, ShellDialect.Posix);

        Assert.All(routes, route => Assert.False(route.Unreadable, route.Form));
        Assert.Equal(changes.Split(' ', StringSplitOptions.RemoveEmptyEntries), Switches(command, ShellDialect.Posix));
    }

    [Fact]
    public void BranchChange_InASubstitutionWithoutAPush_TakesNoRoute()
    {
        Assert.Empty(PrCallScanner.FindMergeRoutes("echo \"$(git checkout main)\" `git switch x`", ShellDialect.Posix));
    }

    // A PowerShell $( ... ) is a substitution too.
    [Fact]
    public void PowerShellSubexpression_HoldingABranchChange_LeavesThePushUnreadable()
    {
        Assert.True(Single("echo \"$(git checkout main)\"; git push", ShellDialect.PowerShell).Unreadable);
        Assert.False(Single("git checkout main; git push origin \"$(git branch --show-current)\"", ShellDialect.PowerShell).Unreadable);
    }

    [Fact]
    public void EveryBranchChangeBeforeAPush_IsCarried()
    {
        Assert.Equal(["x", "main"], Switches("git checkout x; git checkout main; git push"));
    }

    // B1: a change keeps its tree, so the guard matches it only to a push in the same worktree.
    [Fact]
    public void BranchChange_KeepsItsTree()
    {
        var change = Assert.Single(Single("git -C ../other -C wt checkout main && git push").Changes.Items);

        Assert.Equal(Path.Combine("../other", "wt"), change.Directory);
        Assert.Null(change.AsWritten);
        Assert.Null(Assert.Single(Single("gh pr checkout 5 && git push").Changes.Items).Directory);
    }

    [Fact]
    public void BranchChange_InABackslashTree_KeepsTheTreeAsWritten()
    {
        var change = Assert.Single(Single(@"git -C w\t checkout main && git push", ShellDialect.Posix).Changes.Items);

        Assert.Equal("wt", change.Directory);
        Assert.Equal(@"w\t", change.AsWritten);
    }

    [Fact]
    public void BranchChanges_AreEqualByTheirItems()
    {
        var a = new BranchChanges([new BranchChange("main")]);
        var b = new BranchChanges([new BranchChange("main")]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, BranchChanges.None);
        Assert.False(a.Equals(null));
    }

    [Theory]
    [InlineData("git checkout - && git push")]
    [InlineData("git switch - && git push")]
    [InlineData("git checkout '@{-1}' && git push")]
    [InlineData("git checkout $B && git push")]
    [InlineData("git checkout \"$(echo main)\" && git push")]
    [InlineData("git switch --detach main && git push origin HEAD")]
    [InlineData("git switch -d main && git push")]
    [InlineData("git checkout --detach && git push")]
    [InlineData("git checkout -t origin/main && git push")]
    [InlineData("git checkout --track=direct origin/main && git push")]
    [InlineData("git checkout -b new origin/main && git push")]
    [InlineData("git switch -c new main && git push")]
    [InlineData("git checkout -qb new && git push")]
    [InlineData("git checkout -b && git push")]
    [InlineData("git switch a b && git push")]
    [InlineData("gh pr checkout 5 && git push")]
    [InlineData("gh -R o/r pr checkout 5 && git push")]
    [InlineData("gh co 5 && git push")]
    [InlineData("git checkout --conf diff3 main && git push")]
    [InlineData("git checkout --frobnicate main && git push")]
    [InlineData("git checkout -x main && git push")]
    [InlineData("git checkout -d main && git push")]
    [InlineData("git checkout --no-detach main && git push")]
    [InlineData("git checkout -b HEAD && git push")]
    [InlineData("git branch -m '@{-1}' && git push")]
    public void BranchChange_TheLineDoesNotSayForCertain_IsUnreadable(string command)
    {
        Assert.Equal([MergeRoute.UnreadableBranch], Switches(command).Select(s => s.TrimEnd('&')));
    }

    [Theory]
    [InlineData("git checkout main -- readme.md && git push")]
    [InlineData("git checkout -- readme.md && git push")]
    [InlineData("git checkout main readme.md && git push")]
    [InlineData("git checkout -p main && git push")]
    [InlineData("git checkout --pathspec-from-file=list && git push")]
    [InlineData("git checkout --pathspec-from-file list && git push")]
    [InlineData("git checkout --conflict diff3 -- readme.md && git push")]
    [InlineData("git checkout --frobnicate=1 && git push")]
    [InlineData("git checkout --frobnicate && git push")]
    [InlineData("git checkout HEAD && git push")]
    [InlineData("git checkout && git push")]
    [InlineData("git branch -d x && git push")]
    [InlineData("git branch -m && git push")]
    [InlineData("gh pr checkout --help && git push")]
    [InlineData("gh pr view 5 && git push")]
    [InlineData("git push && git checkout main")]
    [InlineData("git checkout main && git push origin feature/x")]
    [InlineData("git checkout main && git push origin HEAD:feature/x")]
    [InlineData("echo 'git checkout main'; git push")]
    public void NoBranchChangeBeforeAPushOfTheCurrentBranch_JudgesHeadAlone(string command)
    {
        Assert.Empty(Switches(command));
    }

    [Fact]
    public void BranchChange_WithoutAPush_TakesNoRoute()
    {
        Assert.Empty(PrCallScanner.FindMergeRoutes("git checkout main && git merge feature/x"));
        Assert.Empty(PrCallScanner.FindMergeRoutesInArgv(["git", "checkout", "main"]));
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

    // DYD-641: pwsh 7.6.9 ends a statement and a comment at a bare carriage return, closes a string
    // at a typographic quote, and keeps a caret literal, so the route after each is taken. DYD-665:
    // a backtick before CRLF inside a word keeps the carriage return, and the line feed ends the statement.
    [Theory]
    [InlineData("echo 'x'\rgh pr merge 5")]
    [InlineData("echo x # note\rgh pr merge 5")]
    [InlineData("echo ‘a'; gh pr merge 5; ’b'")]
    [InlineData("echo \"a”; gh pr merge 5; “b\"")]
    [InlineData("echo a^;gh pr merge 5")]
    [InlineData("x`\r\ngh pr merge 5")]
    public void PowerShellReading_FindsTheMergePwshRuns(string command)
    {
        Assert.Contains(PrCallScanner.FindMergeRoutes(command, ShellDialect.PowerShell), route => route.Kind == MergeRouteKind.Merge);
    }

    [Theory]
    [InlineData("echo 'x'\rgit push origin main")]
    [InlineData("echo ‘a'; git push origin main; ’b'")]
    [InlineData("echo a^;git push origin main")]
    [InlineData("x`\r\ngit push origin main")]
    public void PowerShellReading_FindsThePushPwshRuns(string command)
    {
        Assert.Contains(PrCallScanner.FindMergeRoutes(command, ShellDialect.PowerShell),
            route => route.Kind == MergeRouteKind.Push && route.Destination == "main");
    }

    // DYD-665's twin: between words, and after a quoted argument, the backtick continues the line,
    // so the route's words are echo's arguments.
    [Theory]
    [InlineData("echo x `\r\ngh pr merge 5")]
    [InlineData("echo 'x'`\r\ngh pr merge 5")]
    [InlineData("echo \"x\"`\r\ngit push origin main")]
    public void PowerShellReading_BacktickCrlfBetweenWords_TakesNoRoute(string command)
    {
        Assert.Empty(PrCallScanner.FindMergeRoutes(command, ShellDialect.PowerShell));
    }

    // The PR check's bare-argv rule (admiral ruling 2026-10-27) is its own and stays: there another
    // program's word that reads as a PR call is still refused as a wrapper's.
    [Fact]
    public void PrCheckBareArgvRule_IsUnchanged()
    {
        Assert.Equal("grep", Assert.Single(PrCallScanner.FindInArgv(["grep", "-rn", "gh pr create", "."])).Wrapper);
    }
}
