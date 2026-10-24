namespace DynaDocs.Services;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>Runs git directly, without a shell, and never waits on a prompt.</summary>
internal static class GitRunner
{
    public static GitResult Run(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(start)!;
            process.StandardInput.Close();
            var error = process.StandardError.ReadToEndAsync();
            using var output = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(output);
            process.WaitForExit();
            return new GitResult(process.ExitCode, output.ToArray(), error.Result.Trim());
        }
        catch (Win32Exception ex)
        {
            return new GitResult(-1, [], $"git could not start: {ex.Message}");
        }
    }
}
