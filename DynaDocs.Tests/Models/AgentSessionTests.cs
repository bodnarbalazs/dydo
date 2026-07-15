namespace DynaDocs.Tests.Models;

using DynaDocs.Models;

public class AgentSessionTests
{
    [Theory]
    [InlineData("claude", "claude")]
    [InlineData("CLAUDE", "claude")]
    [InlineData("codex", "codex")]
    [InlineData("  Codex  ", "codex")]
    [InlineData("other", "unknown")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void NormalizeHost_MapsKnownHosts(string? input, string expected)
    {
        Assert.Equal(expected, AgentSession.NormalizeHost(input));
    }

    [Theory]
    [InlineData("gpt-5", "gpt-5")]
    [InlineData("  Opus 4.8  ", "Opus 4.8")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void NormalizeModel_TrimsOrDefaults(string? input, string expected)
    {
        Assert.Equal(expected, AgentSession.NormalizeModel(input));
    }
}
