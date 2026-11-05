namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Services.Statechart;

public class StatechartCommandTests
{
    [Fact]
    public void DoctorIsAnExplicitOfflineOperationWithoutAnApprovalFlag()
    {
        var command = StatechartCommand.Create();
        Assert.Equal("statechart", command.Name);
        Assert.Contains(command.Subcommands, child => child.Name == "doctor");
        Assert.DoesNotContain(command.Options, option => option.Name.Contains("approved", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task InstalledDoctorExtractsPayloadAndNeverClaimsChartReadinessOrInstallsPrerequisites()
    {
        using var scope = new StatechartTestDirectory();
        Skip.IfNot(OperatingSystem.IsWindows(), "statechart doctor is Windows-only.");
        var start = StatechartTestBinary.Start();
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.ArgumentList.Add("statechart");
        start.ArgumentList.Add("doctor");
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DYDO_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        start.Environment["DYDO_STATECHART_ASSET_ROOT"] = Path.Combine(scope.Path, "assets");
        start.Environment["DYDO_STATECHART_DEPENDENCY_ROOT"] = Path.Combine(scope.Path, "dependencies");
        start.Environment["DYDO_STATECHART_STATE_ROOT"] = Path.Combine(scope.Path, "state");
        start.Environment.Remove("JAVA");
        start.Environment.Remove("TLA2TOOLS");
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        Assert.Equal("", await errors);
        Assert.Equal(2, child.ExitCode);
        using var json = JsonDocument.Parse(await output);
        Assert.False(json.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal("diagnostic", json.RootElement.GetProperty("state").GetString());
        Assert.True(Directory.Exists(json.RootElement.GetProperty("payload").GetString()));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "dependencies")));
        Assert.All(Directory.GetFiles(Path.Combine(scope.Path, "state"), "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    // Native diagnostics still need platform validation outside Windows, so doctor declines before touching anything.
    [SkippableFact]
    public async Task DoctorDeclinesOutsideWindowsBeforeExtractingOrProbing()
    {
        Skip.If(OperatingSystem.IsWindows(), "Windows runs the native diagnosis.");
        using var scope = new StatechartTestDirectory();
        var result = await Run(scope.Path, "", "statechart", "doctor");
        Assert.Equal(2, result.Exit);
        Assert.Equal("Statechart diagnostics: Native statechart diagnostics on this host still require platform validation; " +
            "use the supported statechart skill entry.", result.Output.Trim());
        Assert.Equal("", result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scope.Path));
    }

    [Fact]
    public void TheDiagnosticReportNamesWhatWasFoundButNeverClaimsReadiness()
    {
        var assets = new StatechartAssets();
        using var report = JsonDocument.Parse(StatechartCommand.Report(assets, "/payload", null, "missing",
            "/runtime/java", "openjdk version \"21.0.1\"", "/checker/tla2tools.jar"));
        var root = report.RootElement;
        Assert.Equal(1, root.GetProperty("schema").GetInt32());
        Assert.Equal("diagnostic", root.GetProperty("state").GetString());
        Assert.False(root.GetProperty("ready").GetBoolean());
        Assert.Equal(["/payload", assets.Digest, assets.LockDigest],
            new[] { "payload", "payloadDigest", "lockDigest" }.Select(name => root.GetProperty(name).GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("node").ValueKind);
        Assert.Equal("missing", root.GetProperty("nodeVersion").GetString());
        Assert.False(root.GetProperty("nodeCompatible").GetBoolean());
        Assert.Equal("/runtime/java", root.GetProperty("java").GetString());
        Assert.True(root.GetProperty("javaCompatible").GetBoolean());
        Assert.Equal("/checker/tla2tools.jar", root.GetProperty("tlc").GetString());
        Assert.Equal("managed renderer readiness receipt required", root.GetProperty("renderer").GetString());
    }

    // A fake Node stands in for the embedded projection installer; the skill placement and owned probe are real.
    [SkippableFact]
    [UnsupportedOSPlatform("windows")]
    public async Task InstallSkillPlacesTheSkillThenRunsTheProjectionThroughAnOwnedProbe()
    {
        Skip.IfNot(OperatingSystem.IsLinux(), "POSIX ownership runs on Linux.");
        using var scope = new StatechartTestDirectory();
        var project = Directory.CreateDirectory(Path.Combine(scope.Path, "project")).FullName;
        var bin = Directory.CreateDirectory(Path.Combine(scope.Path, "bin")).FullName;
        var node = Path.Combine(bin, "node");
        File.WriteAllText(node, "#!/bin/sh\necho \"projected from $(pwd): $*\"\n");
        File.SetUnixFileMode(node, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var result = await Run(scope.Path, bin, "statechart", "install-skill", "--root", project);
        Assert.True(result.Exit == 0, result.Error);
        var assets = new StatechartAssets();
        var payload = Path.Combine(scope.Path, "assets", assets.Release, assets.Digest);
        Assert.Equal($"projected from {project}: {Path.Combine(payload, "setup-skills.mjs")} --root {project} --skill statechart",
            result.Output.Trim());
        var skill = Path.Combine(project, "skills", "engineering", "statechart");
        Assert.Equal(skill, StatechartSkill.Validate(skill, assets));
        Assert.All(Directory.GetFiles(Path.Combine(scope.Path, "state"), "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    [Fact]
    public async Task InstallSkillWithoutNodeKeepsThePlacedSkillAndNamesTheMissingInstaller()
    {
        using var scope = new StatechartTestDirectory();
        var project = Directory.CreateDirectory(Path.Combine(scope.Path, "project")).FullName;
        var result = await Run(scope.Path, "", "statechart", "install-skill", "--root", project);
        Assert.Equal(2, result.Exit);
        Assert.Equal("Statechart: No usable Node for the embedded project projection installer; statechart invocation can " +
            "prepare its locked prerequisite plan.", result.Error.Trim());
        Assert.True(File.Exists(Path.Combine(project, "skills", "engineering", "statechart", ".statechart-skill.json")));
    }

    [Theory]
    [InlineData("install-skill", "--root")]
    [InlineData("resume", "--request")]
    public async Task ASubcommandWithoutItsRequiredOptionChangesNothing(string subcommand, string option)
    {
        using var scope = new StatechartTestDirectory();
        var result = await Run(scope.Path, "", "statechart", subcommand);
        Assert.Equal(2, result.Exit);
        Assert.Equal($"Statechart: Option '{option}' is required.", result.Error.Trim());
        Assert.Equal("", result.Output);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scope.Path));
    }

    // The npm lifetime pipe closes while another run holds the payload extraction lock, so the wait ends in cancellation.
    [Fact]
    public async Task LosingTheNpmLifetimeCancelsAWaitingRunWithoutAPlan()
    {
        using var scope = new StatechartTestDirectory();
        File.WriteAllText(Path.Combine(scope.Path, "model.tla"), "---- MODULE model ----\n====\n");
        File.WriteAllText(Path.Combine(scope.Path, "model.cfg"), "\n");
        var release = Directory.CreateDirectory(Path.Combine(scope.Path, "assets", new StatechartAssets().Release)).FullName;
        using var held = new FileStream(Path.Combine(release, ".extract.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var start = Start(scope.Path, "", "statechart", "model.tla");
        start.RedirectStandardInput = true;
        start.Environment["DYDO_STATECHART_NPM_LIFETIME"] = "1";
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        child.StandardInput.Close();
        await child.WaitForExitAsync();
        Assert.Equal(130, child.ExitCode);
        Assert.Equal("Statechart cancelled; pending request retained.", (await errors).Trim());
        Assert.Equal("", await output);
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "state", "requests")));
    }

    private static ProcessStartInfo Start(string root, string path, params string[] arguments) =>
        StatechartOfflineProcess.Start(root, path, arguments);

    private static Task<(int Exit, string Output, string Error)> Run(string root, string path, params string[] arguments) =>
        StatechartOfflineProcess.Run(Start(root, path, arguments));
}
