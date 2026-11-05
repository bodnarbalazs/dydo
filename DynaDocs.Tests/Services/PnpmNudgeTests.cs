namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// DYD-553: the repository's pnpm warn nudges, driven through the guard's own block-then-warn order
/// (CheckBlockNudges, then CheckWarnNudges and its DYD-499 pass-through) from a dydo.json that holds
/// <see cref="PnpmNudgeExpectation.DydoJsonEntries"/> verbatim.
/// </summary>
[Collection("Integration")]
public sealed class PnpmNudgeTests : IDisposable
{
    private const string RetryHint = "Run the same command again to proceed anyway.";
    private static readonly ShellDialect[] Dialects = [ShellDialect.Unknown, ShellDialect.Posix];
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-pnpm-nudge-" + Guid.NewGuid().ToString("N")[..8]);

    public PnpmNudgeTests() => Directory.CreateDirectory(Path.Combine(_root, "dydo"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public void DydoJsonEntries_LoadAsTheTwoWarnNudges_AndNeitherIsAShippedDefault()
    {
        var env = Load(withInstallNudge: false);

        var npm = Assert.Single(env.Config!.Nudges, n => n.Pattern == PnpmNudgeExpectation.NpmPattern);
        var npx = Assert.Single(env.Config.Nudges, n => n.Pattern == PnpmNudgeExpectation.NpxPattern);
        Assert.Equal((PnpmNudgeExpectation.NpmMessage, "warn"), (npm.Message, npm.Severity));
        Assert.Equal((PnpmNudgeExpectation.NpxMessage, "warn"), (npx.Message, npx.Severity));
        Assert.True(npm.IsCommandMatch && npx.IsCommandMatch);
        var defaults = GuardCommand.MergeSystemNudges([]).Concat(ConfigFactory.DefaultNudges).Select(n => n.Pattern);
        Assert.DoesNotContain(PnpmNudgeExpectation.NpmPattern, defaults);
        Assert.DoesNotContain(PnpmNudgeExpectation.NpxPattern, defaults);
    }

    [Theory]
    [InlineData("npm install left-pad", "Use pnpm, not npm. Try: pnpm install left-pad")]
    [InlineData("npm i left-pad", "Use pnpm, not npm. Try: pnpm i left-pad")]
    [InlineData("npm i -D vitest", "Use pnpm, not npm. Try: pnpm i -D vitest")]
    [InlineData("npm install", "Use pnpm, not npm. Try: pnpm install")]
    [InlineData("git status && npm install left-pad", "Use pnpm, not npm. Try: pnpm install left-pad")]
    [InlineData("cd viewer\nnpm install", "Use pnpm, not npm. Try: pnpm install")]
    [InlineData("npx tsc --noEmit", "Use pnpm, not npx. Try: pnpm exec tsc --noEmit")]
    [InlineData("cd viewer; npx vitest run", "Use pnpm, not npx. Try: pnpm exec vitest run")]
    public void FirstAttempt_WarnsWithThePnpmEquivalent_AndTheExactRetryPasses(string command, string message)
    {
        var env = Load(withInstallNudge: false);

        var (first, firstStderr) = Run(command, env);
        var (retry, retryStderr) = Run(command, env);

        Assert.Equal(ExitCodes.ToolError, first);
        Assert.Contains($"BLOCKED: {message}\n", Normalized(firstStderr));
        Assert.Contains(RetryHint, firstStderr);
        Assert.Null(retry);
        Assert.Empty(retryStderr);
        Assert.Empty(Markers(env));
    }

    [Fact]
    public void ADifferentCommand_AfterAWarn_IsWarnedAgain()
    {
        var env = Load(withInstallNudge: false);
        Run("npm install left-pad", env);

        var (other, otherStderr) = Run("npm install right-pad", env);

        Assert.Equal(ExitCodes.ToolError, other);
        Assert.Contains("Try: pnpm install right-pad", otherStderr);
    }

    [Theory]
    [InlineData("npx dydo check")]
    [InlineData("npx --yes dydo check")]
    [InlineData("git status && npx dydo whoami")]
    public void NpxDydo_StillBlocksOnEveryAttempt_AndArmsNoPnpmMarker(string command)
    {
        var env = Load(withInstallNudge: false);

        foreach (var attempt in new[] { Run(command, env), Run(command, env) })
        {
            Assert.Equal(ExitCodes.ToolError, attempt.Result);
            Assert.Contains("BLOCKED: Don't use npx to run dydo", attempt.Stderr);
            Assert.DoesNotContain("Use pnpm", attempt.Stderr);
        }
        Assert.Empty(Markers(env));
    }

    [Theory]
    [InlineData("pnpm install")]
    [InlineData("pnpm install --frozen-lockfile")]
    [InlineData("pnpm -C DynaDocs.Tests/coverage install --frozen-lockfile")]
    [InlineData("pnpm exec tsc --noEmit")]
    [InlineData("pnpx tsc")]
    [InlineData("npm ci")]
    [InlineData("npm run build")]
    [InlineData("npm test")]
    [InlineData("npm publish --access public --provenance --tag latest")]
    [InlineData("npm init")]
    [InlineData("npm install-test")]
    [InlineData("npm ls -g dydo")]
    [InlineData("npx -y cowsay")]
    [InlineData("npx --yes cowsay")]
    [InlineData("echo npm install left-pad")]
    [InlineData("echo \"npm install left-pad\"")]
    [InlineData("rg -n 'text; npm install left-pad' docs")]
    [InlineData("grep -iE \"a; npx tsc\" notes.md")]
    [InlineData("echo ready # then; npm install left-pad")]
    [InlineData("echo ready # x; npx tsc")]
    [InlineData("cat > notes.md <<'EOF'\nthen; npm install left-pad\nnpx tsc\nEOF")]
    public void NonMatches_StaySilent_AndArmNoMarker(string command)
    {
        var env = Load(withInstallNudge: false);

        foreach (var shell in Dialects)
        {
            var (result, stderr) = Run(command, env, shell);

            Assert.Null(result);
            Assert.Empty(stderr);
        }
        Assert.Empty(Markers(env));
    }

    // The shipped install nudge (DYD-499) reaches a project only through its dydo.json. Beside it,
    // a plain install trips both warns as one block, and the exact retry spends both markers.
    [Fact]
    public void WithTheInstallNudge_APlainInstallWarnsOnceWithBothMessages_AndTheRetrySpendsBoth()
    {
        var env = Load(withInstallNudge: true);

        var (first, firstStderr) = Run("npm install left-pad", env);

        Assert.Equal(ExitCodes.ToolError, first);
        Assert.Contains("BLOCKED: Installing or adding a library or tool needs the user's permission (npm install).", firstStderr);
        Assert.Contains("BLOCKED: Use pnpm, not npm. Try: pnpm install left-pad", firstStderr);
        Assert.Equal(2, Markers(env).Count);

        var (retry, retryStderr) = Run("npm install left-pad", env);

        Assert.Null(retry);
        Assert.Empty(retryStderr);
        Assert.Empty(Markers(env));
    }

    [Theory]
    [InlineData("npm install", false, true)]
    [InlineData("npx tsc", false, true)]
    [InlineData("npx -y cowsay", true, false)]
    [InlineData("pnpm add left-pad", true, false)]
    [InlineData("npm ci", false, false)]
    [InlineData("pnpm install --frozen-lockfile", false, false)]
    public void WithTheInstallNudge_EachWarnSpeaksOnlyForItsOwnCommands(string command, bool install, bool pnpm)
    {
        var env = Load(withInstallNudge: true);

        var (result, stderr) = Run(command, env);

        Assert.Equal(install || pnpm ? (int?)ExitCodes.ToolError : null, result);
        Assert.Equal(install, stderr.Contains("needs the user's permission", StringComparison.Ordinal));
        Assert.Equal(pnpm, stderr.Contains("Use pnpm, not np", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryConfig_TheInstallNudgeReachesTheGuardOnlyThroughDydoJson()
    {
        var config = GuardCommand.GuardEnv.Load(RepositoryRoot()).Config!;

        var declared = config.Nudges.Any(n => n.Pattern == InstallNudgeExpectation.Pattern);
        var effective = GuardCommand.MergeSystemNudges(config.Nudges).Any(n => n.Pattern == InstallNudgeExpectation.Pattern);

        Assert.Equal(declared, effective);
    }

    private GuardCommand.GuardEnv Load(bool withInstallNudge)
    {
        var install = withInstallNudge
            ? $$"""{"pattern": {{JsonSerializer.Serialize(InstallNudgeExpectation.Pattern)}}, "message": {{JsonSerializer.Serialize(InstallNudgeExpectation.Message)}}, "severity": "warn"},"""
            : "";
        File.WriteAllText(Path.Combine(_root, "dydo.json"), $$"""
            {
              "version": 1,
              "structure": { "root": "dydo" },
              "nudges": [
                {{install}}
            {{PnpmNudgeExpectation.DydoJsonEntries}}
              ]
            }
            """);
        return GuardCommand.GuardEnv.Load(_root);
    }

    private static (int? Result, string Stderr) Run(string command, GuardCommand.GuardEnv env, ShellDialect shell = ShellDialect.Unknown)
    {
        var original = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        try
        {
            return (GuardCommand.CheckBlockNudges(command, env, shell) ?? GuardCommand.CheckWarnNudges(command, env, shell: shell),
                capture.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static List<string> Markers(GuardCommand.GuardEnv env) =>
        Directory.Exists(env.MarkerDir) ? Directory.EnumerateFiles(env.MarkerDir, ".nudge-*").ToList() : [];

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dydo.json")) && File.Exists(Path.Combine(directory.FullName, "DynaDocs.sln")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
