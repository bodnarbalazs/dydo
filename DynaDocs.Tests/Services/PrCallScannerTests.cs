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
    [InlineData("gh api https://ghe.example.com/api/v3/repos/a/b/pulls -X POST", "ghe.example.com/a/b")]
    [InlineData("gh api https://api.github.com/repos/a/b/pulls -X POST", "github.com/a/b")]
    [InlineData("gh api --hostname ghe.example.com repos/a/b/pulls -X POST", "ghe.example.com/a/b")]
    [InlineData("gh pr ready https://ghe.example.com/a/b/pull/12", "ghe.example.com/a/b")]
    [InlineData("gh pr ready -R c/d https://github.com/a/b/pull/12", "github.com/a/b")]
    public void NamedRepository_IsTheCallsRepository(string command, string repo)
    {
        Assert.Equal(repo, Single(command).Repo);
    }

    [Fact]
    public void RestEndpointWithPlaceholders_NamesNoRepository()
    {
        Assert.Null(Single("gh api repos/{owner}/{repo}/pulls -f head=x").Repo);
    }

    // B3: gh acts on the last -R/--repo, else an inline GH_REPO, else the inherited one.
    [Theory]
    [InlineData("GH_REPO=x/y gh pr create -R a/b", null, "a/b")]
    [InlineData("GH_REPO=x/y gh -R a/b pr create", null, "a/b")]
    [InlineData("gh -R x/y pr create --repo a/b", null, "a/b")]
    [InlineData("GH_REPO=x/y GH_REPO=a/b gh pr create", null, "a/b")]
    [InlineData("gh pr create", "a/b", "a/b")]
    [InlineData("GH_REPO=a/b gh pr create", "x/y", "a/b")]
    [InlineData("gh pr create -R a/b", "x/y", "a/b")]
    [InlineData("GH_REPO= gh pr create", "x/y", null)]
    [InlineData("gh pr create", "", null)]
    [InlineData("gh api repos/{owner}/{repo}/pulls -f head=x", "a/b", "a/b")]
    [InlineData("gh api repos/a/b/pulls -f head=x", "x/y", "a/b")]
    [InlineData("gh api graphql -f query=createPullRequest", "x/y", null)]
    public void EffectiveRepository_IsTheOneGhUses(string command, string? inherited, string? repo)
    {
        Assert.Equal(repo, Assert.Single(PrCallScanner.Find(command, inheritedRepo: inherited)).Repo);
    }

    // C2: command position is the shared reading, so these prefixes reach gh as they reach dydo.
    [Theory]
    [InlineData("nohup gh pr create --fill")]
    [InlineData("command gh pr create --fill")]
    [InlineData("exec gh pr ready 12")]
    [InlineData("env -- gh pr create --fill")]
    [InlineData("timeout -- 60 gh pr create --fill")]
    [InlineData("sudo -u me gh pr create --fill")]
    [InlineData("gh.cmd pr create --fill")]
    public void RunnerPrefix_ReachesGh(string command)
    {
        Assert.Null(Single(command).Wrapper);
    }

    // C1, B2: a line is read as its shell reads it.
    [Theory]
    [InlineData(@"C:\tools\gh.exe pr create --fill", "PowerShell", 1)]
    [InlineData(@"C:\tools\gh.exe pr create --fill", "Unknown", 1)]
    [InlineData(@"C:\tools\gh.exe pr create --fill", "Posix", 0)]
    [InlineData(@"& C:\tools\gh.exe pr create --fill", "Posix", 1)]
    [InlineData("$env:GH_TOKEN = 'x'; C:\\tools\\gh.exe pr create --fill", "Posix", 1)]
    public void Dialect_DecidesTheReading(string command, string dialect, int calls)
    {
        Assert.Equal(calls, PrCallScanner.Find(command, Enum.Parse<ShellDialect>(dialect)).Count);
    }

    [Theory]
    [InlineData("PowerShell")]
    [InlineData("Posix")]
    public void PowerShellContinuation_KeepsTheHead(string dialect)
    {
        var calls = PrCallScanner.Find("gh pr create `\n  --head topic `\n  --fill", Enum.Parse<ShellDialect>(dialect));

        Assert.Contains(calls, c => c.Branch == "topic");
    }

    // A5: a wrapper's command string is read by the same recogniser.
    [Theory]
    [InlineData("bash -c 'gh api repos/o/r/pulls'")]
    [InlineData("bash -c 'gh pr list --search ready'")]
    [InlineData("cmd /c gh pr list --search create")]
    [InlineData("eval \"gh pr status\"")]
    [InlineData("bash -c 'echo gh pr create'")]
    [InlineData("pwsh -NoProfile -Command \"gh api repos/o/r/pulls?state=open\"")]
    [InlineData("bash -c 'gh api -X PATCH repos/o/r/pulls/12 -f draft=false'")]
    [InlineData("bash script.sh gh pr create")]
    [InlineData("pwsh -File ready.ps1")]
    public void WrapperRunningNoPrCall_HasNoCall(string command)
    {
        Assert.Empty(PrCallScanner.Find(command));
    }

    [Theory]
    [InlineData("bash -c 'gh api -X POST -H \"Content-Type: application/json; charset=utf-8\" repos/o/r/pulls --input b.json'", "bash")]
    [InlineData("pwsh -Command \"gh pr create --title 'a; b' --fill\"", "pwsh")]
    [InlineData("bash -c 'echo \"x | y\" && gh pr ready 12'", "bash")]
    [InlineData("bash -c \"sh -c 'eval gh pr ready 1'\"", "bash")]
    [InlineData("pwsh -NoProfile -EncodedCommand ZwBpAHQAIABzAHQAYQB0AHUAcwA=", "pwsh")]
    public void WrapperRunningAPrCall_IsTheCall(string command, string wrapper)
    {
        Assert.Equal(wrapper, Single(command).Wrapper);
    }

    // B1: Codex's shell envelope is its script; any other array is its first word's call.
    [Theory]
    [InlineData(new[] { "bash", "-lc", "gh pr ready topic" }, null, "topic")]
    [InlineData(new[] { "/usr/bin/zsh", "-c", "git status && gh pr create --head topic" }, null, "topic")]
    [InlineData(new[] { @"C:\Program Files\PowerShell\7\pwsh.exe", "-NoProfile", "-Command", @"& C:\tools\gh.exe pr ready topic" }, null, "topic")]
    [InlineData(new[] { "powershell", "-NoLogo", "-ExecutionPolicy", "Bypass", "-Command", "gh pr ready topic" }, null, "topic")]
    [InlineData(new[] { "gh", "pr", "ready", "topic" }, null, "topic")]
    [InlineData(new[] { "bash", "-lc", "bash -c 'gh pr ready 1'" }, "bash", null)]
    [InlineData(new[] { "bash", "-c", "gh pr ready 1", "extra" }, "bash", null)]
    [InlineData(new[] { "pwsh", "-Command", "gh", "pr", "ready" }, "pwsh", null)]
    [InlineData(new[] { "pwsh", "-EncodedCommand", "ZwBpAHQAIABzAHQAYQB0AHUAcwA=" }, "pwsh", null)]
    [InlineData(new[] { "cmd.exe", "/c", "gh pr ready 1" }, "cmd", null)]
    [InlineData(new[] { "node", "-e", "gh pr create --fill" }, "node", null)]
    public void ArgvArray_IsReadByItsShape(string[] argv, string? wrapper, string? branch)
    {
        var call = Assert.Single(PrCallScanner.FindInArgv(argv));

        Assert.Equal(wrapper, call.Wrapper);
        Assert.Equal(branch, call.Branch);
    }

    [Theory]
    [InlineData(new object[] { new[] { "bash", "-lc", "gh pr view 12" } })]
    [InlineData(new object[] { new[] { "pwsh", "-NoProfile", "-Command", "git status" } })]
    [InlineData(new object[] { new[] { "node", "-e", "console.log(1)" } })]
    [InlineData(new object[] { new[] { "gh", "pr", "view", "gh pr create" } })]
    public void ArgvArrayRunningNoPrCall_HasNoCall(string[] argv)
    {
        Assert.Empty(PrCallScanner.FindInArgv(argv));
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

    // R2-1: a reading that loses the head another reading names adds no headless call.
    [Theory]
    [InlineData("Posix", "gh pr create `\n  --head topic `\n  --fill")]
    [InlineData("Unknown", "gh pr create `\n  --head topic `\n  --title t --body '## Independent review'")]
    [InlineData("Unknown", "gh pr create `\r\n  --head topic `\r\n  --fill")]
    public void AmbiguousContinuation_AddsNoHeadlessCall(string dialect, string command)
    {
        var call = Assert.Single(PrCallScanner.Find(command, Enum.Parse<ShellDialect>(dialect)));

        Assert.Equal("topic", call.Branch);
    }

    [Fact]
    public void AmbiguousLine_KeepsAHeadlessCallBothReadingsMake()
    {
        var calls = PrCallScanner.Find("gh pr ready 12; gh pr create `\n  --head topic `\n  --fill", ShellDialect.Unknown);

        Assert.Equal(2, calls.Count);
        Assert.Contains(calls, c => c.Branch == null);
        Assert.Contains(calls, c => c.Branch == "topic");
    }

    // R2-2: env's -u/--unset, -i and VAR= act on GH_REPO in order, left to right.
    [Theory]
    [InlineData("env -u GH_REPO gh pr ready 12", "x/y", null)]
    [InlineData("env --unset=GH_REPO gh pr ready 12", "x/y", null)]
    [InlineData("env --unset GH_REPO gh pr ready 12", "x/y", null)]
    [InlineData("env -i gh pr ready 12", "x/y", null)]
    [InlineData("env --ignore-environment gh pr ready 12", "x/y", null)]
    [InlineData("env GH_REPO= gh pr ready 12", "x/y", null)]
    [InlineData("env -u OTHER gh pr ready 12", "x/y", "x/y")]
    [InlineData("GH_REPO=a/b env -u GH_REPO gh pr ready 12", null, null)]
    [InlineData("env -u GH_REPO GH_REPO=a/b gh pr ready 12", "x/y", "a/b")]
    [InlineData("GH_REPO=a/b env -i gh pr ready 12", null, null)]
    [InlineData("env -i GH_REPO=a/b gh pr ready 12", "x/y", "a/b")]
    [InlineData("env GH_REPO=a/b env -u GH_REPO gh pr ready 12", null, null)]
    public void EnvEffects_OnGhRepo_ApplyInOrder(string command, string? inherited, string? repo)
    {
        Assert.Equal(repo, Assert.Single(PrCallScanner.Find(command, inheritedRepo: inherited)).Repo);
    }

    // R2-3: each api path placeholder resolves on its own; a literal owner or name stays.
    [Theory]
    [InlineData("gh api repos/{owner}/other/pulls -X POST", null, ":owner/other")]
    [InlineData("gh api repos/other/{repo}/pulls -X POST", null, "other/:repo")]
    [InlineData("gh api repos/{owner}/other/pulls -X POST", "acme/lc", "acme/other")]
    [InlineData("gh api repos/other/{repo}/pulls -X POST", "acme/lc", "other/lc")]
    [InlineData("gh api repos/{owner}/{repo}/pulls -X POST", "acme/lc", "acme/lc")]
    [InlineData("gh api repos/{owner}/{repo}/pulls -X POST", null, null)]
    public void ApiPathPlaceholders_ResolveSegmentBySegment(string command, string? inherited, string? repo)
    {
        Assert.Equal(repo, Assert.Single(PrCallScanner.Find(command, inheritedRepo: inherited)).Repo);
    }

    [Theory]
    [InlineData(":owner/other", "https://github.com/Acme/LC.git", "https://github.com/Acme/other")]
    [InlineData("other/:repo", "git@github.com:Acme/LC.git", "git@github.com:other/LC")]
    [InlineData(":owner/lc", "ghe.example.com/acme/lc", "ghe.example.com/acme/lc")]
    [InlineData("ghe.example.com/:owner/lc", "https://github.com/acme/lc", "ghe.example.com/acme/lc")]
    [InlineData("other/x", "https://github.com/acme/lc", "other/x")]
    [InlineData(":owner/other", "", ":owner/other")]
    public void InRepository_FillsPlaceholdersFromTheCurrentRepository(string repo, string current, string resolved)
    {
        Assert.Equal(resolved, PrCallScanner.InRepository(repo, current));
    }

    // R2-4: env -S and --split-string are a string wrapper.
    [Theory]
    [InlineData("env -S 'gh pr ready 12'")]
    [InlineData("env --split-string='gh pr ready 12'")]
    [InlineData("env --split-string 'gh pr ready 12'")]
    [InlineData("env -u X -S'gh pr create' --fill")]
    public void EnvSplitStringMakingAPrCall_IsAWrapper(string command)
    {
        Assert.Equal("env", Single(command).Wrapper);
    }

    [Theory]
    [InlineData("env -S 'gh pr view 12'")]
    [InlineData("env --split-string='git status'")]
    [InlineData("env -S")]
    public void EnvSplitStringMakingNoPrCall_HasNoCall(string command)
    {
        Assert.Empty(PrCallScanner.Find(command));
    }

    [Fact]
    public void EnvSplitStringArgv_IsAWrapper()
    {
        Assert.Equal("env", Assert.Single(PrCallScanner.FindInArgv(["env", "-S", "gh pr ready 12"])).Wrapper);
    }

    [Fact]
    public void EveryCallInALine_IsFound()
    {
        Assert.Equal(2, PrCallScanner.Find("gh pr create --head a && git push; gh pr ready b").Count);
    }
}
