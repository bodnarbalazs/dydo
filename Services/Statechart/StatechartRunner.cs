namespace DynaDocs.Services.Statechart;

using System.Text.Json;
using System.Text.Json.Nodes;

internal static class StatechartRunner
{
    public static Task<int> RunAsync(StatechartRequest request, StatechartDependencies dependencies,
        string payload, StatechartHome home, TextWriter output, CancellationToken token, StatechartContinuation? continuation = null) =>
        OperatingSystem.IsWindows()
            ? RunWindowsAsync(request, dependencies, payload, home, output, token)
            : PosixStatechartRun.RunAsync(request, dependencies, payload, home, continuation, StatechartExecutable.Current(), token);

    private static async Task<int> RunWindowsAsync(StatechartRequest request, StatechartDependencies dependencies,
        string payload, StatechartHome home, TextWriter output, CancellationToken token)
    {
        var parent = Path.GetDirectoryName(request.Output)!;
        while (!Directory.Exists(parent)) parent = Path.GetDirectoryName(parent)!;
        StatechartPaths.RequirePlainDirectory(parent);
        await using var owner = await WindowsStatechartLifetime.StartAsync(StatechartExecutable.Current(),
            parent, home.State, Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
        await owner.RegisterAsync();
        try
        {
            var configuration = new JsonObject { ["mode"] = "check", ["request"] =
                JsonSerializer.SerializeToNode(request, StatechartRequestJson.Default.StatechartRequest) };
            var status = await StatechartNode.RunAsync(dependencies, payload, configuration, owner, request.Caller, token);
            if (!await owner.DrainAsync()) throw new IOException("Checking drain uncertain; output and scratch retained.");
            return await PublishAsync(owner.Stage, status, request.Output, output, token);
        }
        finally
        {
            if (!await owner.CompleteAsync()) throw new IOException("Checking cleanup drain uncertain; scratch retained.");
        }
    }

    // Publishes only a complete verdict the checker's exit agrees with, unless the renderer recorded why it could not.
    internal static async Task<int> PublishAsync(string stage, int status, string destination, TextWriter output, CancellationToken token)
    {
        var done = Path.Combine(stage, "done");
        var result = Path.Combine(stage, "verdict.json");
        if (!File.Exists(result) || !File.Exists(Path.Combine(done, "report.md"))) return 2;
        var verdict = JsonNode.Parse(File.ReadAllBytes(result))!["verdict"]!.GetValue<int>();
        if (verdict is not (0 or 1) || status != verdict && !File.Exists(Path.Combine(done, "render-error.json"))) return 2;
        token.ThrowIfCancellationRequested();
        Publish(done, destination);
        await output.WriteLineAsync(await File.ReadAllTextAsync(Path.Combine(destination, "report.md"), CancellationToken.None));
        await output.WriteLineAsync("Wrote " + destination);
        return status;
    }

    internal static void Publish(string done, string destination)
    {
        StatechartPaths.EnsureDirectory(Path.GetDirectoryName(destination)!);
        if (Directory.Exists(destination))
        {
            StatechartPaths.RequirePlainDirectory(destination);
            Directory.Delete(destination, recursive: false);
        }
        else if (new DirectoryInfo(destination).LinkTarget is not null || File.Exists(destination))
            throw new IOException("Statechart output changed while checking; foreign destination retained.");
        Directory.Move(done, destination);
    }
}
