namespace DynaDocs.Tests.Statechart;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Services.Statechart;

public class StatechartReadyTests
{
    private static readonly string Relative = Path.Combine(new StatechartAssets().LockDigest, RuntimeInformation.RuntimeIdentifier);

    // Reads the verified fixture at its stable runtime path; only the other cases need a private copy.
    [SkippableFact]
    public async Task InstalledReadyInvocationRunsOfflineWithoutTouchingTheInstall()
    {
        using var scope = new StatechartTestDirectory();
        var fixture = Fixture();
        var before = Content(fixture);
        var result = await RunReadyModel(scope.Path, fixture);
        Assert.Equal(1, result.Exit);
        Assert.Equal("", result.Error);
        Assert.Contains("Bound breaks", result.Output);
        Assert.Contains("shortest path has 2 steps", result.Output);
        Assert.Contains("bugPath", File.ReadAllText(Path.Combine(scope.Path, "chart", "chart.svg")));
        Assert.All(Directory.GetFiles(Path.Combine(scope.Path, "state"), "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
        // A fixture verified by an earlier build is re-stamped once; its content never changes.
        Assert.Equal(before, Content(fixture));
        AssertCurrentReceipts(fixture);
    }

    [SkippableFact]
    public async Task ChangedManagedBytesYieldARepairPlanAndStayInPlace()
    {
        using var scope = new StatechartTestDirectory();
        var dependencies = PrivateCopy(Fixture(), scope.Path);
        var renderer = Path.Combine(dependencies, Relative, "pele", "content", "package", "dist", "pele.js");
        File.AppendAllText(renderer, "\nchanged managed content");
        var before = Files(dependencies);
        var result = await RunReadyModel(scope.Path, dependencies);
        Assert.Equal(3, result.Exit);
        using var plan = JsonDocument.Parse(result.Output);
        Assert.Equal("approval-required", plan.RootElement.GetProperty("state").GetString());
        Assert.Equal(["pele"], plan.RootElement.GetProperty("repair").EnumerateArray().Select(item => item.GetString()));
        Assert.False(Directory.Exists(Path.Combine(scope.Path, "chart")));
        Assert.EndsWith("changed managed content", File.ReadAllText(renderer));
        Assert.Equal(before, Files(dependencies));
    }

    [SkippableFact]
    public async Task AnUpdateThatKeepsTheLockReverifiesAndRestampsWithoutInstalling()
    {
        using var scope = new StatechartTestDirectory();
        var dependencies = PrivateCopy(Fixture(), scope.Path);
        var receipts = Directory.GetFiles(Path.Combine(dependencies, Relative), ".ready.json", SearchOption.AllDirectories);
        Assert.Equal(4, receipts.Length);
        foreach (var receipt in receipts)
        {
            var identity = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
            identity["release"] = "0.0.1";
            identity["payload"] = "earlier-payload";
            File.WriteAllText(receipt, identity.ToJsonString());
        }
        var before = Content(dependencies);
        var result = await RunReadyModel(scope.Path, dependencies);
        Assert.True(result.Exit == 1, result.Error);
        Assert.DoesNotContain("approval-required", result.Output);
        Assert.Contains("Bound breaks", result.Output);
        AssertCurrentReceipts(dependencies);
        Assert.Equal(before, Content(dependencies));
    }

    [SkippableFact]
    public async Task AnUnchangedSkillFromAnEarlierReleaseUpdatesThroughInstallSkillAndCharts()
    {
        using var scope = new StatechartTestDirectory();
        var fixture = Fixture();
        var project = Path.Combine(scope.Path, "project");
        Directory.CreateDirectory(project);
        var installed = await Run(scope.Path, fixture, ["statechart", "install-skill", "--root", project]);
        Assert.True(installed.Exit == 0, installed.Error);
        var skill = Path.Combine(project, "skills", "engineering", "statechart");
        File.AppendAllText(Path.Combine(skill, "SKILL.md"), "\nEarlier release wording.");
        var receipt = new JsonObject { ["schema"] = 1, ["protocol"] = 1, ["release"] = "0.0.1", ["payload"] = "earlier-payload",
            ["lock"] = "earlier-lock", ["files"] = StatechartInventory.Capture(skill, ".statechart-skill.json") };
        File.WriteAllText(Path.Combine(skill, ".statechart-skill.json"), receipt.ToJsonString());
        var stranded = await RunReadyModel(scope.Path, fixture, "--skill", skill);
        Assert.Equal(2, stranded.Exit);
        Assert.Contains("dydo statechart install-skill --root " + project, stranded.Error);
        var updated = await Run(scope.Path, fixture, ["statechart", "install-skill", "--root", project]);
        Assert.True(updated.Exit == 0, updated.Error);
        var result = await RunReadyModel(scope.Path, fixture, "--skill", skill);
        Assert.True(result.Exit == 1, result.Error);
        Assert.Contains("Bound breaks", result.Output);
        Assert.DoesNotContain("Earlier release wording", File.ReadAllText(Path.Combine(skill, "SKILL.md")));
    }

    private static string[] Content(string dependencies) => Files(Path.Combine(dependencies, Relative))
        .Where(file => !file.Contains(".ready.json", StringComparison.Ordinal)).ToArray();

    private static void AssertCurrentReceipts(string dependencies)
    {
        var assets = new StatechartAssets();
        var receipts = Directory.GetFiles(Path.Combine(dependencies, Relative), ".ready.json", SearchOption.AllDirectories);
        Assert.Equal(4, receipts.Length);
        Assert.All(receipts, receipt =>
        {
            var identity = JsonNode.Parse(File.ReadAllText(receipt))!.AsObject();
            Assert.Equal(assets.Release, identity["release"]!.GetValue<string>());
            Assert.Equal(assets.Digest, identity["payload"]!.GetValue<string>());
        });
    }

    private static string Fixture()
    {
        var fixture = Environment.GetEnvironmentVariable("STATECHART_TEST_DEPENDENCY_ROOT");
        Skip.If(fixture is null, "STATECHART_TEST_DEPENDENCY_ROOT must name a real verified managed installation (Node, Java, TLC, Pele).");
        Assert.True(Path.IsPathFullyQualified(fixture) && Directory.Exists(fixture),
            "STATECHART_TEST_DEPENDENCY_ROOT must name the real task-local verified installation; no fake ready fixture.");
        return fixture;
    }

    private static string PrivateCopy(string fixture, string scope)
    {
        var dependencies = Path.Combine(scope, "dependencies");
        Copy(Path.Combine(fixture, Relative), Path.Combine(dependencies, Relative));
        return dependencies;
    }

    // Runs the built dydo offline on a model whose Bound breaks after two steps.
    private static Task<(int Exit, string Output, string Error)> RunReadyModel(string scope, string dependencies, params string[] options)
    {
        File.WriteAllText(Path.Combine(scope, "ReadyModel.tla"),
            "---- MODULE ReadyModel ----\nEXTENDS Naturals\nVARIABLE x\nInit == x = 0\nNext == (x < 2 /\\ x' = x + 1) \\/ (x = 2 /\\ x' = x)\nBound == x < 2\n====\n");
        File.WriteAllText(Path.Combine(scope, "ReadyModel.cfg"), "INIT Init\nNEXT Next\nINVARIANT Bound\nCHECK_DEADLOCK FALSE\n");
        return Run(scope, dependencies, ["statechart", "ReadyModel.tla", "--out", "chart", .. options]);
    }

    // Runs the built dydo offline with the given managed dependencies and private asset and state roots.
    private static async Task<(int Exit, string Output, string Error)> Run(string scope, string dependencies, string[] arguments)
    {
        var start = StatechartTestBinary.Start();
        start.WorkingDirectory = scope;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("DYDO_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        start.Environment["DYDO_STATECHART_DEPENDENCY_ROOT"] = dependencies;
        start.Environment["DYDO_STATECHART_ASSET_ROOT"] = Path.Combine(scope, "assets");
        start.Environment["DYDO_STATECHART_STATE_ROOT"] = Path.Combine(scope, "state");
        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" }) start.Environment[name] = "http://127.0.0.1:9";
        start.Environment["NO_PROXY"] = "";
        start.Environment["PATH"] = "";
        start.Environment.Remove("JAVA");
        start.Environment.Remove("TLA2TOOLS");
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        return (child.ExitCode, await output, await errors);
    }

    private static string[] Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(file => Path.GetRelativePath(root, file) + ":" +
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))).ToArray();

    private static void Copy(string source, string target)
    {
        Assert.True(Directory.Exists(source), "Missing verified managed fixture component root.");
        Directory.CreateDirectory(target);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            var directory = (attributes & FileAttributes.Directory) != 0;
            var destination = Path.Combine(target, Path.GetFileName(entry));
            // POSIX runtime archives carry relative links; the managed inventory records them as links.
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                var link = directory ? new DirectoryInfo(entry).LinkTarget! : new FileInfo(entry).LinkTarget!;
                if (directory) Directory.CreateSymbolicLink(destination, link);
                else File.CreateSymbolicLink(destination, link);
            }
            else if (directory) Copy(entry, destination);
            else File.Copy(entry, destination, overwrite: false);
        }
    }
}
