namespace DynaDocs.Services.Statechart;

using System.Text.Json.Nodes;

internal static class StatechartReady
{
    public static async Task<JsonObject> VerifyAsync(StatechartDependencies dependencies, string payload,
        StatechartHome home, bool full, CancellationToken token)
    {
        StatechartPaths.EnsureDirectory(home.State);
        await using var owner = await StatechartLifetime.CreateAsync(StatechartExecutable.Current(),
            home.State, home.State, Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        await owner.RegisterAsync();
        try
        {
            var checker = StatechartChecker.Prepare(dependencies.Tlc!, owner.Stage);
            checker.TlcArguments(payload);
            var node = await StatechartProbe.CaptureAsync(dependencies.Node!, ["--version"], owner, owner.Temp, token);
            var java = await StatechartProbe.CaptureAsync(dependencies.Java!, ["-version"], owner, owner.Temp, token);
            if (!StatechartPrerequisites.Compatible("node", node) || !StatechartPrerequisites.Compatible("java", java))
                throw new IOException("Selected statechart runtimes are incompatible.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            if (await StatechartNode.RunAsync(dependencies, payload, new JsonObject { ["mode"] = "render-ready" },
                    owner, owner.Temp, deadline.Token) != 0)
                throw new IOException("Locked Pele renderer readiness failed.");
            if (full) await TlcAsync(dependencies, payload, owner, checker, token);
            return new JsonObject { ["node"] = node, ["java"] = java, ["renderer"] = "Pele1.9.0 import/render verified",
                ["sanyAndTlc"] = full };
        }
        finally
        {
            if (!await owner.CompleteAsync()) throw new IOException("Prerequisite readiness drain uncertain; scratch retained.");
        }
    }

    private static async Task TlcAsync(StatechartDependencies dependencies, string payload, StatechartLifetime owner,
        StatechartChecker checker, CancellationToken token)
    {
        var model = Path.Combine(owner.Stage, "Readiness.tla");
        var config = Path.Combine(owner.Stage, "Readiness.cfg");
        await File.WriteAllTextAsync(model, "---- MODULE Readiness ----\nEXTENDS Naturals\nVARIABLE x\nInit == x = 0\nNext == x' = x\nSpec == Init /\\ [][Next]_x\n====\n", token);
        await File.WriteAllTextAsync(config, "SPECIFICATION Spec\nCHECK_DEADLOCK FALSE\n", token);
        await StatechartProbe.CaptureAsync(dependencies.Java!, ["-cp", checker.Verify(), "tla2sany.SANY", model], owner, owner.Temp, token);
        var result = await StatechartProbe.CaptureAsync(dependencies.Java!, ["-XX:+UseParallelGC", .. checker.TlcArguments(payload),
            "-workers", "1", "-fp", "0", "-lncheck", "final", "-config", config, model], owner, owner.Temp, token);
        if (!result.Contains("No error has been found", StringComparison.Ordinal))
            throw new IOException("Locked TLC did not complete the real readiness fixture.");
    }
}
