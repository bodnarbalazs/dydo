namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public class NotionPropertyMapperTests
{
    private static NotionPropertyValue Title(string s) =>
        new() { Type = "title", Title = [new NotionRichText { PlainText = s }] };

    private static NotionPropertyValue RichText(string s) =>
        new() { Type = "rich_text", RichText = [new NotionRichText { PlainText = s }] };

    [Fact]
    public void ToFields_RendersEverySupportedType()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["Name"] = Title("My Task"),
            ["Notes"] = RichText("some notes"),
            ["Status"] = new() { Type = "select", Select = new NotionSelectOption { Name = "open" } },
            ["Tags"] = new()
            {
                Type = "multi_select",
                MultiSelect = [new NotionSelectOption { Name = "a" }, new NotionSelectOption { Name = "b" }],
            },
            ["Points"] = new() { Type = "number", Number = 3 },
            ["Done"] = new() { Type = "checkbox", Checkbox = true },
            ["Due"] = new() { Type = "date", Date = new NotionDate { Start = "2026-06-19" } },
            ["Link"] = new() { Type = "url", Url = "https://x.dev" },
        };

        var fields = NotionPropertyMapper.ToFields(props);

        // The title property sorts first; everything else is by name for a stable order.
        Assert.Equal("Name", fields[0].Key);
        Assert.Equal("My Task", fields[0].Value);
        Assert.Equal("some notes", Value(fields, "Notes"));
        Assert.Equal("open", Value(fields, "Status"));
        Assert.Equal("a, b", Value(fields, "Tags"));
        Assert.Equal("3", Value(fields, "Points"));
        Assert.Equal("true", Value(fields, "Done"));
        Assert.Equal("2026-06-19", Value(fields, "Due"));
        Assert.Equal("https://x.dev", Value(fields, "Link"));
    }

    [Fact]
    public void ToFields_UnknownType_RendersBestEffortString()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["Owner"] = new() { Type = "people", Title = null },
        };

        var fields = NotionPropertyMapper.ToFields(props);
        Assert.Equal("", Value(fields, "Owner"));
    }

    [Fact]
    public void ToProperties_BuildsEverySupportedType()
    {
        var schema = new Dictionary<string, string>
        {
            ["Name"] = "title",
            ["Notes"] = "rich_text",
            ["Status"] = "select",
            ["Tags"] = "multi_select",
            ["Points"] = "number",
            ["Done"] = "checkbox",
            ["Due"] = "date",
            ["Link"] = "url",
        };
        var fields = F(
            ("Name", "My Task"), ("Notes", "n"), ("Status", "open"), ("Tags", "a, b"),
            ("Points", "3"), ("Done", "true"), ("Due", "2026-06-19"), ("Link", "https://x.dev"));

        var props = NotionPropertyMapper.ToProperties(fields, schema);

        Assert.Equal("My Task", NotionRichText.Flatten(props["Name"].Title));
        Assert.Equal("n", NotionRichText.Flatten(props["Notes"].RichText));
        Assert.Equal("open", props["Status"].Select!.Name);
        Assert.Equal(["a", "b"], props["Tags"].MultiSelect!.Select(o => o.Name));
        Assert.Equal(3, props["Points"].Number);
        Assert.True(props["Done"].Checkbox);
        Assert.Equal("2026-06-19", props["Due"].Date!.Start);
        Assert.Equal("https://x.dev", props["Link"].Url);
    }

    [Fact]
    public void ToProperties_SkipsFieldWithNoMatchingProperty()
    {
        var schema = new Dictionary<string, string> { ["Status"] = "select" };
        var props = NotionPropertyMapper.ToProperties(F(("Status", "open"), ("local-id", "t1")), schema);

        Assert.True(props.ContainsKey("Status"));
        Assert.False(props.ContainsKey("local-id"));
    }

    [Fact]
    public void ToProperties_SkipsUnsupportedPropertyType()
    {
        var schema = new Dictionary<string, string> { ["Owner"] = "people" };
        var props = NotionPropertyMapper.ToProperties(F(("Owner", "someone")), schema);
        Assert.Empty(props);
    }

    [Fact]
    public void ToProperties_EmptyValues_ProduceClearedPayloads()
    {
        var schema = new Dictionary<string, string>
        {
            ["Status"] = "select",
            ["Due"] = "date",
            ["Link"] = "url",
            ["Points"] = "number",
            ["Tags"] = "multi_select",
            ["Done"] = "checkbox",
        };
        var props = NotionPropertyMapper.ToProperties(
            F(("Status", ""), ("Due", ""), ("Link", ""), ("Points", "not-a-number"), ("Tags", ""), ("Done", "no")),
            schema);

        Assert.Null(props["Status"].Select);          // empty select cleared
        Assert.Null(props["Due"].Date);                // empty date cleared
        Assert.Null(props["Link"].Url);                // empty url cleared
        Assert.Null(props["Points"].Number);           // unparseable number cleared
        Assert.Empty(props["Tags"].MultiSelect!);      // empty multi-select
        Assert.False(props["Done"].Checkbox);          // non-"true" is false
    }

    [Fact]
    public void ToFields_RendersEmptyAndAbsentSubValues()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["Status"] = new() { Type = "select", Select = null },
            ["Due"] = new() { Type = "date", Date = null },
            ["Link"] = new() { Type = "url", Url = null },
            ["Points"] = new() { Type = "number", Number = null },
            ["Done"] = new() { Type = "checkbox", Checkbox = false },
            ["Tags"] = new() { Type = "multi_select", MultiSelect = null },
        };

        var fields = NotionPropertyMapper.ToFields(props);
        Assert.Equal("", Value(fields, "Status"));
        Assert.Equal("", Value(fields, "Due"));
        Assert.Equal("", Value(fields, "Link"));
        Assert.Equal("", Value(fields, "Points"));
        Assert.Equal("false", Value(fields, "Done"));
        Assert.Equal("", Value(fields, "Tags"));
    }

    [Fact]
    public void ToFields_UnknownType_WithTitleLikeContent_RendersThatText()
    {
        // The unknown-type fallback prefers a title-shaped value when present.
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["Weird"] = new() { Type = "people", Title = [new NotionRichText { PlainText = "fallback" }] },
        };
        Assert.Equal("fallback", NotionPropertyMapper.ToFields(props).First(f => f.Key == "Weird").Value);
    }

    [Fact]
    public void ToProperties_Relation_ResolvesLocalIdToPageId()
    {
        var schema = new Dictionary<string, string> { ["campaign"] = "relation" };
        var localToPage = new Dictionary<string, string> { ["dydo-2-0"] = "page-99" };

        var props = NotionPropertyMapper.ToProperties(F(("campaign", "dydo-2-0")), schema, Rel("campaign", localToPage));

        Assert.Equal("page-99", props["campaign"].Relation!.Single().Id);
    }

    [Fact]
    public void ToProperties_Relation_UnresolvedLocalId_IsSkipped()
    {
        var schema = new Dictionary<string, string> { ["campaign"] = "relation" };
        var props = NotionPropertyMapper.ToProperties(
            F(("campaign", "unknown")), schema, Rel("campaign", new Dictionary<string, string>()));

        Assert.False(props.ContainsKey("campaign"));
    }

    [Fact]
    public void ToProperties_Relation_EmptyValue_ClearsTheRelation()
    {
        var schema = new Dictionary<string, string> { ["campaign"] = "relation" };
        var props = NotionPropertyMapper.ToProperties(F(("campaign", "")), schema, Rel("campaign", new Dictionary<string, string>()));

        Assert.Empty(props["campaign"].Relation!);
    }

    [Fact]
    public void ToFields_Relation_RendersPageIdBackToLocalId()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["campaign"] = new() { Type = "relation", Relation = [new NotionRelationRef { Id = "page-99" }] },
        };
        var pageToLocal = new Dictionary<string, string> { ["page-99"] = "dydo-2-0" };

        Assert.Equal("dydo-2-0", NotionPropertyMapper.ToFields(props, pageToLocal).Single().Value);
    }

    [Fact]
    public void ToFields_Relation_UnknownPageId_FallsBackToRawId()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["campaign"] = new() { Type = "relation", Relation = [new NotionRelationRef { Id = "page-99" }] },
        };
        Assert.Equal("page-99", NotionPropertyMapper.ToFields(props).Single().Value);
    }

    [Fact]
    public void ToFields_Relation_Empty_RendersEmptyString()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["campaign"] = new() { Type = "relation", Relation = [] },
        };
        Assert.Equal("", NotionPropertyMapper.ToFields(props).Single().Value);
    }

    [Fact]
    public void ToProperties_MultiRelation_ResolvesEveryLocalIdToPageId()
    {
        var schema = new Dictionary<string, string> { ["blocked-by"] = "relation" };
        var localToPage = new Dictionary<string, string> { ["t1"] = "page-1", ["t2"] = "page-2" };

        var props = NotionPropertyMapper.ToProperties(F(("blocked-by", "t1, t2")), schema, Rel("blocked-by", localToPage));

        Assert.Equal(["page-1", "page-2"], props["blocked-by"].Relation!.Select(r => r.Id));
    }

    [Fact]
    public void ToProperties_MultiRelation_PartialResolution_WritesResolvedSkipsUnresolved()
    {
        // Only the resolvable ids are written; an unresolved id is dropped rather than blocking the rest.
        var schema = new Dictionary<string, string> { ["blocked-by"] = "relation" };
        var localToPage = new Dictionary<string, string> { ["t1"] = "page-1" };

        var props = NotionPropertyMapper.ToProperties(F(("blocked-by", "t1, unknown")), schema, Rel("blocked-by", localToPage));

        Assert.Equal(["page-1"], props["blocked-by"].Relation!.Select(r => r.Id));
    }

    [Fact]
    public void ToProperties_MultiRelation_NoneResolve_IsSkipped()
    {
        // Non-empty input where nothing resolves must NOT clear the relation — skip the field entirely.
        var schema = new Dictionary<string, string> { ["blocked-by"] = "relation" };
        var props = NotionPropertyMapper.ToProperties(
            F(("blocked-by", "x, y")), schema, Rel("blocked-by", new Dictionary<string, string>()));

        Assert.False(props.ContainsKey("blocked-by"));
    }

    [Fact]
    public void ToFields_MultiRelation_RendersEveryRefAsCommaSeparatedLocalIds()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["blocked-by"] = new()
            {
                Type = "relation",
                Relation = [new NotionRelationRef { Id = "page-1" }, new NotionRelationRef { Id = "page-2" }],
            },
        };
        var pageToLocal = new Dictionary<string, string> { ["page-1"] = "t1", ["page-2"] = "t2" };

        Assert.Equal("t1, t2", NotionPropertyMapper.ToFields(props, pageToLocal).Single().Value);
    }

    [Fact]
    public void ToFields_MultiRelation_UnknownPageId_FallsBackToRawIdPerEntry()
    {
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["blocked-by"] = new()
            {
                Type = "relation",
                Relation = [new NotionRelationRef { Id = "page-1" }, new NotionRelationRef { Id = "page-2" }],
            },
        };
        var pageToLocal = new Dictionary<string, string> { ["page-1"] = "t1" };

        Assert.Equal("t1, page-2", NotionPropertyMapper.ToFields(props, pageToLocal).Single().Value);
    }

    [Fact]
    public void ToFields_SkipsComputedProperties_TheyNeverEnterFrontmatter()
    {
        // Formula/rollup are view-only (DR 029/030): computed in Notion, never stored. They must be
        // dropped on read so a base snapshot and frontmatter carry only canonical fields.
        var props = new Dictionary<string, NotionPropertyValue>
        {
            ["Name"] = Title("A Sprint"),
            ["done"] = new() { Type = "formula", Checkbox = true },
            ["progress"] = new() { Type = "rollup", Number = 0.5 },
        };

        var fields = NotionPropertyMapper.ToFields(props);

        Assert.Equal(["Name"], fields.Select(f => f.Key));
        Assert.DoesNotContain(fields, f => f.Key == "done");
        Assert.DoesNotContain(fields, f => f.Key == "progress");
    }

    [Fact]
    public void ToProperties_SkipsComputedProperties_TheyAreNeverWritten()
    {
        // Even if a computed property name reached the field list, it is never pushed: formula/rollup have
        // no writer, so ToProperties omits them.
        var schema = new Dictionary<string, string> { ["done"] = "formula", ["progress"] = "rollup", ["status"] = "select" };
        var props = NotionPropertyMapper.ToProperties(F(("done", "true"), ("progress", "0.5"), ("status", "done")), schema);

        Assert.False(props.ContainsKey("done"));
        Assert.False(props.ContainsKey("progress"));
        Assert.True(props.ContainsKey("status"));
    }

    [Fact]
    public void InferSchema_TakesTypePerNameAcrossPages()
    {
        var pages = new List<NotionPage>
        {
            new() { Id = "p1", Properties = new() { ["Status"] = new NotionPropertyValue { Type = "select" } } },
            new() { Id = "p2", Properties = new() { ["Points"] = new NotionPropertyValue { Type = "number" } } },
        };

        var schema = NotionPropertyMapper.InferSchema(pages);
        Assert.Equal("select", schema["Status"]);
        Assert.Equal("number", schema["Points"]);
    }

    private static string Value(List<SyncField> fields, string key) =>
        fields.First(f => f.Key == key).Value;

    private static List<SyncField> F(params (string, string)[] fields) =>
        fields.Select(f => new SyncField { Key = f.Item1, Value = f.Item2 }).ToList();

    /// <summary>Wrap a single relation field's local→page map into the per-field form ToProperties takes.</summary>
    private static Dictionary<string, IReadOnlyDictionary<string, string>> Rel(string field, IReadOnlyDictionary<string, string> map) =>
        new() { [field] = map };
}
