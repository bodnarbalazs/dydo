namespace DynaDocs.Tests.Services;

using System.Text.RegularExpressions;
using DynaDocs.Services;

/// <summary>
/// DYD-499: the library-install warn nudge. The expectation below is the interim per-project copy
/// (dydo.json of dydo#223, LC#543, HRSI#6, TheGame#57), byte for byte, so the default never
/// duplicates it: <see cref="ConfigFactory.SameNudge"/> keys on the pattern.
/// </summary>
internal static class InstallNudgeExpectation
{
    public const string Pattern = """(?:^|[;&|\n(][ \t]*)(?:sudo[ \t]+(?:-\S+[ \t]+)*)?(?:env[ \t]+)?(?:[A-Za-z_][A-Za-z0-9_]*=[^\s;&|]*[ \t]+)*((?:pnpm|npm|yarn|bun)[ \t]+(?:(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])[ \t]+[^\s;&|<>]+[ \t]+|(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])=[^\s;&|<>]+[ \t]+|(?:-r|--recursive|-w|--workspace-root)[ \t]+)*(?:add|dlx|create|update|up|upgrade|link)\b|(?:pnpm|npm|yarn|bun)[ \t]+(?:(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])[ \t]+[^\s;&|<>]+[ \t]+|(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])=[^\s;&|<>]+[ \t]+|(?:-r|--recursive|-w|--workspace-root)[ \t]+)*(?:install|i)\b(?=(?:[ \t]+(?:(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])[ \t]+[^\s;&|<>]+|-(?!-?(?:filter|dir|prefix|workspace|reporter|config|registry)\b)(?![CFw](?:[ \t]|$))[^\s;&|<>]+))*[ \t]+(?![0-9]*[<>])[^-\s;&|<>()#\'"])|(?:npx|pnpx|bunx)[ \t]+(?:-y|--yes|-p|--package)\b|(?:[\w.~/-]*/)?uv[ \t]+(?:add|remove|pip[ \t]+install|tool[ \t]+install)\b|(?:[\w.~/-]*/)?uv[ \t]+run\b(?=[^\n;&|]*[ \t]--with\b)|(?:[\w.~/-]*/)?(?:uvx|pipx)\b|(?:[\w.~/-]*/)?(?:pip[0-9.]*[ \t]+install|(?:python[0-9.]*|py)[ \t]+-m[ \t]+pip[ \t]+install)\b|dotnet[ \t]+(?:add\b|tool[ \t]+(?:install|update)\b|workload[ \t]+install\b|new[ \t]+install\b)|(?:apt(?:-get)?[ \t]+(?:install|reinstall)|snap[ \t]+install|flatpak[ \t]+install|(?:cargo|go|gem|brew)[ \t]+install)\b)""";

    public const string Message = """Installing or adding a library or tool needs the user's permission ($1). Retry this exact command only if the user explicitly approved this install, in this conversation or in the approved plan/ticket you are executing that names it. Otherwise stop and ask the user. Restoring locked dependencies (pnpm install --frozen-lockfile, npm ci, uv sync --frozen, dotnet restore) needs no permission.""";
}

public class InstallNudgeTests
{
    private static readonly Regex Compiled = new(InstallNudgeExpectation.Pattern, RegexOptions.IgnoreCase);

    [Fact]
    public void DefaultNudges_CarryTheInstallNudge_ByteIdenticalToTheInterimCopy()
    {
        var nudge = Assert.Single(ConfigFactory.DefaultNudges, n => n.Pattern == InstallNudgeExpectation.Pattern);

        Assert.Equal(InstallNudgeExpectation.Message, nudge.Message);
        Assert.Equal("warn", nudge.Severity);
        Assert.True(nudge.IsCommandMatch);
        Assert.Equal("all", nudge.Audience);
        Assert.Null(nudge.Match);
    }

    [Fact]
    public void InstallNudge_SitsAfterThePrCreateNudge_AndBeforeTheDocsNotice()
    {
        var patterns = ConfigFactory.DefaultNudges.Select(n => n.Pattern).ToList();
        var install = patterns.IndexOf(InstallNudgeExpectation.Pattern);

        Assert.Equal(patterns.FindIndex(p => p.Contains(@"pr\s+create")) + 1, install);
        Assert.Equal(install + 1, patterns.IndexOf(ConfigFactory.DocsOutsideDydoNudge.Pattern));
    }

    [Fact]
    public void InstallNudge_IsExposedAsANamedStatic_AndIsTheDefaultEntry()
    {
        Assert.Equal(InstallNudgeExpectation.Pattern, ConfigFactory.InstallNudge.Pattern);
        Assert.Contains(ConfigFactory.InstallNudge, ConfigFactory.DefaultNudges);
    }

