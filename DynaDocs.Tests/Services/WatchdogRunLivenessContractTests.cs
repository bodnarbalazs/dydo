namespace DynaDocs.Tests.Services;

using System.Text.RegularExpressions;

public class WatchdogRunLivenessContractTests
{
    private const string WatchdogSourcePath = "../../../../Services/WatchdogService.cs";

    private static string ReadRunMethodBody()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, WatchdogSourcePath));
        Assert.True(File.Exists(path), $"Source file not found: {path}");
        var source = File.ReadAllText(path);

        var signatureIdx = source.IndexOf("public static void Run()", StringComparison.Ordinal);
        Assert.True(signatureIdx >= 0, "Run() signature not found");

        // Find the opening brace after the signature and extract the matching body.
        var braceIdx = source.IndexOf('{', signatureIdx);
        Assert.True(braceIdx >= 0, "Run() opening brace not found");

        var depth = 0;
        for (var i = braceIdx; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) return source[braceIdx..(i + 1)];
            }
        }

        Assert.Fail("Run() closing brace not found");
        return string.Empty;
    }

    [Fact]
    public void Run_Source_ContainsAnchorLivenessCheck()
    {
        var body = ReadRunMethodBody();
        var hasAnchorScan = body.Contains("ScanAnchors", StringComparison.Ordinal)
            || body.Contains("anchorsDir", StringComparison.Ordinal);
        Assert.True(hasAnchorScan,
            "Run() must reference the anchors directory (ScanAnchors / anchorsDir) to self-terminate when its anchors die.");
    }

    [Fact]
    public void Run_Source_ContainsCancellationOrSignalHandler()
    {
        var body = ReadRunMethodBody();
        var hasCancellation = body.Contains("CancellationToken", StringComparison.Ordinal)
            || body.Contains("ProcessExit", StringComparison.Ordinal)
            || body.Contains("CancelKeyPress", StringComparison.Ordinal);
        Assert.True(hasCancellation,
            "Run() must use CancellationToken, ProcessExit, or CancelKeyPress for graceful shutdown.");
    }

    [Fact]
    public void Run_Source_DeletesPidFileInFinally()
    {
        var body = ReadRunMethodBody();
        var finallyIdx = body.LastIndexOf("finally", StringComparison.Ordinal);
        Assert.True(finallyIdx >= 0, "Run() must have a finally block for pid-file cleanup.");

        var finallyBlock = body[finallyIdx..];
        var deletesPidFile = Regex.IsMatch(finallyBlock, @"File\.Delete\s*\(\s*pidFile");
        Assert.True(deletesPidFile,
            "Run()'s finally block must delete the pid file to avoid orphan entries on shutdown.");
    }
}
