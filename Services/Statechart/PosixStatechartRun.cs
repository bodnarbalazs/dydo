namespace DynaDocs.Services.Statechart;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class PosixStatechartRun
{
    public static async Task<int> RunAsync(StatechartRequest request, StatechartDependencies dependencies, string payload,
        StatechartHome home, StatechartContinuation? continuation, ProcessStartInfo executable, CancellationToken token)
    {
        var parent = Path.GetDirectoryName(request.Output)!;
        while (!Directory.Exists(parent)) parent = Path.GetDirectoryName(parent)!;
        await using var owner = await PosixStatechartLifetime.StartAsync(executable,
            parent, home.State, Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        await owner.RegisterAsync();
        var start = await CheckingAsync(request, dependencies, payload, owner, executable, continuation?.Path, token);
        token.ThrowIfCancellationRequested();
        return await owner.HandoffAsync(start, continuation);
    }

    // The checking runtime's launch: its configuration in the owned stage, the owned temp as its scratch, the caller as its cwd.
    internal static async Task<ProcessStartInfo> CheckingAsync(StatechartRequest request, StatechartDependencies dependencies,
        string payload, StatechartLifetime owner, ProcessStartInfo executable, string? completedRequest, CancellationToken token)
    {
        var configuration = new JsonObject { ["mode"] = "check", ["request"] =
            JsonSerializer.SerializeToNode(request, StatechartRequestJson.Default.StatechartRequest),
            ["stage"] = owner.Stage, ["temp"] = owner.Temp, ["renderer"] = dependencies.Renderer,
            ["renderGuide"] = Path.Combine(payload, "skills/engineering/statechart/references/render.md"),
            ["nativeExecutable"] = executable.FileName,
            ["nativeArguments"] = new JsonArray(executable.ArgumentList.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray()),
            ["completedRequest"] = completedRequest };
        var file = Path.Combine(owner.Stage, "posix-cli.json");
        await File.WriteAllTextAsync(file, configuration.ToJsonString(), token);
        var start = new ProcessStartInfo(dependencies.Node!) { WorkingDirectory = request.Caller };
        foreach (var argument in new[] { "--disable-warning=ExperimentalWarning",
                Path.Combine(payload, "skills/engineering/statechart/scripts/native-entry.ts"), "posix-cli", file })
            start.ArgumentList.Add(argument);
        start.Environment["JAVA"] = dependencies.Java;
        start.Environment["TLA2TOOLS"] = dependencies.Tlc;
        foreach (var variable in new[] { "TEMP", "TMP", "TMPDIR" }) start.Environment[variable] = owner.Temp;
        return start;
    }
}
