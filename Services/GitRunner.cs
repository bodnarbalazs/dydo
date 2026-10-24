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
        Start(workingDirectory, new Dictionary<string, string>(), inheritGitVariables: true, arguments);

    /// <summary>
    /// Runs git with every inherited <c>GIT_*</c> variable removed (config such as <c>GIT_CONFIG_COUNT</c> or
    /// <c>GIT_CONFIG_PARAMETERS</c>, repository paths, object directories) and <paramref name="environment"/> added.
    /// </summary>
    public static GitResult RunIsolated(string workingDirectory, IReadOnlyDictionary<string, string> environment, params string[] arguments) =>
        Start(workingDirectory, environment, inheritGitVariables: false, arguments);

    /// <summary>The process <see cref="RunIsolated"/> starts, its environment final.</summary>
    internal static ProcessStartInfo IsolatedStartInfo(string workingDirectory, IReadOnlyDictionary<string, string> environment,
        params string[] arguments) =>
        StartInfo(workingDirectory, environment, inheritGitVariables: false, arguments);

    private static GitResult Start(string workingDirectory, IReadOnlyDictionary<string, string> environment,
        bool inheritGitVariables, string[] arguments)
    {
        var start = StartInfo(workingDirectory, environment, inheritGitVariables, arguments);
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

    private static ProcessStartInfo StartInfo(string workingDirectory, IReadOnlyDictionary<string, string> environment,
        bool inheritGitVariables, string[] arguments)
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
        if (!inheritGitVariables)
            RemoveGitVariables(start.Environment);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var (name, value) in environment)
            start.Environment[name] = value;
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        return start;
    }

    // A prefix, not a list, so variables a later git adds are covered too; case-insensitive, as Windows names are.
    private static void RemoveGitVariables(IDictionary<string, string?> environment)
    {
        foreach (var name in environment.Keys.Where(name => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
            environment.Remove(name);
    }
}
