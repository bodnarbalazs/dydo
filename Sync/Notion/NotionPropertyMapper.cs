namespace DynaDocs.Sync.Notion;

using System.Globalization;
using DynaDocs.Models;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Generic, schema-driven mapping between dydo frontmatter fields and Notion page properties
/// (Decision 025 §6, slice brief §3). Mapping is BY NAME: a frontmatter key maps to the Notion
/// property of the same name. Supported both directions: title, rich_text, select, multi_select,
/// number, checkbox, date, url, and relation. The title property maps to a frontmatter field keyed
/// by the title property's Notion name. On read, unknown property types are rendered best-effort to
/// a string; on write, a field with no matching property — or a property of an unsupported type — is
/// skipped (creating new DB schema is out of scope). Type dispatch is table-driven so each type
/// handler is a small, independently-testable function rather than one giant switch.
///
/// Relations are the one cross-object type: a frontmatter relation value holds the parent object's
/// stable local id, while Notion holds the parent's page id. The optional id maps translate between
/// them — <paramref name="relationLocalToPageId" /> on write, the inverse on read.
/// </summary>
public static class NotionPropertyMapper
{
    private static readonly Dictionary<string, Func<NotionPropertyValue, string>> Readers = new()
    {
        ["title"] = v => NotionRichText.Flatten(v.Title),
        ["rich_text"] = v => NotionRichText.Flatten(v.RichText),
        ["select"] = v => v.Select?.Name ?? "",
        ["multi_select"] = v => string.Join(", ", (v.MultiSelect ?? []).Select(o => o.Name)),
        ["number"] = v => v.Number?.ToString(CultureInfo.InvariantCulture) ?? "",
        ["checkbox"] = v => v.Checkbox == true ? "true" : "false",
        ["date"] = v => v.Date?.Start ?? "",
        ["url"] = v => v.Url ?? "",
    };

    private static readonly Dictionary<string, Func<string, NotionPropertyValue>> Writers = new()
    {
        ["title"] = raw => new NotionPropertyValue { Type = "title", Title = NotionRichText.Of(raw) },
        ["rich_text"] = raw => new NotionPropertyValue { Type = "rich_text", RichText = NotionRichText.Of(raw) },
        ["select"] = BuildSelect,
        ["multi_select"] = BuildMultiSelect,
        ["number"] = BuildNumber,
        ["checkbox"] = BuildCheckbox,
        ["date"] = BuildDate,
        ["url"] = BuildUrl,
    };

    /// <summary>Render a page's properties to an ordered field list: the title property first,
    /// then the rest by name, so the field order is stable across sync ticks. A relation value is
    /// rendered to the related object's local id via <paramref name="relationPageIdToLocalId"/>.</summary>
    public static List<SyncField> ToFields(
        IReadOnlyDictionary<string, NotionPropertyValue> properties,
        IReadOnlyDictionary<string, string>? relationPageIdToLocalId = null)
    {
        var ordered = properties
            .OrderBy(p => p.Value.Type == "title" ? 0 : 1)
            .ThenBy(p => p.Key, StringComparer.Ordinal);

        var fields = new List<SyncField>();
        foreach (var (name, value) in ordered)
            fields.Add(new SyncField { Key = name, Value = Render(value, relationPageIdToLocalId) });
        return fields;
    }

    /// <summary>The property name -> type schema observed across a set of pages. Used on write to
    /// know each property's type without a separate schema fetch.</summary>
    public static Dictionary<string, string> InferSchema(IEnumerable<NotionPage> pages)
    {
        var schema = new Dictionary<string, string>();
        foreach (var page in pages)
            foreach (var (name, value) in page.Properties)
                if (value.Type != null && !schema.ContainsKey(name))
                    schema[name] = value.Type;
        return schema;
    }

