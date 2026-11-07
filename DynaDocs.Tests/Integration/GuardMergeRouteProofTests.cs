namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using static DynaDocs.Tests.Integration.PushedRepository;

/// <summary>
/// DYD-431 A-03 proof: pushes that update the default branch, which DR 061 line 82 requires the guard
/// to refuse, through the real hook entry point. Each test fails on d8ca1786.
/// </summary>
[Collection("Integration")]
public class GuardMergeRouteProofTests : IntegrationTestBase
{
    private static readonly string[] Envelopes = ["claude-bash", "claude-powershell", "codex-bash", "codex-pwsh", "codex-argv"];

    private async Task InitPushedProject()
    {
        await InitProjectAsync("none");
        File.AppendAllText(Path.Combine(TestDir, ".gitignore"), "\n/origin.git/\n/wt/\n");
        Create(TestDir, Path.Combine(TestDir, "origin.git"));
    }

    private static string Line(string[] words) =>
        string.Join(' ', words.Select(w => w.Any(c => c is ' ' or '"' or '{' or '}' or '(' or ')') ? $"'{w}'" : w));

    private string Payload(string[] words, string envelope, string? rawLine = null)
    {
        var line = rawLine ?? Line(words);
        object command = envelope switch
        {
            "codex-bash" => new[] { "bash", "-lc", line },
            "codex-pwsh" => new[] { "pwsh", "-Command", line },
            "codex-argv" => words,
            _ => line,
        };
        var payload = new Dictionary<string, object?>
        {
            ["session_id"] = TestSessionId,
            ["hook_event_name"] = "PreToolUse",
            ["tool_name"] = envelope == "claude-powershell" ? "PowerShell" : "Bash",
            ["tool_input"] = new Dictionary<string, object?> { ["command"] = command },
            ["cwd"] = TestDir,
        };
        if (envelope.StartsWith("codex", StringComparison.Ordinal)) payload["turn_id"] = "turn-1";
        return JsonSerializer.Serialize(payload);
    }

    private async Task<List<string>> Misses(bool denied, IEnumerable<string[]> commands, string[]? envelopes = null)
    {
        List<string> misses = [];
        foreach (var words in commands)
        foreach (var envelope in envelopes ?? Envelopes)
        {
            var result = await GuardWithStdinAsync(Payload(words, envelope));
            var wasDenied = !result.IsPreToolUseAllowed;
            if (wasDenied != denied)
                misses.Add($"{envelope} {(wasDenied ? "denied" : "allowed")}: {Line(words)}\n    {result.Stderr.Trim().Replace("\n", "\n    ")}");
        }
        return misses;
    }

    private static void AssertNoMisses(List<string> misses) =>
        Assert.True(misses.Count == 0, $"{misses.Count} misses:\n{string.Join("\n", misses)}");

    private static string[][] Words(params string[] lines) => [.. lines.Select(l => l.Split(' '))];

    // git resolves a destination outside refs/ against the remote's refs, so heads/main is refs/heads/main
    // (git 2.x: `git push origin HEAD:heads/main` prints "HEAD -> main"). The scanner keeps heads/main.
    [Fact]
    public async Task PushToAHeadsShorthandDestination_UpdatesMain_IsDenied()
    {
        await InitPushedProject();
        AssertNoMisses(await Misses(denied: true, Words("git push origin HEAD:heads/main", "git push origin feature:heads/master")));
    }
}
