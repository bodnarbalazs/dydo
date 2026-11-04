namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.Json;
using System.Runtime.InteropServices;
using DynaDocs.Services.Statechart;

public static class StatechartCommand
{
    public static Command Create()
    {
        var command = new Command("statechart", "Check a TLA+ model with managed prerequisites and native process ownership");
        command.Arguments.Add(new Argument<string?>("spec") { Arity = ArgumentArity.ZeroOrOne });
        foreach (var option in new[] { "--config", "--view", "--out", "--skill" }) command.Options.Add(new Option<string?>(option));
        var resume = new Command("resume", "Install missing locked prerequisites and continue a saved request through normal host authorization");
        resume.Options.Add(new Option<string>("--request") { Required = true });
        command.Subcommands.Add(resume);
        var install = new Command("install-skill", "Install this release's statechart skill and both project projections");
        install.Options.Add(new Option<string>("--root") { Required = true });
        command.Subcommands.Add(install);
        var doctor = new Command("doctor", "Inspect installed statechart assets and local prerequisite candidates offline");
        doctor.SetAction(async _ => await DoctorAsync(Console.Out));
        command.Subcommands.Add(doctor);
        return command;
    }

    public static async Task<int> ExecuteAsync(string[] arguments, string caller, CancellationToken token)
    {
        try
        {
            if (arguments is ["doctor"]) return await DoctorAsync(Console.Out);
            if (arguments.Length > 0 && arguments[0] == "install-skill")
            {
                var root = new Option<string>("--root") { Required = true };
                var install = new Command("install-skill");
                install.Options.Add(root);
                var parsed = install.Parse(arguments[1..]);
                if (parsed.Errors.Count != 0) throw new IOException(string.Join(" ", parsed.Errors.Select(error => error.Message)));
                await StatechartSkill.InstallAsync(parsed.GetValue(root)!, StatechartHome.Current(), new StatechartAssets(),
                    RuntimeInformation.RuntimeIdentifier, Console.Out, token);
                return 0;
            }
            var flow = new StatechartFlow(StatechartHome.Current(), new StatechartAssets(), RuntimeInformation.RuntimeIdentifier);
            if (arguments.Length > 0 && arguments[0] == "resume")
            {
                var request = new Option<string>("--request") { Required = true };
                var command = new Command("resume");
                command.Options.Add(request);
                var parsed = command.Parse(arguments[1..]);
                if (parsed.Errors.Count != 0) throw new IOException(string.Join(" ", parsed.Errors.Select(error => error.Message)));
                return await flow.ResumeAsync(parsed.GetValue(request)!, Console.Out, token);
            }
            return await flow.StartAsync(arguments, caller, Console.Out, token);
        }
        catch (OperationCanceledException) { await Console.Error.WriteLineAsync("Statechart cancelled; pending request retained."); return 130; }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or
            System.ComponentModel.Win32Exception or PlatformNotSupportedException or HttpRequestException or JsonException or TimeoutException)
        {
            await Console.Error.WriteLineAsync("Statechart: " + error.Message);
            return 2;
        }
    }

    internal static async Task<int> DoctorAsync(TextWriter output)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Native statechart diagnostics on this host still require platform validation; use the supported statechart skill entry.");
            await DiagnoseAsync(output);
            return 2;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            await output.WriteLineAsync("Statechart diagnostics: " + error.Message);
            return 2;
        }
    }

    // Probes local candidates under a Job Object lifetime, so the diagnosis itself runs only on Windows.
    private static async Task DiagnoseAsync(TextWriter output)
    {
        var assets = new StatechartAssets();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(home, "dydo", "statechart");
        var cache = StatechartPaths.Root("DYDO_STATECHART_ASSET_ROOT", Path.Combine(root, "assets"));
        var state = StatechartPaths.Root("DYDO_STATECHART_STATE_ROOT", Path.Combine(root, "state"));
        StatechartPaths.EnsureDirectory(state);
        var payload = await assets.ExtractAsync(cache, CancellationToken.None);
        await using var lifetime = await WindowsStatechartLifetime.StartAsync(StatechartExecutable.Current(),
            state, state, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
        await lifetime.RegisterAsync();
        var node = StatechartPrerequisites.Find("node", Environment.GetEnvironmentVariable("PATH"));
        var java = StatechartPrerequisites.Java(Environment.CurrentDirectory,
            Environment.GetEnvironmentVariable("JAVA"), Environment.GetEnvironmentVariable("PATH"));
        var tlc = StatechartPrerequisites.Tlc(Environment.CurrentDirectory, Environment.GetEnvironmentVariable("TLA2TOOLS"));
        var nodeVersion = await VersionAsync(node, "--version", lifetime);
        var javaVersion = await VersionAsync(java, "-version", lifetime);
        if (!await lifetime.CompleteAsync()) throw new IOException("Statechart readiness cleanup drain is uncertain; scratch retained.");
        await output.WriteLineAsync(Report(assets, payload, node, nodeVersion, java, javaVersion, tlc));
    }

    private static async Task<string> VersionAsync(string? runtime, string flag, StatechartLifetime lifetime) => runtime is null
        ? "missing" : await StatechartProbe.CaptureAsync(runtime, [flag], lifetime, Environment.CurrentDirectory, CancellationToken.None);

    // The offline diagnosis never claims chart readiness: only a managed renderer receipt can.
    internal static string Report(StatechartAssets assets, string payload, string? node, string nodeVersion,
        string? java, string javaVersion, string? tlc)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("schema", 1);
            json.WriteString("state", "diagnostic");
            json.WriteBoolean("ready", false);
            json.WriteString("payload", payload);
            json.WriteString("payloadDigest", assets.Digest);
            json.WriteString("lockDigest", assets.LockDigest);
            json.WriteString("node", node);
            json.WriteString("nodeVersion", nodeVersion);
            json.WriteBoolean("nodeCompatible", StatechartPrerequisites.Compatible("node", nodeVersion));
            json.WriteString("java", java);
            json.WriteString("javaVersion", javaVersion);
            json.WriteBoolean("javaCompatible", StatechartPrerequisites.Compatible("java", javaVersion));
            json.WriteString("tlc", tlc);
            json.WriteString("renderer", "managed renderer readiness receipt required");
            json.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
