namespace DynaDocs.Services.Statechart;

using System.Diagnostics;

internal static class StatechartProbe
{
    public static async Task<int> ChildAsync(string[] arguments)
    {
        if (arguments.Length == 0) throw new InvalidDataException("Missing statechart probe executable.");
        await StatechartProtocol.WaitForGoAsync(Console.In, Console.Out);
        var start = new ProcessStartInfo(arguments[0]) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments.Skip(1)) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Cannot start statechart readiness probe.");
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput());
        var stderr = process.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError());
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }

    public static async Task<string> CaptureAsync(string file, string argument, StatechartLifetime lifetime)
        => await CaptureAsync(file, [argument], lifetime, Environment.CurrentDirectory, CancellationToken.None);

    public static async Task<string> CaptureAsync(string file, IReadOnlyList<string> arguments,
        StatechartLifetime lifetime, string cwd, CancellationToken token)
    {
        var start = StatechartExecutable.Current();
        start.ArgumentList.Add("__statechart-probe");
        start.ArgumentList.Add(file);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.WorkingDirectory = cwd;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = Process.Start(lifetime.Gate(start)) ?? throw new IOException("Cannot start gated statechart probe.");
        var protocol = new StatechartProtocol();
        protocol.AcceptReady(await StatechartCleanup.Read(process.StandardOutput).WaitAsync(TimeSpan.FromSeconds(30), token));
        lifetime.Attach(process.Id);
        protocol.ConfirmOwnership();
        protocol.ConfirmScratchRegistration();
        await protocol.ReleaseAsync(process.StandardInput, token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var stdout = Bounded(process.StandardOutput, timeout.Token);
        var stderr = Bounded(process.StandardError, timeout.Token);
        await lifetime.WaitAsync(process, timeout.Token);
        var output = await stdout + await stderr;
        if (process.ExitCode != 0) throw new IOException("Statechart prerequisite probe failed: " + output);
        return output.Trim();
    }

    internal static async Task<string> Bounded(TextReader reader, CancellationToken cancellationToken)
    {
        var output = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
        {
            if (output.Length + read > 65536) throw new IOException("Statechart prerequisite probe output exceeds 64KiB.");
            output.Append(buffer, 0, read);
        }
        return output.ToString();
    }
}
