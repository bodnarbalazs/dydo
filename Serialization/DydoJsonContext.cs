namespace DynaDocs.Serialization;

using System.Text.Json.Serialization;
using DynaDocs.Models;

/// <summary>
/// JSON serializer context for dydo.json configuration files.
/// Uses camelCase naming policy to match the config file format.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DydoConfig))]
[JsonSerializable(typeof(StructureConfig))]
[JsonSerializable(typeof(AgentsConfig))]
[JsonSerializable(typeof(Dictionary<string, bool>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
[JsonSerializable(typeof(List<string>))]
internal partial class DydoConfigJsonContext : JsonSerializerContext { }

/// <summary>
/// JSON serializer context for types that use default naming (PascalCase)
/// or explicit JsonPropertyName attributes.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(HookInput))]
[JsonSerializable(typeof(ToolInputData))]
[JsonSerializable(typeof(AgentSession))]
internal partial class DydoDefaultJsonContext : JsonSerializerContext { }
