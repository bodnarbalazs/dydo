namespace DynaDocs.Tests.Models;

using DynaDocs.Models;

public class PresetAgentNamesTests
{
    [Fact]
    public void AllSets_ContainNoDuplicateNames()
    {
        var allNames = PresetAgentNames.Set1
            .Concat(PresetAgentNames.Set2)
            .Concat(PresetAgentNames.Set3)
            .Concat(PresetAgentNames.Set4)
            .ToList();

        var uniqueNames = allNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Equal(uniqueNames.Count, allNames.Count);
    }

    [Fact]
    public void EachSet_Contains26Names()
    {
        Assert.Equal(26, PresetAgentNames.Set1.Count);
        Assert.Equal(26, PresetAgentNames.Set2.Count);
        Assert.Equal(26, PresetAgentNames.Set3.Count);
        Assert.Equal(26, PresetAgentNames.Set4.Count);
    }

    [Fact]
    public void GetNames_ReturnsCorrectCount_AcrossAllSets()
    {
        // Request more than Set1+Set2 to test Set3/Set4
        var names = PresetAgentNames.GetNames(80);

        Assert.Equal(80, names.Count);
        Assert.Equal(names.Distinct().Count(), names.Count); // No duplicates
    }
}
