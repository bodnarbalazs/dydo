namespace DynaDocs.Services.Statechart;

using System.Diagnostics;
using System.Text.Json.Nodes;

internal static class StatechartNode
{
    public static async Task<int> RunAsync(StatechartDependencies dependencies, string payload,
        JsonObject configuration, WindowsStatechartLifetime lifetime, string caller, CancellationToken token)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var file = Path.Combine(lifetime.Stage, "node-" + nonce + ".json");
        configuration["stage"] = lifetime.Stage;
        configuration["renderer"] = dependencies.Renderer;
        configuration["renderGuide"] = Path.Combine(payload, "skills/engineering/statechart/references/render.md");
        await File.WriteAllTextAsync(file, configuration.ToJsonString(), token);
        var start = new ProcessStartInfo(dependencies.Node!)
        {
            WorkingDirectory = caller, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "--disable-warning=ExperimentalWarning",
                Path.Combine(payload, "skills/engineering/statechart/scripts/native-entry.ts"), nonce, file })
            start.ArgumentList.Add(argument);
        start.Environment["JAVA"] = dependencies.Java;
        start.Environment["TLA2TOOLS"] = dependencies.Tlc;
        foreach (var variable in new[] { "TEMP", "TMP", "TMPDIR" }) start.Environment[variable] = lifetime.Temp;
        using var child = Process.Start(start) ?? throw new IOException("Cannot start native statechart bootstrap.");
        child.StandardInput.NewLine = "\n";
        var protocol = new StatechartProtocol(nonce);
        protocol.AcceptReady(await StatechartCleanup.Read(child.StandardOutput).WaitAsync(TimeSpan.FromSeconds(30), token));
        lifetime.Attach(child.Id);
        protocol.ConfirmOwnership();
        protocol.ConfirmScratchRegistration();
        token.ThrowIfCancellationRequested();
        var stdout = StatechartProbe.Bounded(child.StandardOutput, token);
        var stderr = StatechartProbe.Bounded(child.StandardError, token);
        await protocol.ReleaseAsync(child.StandardInput, token);
        await lifetime.WaitAsync(child, token);
        var output = await stdout;
        var errors = await stderr;
        if (output.Length != 0) await Console.Error.WriteAsync(output);
        if (errors.Length != 0) await Console.Error.WriteAsync(errors);
        return child.ExitCode;
    }
}
