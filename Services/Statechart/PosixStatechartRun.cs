namespace DynaDocs.Services.Statechart;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class PosixStatechartRun
{
    public static async Task<int> RunAsync(StatechartRequest request, StatechartDependencies dependencies,
        string payload, StatechartHome home, StatechartContinuation? continuation, CancellationToken token)
    {
        var parent = Path.GetDirectoryName(request.Output)!;
        while (!Directory.Exists(parent)) parent = Path.GetDirectoryName(parent)!;
        var executable = StatechartExecutable.Current();
        await using var owner = await PosixStatechartLifetime.StartAsync(executable,
            parent, home.State, Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        await owner.RegisterAsync();
        var configuration = new JsonObject { ["mode"] = "check", ["request"] =
            JsonSerializer.SerializeToNode(request, StatechartRequestJson.Default.StatechartRequest),
            ["stage"] = owner.Stage, ["temp"] = owner.Temp, ["renderer"] = dependencies.Renderer,
            ["renderGuide"] = Path.Combine(payload, "skills/engineering/statechart/references/render.md"),
            ["nativeExecutable"] = executable.FileName,
            ["nativeArguments"] = new JsonArray(executable.ArgumentList.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
            ["completedRequest"] = continuation?.Path };
        var file = Path.Combine(owner.Stage, "posix-cli.json");
        await File.WriteAllTextAsync(file, configuration.ToJsonString(), token);
        var start = new ProcessStartInfo(dependencies.Node!) { WorkingDirectory = request.Caller };
        foreach (var argument in new[] { "--disable-warning=ExperimentalWarning",
                Path.Combine(payload, "skills/engineering/statechart/scripts/native-entry.ts"), "posix-cli", file })
            start.ArgumentList.Add(argument);
        start.Environment["JAVA"] = dependencies.Java;
        start.Environment["TLA2TOOLS"] = dependencies.Tlc;
        foreach (var variable in new[] { "TEMP", "TMP", "TMPDIR" }) start.Environment[variable] = owner.Temp;
        token.ThrowIfCancellationRequested();
        return await owner.HandoffAsync(start, continuation);
    }
}
