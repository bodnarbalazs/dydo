namespace DynaDocs.Serialization;

using System.Text.Json.Serialization;
using DynaDocs.Services.Map;

/// <summary>
/// JSON context for `dydo map`: the local HTTP API the viewer reads (plan §3 "HTTP API contract")
/// and the GraphQL request body sent to Linear.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, List<MapTeam>>))]
[JsonSerializable(typeof(Dictionary<string, List<MapProject>>))]
[JsonSerializable(typeof(MapGraph))]
[JsonSerializable(typeof(Dictionary<string, MapError>))]
[JsonSerializable(typeof(GraphQLRequest))]
internal partial class MapJsonContext : JsonSerializerContext { }
