namespace DynaDocs.Tests.Integration;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Services;

/// <summary>
/// Runs the generated OpenCode guard plugin under node against stub `dydo` shims on PATH.
/// This proves the adapter's block / fail-open contract and Windows `.cmd`/`.ps1` resolution
/// without a live OpenCode host: the generated file is the real embedded template.
/// </summary>
public class OpenCodeGuardPluginTests : IDisposable
{
    private readonly string _dir;

    public OpenCodeGuardPluginTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-opencode-plugin-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "package.json"), """{"type":"module"}""");
        File.WriteAllText(Path.Combine(_dir, "dydo-guard.js"),
            TemplateGenerator.ReadBuiltInTemplate("opencode-guard-plugin.js"));
        File.WriteAllText(Path.Combine(_dir, "driver.mjs"), DriverScript);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    // Imports the plugin, invokes one `tool.execute.before` hook, and reports what it saw.
    private const string DriverScript = """
        const config = JSON.parse(process.argv[2])
        const { DydoGuard } = await import("./dydo-guard.js")

        const logs = []
        const client = {
          app: {
            log: async ({ body }) => { logs.push({ level: body.level, message: body.message }) },
          },
        }

        let threw = false
        let message = ""
        try {
          const hooks = await DydoGuard({ client, directory: config.directory || process.cwd() })
          await hooks["tool.execute.before"](
            { tool: config.tool, sessionID: "test-session" },
            { args: config.args || {} },
          )
        } catch (error) {
          threw = true
          message = String(error && error.message ? error.message : error)
        }

        process.stdout.write(JSON.stringify({ threw, message, logs }))
        """;

    #region Guard exit contract

    [Fact]
    public void Plugin_GuardExit2_ThrowsWithStderr()
    {
        var bin = WriteExitStub("block", "blocked by stub", 2);

        var result = RunPlugin(WithBin(bin), "edit", new { filePath = "src/x.cs" });

        Assert.False(result.NodeFailed);
        Assert.True(result.Threw);
        Assert.Contains("blocked by stub", result.Message);
    }

    [Fact]
    public void Plugin_GuardExit0_Allows()
    {
        var bin = WriteExitStub("allow", "ok", 0);

        var result = RunPlugin(WithBin(bin), "edit", new { filePath = "src/x.cs" });

        Assert.False(result.NodeFailed);
        Assert.False(result.Threw);
    }

    [Fact]
    public void Plugin_GuardUnexpectedExit_FailsOpenWithWarning()
    {
        var bin = WriteExitStub("unexpected", "boom", 3);

        var result = RunPlugin(WithBin(bin), "edit", new { filePath = "src/x.cs" });

        Assert.False(result.NodeFailed);
        Assert.False(result.Threw);
        Assert.Contains(result.Logs, log => log.Contains("exited 3"));
    }

    [Fact]
    public void Plugin_DydoAbsent_FailsOpenWithWarning()
    {
        var result = RunPlugin(SanitizedPath(), "edit", new { filePath = "src/x.cs" });

        Assert.False(result.NodeFailed);
        Assert.False(result.Threw);
        Assert.Contains(result.Logs, log => log.Contains("not found on PATH"));
    }

    #endregion

    #region Tool routing

    [Theory]
    [InlineData("read", "{\"filePath\":\"src/x.cs\"}")]
    [InlineData("edit", "{\"filePath\":\"src/x.cs\"}")]
    [InlineData("write", "{\"filePath\":\"src/x.cs\"}")]
    [InlineData("bash", "{\"command\":\"ls\"}")]
    [InlineData("grep", "{\"path\":\"src\"}")]
    [InlineData("glob", "{\"path\":\"src\"}")]
    [InlineData("apply_patch", "{\"patchText\":\"*** Add File: src/x.cs\"}")]
    public void Plugin_RoutedTool_InvokesGuard(string tool, string argsJson)
    {
        var bin = WriteExitStub("routed", "blocked by stub", 2);

        var result = RunPlugin(WithBin(bin), tool, JsonNode.Parse(argsJson)!);

        Assert.False(result.NodeFailed);
        Assert.True(result.Threw, $"{tool} should have reached the guard");
    }

    [Theory]
    [InlineData("task")]
    [InlineData("webfetch")]
    [InlineData("websearch")]
    [InlineData("list")]
    [InlineData("unknown-tool")]
    public void Plugin_IgnoredTool_NeverInvokesGuard(string tool)
    {
        var marker = Path.Combine(_dir, $"invoked-{tool}.marker");
        var bin = WriteMarkerStub("ignored-" + tool, marker);

        var result = RunPlugin(WithBin(bin), tool, new { filePath = "dydo/index.md" });

        Assert.False(result.NodeFailed);
        Assert.False(result.Threw);
        Assert.False(File.Exists(marker), $"{tool} must not invoke the guard");
    }

    #endregion

    #region Windows shim resolution

    [Fact]
    public void Plugin_ResolvesPowerShellShim_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var bin = Path.Combine(_dir, "bin-ps1");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "dydo.ps1"),
            "[Console]::Error.WriteLine(\"blocked by ps1 stub\")\r\nexit 2\r\n");

        var result = RunPlugin(WithBin(bin), "edit", new { filePath = "src/x.cs" });

        Assert.False(result.NodeFailed);
        Assert.True(result.Threw);
        Assert.Contains("blocked by ps1 stub", result.Message);
    }

    #endregion

    #region Harness plumbing

    private PluginResult RunPlugin(string path, string tool, object args)
    {
        var config = JsonSerializer.Serialize(new { tool, args, directory = _dir });
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = _dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("driver.mjs");
        psi.ArgumentList.Add(config);
        psi.Environment["PATH"] = path;

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);

        if (process.ExitCode != 0)
            return new PluginResult(true, false, stderr, []);

        var json = (JsonObject)JsonNode.Parse(stdout)!;
        var logs = (json["logs"] as JsonArray ?? new JsonArray())
            .Select(node => node?["message"]?.GetValue<string>() ?? "")
            .ToList();
        return new PluginResult(false, json["threw"]!.GetValue<bool>(),
            json["message"]!.GetValue<string>(), logs);
    }

    private string WriteExitStub(string name, string message, int exitCode)
    {
        var bin = Path.Combine(_dir, "bin-" + name);
        Directory.CreateDirectory(bin);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(bin, "dydo.cmd"),
                $"@echo off\r\necho {message} 1>&2\r\nexit /b {exitCode}\r\n");
        }
        else
        {
            WriteUnixStub(bin, $"echo \"{message}\" 1>&2\nexit {exitCode}\n");
        }
        return bin;
    }

    private string WriteMarkerStub(string name, string marker)
    {
        var bin = Path.Combine(_dir, "bin-" + name);
        Directory.CreateDirectory(bin);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(bin, "dydo.cmd"),
                $"@echo off\r\necho x> \"{marker}\"\r\nexit /b 2\r\n");
        }
        else
        {
            WriteUnixStub(bin, $"echo x > \"{marker}\"\nexit 2\n");
        }
        return bin;
    }

    private static void WriteUnixStub(string bin, string body)
    {
        var path = Path.Combine(bin, "dydo");
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static string WithBin(string bin) =>
        bin + Path.PathSeparator + SanitizedPath();

    // A PATH with no dydo shim, so the plugin's absence path is exercised and a stub cannot be
    // shadowed by a real `dydo` earlier in the developer's PATH.
    private static string SanitizedPath()
    {
        var entries = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Where(entry => !string.IsNullOrWhiteSpace(entry) && !HasDydo(entry));
        return string.Join(Path.PathSeparator, entries);
    }

    private static bool HasDydo(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
                return false;
            return Directory.EnumerateFiles(dir, "dydo.*")
                .Any(file => Path.GetFileNameWithoutExtension(file)
                    .Equals("dydo", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private sealed record PluginResult(bool NodeFailed, bool Threw, string Message, List<string> Logs);

    #endregion
}
