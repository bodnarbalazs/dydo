namespace DynaDocs.Tests.Statechart;

using System.Runtime.InteropServices;
using DynaDocs.Services.Statechart;

// The Windows check under its Job Object lifetime: the real cleanup helper, scratch and publication, with a Node
// stand-in for the bootstrap that answers the startup gate and leaves the verdict a checking run would.
public class StatechartWindowsRunnerTests
{
    [SkippableTheory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(0, 1, 2)]
    public async Task AWindowsCheckPublishesOnlyAVerdictItsExitAgreesWithThenCleansItsScratch(int verdict, int exit, int expected)
    {
        using var scope = new StatechartTestDirectory();
        var (request, dependencies, payload, home) = Check(scope.Path, verdict, exit);
        using var output = new StringWriter();

        Assert.Equal(expected, await StatechartRunner.RunAsync(request, dependencies, payload, home, output, CancellationToken.None));

        var report = Path.Combine(request.Output, "report.md");
        Assert.Equal(expected != 2, File.Exists(report));
        if (expected != 2)
        {
            Assert.Equal("Bound holds", File.ReadAllText(report));
            Assert.EndsWith("Wrote " + request.Output, output.ToString().TrimEnd());
        }
        Assert.Empty(Directory.GetDirectories(request.Caller, ".statechart-*"));
        Assert.All(Directory.GetFiles(home.State, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    // Cancellation stops the check before the bootstrap starts, and the lifetime still drains and cleans its scratch.
    [SkippableFact]
    public async Task ACancelledWindowsCheckStillDrainsAndCleansItsScratch()
    {
        using var scope = new StatechartTestDirectory();
        var (request, dependencies, payload, home) = Check(scope.Path, 0, 0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StatechartRunner.RunAsync(request, dependencies,
            payload, home, TextWriter.Null, new CancellationToken(canceled: true)));

        Assert.False(Directory.Exists(request.Output));
        Assert.Empty(Directory.GetDirectories(request.Caller, ".statechart-*"));
        Assert.All(Directory.GetFiles(home.State, "scratch-*.journal"), journal =>
            Assert.Contains("deleted-after-authoritative-drain", File.ReadAllText(journal)));
    }

    private static (StatechartRequest, StatechartDependencies, string, StatechartHome) Check(string root, int verdict, int exit)
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The Job Object check runs only on Windows.");
        var node = StatechartPrerequisites.Find("node", Environment.GetEnvironmentVariable("PATH"));
        Skip.If(node is null, "The Node stand-in bootstrap needs a node runtime on PATH.");
        StatechartExecutable.RelaunchThrough(StatechartTestBinary.Start().FileName);
        var caller = Directory.CreateDirectory(Path.Combine(root, "caller")).FullName;
        File.WriteAllText(Path.Combine(caller, "model.tla"), "---- MODULE model ----\nVARIABLE x\nInit == x = 0\nNext == x' = x\n====\n");
        File.WriteAllText(Path.Combine(caller, "model.cfg"), "INIT Init\nNEXT Next\n");
        var payload = Path.Combine(root, "payload");
        var entry = Path.Combine(payload, "skills", "engineering", "statechart", "scripts", "native-entry.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(entry)!);
        File.WriteAllText(entry, Bootstrap(verdict, exit));
        var home = new StatechartHome(Path.Combine(root, "assets"), Path.Combine(root, "dependencies"),
            Directory.CreateDirectory(Path.Combine(root, "state")).FullName);
        // The output's own parent does not exist yet, so the check finds its nearest existing ancestor.
        var request = StatechartRequest.Capture(["model.tla", "--out", Path.Combine("charts", "model")], caller,
            new StatechartAssets(), RuntimeInformation.RuntimeIdentifier);
        return (request, new StatechartDependencies(node, "java", "tla2tools.jar", "renderer"), payload, home);
    }

    // Answers ready, waits for the owner's go, then leaves the verdict and report a finished check writes.
    private static string Bootstrap(int verdict, int exit) => $$"""
        import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
        import { join } from 'node:path';
        import { createInterface } from 'node:readline';
        const [nonce, file] = process.argv.slice(2);
        const { stage } = JSON.parse(readFileSync(file, 'utf8'));
        process.stdout.write(`ready:1:${nonce}\n`);
        for await (const line of createInterface({ input: process.stdin })) {
          if (line !== `go:1:${nonce}`) process.exit(9);
          mkdirSync(join(stage, 'done'));
          writeFileSync(join(stage, 'verdict.json'), JSON.stringify({ verdict: {{verdict}} }));
          writeFileSync(join(stage, 'done', 'report.md'), 'Bound holds');
          process.exit({{exit}});
        }
        process.exit(8);
        """;
}