    /// <summary>Build the Notion property payload for the fields that match the schema. Fields with
    /// no matching property, or matching an unsupported property type, are skipped. A relation field
    /// resolves the related object's local id to its page id via <paramref name="relationLocalToPageId"/>;
    /// an unresolved relation is skipped rather than written as a broken reference.</summary>
    public static Dictionary<string, NotionPropertyValue> ToProperties(
        IReadOnlyList<SyncField> fields,
        IReadOnlyDictionary<string, string> schema,
        IReadOnlyDictionary<string, string>? relationLocalToPageId = null)
    {
        var props = new Dictionary<string, NotionPropertyValue>();
        foreach (var field in fields)
        {
            if (!schema.TryGetValue(field.Key, out var type))
                continue;

            if (type == "relation")
            {
                var relation = BuildRelation(field.Value, relationLocalToPageId);
                if (relation != null)
                    props[field.Key] = relation;
            }
            else if (Writers.TryGetValue(type, out var build))
            {
                props[field.Key] = build(field.Value);
            }
        }
        return props;
    }

    private static string Render(NotionPropertyValue value, IReadOnlyDictionary<string, string>? relationPageIdToLocalId)
    {
        if (value.Type == "relation")
            return RenderRelation(value, relationPageIdToLocalId);
        return value.Type != null && Readers.TryGetValue(value.Type, out var read)
            ? read(value)
            : RenderUnknown(value);
    }

    /// <summary>Render every related page id to its parent local id when known, else the raw page id
    /// per entry (an honest fallback when the parent type has not been synced yet). Multiple related
    /// pages join as comma-separated local ids, consistent with the multi_select convention, so a
    /// multi-value relation round-trips without dropping any target.</summary>
    private static string RenderRelation(NotionPropertyValue value, IReadOnlyDictionary<string, string>? pageIdToLocalId)
    {
        var refs = value.Relation;
        if (refs == null || refs.Count == 0)
            return "";
        return string.Join(", ", refs.Select(r =>
            pageIdToLocalId != null && pageIdToLocalId.TryGetValue(r.Id, out var localId) ? localId : r.Id));
    }

    /// <summary>Build a relation property value from comma-separated local ids (the multi_select
    /// convention). Empty input clears the relation. Each id resolves to a page id via
    /// <paramref name="localToPageId"/>; unresolved ids are skipped so the resolved targets still
    /// write. Null is returned only when nothing resolves from non-empty input, so the caller skips
    /// the field rather than clobbering the current relation with an empty list.</summary>
    private static NotionPropertyValue? BuildRelation(string localIds, IReadOnlyDictionary<string, string>? localToPageId)
    {
        var ids = localIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ids.Length == 0)
            return new NotionPropertyValue { Type = "relation", Relation = [] };

        var refs = new List<NotionRelationRef>();
        foreach (var id in ids)
            if (localToPageId != null && localToPageId.TryGetValue(id, out var pageId))
                refs.Add(new NotionRelationRef { Id = pageId });

        return refs.Count > 0 ? new NotionPropertyValue { Type = "relation", Relation = refs } : null;
    }

    /// <summary>Best-effort plain text for an unsupported property type — prefer a title- or
    /// rich-text-shaped value if Notion happened to include one, else empty.</summary>
    private static string RenderUnknown(NotionPropertyValue v)
    {
        var title = NotionRichText.Flatten(v.Title);
        return title.Length > 0 ? title : NotionRichText.Flatten(v.RichText);
    }

    private static NotionPropertyValue BuildSelect(string raw) => new()
    {
        Type = "select",
        Select = string.IsNullOrEmpty(raw) ? null : new NotionSelectOption { Name = raw },
    };

    private static NotionPropertyValue BuildMultiSelect(string raw) => new()
    {
        Type = "multi_select",
        MultiSelect = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => new NotionSelectOption { Name = n })
            .ToList(),
    };

    private static NotionPropertyValue BuildNumber(string raw) => new()
    {
        Type = "number",
        Number = double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : null,
    };

    private static NotionPropertyValue BuildCheckbox(string raw) => new()
    {
        Type = "checkbox",
        Checkbox = raw.Equals("true", StringComparison.OrdinalIgnoreCase),
    };

    private static NotionPropertyValue BuildDate(string raw) => new()
    {
        Type = "date",
        Date = string.IsNullOrEmpty(raw) ? null : new NotionDate { Start = raw },
    };

    private static NotionPropertyValue BuildUrl(string raw) => new()
    {
        Type = "url",
        Url = string.IsNullOrEmpty(raw) ? null : raw,
    };
}
