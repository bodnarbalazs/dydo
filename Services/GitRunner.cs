namespace DynaDocs.Services;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>
/// Runs git directly, without a shell, and never waits on a prompt. Optional locks are off, so a read
/// such as <c>git status</c> never rewrites an index to refresh its stat cache (a dry run changes nothing).
/// </summary>
internal static class GitRunner
{
    public static GitResult Run(string workingDirectory, params string[] arguments) =>
        Run(workingDirectory, new Dictionary<string, string>(), arguments);

    /// <summary>Runs git with <paramref name="environment"/> added to its environment.</summary>
    public static GitResult Run(string workingDirectory, IReadOnlyDictionary<string, string> environment, params string[] arguments)
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
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var (name, value) in environment)
            start.Environment[name] = value;
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
