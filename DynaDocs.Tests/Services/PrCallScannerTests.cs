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

    // A substitution's command runs, so its PR call is a call: $(...), and a bash backtick substitution
    // anywhere a bash line expands it. Single quotes and PowerShell's backtick escape substitute nothing.
    [Theory]
    [InlineData("echo `gh pr create --fill`", "Posix", 1)]
    [InlineData("git commit -m \"`gh pr create --fill`\"", "Posix", 1)]
    [InlineData("echo \"$(gh pr ready 12)\"", "Posix", 1)]
    [InlineData("echo `bash -c 'gh pr create'`", "Posix", 1)]
    [InlineData("git commit -m '`gh pr create --fill`'", "Posix", 0)]
    [InlineData("Write-Host \"`gh pr create --fill`\"", "PowerShell", 0)]
    [InlineData("Write-Host `\"hi`\"", "PowerShell", 0)]
    public void Substitution_IsReadLikeADirectCall(string command, string dialect, int calls)
    {
        Assert.Equal(calls, PrCallScanner.Find(command, Enum.Parse<ShellDialect>(dialect)).Count);
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

    // GS-06: gh prints a command's help for --help or -h (gh 2.88.1, new as create's alias) and acts on nothing.
    [Theory]
    [InlineData("gh version; gh pr create --help; gh pr ready --help")]
    [InlineData("gh pr create --help")]
    [InlineData("gh pr new -h")]
    [InlineData("gh pr ready 12 -h")]
    [InlineData("gh pr create --title x --help")]
    [InlineData("gh pr create --help | Select-String draft")]
    public void HelpForAPrVerb_IsNoCall(string command)
    {
        Assert.Empty(PrCallScanner.Find(command));
    }

    [Theory]
    [InlineData("gh pr create --help; gh pr create --fill")]
    [InlineData("gh pr create --title --help")]
    [InlineData("gh pr create -t -h --fill")]
    [InlineData("gh pr create --fill -- --help")]
    [InlineData("gh pr ready -- -h")]
    public void HelpOnlyAsAValueOrAfterTheEndOfFlags_StaysACall(string command)
    {
        Assert.NotEmpty(PrCallScanner.Find(command));
    }

    // DYD-406 round 2: these are inert input strings, never shell programs.
    [Theory]
    [InlineData("x=\"$(case a in a) echo y;; esac)\"; gh pr create --title t --body '## Independent review'")]
    [InlineData("cat <<EOF\n$(case a in a) echo y;; esac)\nEOF\ngh pr create --title t --body '## Independent review'")]
    [InlineData("echo \"$(case a in a) echo y;; esac)\" && gh pr ready")]
    [InlineData("arch=\"$(case \"$(uname -m)\" in\n  x86_64) echo amd64;;\n  *) echo other;;\nesac)\"\ngh pr create --fill")]
    [InlineData("x=\"$(echo y)\"; gh pr create --title t --body '## Independent review'")]
    public void FailedReading_LaterPrCall_IsNotLost(string command)
    {
        Assert.NotEmpty(PrCallScanner.Find(command, ShellDialect.Posix));
    }

    [Theory]
    [InlineData("gh pr create --help; gh pr ready -h")]
    [InlineData("cat <<'EOF'\nx=\"$(case a in a) echo y;; esac)\"; gh pr ready\nEOF")]
    [InlineData("echo '$(case a in a) gh pr ready;; esac)'")]
    public void FailedReading_HelpAndLiteralControls_HaveNoCall(string command)
    {
        Assert.Empty(PrCallScanner.Find(command, ShellDialect.Posix));
    }

    [Theory]
    [InlineData("Posix")]
    [InlineData("Unknown")]
    public void FailedReading_TruncatedTarget_IsNeverACurrentTreeCall(string dialect)
    {
        var calls = PrCallScanner.Find(
            "gh pr create --title \"$(case a in a) echo t;; esac)\" --head topic --repo other/repo",
            Enum.Parse<ShellDialect>(dialect));

        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.True(call.Wrapper != null
            || (call.Branch == "topic" && call.Repo == "other/repo")));
    }

    [Theory]
    [InlineData("gh pr create --head topic", "Posix")]
    [InlineData("gh pr ready topic --repo other/repo", "Unknown")]
    [InlineData("GH_REPO=other/repo gh pr create", "Posix")]
    [InlineData("gh api repos/other/repo/pulls -f head=topic", "Posix")]
    [InlineData("gh api graphql -f query=createPullRequest", "Unknown")]
    public void FailedReading_LaterTargetOrApiCall_IsRefusedUnread(string suffix, string dialect)
    {
        var calls = PrCallScanner.Find("x=\"$(case a in a) echo y;; esac)\"; " + suffix,
            Enum.Parse<ShellDialect>(dialect), "inherited/repo");

        Assert.Contains(calls, call => call.Wrapper != null);
    }

    [Theory]
    [InlineData("gh pr create --title \"unfinished", "Posix")]
    [InlineData("Write-Host \"$(gh pr create", "PowerShell")]
    public void FailedReading_PartialCall_IsRefusedUnread(string command, string dialect)
    {
        var call = Assert.Single(PrCallScanner.Find(command, Enum.Parse<ShellDialect>(dialect)));

        Assert.NotNull(call.Wrapper);
    }

    [Theory]
    [InlineData(new object[] { new[] { "bash", "-lc", "x=\"$(case a in a) echo y;; esac)\"; gh pr ready" } })]
    [InlineData(new object[] { new[] { "node", "-e", "x=\"$(case a in a) echo y;; esac)\"; gh pr ready" } })]
    public void FailedReading_ArgvEnvelopeAndWrapper_AreRefusedUnread(string[] argv)
    {
        Assert.Contains(PrCallScanner.FindInArgv(argv), call => call.Wrapper != null);
    }

    // DYD-515: inert input strings, never shell programs. Each fails its reading after a child scope:
    // the bare case word under bash, the unpaired apostrophe under PowerShell.
    internal const string ArchIdiom = "arch=\"$(case \"$(uname -m)\" in\n  x86_64) echo amd64;;\n  *) echo other;;\nesac)\"\necho \"$arch\"";
    internal const string EchoCase = "echo \"$(echo case)\"";
    internal const string EscapedApostrophe = "v=$(date); echo it\\'s";
    // Fails both readings, so neither reads the gh call after it.
    internal const string FailsBothReadings = "v=$(date); echo it\\'s \"$(echo case)\"";

    [Theory]
    [InlineData(ArchIdiom, "Posix")]
    [InlineData(ArchIdiom, "Unknown")]
    [InlineData(EchoCase, "Posix")]
    [InlineData(EchoCase, "Unknown")]
    [InlineData(EscapedApostrophe, "Unknown")]
    [InlineData(EscapedApostrophe, "PowerShell")]
    [InlineData("bash -c '" + ArchIdiom + "'", "Unknown")]
    [InlineData("bash -c '" + EchoCase + "'", "PowerShell")]
    [InlineData("pwsh -Command 'v=$(date); echo it\\''s'", "PowerShell")]
    public void FailedReading_WithoutAGhWord_HasNoCall(string command, string dialect)
    {
        Assert.Empty(PrCallScanner.Find(command, Enum.Parse<ShellDialect>(dialect)));
    }

    [Theory]
    [InlineData(new object[] { new[] { "bash", "-lc", ArchIdiom } })]
    [InlineData(new object[] { new[] { "bash", "-lc", EchoCase } })]
    [InlineData(new object[] { new[] { "pwsh", "-Command", EscapedApostrophe } })]
    [InlineData(new object[] { new[] { "node", "-e", ArchIdiom } })]
    [InlineData(new object[] { new[] { "node", "-e", EchoCase } })]
    [InlineData(new object[] { new[] { "node", "-e", EscapedApostrophe } })]
    public void FailedReading_ArgvWithoutAGhWord_HasNoCall(string[] argv)
    {
        Assert.Empty(PrCallScanner.FindInArgv(argv));
    }

    // gh in any spelling, past the failure, inside a substitution or inside a wrapper's string.
    [Theory]
    [InlineData(ArchIdiom + "\n/usr/bin/gh pr create --fill", "Posix")]
    [InlineData(EchoCase + " && GH.EXE pr ready", "Posix")]
    [InlineData(EchoCase + " \"$(gh pr ready)\"", "Posix")]
    [InlineData(FailsBothReadings + "; gh pr ready", "Unknown")]
    [InlineData(EscapedApostrophe + "; C:\\tools\\gh.exe pr ready", "PowerShell")]
    [InlineData("bash -c '" + ArchIdiom + "\ngh pr ready'", "Unknown")]
    [InlineData("bash -c '" + EchoCase + "; Gh pr create --fill'", "PowerShell")]
    public void FailedReading_LaterGhWord_IsRefusedUnread(string command, string dialect)
    {
        Assert.Contains(PrCallScanner.Find(command, Enum.Parse<ShellDialect>(dialect)), call => call.Wrapper != null);
    }

    [Theory]
    [InlineData(new object[] { new[] { "bash", "-lc", EchoCase + "; gh pr ready" } })]
    [InlineData(new object[] { new[] { "pwsh", "-Command", EscapedApostrophe + "; gh pr ready" } })]
    [InlineData(new object[] { new[] { "node", "-e", FailsBothReadings + "; gh pr ready" } })]
    public void FailedReading_ArgvWithALaterGhWord_IsRefusedUnread(string[] argv)
    {
        Assert.Contains(PrCallScanner.FindInArgv(argv), call => call.Wrapper != null);
    }

    // An encoded command hides gh without its word; only pwsh or powershell reads one (DYD-515).
    internal const string EncodedGhPrReady = "ZwBoACAAcAByACAAcgBlAGEAZAB5AA==";

    [Theory]
    [InlineData(EchoCase + "; pwsh -EncodedCommand " + EncodedGhPrReady, "Posix")]
    [InlineData(ArchIdiom + "\n/usr/bin/PWSH.EXE -enc " + EncodedGhPrReady, "Posix")]
    [InlineData(EscapedApostrophe + "; powershell -ec " + EncodedGhPrReady, "PowerShell")]
    [InlineData("bash -c '" + EchoCase + "; pwsh -enc " + EncodedGhPrReady + "'", "Posix")]
    [InlineData("bash -c '" + EchoCase + "; powershell.exe -EncodedCommand " + EncodedGhPrReady + "'", "PowerShell")]
    public void FailedReading_LaterEncodedCommand_IsRefusedUnread(string command, string dialect)
    {
        Assert.Contains(PrCallScanner.Find(command, Enum.Parse<ShellDialect>(dialect)), call => call.Wrapper != null);
    }

    // DYD-641: pwsh 7.6.9 ends a statement and a comment at a bare carriage return, closes a string
    // at a typographic quote, and keeps a caret literal, so the PR call after each runs.
    [Theory]
    [InlineData("echo 'x'\rgh pr create --fill")]
    [InlineData("echo x # note\rgh pr create --fill")]
    [InlineData("echo ‘a'; gh pr create --fill; ’b'")]
    [InlineData("echo \"a”; gh pr create --fill; “b\"")]
    [InlineData("echo a^;gh pr create --fill")]
    public void PowerShellReading_FindsThePrCallPwshRuns(string command)
    {
        Assert.NotEmpty(PrCallScanner.Find(command, ShellDialect.PowerShell));
    }

    [Theory]
    [InlineData(new object[] { new[] { "bash", "-lc", EchoCase + "; pwsh -EncodedCommand " + EncodedGhPrReady } })]
    [InlineData(new object[] { new[] { "pwsh", "-Command", EscapedApostrophe + "; pwsh -ec " + EncodedGhPrReady } })]
    public void FailedReading_ArgvWithALaterEncodedCommand_IsRefusedUnread(string[] argv)
    {
        Assert.Contains(PrCallScanner.FindInArgv(argv), call => call.Wrapper != null);
    }

    // DYD-639: the body file of the gh pr create launched at an index, its last --body-file or -F as gh takes it.
    [Theory]
    [InlineData("gh pr create --body-file a.md", "a.md")]
    [InlineData("gh pr create --body-file=a.md", "a.md")]
    [InlineData("gh pr create -F a.md", "a.md")]
    [InlineData("gh pr create -Fa.md", "a.md")]
    [InlineData("gh pr create -dFa.md", "a.md")]
    [InlineData("gh pr create -F 'my body.md' --title t", "my body.md")]
    [InlineData("gh pr create -F a.md --body-file b.md", "b.md")]
    [InlineData("gh pr Create -R o/r -F a.md", "a.md")]
    [InlineData("gh pr create -F -", "-")]
    [InlineData("gh pr create -F \"dydo/agents/a b.md\"", "dydo/agents/a b.md")]
    public void BodyFileAt_IsTheLaunchedCallsLastBodyFile(string command, string file)
    {
        Assert.Equal(file, PrCallScanner.BodyFileAt(command, 0, ShellDialect.Posix));
    }

    [Theory]
    [InlineData("gh pr create --fill")]
    [InlineData("gh pr create --body 'see a.md'")]
    [InlineData("gh pr new -F a.md")]
    [InlineData("gh pr ready -F a.md")]
    [InlineData("gh pr create -F a.md --help")]
    [InlineData("gh pr")]
    [InlineData("gh api repos/o/r/pulls -F body=@a.md")]
    [InlineData("gh issue create -F a.md")]
    [InlineData("hub pr create -F a.md")]
    [InlineData("gh pr create -F a.md 'unfinished")]
    [InlineData("gh pr create -F a.md'")]
    [InlineData("GH_REPO=o/r")]
    [InlineData("gh pr create -R {owner}/r -F a.md")]
    public void CallReadingNoBodyFile_NamesNone(string command)
    {
        Assert.Null(PrCallScanner.BodyFileAt(command, 0, ShellDialect.Posix));
    }

    // R2-B1: a body-file operand holding anything the shell expands names no file the guard can
    // know, so it names none and the warning stays. An expansion in another word leaves it alone.
    [Theory]
    [InlineData("Posix", "gh pr create -F \"a.md$(printf .plain)\"")]
    [InlineData("Posix", "gh pr create -F \"a.md`printf .plain`\"")]
    [InlineData("Posix", "gh pr create -F a.md`printf .plain`")]
    [InlineData("Posix", "gh pr create -F \"a.md$SUFFIX\"")]
    [InlineData("Posix", "gh pr create -F $BODY")]
    [InlineData("Posix", "gh pr create -F \"${BODY}\"")]
    [InlineData("Posix", "gh pr create --body-file=\"$(echo a.md)\"")]
    [InlineData("Posix", "gh pr create -F\"$BODY\"")]
    [InlineData("Posix", "gh pr create -F ~/a.md")]
    [InlineData("Posix", "gh pr create -F a*.md")]
    [InlineData("Posix", "gh pr create -F a?.md")]
    [InlineData("Posix", "gh pr create -F a[12].md")]
    [InlineData("Posix", "gh pr create -F a{1,2}.md")]
    [InlineData("Posix", "gh pr create -F {a,b}.md")]
    [InlineData("Posix", "gh pr create -F a}b.md")]
    [InlineData("PowerShell", "gh pr create -F \"a$suffix.md\"")]
    [InlineData("PowerShell", "gh pr create -F $body")]
    [InlineData("PowerShell", "gh pr create -F \"a$(Get-Suffix).md\"")]
    [InlineData("PowerShell", "gh pr create -F (Get-Body)")]
    [InlineData("PowerShell", "gh pr create -F @body")]
    [InlineData("Unknown", "gh pr create -F \"a.md$(printf .plain)\"")]
    [InlineData("Posix", "gh pr create -F")]
    public void BodyFileWithAnExpansion_NamesNone(string dialect, string command)
    {
        Assert.Null(PrCallScanner.BodyFileAt(command, 0, Enum.Parse<ShellDialect>(dialect)));
    }

    // Ruling 7: an expansion in another word of the line, quoted or not, names none too.
    [Theory]
    [InlineData("gh pr create --title \"$(git log -1 --format=%s)\" -F a.md")]
    [InlineData("gh pr create -F a.md --title \"$TITLE\"")]
    [InlineData("gh pr create -F \"$OLD\" -F a.md")]
    public void ExpansionOutsideTheBodyFileOperand_NamesNone(string command)
    {
        Assert.Null(PrCallScanner.BodyFileAt(command, 0, ShellDialect.Posix));
    }

    // An unknown shell reads the call both ways: one file when they agree, none when they differ.
    [Theory]
    [InlineData("Unknown", @"gh pr create -F a.md", "a.md")]
    [InlineData("PowerShell", @"gh pr create -F a\ b.md", @"a\")]
    [InlineData("Posix", @"gh pr create -F a\ b.md", null)]
    [InlineData("Unknown", @"gh pr create -F a\ b.md", null)]
    public void BodyFileAt_HoldsOnlyWhereEveryReadingAgrees(string dialect, string command, string? file)
    {
        Assert.Equal(file, PrCallScanner.BodyFileAt(command, 0, Enum.Parse<ShellDialect>(dialect)));
    }

    // R3-B3, R3-S1: an operand is read only as written plainly, bare or wholly in one pair of quotes,
    // of A-Z a-z 0-9 . _ - + / and, quoted, space, between blanks or separators. PowerShell also
    // takes \ and a leading drive; bash keeps no backslash.
    [Theory]
    [InlineData("Posix", "gh pr create -F 'a b.md'", "a b.md")]
    [InlineData("Posix", "gh pr create -F \"a+b_c-d.md\" --title t", "a+b_c-d.md")]
    [InlineData("Posix", "gh pr create -dF'a b.md'", "a b.md")]
    [InlineData("Posix", "gh pr create -F\ta.md \n", "a.md")]
    [InlineData("Posix", "gh pr create -F\ta.md\necho done", null)]
    [InlineData("Posix", "gh pr create -F a.md|cat", null)]
    [InlineData("Posix", "gh pr create -F a.md\\", null)]
    [InlineData("Posix", "gh pr create -F a\\\r\n.md", null)]
    [InlineData("Posix", "gh pr create -F a.md\r\n", null)]
    [InlineData("Posix", "gh pr create -F a.md\u00a0x", null)]
    [InlineData("Posix", "gh pr create -F\u00a0a.md", null)]
    [InlineData("Posix", "gh pr create -F 'a.md{repo}'", null)]
    [InlineData("Posix", "gh pr create -F 'a.md'x", null)]
    [InlineData("Posix", "gh pr create -F a'.md'", null)]
    [InlineData("Posix", "gh pr create '-Fa.md'", null)]
    [InlineData("Posix", "gh pr create -F 'a\\b.md'", null)]
    [InlineData("Posix", "gh pr create -F a:b.md", null)]
    [InlineData("Posix", "gh pr create -F=a.md", null)]
    [InlineData("Posix", "gh pr create -F==a.md", null)]
    [InlineData("Posix", "gh pr create --body-file==a.md", null)]
    [InlineData("Posix", @"gh pr create -F C:\pr\body.md", null)]
    [InlineData("PowerShell", @"gh pr create -F C:\pr\body.md", @"C:\pr\body.md")]
    [InlineData("PowerShell", "gh pr create -F 'C:/pr/a b.md'", "C:/pr/a b.md")]
    [InlineData("PowerShell", "gh pr create -F C:body.md", null)]
    [InlineData("PowerShell", @"gh pr create -F pr\C:\body.md", null)]
    [InlineData("Unknown", @"gh pr create -F 'C:\pr\body.md'", null)]
    public void BodyFileAt_HoldsOnlyAPlainlyWrittenOperand(string dialect, string command, string? file)
    {
        Assert.Equal(file, PrCallScanner.BodyFileAt(command, 0, Enum.Parse<ShellDialect>(dialect)));
    }

    // R3-B1, ruling 7: a command before the call may move the shell's directory, so a relative body
    // file after any earlier command names none.
    [Theory]
    [InlineData("Posix", "dydo pr-ready --body a.md && gh pr create --body-file a.md")]
    [InlineData("Posix", "git status --short && echo ok; printf x | true || test -f a.md && gh pr create -F a.md")]
    [InlineData("Posix", "/usr/bin/git status\ngit push; gh pr create -F a.md")]
    [InlineData("Posix", "echo \"$(git rev-parse HEAD)\"; gh pr create -F a.md")]
    [InlineData("Posix", "cd sub && gh pr create -F a.md")]
    [InlineData("Posix", "git status && cd sub && gh pr create -F a.md")]
    [InlineData("Posix", "pushd sub; gh pr create -F a.md")]
    [InlineData("Posix", "popd; gh pr create -F a.md")]
    [InlineData("Posix", "source env.sh; gh pr create -F a.md")]
    [InlineData("Posix", ". env.sh; gh pr create -F a.md")]
    [InlineData("Posix", "eval cd sub; gh pr create -F a.md")]
    [InlineData("Posix", "exec 3<a.md; gh pr create -F a.md")]
    [InlineData("Posix", "f() { true; }; f; gh pr create -F a.md")]
    [InlineData("Posix", "(git status) && gh pr create -F a.md")]
    [InlineData("Posix", "{ git status; } && gh pr create -F a.md")]
    [InlineData("Posix", "git status > log.txt && gh pr create -F a.md")]
    [InlineData("Posix", "# note\ngit status; gh pr create -F a.md")]
    [InlineData("Posix", "GIT_DIR=x git status; gh pr create -F a.md")]
    [InlineData("Posix", "> log.txt; gh pr create -F a.md")]
    [InlineData("Posix", "echo \"$(cd sub)\"; gh pr create -F a.md")]
    [InlineData("Posix", "echo ${ cd sub; }; gh pr create -F a.md")]
    [InlineData("Posix", "echo ${| cd sub; }; gh pr create -F a.md")]
    [InlineData("Posix", "gh pr create --title \"$(cd sub; pwd)\" -F a.md")]
    [InlineData("PowerShell", "git status; gh pr create -F a.md")]
    [InlineData("PowerShell", "git status; Set-Location sub; gh pr create -F a.md")]
    [InlineData("PowerShell", "sl sub; gh pr create -F a.md")]
    [InlineData("PowerShell", "Push-Location sub; gh pr create -F a.md")]
    [InlineData("PowerShell", "echo (Set-Location sub); gh pr create -F a.md")]
    public void RelativeBodyFileAfterAnEarlierCommand_NamesNone(string dialect, string command)
    {
        var launch = command.LastIndexOf("gh pr create", StringComparison.Ordinal);

        Assert.Null(PrCallScanner.BodyFileAt(command, launch, Enum.Parse<ShellDialect>(dialect)));
    }

    // Only a call the line runs at its top level, starting at the launch, names a body file.
    [Theory]
    [InlineData("x gh pr create -F a.md", 2)]
    [InlineData("echo $(gh pr create -F a.md)", 7)]
    [InlineData("echo 'x; gh pr create -F a.md'", 9)]
    public void LaunchStartingNoTopLevelCommand_NamesNone(string command, int launch)
    {
        Assert.Null(PrCallScanner.BodyFileAt(command, launch, ShellDialect.Posix));
    }

    // Ruling 7: an absolute body file is read, as a relative one is, only for a lone call.
    [Theory]
    [InlineData("gh pr create -F {0}", true)]
    [InlineData("git status && gh pr create -F {0}", false)]
    [InlineData("cd sub && gh pr create -F {0}", false)]
    [InlineData("printf x > {0} && gh pr create -F {0}", false)]
    public void AbsoluteBodyFile_IsReadOnlyForALoneCall(string template, bool read)
    {
        var (dialect, file) = OperatingSystem.IsWindows() ? (ShellDialect.PowerShell, @"C:\pr\a.md") : (ShellDialect.Posix, "/pr/a.md");
        var command = string.Format(template, file);

        Assert.Equal(read ? file : null, PrCallScanner.BodyFileAt(command, command.IndexOf("gh", StringComparison.Ordinal), dialect));
    }

    // On Windows a path rooted at no drive is opened on the shell's current drive, so it is not read there.
    [Fact]
    public void BodyFileRootedAtNoDrive_IsReadOnlyWhereThatIsAbsolute()
    {
        Assert.Equal(OperatingSystem.IsWindows() ? null : "/pr/a.md", PrCallScanner.BodyFileAt("gh pr create -F /pr/a.md", 0, ShellDialect.Posix));
    }

    // Addendum 5: gh takes the last body file it is given, so a word of the same call that the shell
    // may expand into more arguments, such as $EXTRA holding "-F b.md", may name another file. bash
    // splits an unquoted expansion and expands a glob, brace or tilde; "$@" and an array's [@] or [*]
    // expand in double quotes too. PowerShell passes an array variable, a splat, a group or a comma
    // list as many arguments, and --% hands on the rest of the line unread.
    [Theory]
    [InlineData("Posix", "gh pr create -F a.md $EXTRA")]
    [InlineData("Posix", "gh pr create $EXTRA -F a.md")]
    [InlineData("Posix", "gh pr create -F a.md ${EXTRA}")]
    [InlineData("Posix", "gh pr create -F a.md --title=$TITLE")]
    [InlineData("Posix", "gh pr create -F a.md $(printf -- '-F b.md')")]
    [InlineData("Posix", "gh pr create -F a.md `printf -- '-F b.md'`")]
    [InlineData("Posix", "gh pr create -F a.md \"$@\"")]
    [InlineData("Posix", "gh pr create -F a.md \"${@}\"")]
    [InlineData("Posix", "gh pr create -F a.md \"${EXTRA[@]}\"")]
    [InlineData("Posix", "gh pr create -F a.md \"${EXTRA[*]}\"")]
    [InlineData("Posix", "gh pr create -F a.md \"${!EXTRA@}\"")]
    [InlineData("Posix", "gh pr create -F a.md -? b.md")]
    [InlineData("Posix", "gh pr create -F a.md [-]F b.md")]
    [InlineData("Posix", "gh pr create -F a.md *")]
    [InlineData("Posix", "gh pr create -F a.md {-F,b.md}")]
    [InlineData("Posix", "gh pr create -F a.md ~")]
    [InlineData("Posix", "gh pr create -F a.md --title x\\\"$EXTRA\\\"")]
    [InlineData("Posix", "gh pr create -F a.md >&2 $EXTRA")]
    [InlineData("PowerShell", "gh pr create -F a.md $extra")]
    [InlineData("PowerShell", "gh pr create -F a.md @extra")]
    [InlineData("PowerShell", "gh pr create -F a.md \"$extra\"")]
    [InlineData("PowerShell", "gh pr create -F a.md “$extra”")]
    [InlineData("PowerShell", "gh pr create -F a.md (echo '-F' 'b.md')")]
    [InlineData("PowerShell", "gh pr create -F a.md x,-F,b.md")]
    [InlineData("PowerShell", "gh pr create -F a.md --% %EXTRA%")]
    [InlineData("Unknown", "gh pr create -F a.md --title \"$TITLE\"")]
    public void WordThatMayExpandIntoMoreArguments_NamesNone(string dialect, string command)
    {
        Assert.Null(PrCallScanner.BodyFileAt(command, 0, Enum.Parse<ShellDialect>(dialect)));
    }

    // Quoted text that cannot expand stays read: bash's single quotes, and its double quotes around
    // literal glob, brace and tilde characters; PowerShell's single quotes, and double quotes holding
    // no $ or @.
    [Theory]
    [InlineData("Posix", "gh pr create --title 'Fix $HOME, * {a,b} ~ \"x' -F a.md")]
    [InlineData("Posix", "gh pr create --title \"Why? [WIP] {a,b} * ~ it's\" -F a.md")]
    [InlineData("PowerShell", "gh pr create --title 'Fix $x @y (z), {w} \"q' -F a.md")]
    [InlineData("PowerShell", "gh pr create --title \"Fix (z), {w} it's\" -F a.md")]
    [InlineData("Unknown", "gh pr create --title 'Fix: a [WIP] thing?' -F a.md")]
    public void WordThatCannotAddArguments_LeavesTheBodyFileRead(string dialect, string command)
    {
        Assert.Equal("a.md", PrCallScanner.BodyFileAt(command, 0, Enum.Parse<ShellDialect>(dialect)));
    }

    // Ruling 7: the guard reads the body file as it stands when the hook runs, and a command before
    // the call, even one that only reads, may change it first, as git diff does given --output in
    // an exported variable. A body file after any earlier command names none.
    [Theory]
    [InlineData("Posix", "git diff --stat; git log -1 --format=%H; git show HEAD; git rev-parse HEAD; gh pr create -F a.md")]
    [InlineData("Posix", "printf '%s' x; test -f a.md; true; echo -n; gh pr create -F a.md")]
    [InlineData("Posix", "dydo pr-ready --body a.md 2>&1 && gh pr create -F a.md")]
    [InlineData("Posix", "git status < /dev/null && gh pr create -F a.md")]
    [InlineData("Posix", "echo \"$(git log -1 --format=%s)\" && gh pr create -F a.md")]
    [InlineData("Posix", "true & git status ||\ngh pr create -F a.md")]
    [InlineData("Posix", "git status & gh pr create -F a.md")]
    [InlineData("Posix", "git status\n# note\ngh pr create -F a.md")]
    [InlineData("Posix", "git status <<EOF --short && gh pr create -F a.md\nx\nEOF")]
    [InlineData("Posix", "git switch plain && gh pr create -F a.md")]
    [InlineData("Posix", "git checkout x && gh pr create -F a.md")]
    [InlineData("Posix", "git -C x status && gh pr create -F a.md")]
    [InlineData("Posix", "git stash pop && gh pr create -F a.md")]
    [InlineData("Posix", "git diff --output=a.md && gh pr create -F a.md")]
    [InlineData("Posix", "git log --output a.md && gh pr create -F a.md")]
    [InlineData("Posix", "git && gh pr create -F a.md")]
    [InlineData("Posix", "dydo init && gh pr create -F a.md")]
    [InlineData("Posix", "printf -v 'x[$(rm a.md)]' y && gh pr create -F a.md")]
    [InlineData("Posix", "test -v 'x[$(rm a.md)]' && gh pr create -F a.md")]
    [InlineData("Posix", "/usr/bin/git status && gh pr create -F a.md")]
    [InlineData("Posix", "./echo x; gh pr create -F a.md")]
    [InlineData("Posix", "gh pr create -F b.md; gh pr create -F a.md")]
    [InlineData("Posix", "git status > log && gh pr create -F a.md")]
    [InlineData("Posix", "echo \"$(git checkout x)\"; gh pr create -F a.md")]
    [InlineData("Posix", "echo \"$(git status > a.md)\"; gh pr create -F a.md")]
    [InlineData("Posix", "echo \"`git checkout x`\"; gh pr create -F a.md")]
    [InlineData("Posix", "echo ${x:-$(git checkout x)}; gh pr create -F a.md")]
    [InlineData("Posix", "echo $'\\'' ; git checkout x ; echo \\' ; gh pr create -F a.md")]
    [InlineData("Posix", "echo <<EOF; gh pr create -F a.md; echo\n$(git checkout x)\nEOF")]
    [InlineData("PowerShell", "git status; echo ok; gh pr create -F a.md")]
    [InlineData("PowerShell", "echo $x.Delete(); gh pr create -F a.md")]
    [InlineData("PowerShell", "Set-Content a.md x; gh pr create -F a.md")]
    public void BodyFileAfterAnEarlierCommand_NamesNone(string dialect, string command)
    {
        var launch = command.LastIndexOf("gh pr create", StringComparison.Ordinal);

        Assert.Null(PrCallScanner.BodyFileAt(command, launch, Enum.Parse<ShellDialect>(dialect)));
    }

    // Ruling 7: nothing may follow the call, in its pipeline, its list or the background, and it
    // takes no redirection or heredoc of any kind, so a body file there names none.
    [Theory]
    [InlineData("Posix", "gh pr create -F a.md 2>&1 | cat")]
    [InlineData("Posix", "gh pr create -F a.md | head -5 | tail -1 | grep -v x")]
    [InlineData("Posix", "gh pr create -F a.md < /dev/null >&2 0<&-")]
    [InlineData("Posix", "gh pr create -F a.md |\n cat && echo done || true; git checkout x > a.md")]
    [InlineData("Posix", "gh pr create -F a.md || echo failed")]
    [InlineData("Posix", "gh pr create -F a.md && echo 'x' 2>&1 <&0 &>/dev/null")]
    [InlineData("Posix", "gh pr create -F a.md | grep \"$(git log -1 --format=%s)\"")]
    [InlineData("Posix", "git status && gh pr create -F a.md > a.md")]
    [InlineData("Posix", "gh pr create <<EOF -F a.md\nx\nEOF")]
    [InlineData("Posix", "gh pr create -F a.md > a.md")]
    [InlineData("Posix", "gh pr create -F a.md >> a.md")]
    [InlineData("Posix", "gh pr create -F a.md &> a.md")]
    [InlineData("Posix", "gh pr create -F a.md 2>a.md")]
    [InlineData("Posix", "gh pr create -F a.md >| a.md")]
    [InlineData("Posix", "gh pr create -F a.md <> a.md")]
    [InlineData("Posix", "gh pr create -F a.md >&a.md")]
    [InlineData("Posix", "gh pr create -F a.md 2>/dev/null")]
    [InlineData("Posix", "gh pr create -F a.md <<EOF\nx\nEOF")]
    [InlineData("Posix", "gh pr create -F a.md & git status")]
    [InlineData("Posix", "gh pr create -F a.md &")]
    [InlineData("Posix", "gh pr create -F a.md && true & git status")]
    [InlineData("Posix", "gh pr create -F a.md | tee a.md")]
    [InlineData("Posix", "gh pr create -F a.md | cat > a.md")]
    [InlineData("Posix", "gh pr create -F a.md | cat | tee a.md")]
    [InlineData("Posix", "gh pr create -F a.md | grep \"$(git checkout x)\"")]
    [InlineData("Posix", "gh pr create -F a.md | cat <(git checkout x)")]
    [InlineData("Posix", "gh pr create -F a.md >(tee a.md)")]
    [InlineData("Posix", "gh pr create -F a.md |& cat")]
    [InlineData("Posix", "gh pr create -F a.md | > a.md")]
    [InlineData("Posix", "echo x | gh pr create -F a.md")]
    [InlineData("PowerShell", "gh pr create -F a.md 2>&1 | cat")]
    [InlineData("PowerShell", "gh pr create -F a.md > a.md")]
    [InlineData("PowerShell", "gh pr create -F a.md *> a.md")]
    [InlineData("PowerShell", "gh pr create -F a.md | Out-File a.md")]
    [InlineData("PowerShell", "gh pr create -F a.md | Set-Content a.md")]
    [InlineData("PowerShell", "gh pr create -F a.md | Tee-Object a.md")]
    [InlineData("PowerShell", "gh pr create -F a.md &")]
    public void BodyFileBesideALaterCommandOrARedirection_NamesNone(string dialect, string command)
    {
        var launch = command.IndexOf("gh pr create", StringComparison.Ordinal);

        Assert.Null(PrCallScanner.BodyFileAt(command, launch, Enum.Parse<ShellDialect>(dialect)));
    }

    // Ruling 7: the body file is read only when gh pr create is the line's one command. Anything
    // joined before or after it, a redirection, an expansion or an assignment names no file.
    [Theory]
    [InlineData("Posix", "dydo pr-ready --body a.md && gh pr create --title t -F a.md")]
    [InlineData("Posix", "git status && gh pr create -F a.md")]
    [InlineData("Posix", "echo x; gh pr create -F a.md")]
    [InlineData("Posix", "gh pr create -F a.md 2>&1 | cat")]
    [InlineData("Posix", "gh pr create -F a.md < /dev/null")]
    [InlineData("Posix", "gh pr create -F a.md && echo done")]
    [InlineData("Posix", "gh pr create -F a.md; true")]
    [InlineData("Posix", "gh pr create --title \"$TITLE\" -F a.md")]
    [InlineData("Posix", "FOO=1 gh pr create -F a.md")]
    [InlineData("Posix", "echo \"$(git diff $DIFF_OPTIONS)\" && gh pr create --title \"DYD-1 fix: thing\" -F dydo/agents/body.md")]
    [InlineData("Posix", "git diff \"$DIFF_OPTION\" && gh pr create --title t -F dydo/agents/body.md")]
    [InlineData("Posix", "gh pr create -F a.md; gh pr create -F b.md")]
    [InlineData("Posix", "echo -F a.md; gh pr create -F b.md && echo done")]
    [InlineData("Posix", "gh pr create --title \"$(git log -1 --format=\"%s\")\" -F a.md")]
    [InlineData("Posix", "gh pr create -F a.md --title t 2>&1 | cat")]
    [InlineData("Posix", "gh pr create -F a.md 2>&1")]
    [InlineData("Posix", "gh pr create -F a.md <<< x")]
    [InlineData("Posix", "gh pr create -F a.md # note")]
    [InlineData("Posix", "(gh pr create -F a.md)")]
    [InlineData("Posix", "{ gh pr create -F a.md; }")]
    [InlineData("PowerShell", "git status; gh pr create -F a.md")]
    [InlineData("PowerShell", "gh pr create -F a.md 2>&1 | cat")]
    public void BodyFile_IsReadOnlyForALoneCall(string dialect, string command)
    {
        var launch = command.IndexOf("gh pr create", StringComparison.Ordinal);

        Assert.Null(PrCallScanner.BodyFileAt(command, launch, Enum.Parse<ShellDialect>(dialect)));
    }
}
