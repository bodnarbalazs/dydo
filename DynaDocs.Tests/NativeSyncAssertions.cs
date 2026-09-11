namespace DynaDocs.Tests;

using System.Text.RegularExpressions;

internal static class NativeSyncAssertions
{
    internal static void AssertSummary(int exitCode, string stdout, string stderr, string integration)
    {
        Assert.Equal(0, exitCode);
        Assert.Empty(stderr);
        var (claude, codex) = Hosts(integration);
        List<string> patterns = [];
        if (claude)
        {
            patterns.Add(@"Synced [1-9][0-9]* agent\(s\) to \.claude/ \(agents \+ skills\): \S[^\r\n]*");
            patterns.Add(@"Synced [1-9][0-9]* skill\(s\) to \.claude/ \(skills only\): \S[^\r\n]*");
        }
        if (codex)
            patterns.Add(@"Synced Codex artifacts to \.agents/skills and \.codex/agents\.");
        if (!claude || !codex)
            patterns.Add($@"Skipped {(claude ? "Codex" : "Claude")} artifacts (?:—|-) not recorded in dydo\.json integrations \(add it with 'dydo init <integration> --join'\)\.");
        Assert.True(Regex.IsMatch(stdout.Replace("\r\n", "\n", StringComparison.Ordinal),
            @"\A" + string.Join("\n", patterns) + @"\n?\z"), stdout);
    }

    internal static void AssertArtifacts(string projectRoot, string integration)
    {
        var (claude, codex) = Hosts(integration);
        AssertArtifacts(projectRoot, claude,
            [".claude/agents/implementer.md", ".claude/skills/implementer/SKILL.md", ".claude/skills/co-thinker/SKILL.md"]);
        AssertArtifacts(projectRoot, codex,
            [".codex/agents/implementer.toml", ".agents/skills/implementer/SKILL.md", ".agents/skills/co-thinker/SKILL.md"]);
    }

    private static void AssertArtifacts(string projectRoot, bool expected, string[] paths)
    {
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(projectRoot, path);
            Assert.Equal(expected, File.Exists(fullPath));
            if (expected)
                Assert.NotEmpty(File.ReadAllBytes(fullPath));
        }
    }

    private static (bool Claude, bool Codex) Hosts(string integration) => integration switch
    {
        "none" or "all" => (true, true),
        "claude" => (true, false),
        "codex" => (false, true),
        _ => throw new ArgumentException("Unknown fixture integration.", nameof(integration))
    };
}
