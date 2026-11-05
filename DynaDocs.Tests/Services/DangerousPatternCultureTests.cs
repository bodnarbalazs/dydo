namespace DynaDocs.Tests.Services;

using System.Diagnostics;
using System.Text.Json.Nodes;
using DynaDocs.Tests.EndToEnd;
using DynaDocs.Utils;

// The NonBacktracking rules (DYD-610) are built when dydo first checks a command, and an IgnoreCase rule
// built then folds case the way the culture of that moment does unless it is culture-invariant. Turkish
// and Azeri pair I with dotless ı and i with dotted İ, so a rule spelled `ri` or `GIT` would let `RI` or
// `git` through. The source-generated rules they replaced fold case invariantly, fixed when dydo is built.
// Each case runs dydo guard in a fresh process whose culture a startup hook sets before Main, so before
// any rule exists. dydo's runtimeconfig turns startup hooks off, so the case runs with a copy that turns
// them on and changes nothing else.
[Collection("EndToEnd")]
public class DangerousPatternCultureTests(EndToEndFixture fixture) : IDisposable
{
    private const string StartupHooksSwitch = "System.StartupHookProvider.IsSupported";

    private readonly string _directory = Directory.CreateTempSubdirectory("dydo-culture-").FullName;

    // Commands each rule refuses with invariant case folding and that a Turkish fold of the rule misses:
    // the command's I or i is not the rule's.
    private static readonly string[] Commands =
    [
        "GIT PUSH --FORCE",
        "GIT REFLOG EXPIRE --EXPIRE=NOW",
        "DISKUTIL ERASEDISK",
        "GH API X -X DELETE",
        "GH REPO EDIT --VISIBILITY=PUBLIC",
        "KEEPASSXC-CLI",
        "OP SIGNIN",
        "SECURITY FIND-INTERNET-PASSWORD",
        "BITWARDEN.APP",
        "remove-item -recurse worktrees/x",
        "Remove-item -Recurse worktrees/x",
        "RI -Recurse worktrees/x",
    ];

    public static TheoryData<string, string> Cases()
    {
        var cases = new TheoryData<string, string>();
        foreach (var culture in new[] { "tr-TR", "az-Latn-AZ" })
            foreach (var command in Commands)
                cases.Add(culture, command);
        return cases;
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Guard_StartedInATurkishCasingCulture_RefusesTheCommand(string culture, string command)
    {
        var (exitCode, stderr) = await GuardInFreshProcess(culture, command);

        // No marker: the hook did not run, or it threw because the culture or its casing data is missing.
        Assert.True(stderr.Contains(StartupHook.CultureMarker + culture, StringComparison.Ordinal), $"culture not set:\n{stderr}");
        Assert.True(exitCode == ExitCodes.ToolError && stderr.Contains("BLOCKED: Dangerous command pattern detected.", StringComparison.Ordinal),
            $"{culture} allowed {command}: exit {exitCode}\n{stderr}");
    }

    private async Task<(int ExitCode, string Stderr)> GuardInFreshProcess(string culture, string command)
    {
        var dll = fixture.DllPath;
        var runtimeConfig = JsonNode.Parse(File.ReadAllText(Path.ChangeExtension(dll, ".runtimeconfig.json")))!;
        runtimeConfig["runtimeOptions"]!["configProperties"]![StartupHooksSwitch] = true;
        var runtimeConfigPath = Path.Combine(_directory, "dydo.runtimeconfig.json");
        File.WriteAllText(runtimeConfigPath, runtimeConfig.ToJsonString());

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "exec", "--runtimeconfig", runtimeConfigPath, "--depsfile", Path.ChangeExtension(dll, ".deps.json"),
                     dll, "guard", "--command", command })
            start.ArgumentList.Add(argument);
        start.Environment["DOTNET_STARTUP_HOOKS"] = typeof(StartupHook).Assembly.Location;
        start.Environment[StartupHook.CultureVariable] = culture;

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await stdout;
        await process.WaitForExitAsync();
        return (process.ExitCode, stderr);
    }
}
