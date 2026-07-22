namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json;
using System.Text.Json.Serialization;
using DynaDocs.Serialization;

/// <summary>Write-only converter for the page-update body (issue 0299, F5). The source generator's WhenWritingNull
/// omits a null property value, so an explicit Notion CLEAR (<c>{"select": null}</c>, <c>{"date": null}</c>, …)
/// cannot be expressed by the typed <see cref="NotionPropertyValue"/>. This merges the typed properties with the
/// clear map under one <c>properties</c> object, writing the exact clear shape per type. The request is never
/// deserialized (it is a request body), so <see cref="Read"/> is unreachable.</summary>
public sealed class NotionPageUpdateRequestConverter : JsonConverter<NotionPageUpdateRequest>
{
    public override NotionPageUpdateRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("NotionPageUpdateRequest is write-only");

    public override void Write(Utf8JsonWriter writer, NotionPageUpdateRequest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var hasProps = value.Properties is { Count: > 0 };
        var hasClears = value.PropertyClears is { Count: > 0 };
        if (hasProps || hasClears)
        {
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            if (hasProps)
                foreach (var (name, propertyValue) in value.Properties!)
                {
                    writer.WritePropertyName(name);
                    JsonSerializer.Serialize(writer, propertyValue, NotionJsonContext.Default.NotionPropertyValue);
                }
            if (hasClears)
                foreach (var (name, type) in value.PropertyClears!)
                {
                    writer.WritePropertyName(name);
                    WriteClear(writer, type);
                }
            writer.WriteEndObject();
        }

        if (value.Archived is { } archived)
            writer.WriteBoolean("archived", archived);

        writer.WriteEndObject();
    }

    /// <summary>The Notion clear shape per type (live-probed 2026-07-22): an empty array for the collection-shaped
    /// types, an explicit null for the scalar-shaped ones, and <c>false</c> for a checkbox.</summary>
    private static void WriteClear(Utf8JsonWriter writer, string type)
    {
        writer.WriteStartObject();
        switch (type)
        {
            case "rich_text" or "title" or "multi_select" or "relation":
                writer.WritePropertyName(type);
                writer.WriteStartArray();
                writer.WriteEndArray();
                break;
            case "checkbox":
                writer.WriteBoolean("checkbox", false);
                break;
            default: // select, date, number, url, and any other scalar clear to an explicit null
                writer.WriteNull(type);
                break;
        }
        writer.WriteEndObject();
    }
}
