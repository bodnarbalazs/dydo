namespace DynaDocs.Services.Statechart;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(StatechartRequest))]
internal sealed partial class StatechartRequestJson : JsonSerializerContext;