    [Theory]
    // installs and adds
    [InlineData("pnpm add left-pad")]
    [InlineData("pnpm add -D vitest")]
    [InlineData("pnpm -C client add x")]
    [InlineData("pnpm --filter web add x")]
    [InlineData("pnpm -r add x")]
    [InlineData("pnpm dlx create-vite")]
    [InlineData("pnpm create vite")]
    [InlineData("pnpm update")]
    [InlineData("pnpm up left-pad")]
    [InlineData("pnpm upgrade")]
    [InlineData("pnpm link ../x")]
    [InlineData("pnpm install left-pad")]
    [InlineData("pnpm i left-pad")]
    [InlineData("npm install left-pad")]
    [InlineData("npm install -D vitest")]
    [InlineData("npm i left-pad")]
    [InlineData("npm update")]
    [InlineData("yarn add left-pad")]
    [InlineData("bun add left-pad")]
    [InlineData("npx -y cowsay")]
    [InlineData("npx --yes cowsay")]
    [InlineData("pnpx -p x y")]
    [InlineData("uv add requests")]
    [InlineData("uv remove requests")]
    [InlineData("uv pip install requests")]
    [InlineData("uv tool install ruff")]
    [InlineData("uv run --with rich python x.py")]
    [InlineData("uvx ruff")]
    [InlineData("pipx install black")]
    [InlineData("pip install x")]
    [InlineData("pip3 install x")]
    [InlineData(".venv/bin/pip install x")]
    [InlineData("python -m pip install x")]
    [InlineData("python3.12 -m pip install x")]
    [InlineData("py -m pip install x")]
    [InlineData("tools/world/.venv/bin/python -m pip install x")]
    [InlineData("dotnet add package Newtonsoft.Json")]
    [InlineData("dotnet tool install -g dotnet-ef")]
    [InlineData("dotnet tool update -g dotnet-ef")]
    [InlineData("dotnet workload install wasm-tools")]
    [InlineData("dotnet new install Foo.Templates")]
    [InlineData("apt install x")]
    [InlineData("apt-get install x")]
    [InlineData("sudo apt install x")]
    [InlineData("snap install x")]
    [InlineData("flatpak install x")]
    [InlineData("cargo install ripgrep")]
    [InlineData("go install example.com/x@latest")]
    [InlineData("gem install rails")]
    [InlineData("brew install jq")]
    // behind a prefix, after another command, or in a chain
    [InlineData("env FOO=1 pip install x")]
    [InlineData("FOO=1 pip install x")]
    [InlineData("cd client && pnpm add x")]
    [InlineData("cd client; pnpm add x")]
    [InlineData("cd client;pnpm add x")]
    [InlineData("git pull | pnpm add x")]
    [InlineData("cd client\npnpm add x")]
    [InlineData("PNPM ADD x")]
    public void Pattern_MatchesAnInstall(string command)
    {
        Assert.Matches(Compiled, command);
    }

    [Theory]
    // restoring locked or declared dependencies needs no permission
    [InlineData("pnpm install")]
    [InlineData("npm install")]
    [InlineData("pnpm install --frozen-lockfile")]
    [InlineData("pnpm install --frozen-lockfile --dir client")]
    [InlineData("pnpm install --frozen-lockfile; pnpm test")]
    [InlineData("pnpm install 2>&1 | tail -5")]
    [InlineData("pnpm install > log.txt")]
    [InlineData("npm install 2>&1")]
    [InlineData("npm ci")]
    [InlineData("yarn install --frozen-lockfile")]
    [InlineData("bun install")]
    [InlineData("uv sync")]
    [InlineData("uv sync --frozen")]
    [InlineData("uv sync --locked")]
    [InlineData("uv run pytest")]
    [InlineData("dotnet restore")]
    [InlineData("dotnet tool restore")]
    [InlineData("cd client && pnpm install --frozen-lockfile")]
    // everything else
    [InlineData("pnpm test")]
    [InlineData("pnpm run build")]
    [InlineData("npm run install-hooks")]
    [InlineData("pip list")]
    [InlineData("pip freeze")]
    [InlineData("npm ls")]
    [InlineData("pnpm list")]
    [InlineData("uv pip list")]
    [InlineData("dotnet build")]
    [InlineData("dotnet test")]
    [InlineData("dotnet list package")]
    [InlineData("apt list --installed")]
    [InlineData("brew list")]
    [InlineData("cargo build")]
    [InlineData("go build ./...")]
    [InlineData("git commit -m 'pip install x'")]
    [InlineData("echo pnpm add x")]
    [InlineData("grep -r \"pip install\" docs")]
    public void Pattern_PassesARestoreOrAnythingElse(string command)
    {
        Assert.DoesNotMatch(Compiled, command);
    }
}
