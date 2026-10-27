namespace DynaDocs.Tests.Serialization;

using System.Text.Json.Nodes;
using DynaDocs.Serialization;

public class JsonTextTests
{
    private static JsonNode Parse(string json) => JsonText.Parse(json)!;

    [Fact]
    public void Render_UnchangedTarget_ReturnsTheOriginalText()
    {
        const string original = "﻿{\r\n  \"a\" :  [1,2],\r\n  \"s\": \"it\\u0027s\"\r\n}\r\n";

        Assert.Equal(original, JsonText.Render(original, Parse(original)));
    }

    [Fact]
    public void Render_ChangedValue_RewritesOnlyThatValueWithRelaxedEscaping()
    {
        const string original = "{\"a\": 1,\n \"b\": {\"c\": \"x\"}}";
        var target = Parse(original);
        target["b"]!["c"] = "it's <&>";

        Assert.Equal("{\"a\": 1,\n \"b\": {\"c\": \"it's <&>\"}}", JsonText.Render(original, target));
    }

    [Fact]
    public void Render_AddedMember_TakesTheFileIndentAndNewline()
    {
        const string original = "{\r\n\t\"a\": 1\r\n}";
        var target = Parse(original);
        target["b"] = Parse("{\"c\": [true]}");

        Assert.Equal("{\r\n\t\"a\": 1,\r\n\t\"b\": {\r\n\t\t\"c\": [\r\n\t\t\ttrue\r\n\t\t]\r\n\t}\r\n}",
            JsonText.Render(original, target));
    }

    [Fact]
    public void Render_RemovedItem_LeavesTheOthersVerbatim()
    {
        const string original = "[\n  {\"k\":1},\n  {\"k\": 2},\n  { \"k\" : 3 }\n]\n";
        var target = Parse(original).AsArray();
        target.RemoveAt(1);

        Assert.Equal("[\n  {\"k\":1},\n  { \"k\" : 3 }\n]\n", JsonText.Render(original, target));
    }

    [Fact]
    public void Render_EmptyObjectGainingAMember_OpensOnItsOwnLines()
    {
        var target = Parse("{}");
        target["a"] = 1;

        Assert.Equal("{\n  \"a\": 1\n}", JsonText.Render("{}", target));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ nope")]
    public void Render_NoParseableOriginal_IsAFreshLfDocument(string? original)
    {
        Assert.Equal("{\n  \"a\": \"don't\"\n}\n", JsonText.Render(original, Parse("{\"a\": \"don't\"}")));
    }

    [Fact]
    public void Merge_AppliesOnlyTheChangeFromBaselineToUpdated()
    {
        var original = Parse("{\"x\": 1, \"unknown\": true, \"list\": [1, 2, 3], \"gone\": 0}");
        var baseline = Parse("{\"x\": 1, \"defaulted\": [], \"list\": [1, 2, 3], \"gone\": 0}");
        var updated = Parse("{\"x\": 2, \"defaulted\": [], \"list\": [1, 5], \"added\": \"a\"}");

        var merged = JsonText.Merge(original, baseline, updated);

        Assert.Same(original, merged);
        Assert.True(JsonNode.DeepEquals(
            Parse("{\"x\": 2, \"unknown\": true, \"list\": [1, 5], \"added\": \"a\"}"), merged));
    }

    [Fact]
    public void Merge_ArrayTheModelReshaped_TakesTheUpdate()
    {
        var merged = JsonText.Merge(Parse("[1, 2]"), Parse("[1]"), Parse("[3]"));

        Assert.True(JsonNode.DeepEquals(Parse("[3]"), merged));
    }

    [Fact]
    public void Merge_AppendedItem_IsAddedToTheOriginal()
    {
        var original = Parse("[1]");

        var merged = JsonText.Merge(original, Parse("[1]"), Parse("[1, 2]"));

        Assert.Same(original, merged);
        Assert.True(JsonNode.DeepEquals(Parse("[1, 2]"), merged));
    }
}
