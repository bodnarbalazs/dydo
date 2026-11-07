namespace DynaDocs.Services.Merge;

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>
/// GitHub's REST API through the gh CLI on one host. gh runs without a shell, with an explicit
/// <c>--hostname</c> and without <c>GH_REPO</c> or <c>GH_HOST</c>, so it reads and writes only the
/// path the caller names. A body goes on standard input, never on the command line.
/// </summary>
internal sealed partial class GhApi(string host, string executable = "gh") : IGitHubApi
{
    private static readonly UTF8Encoding Utf8 = new(false);

    public GitHubReply Send(string method, string path, JsonObject? body = null)
    {
        try
        {
            using var process = Process.Start(StartInfo(executable, host, method, path, body != null))!;
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            WriteInput(process, body);
            process.WaitForExit();
            return Parse(output.Result, error.Result.Trim());
        }
        catch (Win32Exception ex)
        {
            return new GitHubReply(0, null, $"gh could not start: {ex.Message}");
        }
    }

    internal static ProcessStartInfo StartInfo(string executable, string host, string method, string path, bool hasBody)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            StandardInputEncoding = Utf8,
            CreateNoWindow = true
        };
        start.Environment.Remove("GH_REPO");
        start.Environment.Remove("GH_HOST");
        foreach (var argument in (string[])["api", path, "--hostname", host, "--method", method, "--include"])
            start.ArgumentList.Add(argument);
        if (hasBody)
        {
            start.ArgumentList.Add("--input");
            start.ArgumentList.Add("-");
        }
        return start;
    }

    /// <summary>
    /// gh's <c>--include</c> output: the status line and headers, a blank line, then the body. Output
    /// without a status line means gh never had an answer from GitHub.
    /// </summary>
    internal static GitHubReply Parse(string output, string error)
    {
        var status = StatusLineRegex().Match(output);
        if (!status.Success)
            return new GitHubReply(0, null, error.Length > 0 ? error : "gh printed no HTTP answer");

        var body = HeaderEndRegex().Split(output, 2) is [_, var rest] ? rest.Trim() : "";
        return new GitHubReply(int.Parse(status.Groups["code"].Value), Json(body), error);
    }

    private static JsonNode? Json(string body)
    {
        if (body.Length == 0)
            return null;
        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A gh that fails before it reads its input (no login, say) closes the pipe; its output says why.
    private static void WriteInput(Process process, JsonObject? body)
    {
        try
        {
            if (body != null)
                process.StandardInput.Write(body.ToJsonString());
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The error is read from the process's own output.
        }
    }

    [GeneratedRegex(@"\AHTTP/\S+ (?<code>\d{3})")]
    private static partial Regex StatusLineRegex();

    [GeneratedRegex(@"\r?\n\r?\n")]
    private static partial Regex HeaderEndRegex();
}
