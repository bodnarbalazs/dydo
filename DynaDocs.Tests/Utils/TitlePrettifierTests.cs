// @test-tier: 2
namespace DynaDocs.Tests.Utils;

using DynaDocs.Utils;

public class TitlePrettifierTests
{
    [Theory]
    [InlineData("agent-graph-metrics", "Agent Graph Metrics")] // kebab
    [InlineData("swarm_0119", "Swarm 0119")]                   // underscore
    [InlineData("swarm-0119", "Swarm 0119")]                   // numeric segment
    [InlineData("alpha", "Alpha")]                             // single word
    [InlineData("Fix the login bug", "Fix the login bug")]     // prose passes through verbatim
    [InlineData("-", "-")]                                     // never blank: no segments -> input itself
    public void Prettify_ProducesReadableNonBlankTitle(string input, string expected) =>
        Assert.Equal(expected, TitlePrettifier.Prettify(input));
}
